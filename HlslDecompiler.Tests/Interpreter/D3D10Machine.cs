using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Tests.Interpreter;

/// <summary>
/// The shader model 4+ counterpart of <see cref="D3D9Machine"/>. A register is four
/// untyped 32 bit words; each instruction reinterprets them as float, int or uint.
/// </summary>
public class D3D10Machine
{
    public class UnsupportedException(string message) : Exception(message);

    private const int ConstantRegisterSizeInBytes = 16;

    private readonly ShaderModel _shader;
    private readonly int _trial;
    private readonly uint[][] _temp = NewFile(64);
    // x# registers: each a small array of 4-component elements, sized by its dcl.
    private readonly Dictionary<int, uint[][]> _indexableTemps = [];
    // g# registers. One thread runs, so a sync has nothing to wait for.
    private readonly Dictionary<int, (int Stride, uint[] Words)> _threadGroupSharedMemory = [];

    // Raw g# registers address bytes, not element+offset; declared with stride 4.
    private readonly HashSet<int> _rawThreadGroupSharedMemory = [];
    private readonly uint[][] _input = NewFile(64);
    private readonly uint[][] _output = NewFile(64);
    private readonly Dictionary<int, uint[][]> _constantBuffers = [];
    private readonly List<uint[]> _immediateConstantBuffer = [];
    private readonly Dictionary<int, string> _outputSemantics = [];
    private readonly Dictionary<int, string> _inputSemantics = [];
    private readonly Dictionary<(int Vertex, int Register), uint[]> _vertexInputs = [];
    private readonly Dictionary<string, uint[]> _results = [];
    private uint[] _depth = new uint[4];
    private readonly uint[] _coverage = new uint[4];
    // The results of a geometry shader's stream and a compute shader's stores.
    private readonly List<KeyValuePair<string, uint[]>> _emitted = [];
    private readonly List<KeyValuePair<string, uint[]>> _stored = [];
    private bool _wroteDepth;
    private bool _wroteCoverage;
    private readonly uint[] _stencilRef = new uint[4];
    private bool _wroteStencilRef;

    // Dynamic linkage: where each fcall returns to, and the instances whose class
    // data is loaded. A class's data sits in a buffer of its own past the 14 a
    // shader binds; `this` points at it.
    private const int ClassDataBuffer = 100;
    private LinkageModel _linkage;
    private readonly Stack<int> _calls = new();
    private readonly HashSet<int> _boundSlots = [];

    public bool Discarded { get; private set; }

    private D3D10Machine(ShaderModel shader, int trial)
    {
        _shader = shader;
        _trial = trial;
    }

    private static uint[][] NewFile(int count)
    {
        var file = new uint[count][];
        for (int i = 0; i < count; i++)
        {
            file[i] = new uint[4];
        }
        return file;
    }

    /// <summary>
    /// Runs the shader and returns its outputs by semantic. Constants and inputs are
    /// derived from their names, so register allocation does not affect them.
    /// </summary>
    public static Dictionary<string, float[]> Run(ShaderModel shader, int trial)
    {
        // A hull shader runs as two programs: as one it would stop at the control
        // point phase's ret and never reach the patch constants.
        if (shader.Type == ShaderType.Hull)
        {
            (ShaderModel controlPoint, ShaderModel patchConstant) =
                HullShaderPhases.Split(shader);
            var both = new Dictionary<string, float[]>();
            foreach (ShaderModel phase in new[] { controlPoint, patchConstant })
            {
                if (phase == null)
                {
                    continue;
                }
                foreach (KeyValuePair<string, float[]> result in RunProgram(phase, trial))
                {
                    both[result.Key] = result.Value;
                }
            }
            return both;
        }

        return RunProgram(shader, trial);
    }

    /// <summary>
    /// A whole shader or one hull shader phase. Separate from Run because a phase
    /// is still typed Hull, and splitting it again would return nothing.
    /// </summary>
    private static Dictionary<string, float[]> RunProgram(ShaderModel shader, int trial)
    {
        var machine = new D3D10Machine(shader, trial);
        machine.LoadConstants();
        machine.LoadSignatures();
        machine.Execute();
        if (machine.Discarded)
        {
            return [];
        }

        var results = machine._results.ToDictionary(
            r => r.Key,
            r => r.Value.Select(BitConverter.UInt32BitsToSingle).ToArray());
        if (machine._wroteDepth)
        {
            results["DEPTH"] = machine._depth.Select(BitConverter.UInt32BitsToSingle).ToArray();
        }
        // Converted, not bit-cast: a mask as a float is a denormal, and the
        // comparison's tolerance would make every mask equal.
        if (machine._wroteCoverage)
        {
            results["COVERAGE"] = [.. machine._coverage.Select(bits => (float)bits)];
        }
        if (machine._wroteStencilRef)
        {
            results["STENCILREF"] = [.. machine._stencilRef.Select(bits => (float)bits)];
        }
        foreach (var entry in machine._emitted.Concat(machine._stored))
        {
            results[entry.Key] = [.. entry.Value.Select(BitConverter.UInt32BitsToSingle)];
        }
        return results;
    }

    private void LoadConstants()
    {
        foreach (D3D10ConstantDeclaration declaration
            in _shader.ConstantDeclarations.OfType<D3D10ConstantDeclaration>())
        {
            uint[][] buffer = Buffer(declaration.RegisterIndex);
            // Each word named by its position in the variable, so a different
            // buffer packing still gets the same values.
            int words = Math.Max(declaration.VariableSize / 4, 1);
            for (int word = 0; word < words; word++)
            {
                int byteOffset = declaration.VariableOffset + word * 4;
                int register = byteOffset / ConstantRegisterSizeInBytes;
                int component = byteOffset % ConstantRegisterSizeInBytes / 4;
                if (register < buffer.Length)
                {
                    buffer[register][component] =
                        ConstantWord(declaration, Named($"{declaration.Name}[{word}]")[0]);
                }
            }
        }
    }

    // Integer constants are small and non-negative: float bits read as a uint would
    // make a loop bound that never finishes. Under 4 in even trials, to reach switch
    // cases; up to 16 in odd ones, past the thread ids (0-7), so a bounds check like
    // `any(id >= size)` goes both ways.
    private uint ConstantWord(D3D10ConstantDeclaration declaration, float value)
    {
        switch (declaration.TypeInfo.ParameterType)
        {
            case ParameterType.Int:
            case ParameterType.Uint:
            case ParameterType.Uint8:
            case ParameterType.Bool:
                return _trial % 2 == 0
                    ? (uint)Math.Abs(value * 2) % 4
                    : (uint)Math.Abs(value * 8) % 17;
            default:
                return BitConverter.SingleToUInt32Bits(value);
        }
    }

    private uint[][] Buffer(int slot)
    {
        if (!_constantBuffers.TryGetValue(slot, out uint[][] buffer))
        {
            buffer = NewFile(4096);
            _constantBuffers[slot] = buffer;
        }
        return buffer;
    }

    private void LoadSignatures()
    {
        foreach (RegisterSignature signature in _shader.InputSignatures)
        {
            int number = signature.RegisterKey.Number;
            // A system value with no register (the primitive id) has number -1.
            if (number < 0 || number >= _input.Length)
            {
                continue;
            }

            _inputSemantics[number] = signature.Name + signature.Index;
            float[] value = Named(signature.Name + signature.Index);
            for (int i = 0; i < 4; i++)
            {
                _input[number][i] = BitConverter.SingleToUInt32Bits(value[i]);
            }
        }

        foreach (RegisterSignature signature in _shader.OutputSignatures)
        {
            _outputSemantics[signature.RegisterKey.Number] = signature.Name + signature.Index;
        }
    }

    // Named by semantic so that input register numbering does not matter.
    private uint[] VertexInput(int vertex, int register)
    {
        if (!_vertexInputs.TryGetValue((vertex, register), out uint[] value))
        {
            string semantic = _inputSemantics.TryGetValue(register, out string name)
                ? name
                : "INPUT" + register;
            value = [.. Named($"{semantic}[{vertex}]").Select(BitConverter.SingleToUInt32Bits)];
            _vertexInputs[(vertex, register)] = value;
        }
        return value;
    }

    private float[] Named(string name)
    {
        return PseudoRandom.Vector(name, _trial);
    }

    private int ControlPointCount =>
        _shader.Instructions.OfType<D3D10Instruction>()
            .FirstOrDefault(i => i.Opcode == D3D10Opcode.DclOutputControlPointCount)
            ?.ControlPointCount ?? 1;

