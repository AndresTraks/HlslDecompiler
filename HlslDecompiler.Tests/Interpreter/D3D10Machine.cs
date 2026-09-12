using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Tests.Interpreter;

/// <summary>
/// The shader model 4 counterpart of <see cref="D3D9Machine"/>: runs a vertex or
/// pixel shader so that two decompilations of it can be compared by what they
/// compute.
///
/// A register here is four untyped 32 bit words. Whether a word is a float, an int
/// or a uint is the instruction's business, not the register's, which is why the
/// file is unsigned and every operation reinterprets.
/// </summary>
public class D3D10Machine
{
    /// <summary>Thrown for anything not modelled, so the caller can say so.</summary>
    public class UnsupportedException(string message) : Exception(message);

    private const int ConstantRegisterSizeInBytes = 16;

    private readonly ShaderModel _shader;
    private readonly int _trial;
    private readonly uint[][] _temp = NewFile(64);
    private readonly uint[][] _input = NewFile(64);
    private readonly uint[][] _output = NewFile(64);
    private readonly Dictionary<int, uint[][]> _constantBuffers = [];
    private readonly List<uint[]> _immediateConstantBuffer = [];
    private readonly Dictionary<int, string> _outputSemantics = [];
    private readonly Dictionary<int, string> _inputSemantics = [];
    private readonly Dictionary<(int Vertex, int Register), uint[]> _vertexInputs = [];
    private readonly Dictionary<string, uint[]> _results = [];
    private uint[] _depth = new uint[4];
    // What a geometry shader put on its stream and what a compute shader wrote,
    // which are the results those two have instead of a return value.
    private readonly List<KeyValuePair<string, uint[]>> _emitted = [];
    private readonly List<KeyValuePair<string, uint[]>> _stored = [];
    private bool _wroteDepth;

    /// <summary>Set by discard: the pixel is thrown away, whatever was written.</summary>
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
    /// Runs the shader and returns what it wrote, by semantic. Constants and inputs
    /// are derived from their own names and the trial number, so that two programs
    /// holding one value in different registers still agree on it.
    /// </summary>
    public static Dictionary<string, float[]> Run(ShaderModel shader, int trial)
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
            // A variable occupies a run of bytes in the buffer. Filling it a word at
            // a time, named by its position inside the variable, survives the buffer
            // being packed differently on the other side.
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

    // What a constant word holds depends on the type it was declared with. A float
    // bit pattern read as a uint is an enormous number, and a loop counting up to
    // one of those does not finish.
    private static uint ConstantWord(D3D10ConstantDeclaration declaration, float value)
    {
        switch (declaration.TypeInfo.ParameterType)
        {
            case ParameterType.Int:
            case ParameterType.Uint:
            case ParameterType.Uint8:
            case ParameterType.Bool:
                // Small and whole, and never negative: a negative loop bound read
                // as unsigned is the same problem again.
                return (uint)Math.Abs(value * 2) % 4;
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
            if (number >= _input.Length)
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

    // One value per vertex per register, named by the semantic so that two
    // programs agree on it however they number their inputs.
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

    private void Execute()
    {
        List<D3D10Instruction> instructions = [.. _shader.Instructions.OfType<D3D10Instruction>()];
        D3D10ControlFlow flow = D3D10ControlFlow.Match(instructions);
        // break leaves the innermost loop or switch, whichever it is inside, so
        // the two share one stack.
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
                    return;
                case D3D10Opcode.RetC:
                    if (Test(instruction))
                    {
                        return;
                    }
                    pc++;
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
                case D3D10Opcode.Emit:
                case D3D10Opcode.EmitThenCut:
                    Emit();
                    if (instruction.Opcode == D3D10Opcode.EmitThenCut)
                    {
                        Cut();
                    }
                    pc++;
                    continue;
                case D3D10Opcode.Cut:
                    Cut();
                    pc++;
                    continue;
                case D3D10Opcode.StoreStructured:
                    Store(instruction);
                    pc++;
                    continue;
                case D3D10Opcode.CustomData:
                    LoadImmediateConstantBuffer(instruction);
                    pc++;
                    continue;
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

    // continue belongs to the innermost loop, so a switch between it and that loop
    // is passed over rather than continued.
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

    // udiv writes the quotient to its first destination and the remainder to its
    // second, and a shader wanting only one of the two leaves the other null.
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

    /// <summary>
    /// A geometry shader appends the output registers to its stream. What it emits
    /// is its result, so each vertex is reported under its own name.
    /// </summary>
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
    /// store_structured is what a compute shader has instead of an output register,
    /// so the writes are the result. Named by where they went rather than by the
    /// order they happened in, since two programs may order them differently and
    /// still agree.
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
        _stored.Add(new KeyValuePair<string, uint[]>(
            $"STORE{resource}[{element}][{offset}]", Source(instruction, ValueIndex)));
    }

    // imul writes the high half of the product to its first destination and the
    // low half to its second; a shader wanting an ordinary 32 bit multiply leaves
    // the high one null.
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

    // sincos writes the sine to its first destination and the cosine to its second,
    // either of which may be null.
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

    /// <summary>
    /// The condition of if, breakc and the rest. The test is on the bits, not on a
    /// number: a comparison writes all ones or all zeroes.
    /// </summary>
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
            case D3D10Opcode.Gather4:
                return SampleTexture(instruction);
            case D3D10Opcode.SampleC:
            case D3D10Opcode.SampleCLZ:
                return CompareSample(instruction);
            case D3D10Opcode.LD:
                return LoadTexel(instruction);
            case D3D10Opcode.LdStructured:
                {
                    // Element and byte offset, named so that both programs read the
                    // same element of the same buffer whatever register it is in.
                    int element = Ints(instruction, 1)[0];
                    int offset = Ints(instruction, 2)[0];
                    int resource = instruction.GetParamRegisterNumber(3);
                    return Pack(Named($"buffer{resource}[{element}][{offset}]"));
                }
            case D3D10Opcode.DerivRtx:
            case D3D10Opcode.DerivRty:
                // No neighbouring pixel to difference against. Both programs get
                // zero, so the shader is still compared on everything else.
                return [0, 0, 0, 0];
            default:
                throw new UnsupportedException($"opcode {instruction.Opcode}");
        }
    }