    private void Execute()
    {
        List<D3D10Instruction> instructions = [.. _shader.Instructions.OfType<D3D10Instruction>()];
        D3D10ControlFlow flow = D3D10ControlFlow.Match(instructions);
        // break leaves the innermost loop or switch, so both share one stack.
        var breakable = new Stack<int>();
        int pc = 0;
        int steps = 0;

        while (pc < instructions.Count)
        {
            if (++steps > 200000)
            {
                throw new UnsupportedException("the shader did not terminate");
            }

            D3D10Instruction instruction = instructions[pc];
            switch (instruction.Opcode)
            {
                case D3D10Opcode.Ret:
                    if (_calls.Count != 0)
                    {
                        pc = _calls.Pop();
                        continue;
                    }
                    return;
                case D3D10Opcode.RetC:
                    if (Test(instruction))
                    {
                        if (_calls.Count != 0)
                        {
                            pc = _calls.Pop();
                            continue;
                        }
                        return;
                    }
                    pc++;
                    continue;
                case D3D10Opcode.InterfaceCall:
                    _calls.Push(pc + 1);
                    pc = BodyStart(instructions, instruction);
                    continue;
                case D3D10Opcode.Discard:
                    Discarded |= Test(instruction);
                    pc++;
                    continue;
                case D3D10Opcode.If:
                    pc = Test(instruction) ? pc + 1 : flow.Blocks[pc] + 1;
                    continue;
                case D3D10Opcode.Else:
                    pc = flow.Blocks[pc] + 1;
                    continue;
                case D3D10Opcode.EndSwitch:
                    breakable.Pop();
                    pc++;
                    continue;
                case D3D10Opcode.EndIf:
                case D3D10Opcode.Case:
                case D3D10Opcode.Default:
                case D3D10Opcode.Nop:
                case D3D10Opcode.Label:
                    pc++;
                    continue;
                case D3D10Opcode.Loop:
                    breakable.Push(pc);
                    pc++;
                    continue;
                case D3D10Opcode.EndLoop:
                    pc = breakable.Peek() + 1;
                    continue;
                case D3D10Opcode.Break:
                    pc = flow.Blocks[breakable.Pop()] + 1;
                    continue;
                case D3D10Opcode.BreakC:
                    if (Test(instruction))
                    {
                        pc = flow.Blocks[breakable.Pop()] + 1;
                        continue;
                    }
                    pc++;
                    continue;
                case D3D10Opcode.Continue:
                    pc = flow.Blocks[LoopOf(breakable, flow)];
                    continue;
                case D3D10Opcode.ContinueC:
                    pc = Test(instruction) ? flow.Blocks[LoopOf(breakable, flow)] : pc + 1;
                    continue;
                case D3D10Opcode.Swtich:
                    breakable.Push(pc);
                    pc = flow.SwitchTarget(pc, Source(instruction, 0)[0]);
                    continue;
                // Only one stream is modelled, so the _stream forms are the same.
                case D3D10Opcode.Emit:
                case D3D10Opcode.EmitStream:
                case D3D10Opcode.EmitThenCut:
                case D3D10Opcode.EmitThenCutStream:
                    Emit();
                    if (instruction.Opcode is D3D10Opcode.EmitThenCut
                        or D3D10Opcode.EmitThenCutStream)
                    {
                        Cut();
                    }
                    pc++;
                    continue;
                case D3D10Opcode.Cut:
                case D3D10Opcode.CutStream:
                    Cut();
                    pc++;
                    continue;
                case D3D10Opcode.StoreStructured:
                    Store(instruction);
                    pc++;
                    continue;
                case D3D10Opcode.StoreUAVTyped:
                    StoreTyped(instruction);
                    pc++;
                    continue;
                case D3D10Opcode.StoreRaw:
                    StoreRaw(instruction);
                    pc++;
                    continue;
                case D3D10Opcode.AtomicIAdd:
                case D3D10Opcode.AtomicAnd:
                case D3D10Opcode.AtomicOr:
                case D3D10Opcode.AtomicXor:
                case D3D10Opcode.AtomicIMax:
                case D3D10Opcode.AtomicIMin:
                case D3D10Opcode.AtomicUMax:
                case D3D10Opcode.AtomicUMin:
                case D3D10Opcode.AtomicCmpStore:
                case D3D10Opcode.ImmAtomicIAdd:
                case D3D10Opcode.ImmAtomicAnd:
                case D3D10Opcode.ImmAtomicOr:
                case D3D10Opcode.ImmAtomicXor:
                case D3D10Opcode.ImmAtomicIMax:
                case D3D10Opcode.ImmAtomicIMin:
                case D3D10Opcode.ImmAtomicUMax:
                case D3D10Opcode.ImmAtomicUMin:
                case D3D10Opcode.ImmAtomicExch:
                case D3D10Opcode.ImmAtomicCmpExch:
                    Atomic(instruction);
                    pc++;
                    continue;
                case D3D10Opcode.Sync:
                    pc++;
                    continue;
                case D3D10Opcode.CustomData:
                    LoadImmediateConstantBuffer(instruction);
                    pc++;
                    continue;
                // Phase markers; the phase being run was already split out.
                case D3D10Opcode.HsDecls:
                case D3D10Opcode.HsControlPointPhase:
                case D3D10Opcode.HsForkPhase:
                case D3D10Opcode.HsJoinPhase:
                    pc++;
                    continue;
            }

            if (instruction.Opcode == D3D10Opcode.DclIndexableTemp)
            {
                _indexableTemps[instruction.IndexableTempRegister] =
                    NewFile(instruction.IndexableTempElementCount);
            }
            if (instruction.Opcode == D3D10Opcode.DclThreadGroupSharedMemoryRaw)
            {
                int register = instruction.GetParamRegisterNumber(0);
                int bytes = (int)instruction.GetParamIndexImmediate32(1, 0);
                _threadGroupSharedMemory[register] = (4, new uint[bytes / 4]);
                _rawThreadGroupSharedMemory.Add(register);
            }
            if (instruction.Opcode == D3D10Opcode.DclThreadGroupSharedMemoryStructured)
            {
                int stride = (int)instruction.GetThreadGroupSharedMemoryStride();
                int count = (int)instruction.GetThreadGroupSharedMemoryCount();
                _threadGroupSharedMemory[instruction.GetParamRegisterNumber(0)] =
                    (stride, new uint[stride / 4 * count]);
            }
            if (instruction.Opcode.IsDeclaration())
            {
                pc++;
                continue;
            }

            if (instruction.Opcode == D3D10Opcode.Udiv)
            {
                Divide(instruction);
                pc++;
                continue;
            }
            if (instruction.Opcode is D3D10Opcode.IMul or D3D10Opcode.UMul)
            {
                Multiply(instruction);
                pc++;
                continue;
            }
            if (instruction.Opcode == D3D10Opcode.SinCos)
            {
                SineCosine(instruction);
                pc++;
                continue;
            }

            Store(instruction, Evaluate(instruction));
            pc++;
        }
    }

    // continue skips any switch between it and the innermost loop.
    private static int LoopOf(Stack<int> breakable, D3D10ControlFlow flow)
    {
        foreach (int block in breakable)
        {
            if (flow.IsLoop(block))
            {
                return block;
            }
        }
        throw new UnsupportedException("continue outside a loop");
    }

    // udiv quotient, remainder; either destination may be null.
    private void Divide(D3D10Instruction instruction)
    {
        const int QuotientIndex = 0;
        const int RemainderIndex = 1;
        uint[] dividend = Source(instruction, 2);
        uint[] divisor = Source(instruction, 3);
        // Division by zero gives all ones rather than trapping.
        uint[] quotient = [.. Enumerable.Range(0, 4)
            .Select(i => divisor[i] == 0 ? 0xFFFFFFFF : dividend[i] / divisor[i])];
        uint[] remainder = [.. Enumerable.Range(0, 4)
            .Select(i => divisor[i] == 0 ? 0xFFFFFFFF : dividend[i] % divisor[i])];

        if (instruction.GetOperandType(QuotientIndex) != OperandType.Null)
        {
            Store(instruction, QuotientIndex, quotient);
        }
        if (instruction.GetOperandType(RemainderIndex) != OperandType.Null)
        {
            Store(instruction, RemainderIndex, remainder);
        }
    }

    // Each emitted vertex is a result under its own name.
    private void Emit()
    {
        int vertex = _emitted.Count;
        foreach (var semantic in _outputSemantics.OrderBy(o => o.Key))
        {
            _emitted.Add(new KeyValuePair<string, uint[]>(
                $"EMIT{vertex}_{semantic.Value}", [.. _output[semantic.Key]]));
        }
    }

    // A strip restart carries no value, but where it falls is part of the result.
    private void Cut()
    {
        _emitted.Add(new KeyValuePair<string, uint[]>(
            $"CUT{_emitted.Count}", [0, 0, 0, 0]));
    }

    /// <summary>
    /// store_structured: writes are results named by address, not by order, since
    /// two equivalent programs may order them differently.
    /// </summary>
    private void Store(D3D10Instruction instruction)
    {
        const int UnorderedAccessIndex = 0;
        const int ElementIndex = 1;
        const int OffsetIndex = 2;
        const int ValueIndex = 3;
        int resource = instruction.GetParamRegisterNumber(UnorderedAccessIndex);
        int element = Ints(instruction, ElementIndex)[0];
        int offset = Ints(instruction, OffsetIndex)[0];
        uint[] value = Source(instruction, ValueIndex);
        if (instruction.GetOperandType(UnorderedAccessIndex) == OperandType.ThreadGroupSharedMemory)
        {
            (int stride, uint[] words) = _threadGroupSharedMemory[resource];
            int writeMask = instruction.GetDestinationWriteMask();
            int word = SharedMemoryWord(stride, words, element, offset);
            for (int component = 0, written = 0; component < 4; component++)
            {
                if ((writeMask & (1 << component)) != 0)
                {
                    int index = word + written++;
                    if (index < words.Length)
                    {
                        words[index] = value[component];
                    }
                }
            }
            return;
        }
        // Per dword, so one 4-component store equals four 1-component stores.
        int uavWriteMask = instruction.GetDestinationWriteMask();
        for (int component = 0, written = 0; component < 4; component++)
        {
            if ((uavWriteMask & (1 << component)) != 0)
            {
                _stored.Add(new KeyValuePair<string, uint[]>(
                    $"STORE{resource}[{element}][{offset + 4 * written++}]",
                    [value[component], 0, 0, 0]));
            }
        }
    }