    private uint[] SampleTexture(D3D10Instruction instruction)
    {
        float[] coordinates = WithOffsets(instruction, Floats(instruction, 1));
        float[] sampled = Texture.Sample(instruction.GetParamRegisterNumber(2), coordinates);
        return Pack(ResourceSwizzle(instruction, sampled));
    }

    // The resource operand carries a swizzle that says which channel of the texture
    // each component of the result comes from: `sample r0, v0.xyxx, t2.yzxw, s0`
    // puts the green channel in x. Ignoring it would leave this machine unable to
    // see a decompilation that ignored it too.
    private static float[] ResourceSwizzle(D3D10Instruction instruction, float[] sampled)
    {
        byte[] swizzle = instruction.GetSourceSwizzleComponents(2);
        return [.. swizzle.Select(c => sampled[c])];
    }

    private uint[] CompareSample(D3D10Instruction instruction)
    {
        float[] coordinates = WithOffsets(instruction, Floats(instruction, 1));
        float reference = Floats(instruction, 4)[0];
        float sampled = Texture.Sample(instruction.GetParamRegisterNumber(2), coordinates)[0];
        return BroadcastFloat(sampled >= reference ? 1 : 0);
    }

    private uint[] LoadTexel(D3D10Instruction instruction)
    {
        // The address is in texels rather than in the [0, 1] a sample takes, so it
        // is scaled before being handed to the same function. Both programs scale
        // it the same way; what matters is that they address the same texel.
        int[] address = [.. Ints(instruction, 1)];
        float[] coordinates = WithOffsets(instruction,
            [address[0] * 0.01f, address[1] * 0.01f, address[2] * 0.01f, 0]);
        return Pack(Texture.Sample(instruction.GetParamRegisterNumber(2), coordinates));
    }

    private static float[] WithOffsets(D3D10Instruction instruction, float[] coordinates)
    {
        if (instruction.SampleOffsets == null)
        {
            return coordinates;
        }

        // A texel offset moves the read. Scaled like the load address above, so that
        // dropping it shows up as a different result.
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

    private uint[] MapInt(D3D10Instruction instruction, Func<int, int> transform)
    {
        return [.. Ints(instruction, 1).Select(v => unchecked((uint)transform(v)))];
    }

    // A comparison writes all ones for true and all zeroes for false, which is what
    // makes `and` with a comparison a mask rather than a boolean.
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
    /// One source operand, swizzled and with its modifier applied. The modifier is
    /// arithmetic rather than bitwise - negate on an integer operand of an integer
    /// instruction is an integer negate - so it is applied in the domain the opcode
    /// reads the operand in.
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
                return
                [
                    unchecked((uint)instruction.GetParamInt(index, 0)),
                    unchecked((uint)instruction.GetParamInt(index, 1)),
                    unchecked((uint)instruction.GetParamInt(index, 2)),
                    unchecked((uint)instruction.GetParamInt(index, 3)),
                ];
            case OperandType.Temp:
                return _temp[instruction.GetParamRegisterNumber(index)];
            case OperandType.Input:
                {
                    // A geometry shader reads v[vertex][register], so the register
                    // number is the second index and the first says which vertex of
                    // the primitive. Every vertex gets its own value.
                    // Only a geometry shader indexes by vertex; elsewhere a two
                    // index input is something else and the register number stands.
                    var indices = instruction.OperandTokens.GetOperandIndices(index);
                    if (indices.Length < 2 || _shader.Type != ShaderType.Geometry)
                    {
                        return _input[instruction.GetParamRegisterNumber(index)];
                    }
                    // GetOperandIndices, not GetParamIndexImmediate32: the latter
                    // counts a token per index from the operand token onwards, which
                    // is neither the right place to start nor right for a nested
                    // operand. It gave vertex numbers in the millions.
                    int vertex = (int)indices[0].Immediate;
                    int register = (int)indices[1].Immediate;
                    return VertexInput(vertex, register);
                }
            case OperandType.Output:
                return _output[instruction.GetParamRegisterNumber(index)];
            case OperandType.ConstantBuffer:
                {
                    uint[][] buffer = Buffer(instruction.GetParamRegisterNumber(index));
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
                // Small whole numbers: a thread index taken from float bits would
                // address somewhere no buffer reaches.
                return [.. Named(type.ToString()).Select(v => (uint)Math.Abs(v * 4) % 8)];
            case OperandType.Sampler:
            case OperandType.Resource:
            case OperandType.UnorderedAccessView:
                return new uint[4];
            default:
                throw new UnsupportedException($"reading operand type {type}");
        }
    }

    // cb0[r0.x + 4] adds a register to the immediate. A made up index need not be
    // in range the way a real one is, so the caller clamps; both programs derive it
    // from the same named values, so they clamp to the same element.
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
        uint[] destination;
        switch (type)
        {
            case OperandType.Temp:
                destination = _temp[number];
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
            case OperandType.Null:
                return;
            default:
                throw new UnsupportedException($"writing operand type {type}");
        }

        // Named rather than taken from the first destination: udiv has two, and
        // the first may be the null one.
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