    // store_uav_typed: recorded per written component, named by the full texel
    // coordinate.
    private void StoreTyped(D3D10Instruction instruction)
    {
        const int UnorderedAccessIndex = 0;
        const int CoordinateIndex = 1;
        const int ValueIndex = 2;
        int resource = instruction.GetParamRegisterNumber(UnorderedAccessIndex);
        int[] coordinate = Ints(instruction, CoordinateIndex);
        uint[] value = Source(instruction, ValueIndex);
        int writeMask = instruction.GetDestinationWriteMask();
        for (int component = 0; component < 4; component++)
        {
            if ((writeMask & (1 << component)) != 0)
            {
                _stored.Add(new KeyValuePair<string, uint[]>(
                    $"STORETYPED{resource}[{coordinate[0]}][{coordinate[1]}][{coordinate[2]}][{component}]",
                    [value[component], 0, 0, 0]));
            }
        }
    }

    /// <summary>
    /// An atomic on one thread is plain arithmetic. Applied to groupshared memory,
    /// which is modelled; on a UAV it is recorded by address like a store.
    /// </summary>
    private void Atomic(D3D10Instruction instruction)
    {
        // imm_ forms put the result register first, shifting every operand by one.
        bool keepsOriginal = instruction.Opcode.IsImmediateAtomic();
        int resourceIndex = keepsOriginal ? 1 : 0;
        int addressIndex = resourceIndex + 1;
        int resource = instruction.GetParamRegisterNumber(resourceIndex);
        int[] address = Ints(instruction, addressIndex);
        bool hasCompare = instruction.Opcode
            is D3D10Opcode.AtomicCmpStore or D3D10Opcode.ImmAtomicCmpExch;
        uint value = Source(instruction, addressIndex + (hasCompare ? 2 : 1))[0];
        uint compare = hasCompare ? Source(instruction, addressIndex + 1)[0] : 0;

        if (instruction.GetOperandType(resourceIndex) == OperandType.ThreadGroupSharedMemory)
        {
            (int stride, uint[] words) = _threadGroupSharedMemory[resource];
            int word = _rawThreadGroupSharedMemory.Contains(resource)
                ? SharedMemoryWord(stride, words, address[0] / 4, 0)
                : SharedMemoryWord(stride, words, address[0], address[1]);
            uint held = word < words.Length ? words[word] : 0;
            if (word < words.Length)
            {
                words[word] = ApplyAtomic(instruction.Opcode, held, value, compare);
            }
            if (keepsOriginal)
            {
                Store(instruction, 0, [held, held, held, held]);
            }
            return;
        }
        _stored.Add(new KeyValuePair<string, uint[]>(
            $"ATOMIC{instruction.Opcode}{resource}[{address[0]}][{address[1]}]",
            [value, compare, 0, 0]));
        if (keepsOriginal)
        {
            // A non-zero stand-in, so a decompilation that converts instead of
            // bit-casting the old value is caught (zero survives both).
            uint held = HeldValue(resource, address);
            Store(instruction, 0, [held, held, held, held]);
        }
    }

    // Small, to stay within the comparison's tolerance; never zero, so losing it shows.
    private static uint HeldValue(int resource, int[] address)
    {
        uint hash = 2166136261;
        foreach (int part in new[] { resource, address[0], address[1] })
        {
            hash = (hash ^ (uint)part) * 16777619;
        }
        return hash % 1000 + 1;
    }

    private static uint ApplyAtomic(D3D10Opcode opcode, uint held, uint value, uint compare)
    {
        return opcode switch
        {
            D3D10Opcode.AtomicIAdd => held + value,
            D3D10Opcode.AtomicAnd => held & value,
            D3D10Opcode.AtomicOr => held | value,
            D3D10Opcode.AtomicXor => held ^ value,
            D3D10Opcode.AtomicIMax => (uint)Math.Max((int)held, (int)value),
            D3D10Opcode.AtomicIMin => (uint)Math.Min((int)held, (int)value),
            D3D10Opcode.AtomicUMax => Math.Max(held, value),
            D3D10Opcode.AtomicUMin => Math.Min(held, value),
            D3D10Opcode.AtomicCmpStore => held == compare ? value : held,
            D3D10Opcode.ImmAtomicIAdd => held + value,
            D3D10Opcode.ImmAtomicAnd => held & value,
            D3D10Opcode.ImmAtomicOr => held | value,
            D3D10Opcode.ImmAtomicXor => held ^ value,
            D3D10Opcode.ImmAtomicIMax => (uint)Math.Max((int)held, (int)value),
            D3D10Opcode.ImmAtomicIMin => (uint)Math.Min((int)held, (int)value),
            D3D10Opcode.ImmAtomicUMax => Math.Max(held, value),
            D3D10Opcode.ImmAtomicUMin => Math.Min(held, value),
            D3D10Opcode.ImmAtomicExch => value,
            D3D10Opcode.ImmAtomicCmpExch => held == compare ? value : held,
            _ => throw new UnsupportedException($"opcode {opcode}"),
        };
    }

    // Per dword by byte address, so a Store2 equals two Stores.
    private void StoreRaw(D3D10Instruction instruction)
    {
        int resource = instruction.GetParamRegisterNumber(0);
        int offset = Ints(instruction, 1)[0];
        uint[] value = Source(instruction, 2);
        int writeMask = instruction.GetDestinationWriteMask();
        // Groupshared memory is written, not recorded: the shader reads it back.
        bool shared = instruction.GetOperandType(0) == OperandType.ThreadGroupSharedMemory;
        uint[] words = shared ? _threadGroupSharedMemory[resource].Words : null;
        for (int component = 0, written = 0; component < 4; component++)
        {
            if ((writeMask & (1 << component)) != 0)
            {
                int address = offset + 4 * written++;
                if (shared)
                {
                    int word = address / 4;
                    if (word >= 0 && word < words.Length)
                    {
                        words[word] = value[component];
                    }
                    continue;
                }
                _stored.Add(new KeyValuePair<string, uint[]>(
                    $"STORERAW{resource}[{address}]", [value[component], 0, 0, 0]));
            }
        }
    }

    private uint[] ReadSharedWords(int resource, int byteOffset)
    {
        uint[] words = _threadGroupSharedMemory[resource].Words;
        var read = new uint[4];
        for (int word = 0; word < 4; word++)
        {
            int index = byteOffset / 4 + word;
            read[word] = index >= 0 && index < words.Length ? words[index] : 0;
        }
        return read;
    }

    // Clamped into the array; both programs clamp alike.
    private static int SharedMemoryWord(int stride, uint[] words, int element, int offset)
    {
        int elements = words.Length / (stride / 4);
        element = Math.Clamp(element, 0, Math.Max(elements - 1, 0));
        return element * (stride / 4) + Math.Clamp(offset, 0, stride - 4) / 4;
    }

    // imul high, low; a plain 32 bit multiply leaves high null.
    private void Multiply(D3D10Instruction instruction)
    {
        const int HighIndex = 0;
        const int LowIndex = 1;
        int[] left = Ints(instruction, 2);
        int[] right = Ints(instruction, 3);
        long[] product = [.. Enumerable.Range(0, 4).Select(i => (long)left[i] * right[i])];

        if (instruction.GetOperandType(HighIndex) != OperandType.Null)
        {
            Store(instruction, HighIndex, [.. product.Select(p => unchecked((uint)(p >> 32)))]);
        }
        if (instruction.GetOperandType(LowIndex) != OperandType.Null)
        {
            Store(instruction, LowIndex, [.. product.Select(p => unchecked((uint)p))]);
        }
    }

    // sincos sin, cos; either may be null.
    private void SineCosine(D3D10Instruction instruction)
    {
        const int SineIndex = 0;
        const int CosineIndex = 1;
        float[] angle = Floats(instruction, 2);
        if (instruction.GetOperandType(SineIndex) != OperandType.Null)
        {
            Store(instruction, SineIndex, Pack(angle.Select(MathF.Sin)));
        }
        if (instruction.GetOperandType(CosineIndex) != OperandType.Null)
        {
            Store(instruction, CosineIndex, Pack(angle.Select(MathF.Cos)));
        }
    }

    private void LoadImmediateConstantBuffer(D3D10Instruction instruction)
    {
        uint[] data = instruction.CustomData;
        for (int i = 0; i + 3 < data.Length; i += 4)
        {
            _immediateConstantBuffer.Add([data[i], data[i + 1], data[i + 2], data[i + 3]]);
        }
    }

    // Tests bits, not a number: a comparison writes all ones or all zeroes.
    private bool Test(D3D10Instruction instruction)
    {
        bool nonZero = Source(instruction, 0)[0] != 0;
        return instruction.TestNonZero ? nonZero : !nonZero;
    }

    private uint[] Evaluate(D3D10Instruction instruction)
    {
        switch (instruction.Opcode)
        {
            case D3D10Opcode.Mov:
                return Source(instruction, 1);
            case D3D10Opcode.MovC:
                {
                    uint[] condition = Source(instruction, 1);
                    uint[] whenSet = Source(instruction, 2);
                    uint[] whenClear = Source(instruction, 3);
                    return [.. Enumerable.Range(0, 4).Select(i => condition[i] != 0 ? whenSet[i] : whenClear[i])];
                }
            // Masked sum of absolute byte differences; a zero reference byte is
            // skipped. Both rules were the reader's guess until a WARP compute
            // probe ran the real instruction beside it.
            case D3D10Opcode.MSAD:
                {
                    uint[] reference = Source(instruction, 1);
                    uint[] windows = Source(instruction, 2);
                    uint[] accumulator = Source(instruction, 3);
                    var sums = new uint[4];
                    for (int window = 0; window < 4; window++)
                    {
                        uint sum = accumulator[window];
                        for (int b = 0; b < 4; b++)
                        {
                            uint referenceByte = (reference[window] >> (b * 8)) & 0xFF;
                            if (referenceByte == 0)
                            {
                                continue;
                            }
                            uint sourceByte = (windows[window] >> (b * 8)) & 0xFF;
                            sum += referenceByte > sourceByte
                                ? referenceByte - sourceByte
                                : sourceByte - referenceByte;
                        }
                        sums[window] = sum;
                    }
                    return sums;
                }
            case D3D10Opcode.DAdd:
                return Double(instruction, (a, b) => a + b);
            case D3D10Opcode.DMul:
                return Double(instruction, (a, b) => a * b);
            case D3D10Opcode.DDiv:
                return Double(instruction, (a, b) => a / b);
            case D3D10Opcode.DMin:
                return Double(instruction, Math.Min);
            case D3D10Opcode.DMax:
                return Double(instruction, Math.Max);
            case D3D10Opcode.DRCP:
                {
                    double[] value = Doubles(instruction, 1);
                    return PackValues(instruction,
                        n => DoubleWords(1 / value[instruction.GetValuePair(n)]));
                }
            case D3D10Opcode.DMov:
                {
                    double[] moved = Doubles(instruction, 1);
                    return PackValues(instruction,
                        n => DoubleWords(moved[instruction.GetValuePair(n)]));
                }
            case D3D10Opcode.DFMA:
                {
                    double[] factor1 = Doubles(instruction, 1);
                    double[] factor2 = Doubles(instruction, 2);
                    double[] addend = Doubles(instruction, 3);
                    return PackValues(instruction, n => DoubleWords(Math.FusedMultiplyAdd(
                        factor1[instruction.GetValuePair(n)],
                        factor2[instruction.GetValuePair(n)],
                        addend[instruction.GetValuePair(n)])));
                }
            case D3D10Opcode.DMovC:
                {
                    // One condition word per double, not per component.
                    uint[] condition = Source(instruction, 1);
                    double[] whenSet = Doubles(instruction, 2);
                    double[] whenClear = Doubles(instruction, 3);
                    return PackValues(instruction, n => DoubleWords(
                        condition[n] != 0
                            ? whenSet[instruction.GetValuePair(n)]
                            : whenClear[instruction.GetValuePair(n)]));
                }
            case D3D10Opcode.DEq:
                return DoubleComparison(instruction, (a, b) => a == b);
            case D3D10Opcode.DNe:
                return DoubleComparison(instruction, (a, b) => a != b);
            case D3D10Opcode.DLt:
                return DoubleComparison(instruction, (a, b) => a < b);
            case D3D10Opcode.DGe:
                return DoubleComparison(instruction, (a, b) => a >= b);
            case D3D10Opcode.DToF:
                {
                    double[] converted = Doubles(instruction, 1);
                    return PackValues(instruction,
                        n => [BitConverter.SingleToUInt32Bits((float)converted[n])]);
                }
            case D3D10Opcode.DToI:
                {
                    double[] converted = Doubles(instruction, 1);
                    return PackValues(instruction,
                        n => [unchecked((uint)(int)converted[n])]);
                }
            case D3D10Opcode.DToU:
                {
                    double[] converted = Doubles(instruction, 1);
                    return PackValues(instruction, n => [(uint)converted[n]]);
                }
            case D3D10Opcode.FToD:
                {
                    float[] converted = Floats(instruction, 1);
                    return PackValues(instruction, n => DoubleWords(converted[n]));
                }
            case D3D10Opcode.IToD:
                {
                    int[] converted = Ints(instruction, 1);
                    return PackValues(instruction, n => DoubleWords(converted[n]));
                }
            case D3D10Opcode.UToD:
                {
                    int[] converted = Ints(instruction, 1);
                    return PackValues(instruction, n => DoubleWords(unchecked((uint)converted[n])));
                }
            case D3D10Opcode.Add:
                return Float(instruction, (a, b) => a + b);
            case D3D10Opcode.Mul:
                return Float(instruction, (a, b) => a * b);
            case D3D10Opcode.Div:
                return Float(instruction, (a, b) => a / b);
            case D3D10Opcode.Min:
                return Float(instruction, Math.Min);
            case D3D10Opcode.Max:
                return Float(instruction, Math.Max);
            case D3D10Opcode.Mad:
                {
                    float[] a = Floats(instruction, 1);
                    float[] b = Floats(instruction, 2);
                    float[] c = Floats(instruction, 3);
                    return Pack(Enumerable.Range(0, 4).Select(i => a[i] * b[i] + c[i]));
                }
            case D3D10Opcode.Dp2:
                return BroadcastFloat(Dot(instruction, 2));
            case D3D10Opcode.Dp3:
                return BroadcastFloat(Dot(instruction, 3));
            case D3D10Opcode.Dp4:
                return BroadcastFloat(Dot(instruction, 4));
            case D3D10Opcode.Sqrt:
                return MapFloat(instruction, MathF.Sqrt);
            case D3D10Opcode.Rsq:
                return MapFloat(instruction, v => 1 / MathF.Sqrt(v));
            case D3D10Opcode.Exp:
                return MapFloat(instruction, v => MathF.Pow(2, v));
            case D3D10Opcode.Log:
                return MapFloat(instruction, MathF.Log2);
            case D3D10Opcode.Frc:
                return MapFloat(instruction, v => v - MathF.Floor(v));
            case D3D10Opcode.RoundNe:
                return MapFloat(instruction, v => MathF.Round(v, MidpointRounding.ToEven));
            case D3D10Opcode.RoundNi:
                return MapFloat(instruction, MathF.Floor);
            case D3D10Opcode.RoundPi:
                return MapFloat(instruction, MathF.Ceiling);
            case D3D10Opcode.RoundZ:
                return MapFloat(instruction, MathF.Truncate);
            case D3D10Opcode.Eq:
                return CompareFloat(instruction, (a, b) => a == b);
            case D3D10Opcode.Ne:
                return CompareFloat(instruction, (a, b) => a != b);
            case D3D10Opcode.LT:
                return CompareFloat(instruction, (a, b) => a < b);
            case D3D10Opcode.GE:
                return CompareFloat(instruction, (a, b) => a >= b);

            case D3D10Opcode.IAdd:
                return Int(instruction, (a, b) => a + b);
            case D3D10Opcode.UBFE:
            case D3D10Opcode.IBFE:
                {
                    // Width and offset use their low five bits; a zero width gives 0;
                    // a field past bit 31 is truncated. ibfe sign-extends.
                    uint[] width = Source(instruction, 1);
                    uint[] offset = Source(instruction, 2);
                    uint[] value = Source(instruction, 3);
                    bool signed = instruction.Opcode == D3D10Opcode.IBFE;
                    return [.. Enumerable.Range(0, 4).Select(i =>
                    {
                        int w = (int)(width[i] & 31);
                        int o = (int)(offset[i] & 31);
                        if (w == 0)
                        {
                            return 0u;
                        }
                        int up = w + o < 32 ? 32 - w - o : 0;
                        int down = w + o < 32 ? 32 - w : o;
                        uint raised = value[i] << up;
                        return signed ? unchecked((uint)((int)raised >> down)) : raised >> down;
                    })];
                }
            case D3D10Opcode.BFI:
                {
                    uint[] width = Source(instruction, 1);
                    uint[] offset = Source(instruction, 2);
                    uint[] insert = Source(instruction, 3);
                    uint[] value = Source(instruction, 4);
                    return [.. Enumerable.Range(0, 4).Select(i =>
                    {
                        int w = (int)(width[i] & 31);
                        int o = (int)(offset[i] & 31);
                        uint mask = (w == 0 ? 0u : (w >= 32 ? uint.MaxValue : (1u << w) - 1)) << o;
                        return ((insert[i] << o) & mask) | (value[i] & ~mask);
                    })];
                }
            case D3D10Opcode.IMad:
            case D3D10Opcode.Umad:
                {
                    int[] a = Ints(instruction, 1);
                    int[] b = Ints(instruction, 2);
                    int[] c = Ints(instruction, 3);
                    return [.. Enumerable.Range(0, 4)
                        .Select(i => unchecked((uint)(a[i] * b[i] + c[i])))];
                }
            case D3D10Opcode.IMin:
                return Int(instruction, Math.Min);
            case D3D10Opcode.IMax:
                return Int(instruction, Math.Max);
            case D3D10Opcode.UMin:
                return Bits(instruction, Math.Min);
            case D3D10Opcode.UMax:
                return Bits(instruction, Math.Max);
            case D3D10Opcode.INeg:
                return MapInt(instruction, v => -v);
            case D3D10Opcode.CountBits:
                return MapUInt(instruction, v => (uint)BitOperations.PopCount(v));
            case D3D10Opcode.FirstBitLo:
                // 0xffffffff for zero, unlike TrailingZeroCount.
                return MapUInt(instruction, v => v == 0 ? 0xFFFFFFFF : (uint)BitOperations.TrailingZeroCount(v));
            case D3D10Opcode.FirstBitHi:
                return MapUInt(instruction, v => v == 0 ? 0xFFFFFFFF : (uint)BitOperations.LeadingZeroCount(v));
            case D3D10Opcode.FirstBitSHi:
                // The highest bit differing from the sign; 0 and -1 give 0xffffffff.
                return MapUInt(instruction, v => v == 0 || v == 0xFFFFFFFF
                    ? 0xFFFFFFFF
                    : (uint)BitOperations.LeadingZeroCount((int)v < 0 ? ~v : v));
            case D3D10Opcode.BFRev:
                return MapUInt(instruction, ReverseBits);
            case D3D10Opcode.F32ToF16:
                return [.. Floats(instruction, 1).Select(v => (uint)(ushort)BitConverter.HalfToUInt16Bits((Half)v))];
            case D3D10Opcode.F16ToF32:
                return [.. Ints(instruction, 1).Select(v =>
                    BitConverter.SingleToUInt32Bits((float)BitConverter.UInt16BitsToHalf((ushort)v)))];
            case D3D10Opcode.Ieq:
                return CompareInt(instruction, (a, b) => a == b);
            case D3D10Opcode.Ine:
                return CompareInt(instruction, (a, b) => a != b);
            case D3D10Opcode.Ilt:
                return CompareInt(instruction, (a, b) => a < b);
            case D3D10Opcode.Ige:
                return CompareInt(instruction, (a, b) => a >= b);
            case D3D10Opcode.ULT:
                return CompareUInt(instruction, (a, b) => a < b);
            case D3D10Opcode.UGE:
                return CompareUInt(instruction, (a, b) => a >= b);
            case D3D10Opcode.And:
                return Bits(instruction, (a, b) => a & b);
            case D3D10Opcode.Or:
                return Bits(instruction, (a, b) => a | b);
            case D3D10Opcode.Xor:
                return Bits(instruction, (a, b) => a ^ b);
            case D3D10Opcode.Not:
                return [.. Source(instruction, 1).Select(v => ~v)];
            case D3D10Opcode.IShl:
                return Bits(instruction, (a, b) => a << (int)(b & 31));
            case D3D10Opcode.IShr:
                return Int(instruction, (a, b) => a >> (int)(b & 31));
            case D3D10Opcode.UShr:
                return Bits(instruction, (a, b) => a >> (int)(b & 31));
            case D3D10Opcode.IToF:
                return Pack(Ints(instruction, 1).Select(v => (float)v));
            case D3D10Opcode.UTof:
                return Pack(Source(instruction, 1).Select(v => (float)v));
            case D3D10Opcode.Ftoi:
                return [.. Floats(instruction, 1).Select(v => unchecked((uint)(int)v))];
            case D3D10Opcode.Ftou:
                return [.. Floats(instruction, 1).Select(v => (uint)Math.Max(v, 0))];

            case D3D10Opcode.Sample:
            case D3D10Opcode.SampleL:
            case D3D10Opcode.SampleB:
            case D3D10Opcode.SampleD:
                return SampleTexture(instruction);
            case D3D10Opcode.Gather4:
                return GatherTexture(instruction);
            // A register offset shifts later operands by one; a texel is 0.01 here.
            case D3D10Opcode.Gather4Po:
            case D3D10Opcode.Gather4PoC:
                {
                    float[] coordinates = Floats(instruction, 1);
                    int[] offset = Ints(instruction, 2);
                    float[] offsetCoordinates =
                    [
                        coordinates[0] + offset[0] * 0.01f,
                        coordinates[1] + offset[1] * 0.01f,
                        coordinates[2],
                        coordinates[3],
                    ];
                    float[] sampled = Texture.Sample(
                        instruction.GetParamRegisterNumber(3), offsetCoordinates);
                    int channel = instruction.GetSourceSwizzleComponents(4)[0];
                    if (instruction.Opcode == D3D10Opcode.Gather4PoC)
                    {
                        return BroadcastFloat(sampled[channel] >= Floats(instruction, 5)[0] ? 1 : 0);
                    }
                    return BroadcastFloat(sampled[channel]);
                }
            case D3D10Opcode.SampleC:
            case D3D10Opcode.SampleCLZ:
            // The texture is a function, not a grid, so a gather compares one texel.
            case D3D10Opcode.Gather4C:
                return CompareSample(instruction);
            case D3D10Opcode.LD:
                return LoadTexel(instruction);
            case D3D10Opcode.LdUAVTyped:
                return LoadViewTexel(instruction);
            case D3D10Opcode.LDMS:
                return LoadSample(instruction);
            case D3D10Opcode.ResInfo:
                return ResourceInfo(instruction);
            case D3D10Opcode.BufInfo:
                return BufferInfo(instruction);
            // Append/consume: only the buffer's counter is modelled.
            case D3D10Opcode.ImmAtomicAlloc:
            case D3D10Opcode.ImmAtomicConsume:
                {
                    int resource = instruction.GetParamRegisterNumber(1);
                    _appendCounters.TryGetValue(resource, out uint counter);
                    uint slot = instruction.Opcode == D3D10Opcode.ImmAtomicAlloc
                        ? counter
                        : counter - 1;
                    _appendCounters[resource] = instruction.Opcode == D3D10Opcode.ImmAtomicAlloc
                        ? counter + 1
                        : slot;
                    return [slot, slot, slot, slot];
                }
            // Nothing is multisampled, so an attribute has one value everywhere.
            case D3D10Opcode.EvalSampleIndex:
            case D3D10Opcode.EvalSnapped:
            case D3D10Opcode.EvalCentroid:
                return Source(instruction, 1);
            case D3D10Opcode.SampleInfo:
                return SampleCount(instruction);
            case D3D10Opcode.SamplePos:
                {
                    // Made up, but named by resource and sample index so that asking
                    // for the wrong sample or resource is caught.
                    string asked = instruction.GetOperandType(1) == OperandType.Rasterizer
                        ? "rasterizer"
                        : $"t{instruction.GetParamRegisterNumber(1)}";
                    // fxc swizzles the resource (t0.yxxx) to pick the lanes.
                    float[] position = [.. Named($"samplepos{asked}:{Ints(instruction, 2)[0]}")];
                    byte[] swizzle = instruction.GetSourceSwizzleComponents(1);
                    return Pack([.. swizzle.Select(c => position[c])]);
                }
            case D3D10Opcode.Lod:
                return LevelOfDetail(instruction);
            case D3D10Opcode.LdStructured:
                {
                    int element = Ints(instruction, 1)[0];
                    int offset = Ints(instruction, 2)[0];
                    int resource = instruction.GetParamRegisterNumber(3);
                    if (instruction.GetOperandType(3) == OperandType.ThreadGroupSharedMemory)
                    {
                        (int stride, uint[] words) = _threadGroupSharedMemory[resource];
                        int word = SharedMemoryWord(stride, words, element, offset);
                        var read = new uint[4];
                        for (int component = 0; component < 4; component++)
                        {
                            int index = word + component;
                            read[component] = index < words.Length && component < stride / 4 ? words[index] : 0;
                        }
                        byte[] swizzle = instruction.GetSourceSwizzleComponents(3);
                        return [read[swizzle[0]], read[swizzle[1]], read[swizzle[2]], read[swizzle[3]]];
                    }
                    // Each dword named by its byte offset in the element, so reading a
                    // member at a time equals reading four at once.
                    var element4 = new float[4];
                    for (int component = 0; component < 4; component++)
                    {
                        element4[component] = Named(
                            $"buffer{resource}[{element}][{offset + 4 * component}]")[0];
                    }
                    byte[] elementSwizzle = instruction.GetSourceSwizzleComponents(3);
                    return Pack([
                        element4[elementSwizzle[0]], element4[elementSwizzle[1]],
                        element4[elementSwizzle[2]], element4[elementSwizzle[3]]]);
                }
            case D3D10Opcode.LdRaw:
                {
                    // Each dword named by byte address, so a Load2 equals two Loads.
                    int offset = Ints(instruction, 1)[0];
                    int resource = instruction.GetParamRegisterNumber(2);
                    uint[] read = instruction.GetOperandType(2) == OperandType.ThreadGroupSharedMemory
                        ? ReadSharedWords(resource, offset)
                        : [.. Enumerable.Range(0, 4)
                            .Select(word => Pack(Named($"raw{resource}[{offset + 4 * word}]"))[0])];
                    byte[] swizzle = instruction.GetSourceSwizzleComponents(2);
                    return [read[swizzle[0]], read[swizzle[1]], read[swizzle[2]], read[swizzle[3]]];
                }
            case D3D10Opcode.DerivRtx:
            case D3D10Opcode.DerivRty:
            case D3D10Opcode.DerivRtxCoarse:
            case D3D10Opcode.DerivRtxFine:
            case D3D10Opcode.DerivRtyCoarse:
            case D3D10Opcode.DerivRtyFine:
                // No neighbouring pixels: zero.
                return [0, 0, 0, 0];
            case D3D10Opcode.Rcp:
                return Pack([.. Floats(instruction, 1).Select(v => 1f / v)]);
            default:
                throw new UnsupportedException($"opcode {instruction.Opcode}");
        }
    }

    private uint[] SampleTexture(D3D10Instruction instruction)
    {
        float[] coordinates = WithOffsets(instruction, Floats(instruction, 1));
        int resource = instruction.GetParamRegisterNumber(2);
        float[] sampled = Texture.Sample(resource, coordinates, TextureDimensions(resource));
        return Pack(ResourceSwizzle(instruction, sampled));
    }

    // One texel (the texture is a function, not a grid), of the channel the
    // sampler swizzle names (`s0.w` is alpha), broadcast.
    private uint[] GatherTexture(D3D10Instruction instruction)
    {
        float[] coordinates = WithOffsets(instruction, Floats(instruction, 1));
        int resource = instruction.GetParamRegisterNumber(2);
        float[] sampled = Texture.Sample(resource, coordinates, TextureDimensions(resource));
        int channel = instruction.GetSourceSwizzleComponents(3)[0];
        return BroadcastFloat(sampled[channel]);
    }

    // Coordinate count from dcl_resource; four without one.
    private int TextureDimensions(int resource)
    {
        D3D10Instruction declaration = _shader.Instructions.OfType<D3D10Instruction>()
            .FirstOrDefault(i => i.Opcode == D3D10Opcode.DclResource
                && i.GetParamRegisterNumber(0) == resource);
        return declaration?.GetResourceDimension() switch
        {
            ResourceDimension.Buffer or ResourceDimension.Texture1D => 1,
            ResourceDimension.Texture2D or ResourceDimension.Texture2Dms
                or ResourceDimension.Texture1DArray => 2,
            ResourceDimension.Texture3D or ResourceDimension.TextureCube
                or ResourceDimension.Texture2DArray or ResourceDimension.Texture2DmsArray => 3,
            _ => 4,
        };
    }

    // `sample r0, v0.xyxx, t2.yzxw, s0` puts the green channel in x.
    private static float[] ResourceSwizzle(D3D10Instruction instruction, float[] sampled)
    {
        byte[] swizzle = instruction.GetSourceSwizzleComponents(2);
        return [.. swizzle.Select(c => sampled[c])];
    }

    private uint[] CompareSample(D3D10Instruction instruction)
    {
        float[] coordinates = WithOffsets(instruction, Floats(instruction, 1));
        float reference = Floats(instruction, 4)[0];
        float[] sampled = Texture.Sample(instruction.GetParamRegisterNumber(2), coordinates);
        // A comparison gather tests the channel the sampler swizzle names.
        int channel = instruction.Opcode == D3D10Opcode.Gather4C
            ? instruction.GetSourceSwizzleComponents(3)[0]
            : 0;
        return BroadcastFloat(sampled[channel] >= reference ? 1 : 0);
    }

    // A made up size per slot, halved per mip, mip count in w, z zero. Returned as
    // float, reciprocal or uint per the instruction's return type.
    private uint[] ResourceInfo(D3D10Instruction instruction)
    {
        int slot = instruction.GetParamRegisterNumber(2);
        int mipLevel = Ints(instruction, 1)[0];
        const int mipCount = 9;
        float width = (256 + 16 * slot) >> Math.Clamp(mipLevel, 0, mipCount - 1);
        float height = (128 + 16 * slot) >> Math.Clamp(mipLevel, 0, mipCount - 1);
        float[] info = instruction.ResInfoReturnType == D3D10ResInfoReturnType.RcpFloat
            ? [1 / width, 1 / height, 0, mipCount]
            : [width, height, 0, mipCount];
        float[] swizzled = ResourceSwizzle(instruction, info);
        return instruction.ResInfoReturnType == D3D10ResInfoReturnType.Uint
            ? [.. swizzled.Select(v => (uint)v)]
            : Pack(swizzled);
    }

    private readonly Dictionary<int, uint> _appendCounters = [];

    // A made up element count derived from the slot.
    private uint[] BufferInfo(D3D10Instruction instruction)
    {
        uint count = (uint)(64 + 8 * instruction.GetParamRegisterNumber(1));
        return [count, count, count, count];
    }

    // Small and positive, since shaders index samples with it.
    private static uint[] SampleCount(D3D10Instruction instruction)
    {
        const uint samples = 4;
        uint value = instruction.ResInfoReturnType == D3D10ResInfoReturnType.Uint
            ? samples
            : BitConverter.SingleToUInt32Bits(samples);
        return [value, value, value, value];
    }

    /// <summary>
    /// A made up level derived from the coordinate. Swizzle x is clamped, y is
    /// unclamped, and the two differ so that picking the wrong one shows.
    /// </summary>
    private uint[] LevelOfDetail(D3D10Instruction instruction)
    {
        float[] coordinates = Floats(instruction, 1);
        float unclamped = 8 * (Texture.Sample(
            instruction.GetParamRegisterNumber(2), coordinates)[0] - 0.5f);
        bool isUnclamped = instruction.GetSourceSwizzleComponents(2)[0] == 1;
        float lod = isUnclamped ? unclamped : MathF.Max(unclamped, 0);
        uint bits = BitConverter.SingleToUInt32Bits(lod);
        return [bits, bits, bits, bits];
    }

    private uint[] LoadTexel(D3D10Instruction instruction)
    {
        // Texel addresses scaled to sample coordinates.
        int[] address = [.. Ints(instruction, 1)];
        float[] coordinates = WithOffsets(instruction,
            [address[0] * 0.01f, address[1] * 0.01f, address[2] * 0.01f, 0]);
        // Only the texture's own coordinates: the next component is the mip level.
        int resource = instruction.GetParamRegisterNumber(2);
        return Pack(ResourceSwizzle(instruction,
            Texture.Sample(resource, coordinates, TextureDimensions(resource))));
    }

    // UAV contents are not modelled, so a read returns the texture stand-in. No mip.
    private uint[] LoadViewTexel(D3D10Instruction instruction)
    {
        int[] address = [.. Ints(instruction, 1)];
        return Pack(ResourceSwizzle(instruction, Texture.Sample(
            instruction.GetParamRegisterNumber(2),
            [address[0] * 0.01f, address[1] * 0.01f, 0, 0])));
    }

    // Only xy of the address is read; the sample index becomes the third coordinate.
    private uint[] LoadSample(D3D10Instruction instruction)
    {
        int[] address = [.. Ints(instruction, 1)];
        int sample = Ints(instruction, 3)[0];
        float[] coordinates = WithOffsets(instruction,
            [address[0] * 0.01f, address[1] * 0.01f, sample * 0.1f, 0]);
        return Pack(ResourceSwizzle(instruction,
            Texture.Sample(instruction.GetParamRegisterNumber(2), coordinates)));
    }

    private static float[] WithOffsets(D3D10Instruction instruction, float[] coordinates)
    {
        if (instruction.SampleOffsets == null)
        {
            return coordinates;
        }

        return
        [
            coordinates[0] + instruction.SampleOffsets[0] * 0.01f,
            coordinates[1] + instruction.SampleOffsets[1] * 0.01f,
            coordinates[2] + instruction.SampleOffsets[2] * 0.01f,
            coordinates[3],
        ];
    }

    private float Dot(D3D10Instruction instruction, int length)
    {
        float[] a = Floats(instruction, 1);
        float[] b = Floats(instruction, 2);
        float sum = 0;
        for (int i = 0; i < length; i++)
        {
            sum += a[i] * b[i];
        }
        return sum;
    }

    private uint[] Float(D3D10Instruction instruction, Func<float, float, float> combine)
    {
        float[] a = Floats(instruction, 1);
        float[] b = Floats(instruction, 2);
        return Pack(Enumerable.Range(0, 4).Select(i => combine(a[i], b[i])));
    }

    private uint[] Int(D3D10Instruction instruction, Func<int, int, int> combine)
    {
        int[] a = Ints(instruction, 1);
        int[] b = Ints(instruction, 2);
        return [.. Enumerable.Range(0, 4).Select(i => unchecked((uint)combine(a[i], b[i])))];
    }

    private uint[] Bits(D3D10Instruction instruction, Func<uint, uint, uint> combine)
    {
        uint[] a = Source(instruction, 1);
        uint[] b = Source(instruction, 2);
        return [.. Enumerable.Range(0, 4).Select(i => combine(a[i], b[i]))];
    }

    private uint[] MapFloat(D3D10Instruction instruction, Func<float, float> transform)
    {
        return Pack(Floats(instruction, 1).Select(transform));
    }

    private uint[] MapUInt(D3D10Instruction instruction, Func<uint, uint> transform)
    {
        return [.. Ints(instruction, 1).Select(v => transform(unchecked((uint)v)))];
    }

    private static uint ReverseBits(uint value)
    {
        uint reversed = 0;
        for (int bit = 0; bit < 32; bit++)
        {
            reversed = (reversed << 1) | ((value >> bit) & 1);
        }
        return reversed;
    }

    private uint[] MapInt(D3D10Instruction instruction, Func<int, int> transform)
    {
        return [.. Ints(instruction, 1).Select(v => unchecked((uint)transform(v)))];
    }

    // All ones for true, so `and` with a comparison is a mask.
    private uint[] CompareFloat(D3D10Instruction instruction, Func<float, float, bool> compare)
    {
        float[] a = Floats(instruction, 1);
        float[] b = Floats(instruction, 2);
        return [.. Enumerable.Range(0, 4).Select(i => compare(a[i], b[i]) ? 0xFFFFFFFF : 0)];
    }

    private uint[] CompareInt(D3D10Instruction instruction, Func<int, int, bool> compare)
    {
        int[] a = Ints(instruction, 1);
        int[] b = Ints(instruction, 2);
        return [.. Enumerable.Range(0, 4).Select(i => compare(a[i], b[i]) ? 0xFFFFFFFF : 0)];
    }

    private uint[] CompareUInt(D3D10Instruction instruction, Func<uint, uint, bool> compare)
    {
        uint[] a = Source(instruction, 1);
        uint[] b = Source(instruction, 2);
        return [.. Enumerable.Range(0, 4).Select(i => compare(a[i], b[i]) ? 0xFFFFFFFF : 0)];
    }

    private static uint[] Pack(IEnumerable<float> values)
    {
        return [.. values.Select(BitConverter.SingleToUInt32Bits)];
    }

    /// <summary>
    /// A double is two words, low first; the nth double is at slot 2n (.xyxy for
    /// one double, .xyzw for a double2).
    /// </summary>
    private double[] Doubles(D3D10Instruction instruction, int index)
    {
        uint[] words = Source(instruction, index);
        return [
            BitConverter.UInt64BitsToDouble((ulong)words[1] << 32 | words[0]),
            BitConverter.UInt64BitsToDouble((ulong)words[3] << 32 | words[2]),
        ];
    }

    private static uint[] DoubleWords(double value)
    {
        ulong bits = BitConverter.DoubleToUInt64Bits(value);
        return [(uint)bits, (uint)(bits >> 32)];
    }

    /// <summary>
    /// Lays the nth computed value at the nth written component, or pair for doubles.
    /// </summary>
    private static uint[] PackValues(D3D10Instruction instruction, Func<int, uint[]> value)
    {
        int mask = instruction.GetDestinationWriteMask();
        if (instruction.WritesDoubles)
        {
            mask &= 0b0101;
        }
        var words = new uint[4];
        int ordinal = 0;
        for (int component = 0; component < 4; component++)
        {
            if ((mask & (1 << component)) == 0)
            {
                continue;
            }
            uint[] parts = value(ordinal++);
            for (int part = 0; part < parts.Length; part++)
            {
                words[component + part] = parts[part];
            }
        }
        return words;
    }

    // A value reads the pair it is written at: a double at .zw is the operand's
    // second even when it is the first value.
    private uint[] Double(D3D10Instruction instruction, Func<double, double, double> combine)
    {
        double[] a = Doubles(instruction, 1);
        double[] b = Doubles(instruction, 2);
        return PackValues(instruction, n => DoubleWords(
            combine(a[instruction.GetValuePair(n)], b[instruction.GetValuePair(n)])));
    }

    // One result word per pair.
    private uint[] DoubleComparison(D3D10Instruction instruction, Func<double, double, bool> compare)
    {
        double[] a = Doubles(instruction, 1);
        double[] b = Doubles(instruction, 2);
        return PackValues(instruction, n => [compare(a[n], b[n]) ? 0xFFFFFFFF : 0]);
    }

    private static uint[] BroadcastFloat(float value)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        return [bits, bits, bits, bits];
    }

    private float[] Floats(D3D10Instruction instruction, int index)
    {
        return [.. Source(instruction, index).Select(BitConverter.UInt32BitsToSingle)];
    }

    private int[] Ints(D3D10Instruction instruction, int index)
    {
        return [.. Source(instruction, index).Select(v => unchecked((int)v))];
    }

    /// <summary>
    /// Swizzled, with the modifier applied in the opcode's domain: negate on an
    /// integer instruction is an integer negate.
    /// </summary>
    private uint[] Source(D3D10Instruction instruction, int index)
    {
        uint[] register = ReadRegister(instruction, index);
        byte[] swizzle = instruction.GetSourceSwizzleComponents(index);
        var value = new uint[4];
        for (int i = 0; i < 4; i++)
        {
            value[i] = register[swizzle[i]];
        }

        D3D10OperandModifier modifier = instruction.GetOperandModifier(index);
        if (modifier == D3D10OperandModifier.None)
        {
            return value;
        }

        bool isInteger = instruction.Opcode.IsInteger();
        return [.. value.Select(v => ApplyModifier(modifier, v, isInteger))];
    }

    private static uint ApplyModifier(D3D10OperandModifier modifier, uint value, bool isInteger)
    {
        // Abs applies before Neg, and the two are flags rather than alternatives.
        if (isInteger)
        {
            int signed = unchecked((int)value);
            if (modifier.HasFlag(D3D10OperandModifier.Abs))
            {
                signed = Math.Abs(signed);
            }
            if (modifier.HasFlag(D3D10OperandModifier.Neg))
            {
                signed = -signed;
            }
            return unchecked((uint)signed);
        }

        float single = BitConverter.UInt32BitsToSingle(value);
        if (modifier.HasFlag(D3D10OperandModifier.Abs))
        {
            single = Math.Abs(single);
        }
        if (modifier.HasFlag(D3D10OperandModifier.Neg))
        {
            single = -single;
        }
        return BitConverter.SingleToUInt32Bits(single);
    }

    private uint[] ReadRegister(D3D10Instruction instruction, int index)
    {
        OperandType type = instruction.GetOperandType(index);
        switch (type)
        {
            case OperandType.Immediate32:
            case OperandType.Immediate64:
                // A double immediate's four words already form two doubles.
                return
                [
                    unchecked((uint)instruction.GetParamInt(index, 0)),
                    unchecked((uint)instruction.GetParamInt(index, 1)),
                    unchecked((uint)instruction.GetParamInt(index, 2)),
                    unchecked((uint)instruction.GetParamInt(index, 3)),
                ];
            case OperandType.Temp:
                return _temp[instruction.GetParamRegisterNumber(index)];
            case OperandType.IndexableTemp:
                return IndexableTempElement(instruction, index);
            case OperandType.Input:
                {
                    // Geometry and hull shaders read v[vertex][register]; elsewhere
                    // a two-index input is something else.
                    var indices = instruction.OperandTokens.GetOperandIndices(index);
                    if (indices.Length < 2
                        || _shader.Type is not (ShaderType.Geometry or ShaderType.Hull))
                    {
                        // `v[r0.x + 0]` over a dcl_indexrange, which
                        // GetParamRegisterNumber cannot resolve.
                        if (indices.Length == 1 && indices[0].IsRelative)
                        {
                            int element = (int)indices[0].Immediate
                                + RelativeIndex(instruction, index, 0);
                            return _input[Math.Clamp(element, 0, _input.Length - 1)];
                        }
                        return _input[instruction.GetParamRegisterNumber(index)];
                    }
                    // Not GetParamIndexImmediate32, which miscounts tokens with a
                    // nested relative operand.
                    int vertex = (int)indices[0].Immediate
                        + RelativeIndex(instruction, index, 0);
                    int register = (int)indices[1].Immediate;
                    return VertexInput(vertex, register);
                }
            case OperandType.InputControlPoint:
                {
                    // Named like a geometry shader's v[vertex][register]; the point
                    // includes its relative index (`vicp[r0.x + 1]`).
                    var indices = instruction.OperandTokens.GetOperandIndices(index);
                    int point = (int)indices[0].Immediate + RelativeIndex(instruction, index, 0);
                    return VertexInput(point, (int)indices[1].Immediate);
                }
            case OperandType.OutputControlPoint:
                {
                    // Phases run separately, so this is a made up value.
                    var indices = instruction.OperandTokens.GetOperandIndices(index);
                    int point = (int)indices[0].Immediate + RelativeIndex(instruction, index, 0);
                    int register = (int)indices[1].Immediate;
                    return [.. Named($"OUTPUTPOINT{register}[{point}]").Select(BitConverter.SingleToUInt32Bits)];
                }
            case OperandType.InputPatchConstant:
                {
                    // `vpc[r0.x + 2]` over a dcl_indexrange, as an instanced join reads.
                    var indices = instruction.OperandTokens.GetOperandIndices(index);
                    int patchRegister = indices.Length == 1 && indices[0].IsRelative
                        ? (int)indices[0].Immediate + RelativeIndex(instruction, index, 0)
                        : instruction.GetParamRegisterNumber(index);
                    // In a hull shader, a join phase's vpc# is what a fork phase wrote
                    // to o#; fork and join run in one program.
                    if (_shader.Type == ShaderType.Hull)
                    {
                        return _output[Math.Clamp(patchRegister, 0, _output.Length - 1)];
                    }
                    return [.. Named($"vpc{patchRegister}").Select(BitConverter.SingleToUInt32Bits)];
                }
            case OperandType.InputDomainPoint:
                return [.. Named("vDomain").Select(BitConverter.SingleToUInt32Bits)];
            case OperandType.Output:
                return _output[instruction.GetParamRegisterNumber(index)];
            case OperandType.ConstantBuffer:
                {
                    // A class's data is in the buffer `this` names: cb[r1.y + 0][r1.x + 0].
                    uint[][] buffer = Buffer(instruction.IsRelativelyAddressed(index, 0)
                        ? (int)instruction.OperandTokens.GetOperandIndices(index)[0].Immediate
                            + RelativeIndex(instruction, index, 0)
                        : instruction.GetParamRegisterNumber(index));
                    int offset = instruction.GetParamConstantBufferOffset(index)
                        + RelativeIndex(instruction, index, 1);
                    return buffer[Math.Clamp(offset, 0, buffer.Length - 1)];
                }
            case OperandType.ImmediateConstantBuffer:
                {
                    int row = (int)instruction.OperandTokens.GetOperandIndices(index)[0].Immediate
                        + RelativeIndex(instruction, index, 0);
                    return row >= 0 && row < _immediateConstantBuffer.Count
                        ? _immediateConstantBuffer[row]
                        : new uint[4];
                }
            case OperandType.InputThreadID:
            case OperandType.InputThreadGroupID:
            case OperandType.InputThreadIDInGroup:
            case OperandType.InputThreadIDInGroupFlattened:
            case OperandType.InputPrimitiveID:
            case OperandType.InputGSInstanceID:
                // Small integers: float bits as an index reach no buffer.
                return [.. Named(type.ToString()).Select(v => (uint)Math.Abs(v * 4) % 8)];
            case OperandType.OutputControlPointID:
                // Must be within the patch, since it subscripts it.
                return [.. Named("vOutputControlPointID")
                    .Select(v => (uint)Math.Abs(v * 4) % (uint)Math.Max(ControlPointCount, 1))];
            case OperandType.InputCoverageMask:
                // Independent of the output mask; a few bits at most.
                return [.. Named("COVERAGEINPUT").Select(v => (uint)Math.Abs(v * 4) % 16)];
            case OperandType.InputInnerCoverage:
                return [.. Named("INNERCOVERAGE").Select(v => (uint)Math.Abs(v * 4) % 2)];
            // The phase was unrolled per instance; each copy carries its number.
            case OperandType.InputForkInstanceID:
            case OperandType.InputJoinInstanceID:
                {
                    uint instance = (uint)(instruction.ForkInstance ?? 0);
                    return [instance, instance, instance, instance];
                }
            case OperandType.ThisPointer:
                {
                    // .x the buffer, .y the row the instance's data starts at.
                    int slot = instruction.GetParamRegisterNumber(index);
                    LoadClassData(slot);
                    return [(uint)(ClassDataBuffer + slot), 0, 0, 0];
                }
            case OperandType.Sampler:
            case OperandType.Resource:
            case OperandType.UnorderedAccessView:
                return new uint[4];
            default:
                throw new UnsupportedException($"reading operand type {type}");
        }
    }

    /// <summary>
    /// The body an fcall runs: that of the class bound to the instance. fxc may
    /// number tables and class types differently in the decompilation, so the
    /// class is picked by name, not by table.
    /// </summary>
    private int BodyStart(List<D3D10Instruction> instructions, D3D10Instruction call)
    {
        int function = (int)call.OperandTokens.Tokens[0];
        LinkageModel.InterfaceInfo iface = Linkage().InterfaceByNumber((int)call.OperandTokens.Tokens[2]);
        int instance = (int)call.OperandTokens.Tokens[3];
        int table = iface.Tables[Array.IndexOf(iface.TableClasses, BoundClass(iface, instance))];
        int body = _linkage.Tables.First(t => t.Number == table).Bodies[function];
        return instructions.FindIndex(i => i.Opcode == D3D10Opcode.Label
            && i.GetParamRegisterNumber(0) == body) + 1;
    }

    private LinkageModel Linkage()
    {
        if (_linkage != null)
        {
            return _linkage;
        }
        // Without class names (IFCE), the two sides have nothing to bind alike.
        if (_shader.Interfaces == null)
        {
            throw new UnsupportedException("a call through an interface with no class names");
        }
        try
        {
            _linkage = LinkageModel.Read(_shader);
        }
        catch (NotImplementedException e)
        {
            throw new UnsupportedException($"linkage the decompiler cannot read: {e.Message}");
        }
        return _linkage;
    }

    // One of the classes the instance can hold, by its name and the trial.
    private LinkageModel.ClassInfo BoundClass(LinkageModel.InterfaceInfo iface, int instance)
    {
        LinkageModel.ClassInfo[] classes = [.. iface.TableClasses
            .DistinctBy(c => c.Name).OrderBy(c => c.Name, StringComparer.Ordinal)];
        float pick = Named(iface.InstanceExpression(instance))[0];
        return classes[(int)(Math.Abs(pick) * 1000) % classes.Length];
    }

    /// <summary>
    /// Fills the data of the class bound at an interface slot, named by instance
    /// and class so member layout is all that matters.
    /// </summary>
    private void LoadClassData(int slot)
    {
        if (!_boundSlots.Add(slot))
        {
            return;
        }
        // Slots follow the interfaces in order, one per array element.
        int first = 0;
        foreach (LinkageModel.InterfaceInfo iface in Linkage().Interfaces.OrderBy(i => i.Number))
        {
            int count = Math.Max(iface.InstanceArrayLength, 1);
            if (slot < first + count)
            {
                string instance = iface.InstanceExpression(slot - first);
                string name = $"{instance}:{BoundClass(iface, slot - first).Name}";
                uint[][] buffer = Buffer(ClassDataBuffer + slot);
                for (int row = 0; row < 64; row++)
                {
                    buffer[row] = [.. Named($"{name}[{row}]").Select(BitConverter.SingleToUInt32Bits)];
                }
                return;
            }
            first += count;
        }
        throw new UnsupportedException($"this[{slot}] past the interface slots");
    }

    // x0[r0.x + 1]: array, then element, clamped like a constant buffer index.
    private uint[] IndexableTempElement(D3D10Instruction instruction, int operandIndex)
    {
        var indices = instruction.OperandTokens.GetOperandIndices(operandIndex);
        uint[][] array = _indexableTemps[(int)indices[0].Immediate];
        int element = (int)indices[1].Immediate + RelativeIndex(instruction, operandIndex, 1);
        return array[Math.Clamp(element, 0, array.Length - 1)];
    }

    // The register part of cb0[r0.x + 4]. Made up values may be out of range, so
    // callers clamp.
    private int RelativeIndex(D3D10Instruction instruction, int operandIndex, int index)
    {
        if (!instruction.IsRelativelyAddressed(operandIndex, index))
        {
            return 0;
        }

        (OperandType type, int number, byte component) =
            instruction.OperandTokens.GetRelativeIndexOperand(operandIndex, index);
        return type switch
        {
            OperandType.Temp => unchecked((int)_temp[number][component]),
            OperandType.Input => unchecked((int)_input[number][component]),
            _ => throw new UnsupportedException($"an index in {type}"),
        };
    }

    private void Store(D3D10Instruction instruction, uint[] value)
    {
        int? destinationIndex = instruction.GetDestinationParamIndex();
        if (destinationIndex != null)
        {
            Store(instruction, destinationIndex.Value, value);
        }
    }

    private void Store(D3D10Instruction instruction, int destinationIndex, uint[] value)
    {
        if (instruction.Saturate)
        {
            value = Pack(value.Select(v => Math.Clamp(BitConverter.UInt32BitsToSingle(v), 0, 1)));
        }

        OperandType type = instruction.GetOperandType(destinationIndex);
        int number = instruction.GetParamRegisterNumber(destinationIndex);
        // `mov o[vForkInstanceID.x + 0].x` in an unrolled fork phase: the register
        // is the range start plus this copy's instance.
        if (type == OperandType.Output && instruction.ForkInstance is int instance
            && instruction.OperandTokens.GetOperandIndices(destinationIndex)
                is [{ IsRelative: true } first, ..])
        {
            number = (int)first.Immediate + instance;
        }
        uint[] destination;
        switch (type)
        {
            case OperandType.Temp:
                destination = _temp[number];
                break;
            case OperandType.IndexableTemp:
                destination = IndexableTempElement(instruction, destinationIndex);
                break;
            case OperandType.Output:
                destination = _output[number];
                if (_outputSemantics.TryGetValue(number, out string semantic))
                {
                    _results[semantic] = destination;
                }
                break;
            case OperandType.OutputDepth:
            case OperandType.OutputDepthGreaterEqual:
            case OperandType.OutputDepthLessEqual:
                destination = _depth;
                _wroteDepth = true;
                break;
            case OperandType.OutputCoverageMask:
                destination = _coverage;
                _wroteCoverage = true;
                break;
            case OperandType.OutputStencilRef:
                destination = _stencilRef;
                _wroteStencilRef = true;
                break;
            case OperandType.Null:
                return;
            default:
                throw new UnsupportedException($"writing operand type {type}");
        }

        // By index: udiv has two destinations, and the first may be null.
        int writeMask = instruction.GetWriteMask(destinationIndex);
        for (int i = 0; i < 4; i++)
        {
            if ((writeMask & (1 << i)) != 0)
            {
                destination[i] = value[i];
            }
        }
    }
}
