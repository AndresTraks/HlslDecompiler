using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using HlslDecompiler.Hlsl.FlowControl;
using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace HlslDecompiler;

public class HlslSimpleWriter : HlslWriter
{
    private int _loopVariableIndex = -1;
    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;
    // Per phase, not per shader: a hull shader's phases read their own immediates.
    private IntegerOperandAnalysis _integerOperandAnalysis;

    public HlslSimpleWriter(ShaderModel shader)
        : base(shader)
    {
    }

    protected override bool WritesInstructions => true;

    protected override void WriteMethodBody()
    {
        _integerOperandAnalysis = new IntegerOperandAnalysis(_phaseShader);
        // Before FindMovedDoubles, which queries it.
        _programOrder = new ProgramOrder(_phaseShader,
            (instruction, operand) => GetSourceComponents(
                instruction, operand, instruction.GetSourceSwizzleComponents(operand)));
        FindMovedDoubles();
        // Reset per function: a hull shader's two functions share one writer.
        _loopVariableIndex = -1;
        _allocatedSlots.Clear();
        _consumedSlots.Clear();
        _consumeCount = 0;
        _destinationMaskOverride = null;
        if (_registers.MethodOutputRegisters.Count != 0)
        {
            if (_registers.HasSeveralStreams)
            {
                foreach (int stream in _registers.Streams)
                {
                    WriteLine("{0} {1};", StreamStructureName(stream),
                        _registers.StreamVariableName(stream));
                }
            }
            else
            {
                WriteLine("{0} {1};", _shader.Type == ShaderType.Geometry
                    ? GetOutputStructureName() : GetMethodReturnType(),
                    _registers.OutputVariableName);
            }
            WriteLine();
        }

        // With dynamic linkage, main stops at the first label; the rest are class methods.
        IList<Instruction> mainInstructions =
            _registers.Linkage != null && _registers.Linkage.HasLinkage
            ? [.. _phaseShader.Instructions.Take(_registers.Linkage.MainInstructionCount)]
            : _phaseShader.Instructions;

        WriteTemporaryVariableDeclarations(mainInstructions);
        WriteIndexableTempDeclarations(_integerOperandAnalysis);
        WritePreshaderOutputs();
        for (int index = 0; index < mainInstructions.Count; index++)
        {
            Instruction instruction = mainInstructions[index];
            if (instruction is D3D9Instruction d3d9Instruction)
            {
                WriteInstruction(d3d9Instruction);
            }
            else if (instruction is D3D10Instruction d9d10Instruction)
            {
                // imm_atomic_alloc + store_structured on an append buffer is one Append;
                // neither half can be written alone. The store need not follow directly.
                if (d9d10Instruction.Opcode == D3D10Opcode.ImmAtomicAlloc
                    && IsAppendResource(d9d10Instruction.GetParamRegisterKey(1)))
                {
                    _allocatedSlots[d9d10Instruction.GetParamRegisterKey(0)] = d9d10Instruction;
                    continue;
                }
                if (d9d10Instruction.Opcode == D3D10Opcode.StoreStructured
                    && d9d10Instruction.GetOperandType(1) != OperandType.Immediate32
                    && _allocatedSlots.TryGetValue(
                        d9d10Instruction.GetParamRegisterKey(1), out D3D10Instruction alloc)
                    && IsAppendPair(alloc, d9d10Instruction))
                {
                    if (!WriteStructAppend(alloc, d9d10Instruction))
                    {
                        WriteLine("{0}.Append({1});", GetOperandName(alloc, 1),
                            StoredBits(d9d10Instruction, 3)
                                ?? GetOperandName(d9d10Instruction, 3));
                    }
                    _allocatedSlots.Remove(d9d10Instruction.GetParamRegisterKey(1));
                    continue;
                }
                WriteInstruction(d9d10Instruction);
            }
        }

        if (_registers.MethodOutputRegisters.Count != 0 && _shader.Type != ShaderType.Geometry)
        {
            WriteLine();
            WriteLine("return {0};", _registers.OutputVariableName);
        }
    }

    /// <summary>
    /// Whether the store writes the same buffer at the slot the alloc returned.
    /// </summary>
    private static bool IsAppendPair(D3D10Instruction alloc, D3D10Instruction store)
    {
        return store.Opcode == D3D10Opcode.StoreStructured
            && store.GetParamRegisterNumber(0) == alloc.GetParamRegisterNumber(1)
            && store.GetOperandType(1) != OperandType.Immediate32
            && store.GetParamRegisterKey(1).Equals(alloc.GetParamRegisterKey(0));
    }

    // Pending allocs, by the register holding the slot, until their store arrives.
    private readonly Dictionary<RegisterKey, D3D10Instruction> _allocatedSlots = [];

    // The variable each consume call filled, by the register it left its slot in.
    private readonly Dictionary<RegisterKey, string> _consumedSlots = [];
    private int _consumeCount;

    private bool IsConsumeResource(RegisterKey registerKey)
    {
        return _registers.ResourceDefinitions.Any(d => d.BindPoint == registerKey.Number
            && d.ShaderInputType == D3DShaderInputType.UavConsumeStructured);
    }

    private bool IsAppendResource(RegisterKey registerKey)
    {
        return _registers.ResourceDefinitions.Any(d => d.BindPoint == registerKey.Number
            && d.ShaderInputType == D3DShaderInputType.UavAppendStructured);
    }

    /// <summary>
    /// Writes the constant registers an fx_2_0 preshader fills as locals, ahead of
    /// the body; no uniform is declared for them.
    /// </summary>
    private void WritePreshaderOutputs()
    {
        var registers = _ast.PreshaderOutputs
            .GroupBy(output => output.Key.RegisterKey)
            .OrderBy(register => ((D3D9RegisterKey)register.Key).Type)
            .ThenBy(register => register.Key.Number);
        if (!registers.Any())
        {
            return;
        }

        var compiler = new NodeCompiler(_registers);
        foreach (var register in registers)
        {
            string type = ((D3D9RegisterKey)register.Key).Type switch
            {
                RegisterType.ConstBool => "bool4",
                RegisterType.ConstInt => "int4",
                _ => "float4",
            };
            var components = register.OrderBy(output => output.Key.ComponentIndex).ToList();
            string mask = string.Concat(components.Select(output => "xyzw"[output.Key.ComponentIndex]));
            string name = GetPreshaderOutputName(register.Key);
            WriteLine("{0} {1};", type, name);
            WriteLine("{0}.{1} = {2};", name, mask,
                compiler.Compile(components.Select(output => output.Value).ToList()));
        }
        WriteLine();
    }

    private bool IsPreshaderOutput(RegisterKey registerKey)
    {
        return _ast.PreshaderOutputs.Keys.Any(key => key.RegisterKey.Equals(registerKey));
    }

    // c1 for the register, unless a uniform is already called that.
    private string GetPreshaderOutputName(RegisterKey registerKey)
    {
        string name = ((D3D9RegisterKey)registerKey).Type switch
        {
            RegisterType.ConstBool => "b",
            RegisterType.ConstInt => "i",
            _ => "c",
        } + registerKey.Number;
        while (_registers.ConstantDeclarations.Any(declaration => declaration.Name == name))
        {
            name += "_";
        }
        return name;
    }

    private void WriteTemporaryVariableDeclarations(IList<Instruction> instructions)
    {
        Dictionary<RegisterKey, int> registerWriteMasks = FindTemporaryRegisterAssignments(instructions);
        foreach (var register in registerWriteMasks)
        {
            WriteTemporaryVariableDeclaration(register.Key, register.Value);
        }
    }

    /// <summary>
    /// Declares a temp as wide and of the type it is written as, plus its double shadow.
    /// </summary>
    private void WriteTemporaryVariableDeclaration(RegisterKey registerKey, int writeMask)
    {
        bool isAddressRegister = registerKey is D3D9RegisterKey addressKey
            && addressKey.Type == RegisterType.Addr;
        // Shifts and bitwise operators will not take a float.
        string scalarType = isAddressRegister || IsIntegerTempRegister(registerKey, writeMask)
            ? "int"
            : IsHalfTempRegister(registerKey) ? "half" : "float";
        // `mova a0.xy` loads two indices, so a0 is as wide as it is written.
        string writeMaskName = isAddressRegister ? AddressTypeName(writeMask) : writeMask switch
        {
            0x1 => scalarType,
            0x3 => scalarType + "2",
            0x7 => scalarType + "3",
            0xF => scalarType + "4",
            _ => scalarType + "4",// TODO
        };
        // A register only doubles are written to needs no float variable. Decided by
        // the writes, not the mask: one register may hold a double and a float mask.
        if (FindFloatWrittenComponents(registerKey) != 0)
        {
            WriteLine("{0} {1};", writeMaskName, GetTempRegisterName(registerKey));
        }
        WriteDoubleRegisterDeclaration(registerKey);
    }

    /// <summary>
    /// Declares the shadow holding a register's doubles: `double2 d0` beside
    /// `float4 r0`, with r0.xy as d0.x and r0.zw as d0.y.
    /// </summary>
    private void WriteDoubleRegisterDeclaration(RegisterKey registerKey)
    {
        int pairs = GetDoublePairMask(registerKey);
        if (pairs == 0)
        {
            return;
        }
        WriteLine("{0} {1};", pairs == 1 ? "double" : "double2",
            GetDoubleRegisterName(registerKey));
    }

    /// <summary>
    /// The last instruction of main: before the first label under dynamic linkage.
    /// </summary>
    private Instruction LastMainInstruction()
    {
        return _registers.Linkage != null && _registers.Linkage.HasLinkage
            ? _phaseShader.Instructions[_registers.Linkage.MainInstructionCount - 1]
            : _phaseShader.Instructions[^1];
    }

    private void WriteBranchAttribute()
    {
        if (WritesFlowAttributes)
        {
            WriteLine("[branch]");
        }
    }

    /// <summary>
    /// Appends a struct element built member by member from the register (appending
    /// the float4 itself is X3017). False unless the store writes a struct from offset 0.
    /// </summary>
    private bool WriteStructAppend(D3D10Instruction alloc, D3D10Instruction store)
    {
        RegisterKey buffer = alloc.GetParamRegisterKey(1);
        ResourceDefinition definition = _registers.FindStructuredBuffer(buffer);
        if (definition?.ElementType?.MemberInfo is not { Count: > 0 }
            || store.GetOperandType(2) != OperandType.Immediate32
            || store.GetParamInt(2, 0) != 0)
        {
            return false;
        }
        int writeMask = store.GetWriteMask(0);
        List<int> components = [.. Enumerable.Range(0, 4).Where(c => (writeMask & (1 << c)) != 0)];
        string element = $"appended{_appendCount++}";
        IList<(string Name, int[] Values)> runs = _registers.FindStructuredMemberRuns(buffer, element, 0, components);
        if (runs == null)
        {
            return false;
        }
        string value = GetOperandName(store, 3);
        byte[] swizzle = store.GetSourceSwizzleComponents(3);
        string register = value.Contains('.') ? value[..value.IndexOf('.')] : value;
        WriteLine($"{GetStructuredElementType(definition)} {element};");
        foreach ((string name, int[] values) in runs)
        {
            string picked = string.Concat(values.Select(v => "xyzw"[swizzle[components[v]]]));
            WriteLine($"{name} = {register}.{picked};");
        }
        WriteLine($"{GetOperandName(alloc, 1)}.Append({element});");
        return true;
    }

    private int _appendCount;

    /// <summary>
    /// A call through an interface: the result lands in the register every body
    /// writes; arguments are the registers the bodies read before writing.
    /// </summary>
    private void WriteInterfaceCall(D3D10Instruction instruction)
    {
        int function = (int)instruction.OperandTokens.Tokens[0];
        int interfaceNumber = (int)instruction.OperandTokens.Tokens[2];
        int instance = (int)instruction.OperandTokens.Tokens[3];
        LinkageModel.InterfaceInfo iface = _registers.Linkage.InterfaceByNumber(interfaceNumber);
        LinkageModel.MethodInfo method = iface.Methods[function];
        string arguments = string.Join(", ", method.Parameters.Select(GetLinkageArgumentName));
        WriteLine("{0}{1} = {2}.{3}({4});", GetTempRegisterName(method.ReturnRegister),
            LinkageResultMask(method.ReturnComponents),
            iface.InstanceExpression(instance), method.Name, arguments);
    }

    // A float3 result fills r0.xyz.
    private static string LinkageResultMask(int components) =>
        components == 4 ? "" : "." + "xyzw"[..components];

    private string GetLinkageArgumentName(RegisterKey parameter)
    {
        if (parameter is D3D10RegisterKey { OperandType: OperandType.Input } input)
        {
            return _registers.GetRegisterName(new RegisterComponentKey(input, 0));
        }
        return GetTempRegisterName(parameter);
    }

    /// <summary>
    /// Transcribes a linkage body; its ret becomes a return of the register it writes.
    /// </summary>
    protected override void WriteLinkageMethodBody(LinkageModel.FunctionBodyInfo body)
    {
        // Classes are written before main, so build main's analyses here.
        _integerOperandAnalysis ??= new IntegerOperandAnalysis(_phaseShader);
        _programOrder ??= new ProgramOrder(_phaseShader,
            (instruction, operand) => GetSourceComponents(
                instruction, operand, instruction.GetSourceSwizzleComponents(operand)));
        FindMovedDoubles();

        foreach ((RegisterKey register, int writeMask) in FindTemporaryRegisterAssignments(
            [.. _phaseShader.Instructions.Skip(body.First).Take(body.Last - body.First)
                .Where(i => !_registers.Linkage.IsThisMove((D3D10Instruction)i))]))
        {
            WriteTemporaryVariableDeclaration(register, writeMask);
        }

        for (int index = body.First; index < body.Last; index++)
        {
            if (_phaseShader.Instructions[index] is D3D10Instruction instruction
                && instruction.Opcode != D3D10Opcode.Ret)
            {
                WriteInstruction(instruction);
            }
        }
        WriteLine();
        WriteLine("return {0}{1};", GetTempRegisterName(body.ReturnRegister),
            LinkageResultMask(body.ReturnComponents));
    }

    /// <summary>
    /// Whether the instruction is an append/consume slot write folded into the
    /// Append or Consume call, leaving no assignment of its own.
    /// </summary>
    private bool IsFoldedIntoCall(D3D10Instruction instruction)
    {
        return (instruction.Opcode == D3D10Opcode.ImmAtomicAlloc
                && IsAppendResource(instruction.GetParamRegisterKey(1)))
            || (instruction.Opcode == D3D10Opcode.ImmAtomicConsume
                && IsConsumeResource(instruction.GetParamRegisterKey(1)));
    }

    /// <summary>
    /// The byte offset in a structured atomic's address when it sits in a register
    /// written exactly once, by a mov of an immediate.
    /// </summary>
    private bool TryGetAddressOffsetConstant(
        D3D10Instruction instruction, int addressIndex, out int byteOffset)
    {
        byteOffset = 0;
        if (instruction.GetOperandType(addressIndex) != OperandType.Temp)
        {
            return false;
        }
        RegisterKey addressKey = instruction.GetParamRegisterKey(addressIndex);
        int component = instruction.GetSourceSwizzleComponents(addressIndex)[1];
        D3D10Instruction write = null;
        foreach (Instruction candidate in _phaseShader.Instructions)
        {
            if (candidate is not D3D10Instruction d3d10)
            {
                return false;
            }
            foreach (int destination in GetDestinationParamIndices(d3d10))
            {
                if (!Equals(d3d10.GetParamRegisterKey(destination), addressKey)
                    || (d3d10.GetWriteMask(destination) & (1 << component)) == 0)
                {
                    continue;
                }
                if (write != null)
                {
                    return false;
                }
                write = d3d10;
            }
        }
        if (write == null || write.Opcode != D3D10Opcode.Mov
            || write.GetOperandType(1) != OperandType.Immediate32)
        {
            return false;
        }
        byteOffset = write.GetOperandComponentSelection(1)
                == D3D10OperandNumComponents.Operand1Component
            ? write.GetParamInt(1)
            : write.GetParamInt(1, write.GetSourceSwizzleComponents(1)[component]);
        return true;
    }

    /// <summary>
    /// The components of a register written other than through its double shadow.
    /// </summary>
    private int FindFloatWrittenComponents(RegisterKey registerKey)
    {
        int mask = 0;
        foreach (Instruction instruction in _phaseShader.Instructions)
        {
            if (instruction is not D3D10Instruction d3d10)
            {
                return 0b1111;
            }
            if (IsFoldedIntoCall(d3d10))
            {
                continue;
            }
            // An interface call writes floats to its return register.
            if (InterfaceCallDestination(d3d10) is RegisterKey returned
                && Equals(returned, registerKey))
            {
                mask |= InterfaceCallMask(d3d10);
                continue;
            }
            foreach (int destination in GetDestinationParamIndices(d3d10))
            {
                if (!Equals(d3d10.GetParamRegisterKey(destination), registerKey))
                {
                    continue;
                }
                // Per statement, not per instruction: a load of a double and a uint
                // is written as two, and only the double's part uses the shadow.
                int[] split = SplitPackedOutputMasks(d3d10) ?? SplitDoubleElementMasks(d3d10);
                foreach (int part in split ?? [d3d10.GetWriteMask(destination)])
                {
                    _destinationMaskOverride = split == null ? null : part;
                    try
                    {
                        if (GetDoubleRegisterOperandName(d3d10, destination) == null)
                        {
                            mask |= part;
                        }
                    }
                    finally
                    {
                        _destinationMaskOverride = null;
                    }
                }
            }
        }
        return mask;
    }

    // Bit 0: the pair at .xy ever holds a double; bit 1: the pair at .zw.
    private int GetDoublePairMask(RegisterKey registerKey)
    {
        int pairs = 0;
        for (int pair = 0; pair < 2; pair++)
        {
            if (_doubleRegisterPairs.Contains((registerKey, pair)))
            {
                pairs |= 1 << pair;
            }
        }
        return pairs;
    }

    private static string GetDoubleRegisterName(RegisterKey registerKey)
    {
        return "d" + registerKey.Number;
    }

    // The destination components this statement writes; for an instruction
    // written as several statements, the current part.
    private int DestinationMask(D3D10Instruction instruction)
    {
        return _destinationMaskOverride ?? instruction.GetWriteMask(0);
    }

    /// <summary>
    /// Whether the member at the mask's first component is a double; other members
    /// of the same element may be floats.
    /// </summary>
    private bool IsDoubleStructuredComponent(D3D10Instruction instruction, int resourceOperand)
    {
        int mask = DestinationMask(instruction);
        int component = FirstComponent(mask);
        int elementByteOffset = instruction.GetOperandType(2) == OperandType.Immediate32
            ? instruction.GetParamInt(2, 0)
            : 0;
        int elementComponent = resourceOperand == 3
            ? instruction.GetSourceSwizzleComponents(3)[component]
            : component;
        return _registers.IsDoubleStructuredMember(
            instruction.GetParamRegisterKey(resourceOperand),
            elementByteOffset + elementComponent * 4);
    }

    /// <summary>
    /// Per mov, the destination pairs it copies a double into (bit 0 .xy, bit 1 .zw).
    /// Only the walk in FindMovedDoubles can tell: `mov r0.zw, r0.xxxy` looks the
    /// same whether it moves a double or two floats.
    /// </summary>
    private Dictionary<D3D10Instruction, int> _movedDoublePairs =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Every register pair ever holding a double; shadows are declared from this.
    /// A shader can hold doubles without any double instruction (load and store).
    /// </summary>
    private HashSet<(RegisterKey Register, int Pair)> _doubleRegisterPairs = [];

    /// <summary>
    /// The pairs holding a double just before each instruction (only where any do).
    /// </summary>
    private Dictionary<D3D10Instruction, HashSet<(RegisterKey Register, int Pair)>> _liveDoublePairs =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Per instruction of any opcode, the destination pairs it leaves holding a double.
    /// </summary>
    private Dictionary<D3D10Instruction, int> _madeDoublePairs =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Movs that join two 32-bit values into a double rather than carry one: their
    /// source is not a shadow, only their destination.
    /// </summary>
    private HashSet<D3D10Instruction> _joinedDoubleMovs =
        new(ReferenceEqualityComparer.Instance);

    private bool _hasDoubleInstructions;

    private ProgramOrder _programOrder;

    /// <summary>
    /// Tracks in program order which register pairs hold a double: from a double
    /// write or double-member load until either component is overwritten.
    /// </summary>
    private void FindMovedDoubles()
    {
        _movedDoublePairs = new Dictionary<D3D10Instruction, int>(ReferenceEqualityComparer.Instance);
        _madeDoublePairs = new Dictionary<D3D10Instruction, int>(ReferenceEqualityComparer.Instance);
        _joinedDoubleMovs = new HashSet<D3D10Instruction>(ReferenceEqualityComparer.Instance);
        _hasDoubleInstructions = _phaseShader.Instructions.OfType<D3D10Instruction>()
            .Any(i => i.HasDoubleOperands);
        _liveDoublePairs = new Dictionary<D3D10Instruction, HashSet<(RegisterKey, int)>>(
            ReferenceEqualityComparer.Instance);
        _doubleRegisterPairs = [];
        var live = new HashSet<(RegisterKey Register, int Pair)>();
        foreach (Instruction instruction in _phaseShader.Instructions)
        {
            // Recorded before the destination check: stores have no temp destination.
            if (instruction is D3D10Instruction withSources && live.Count != 0)
            {
                _liveDoublePairs[withSources] = [.. live];
            }
            if (instruction is not D3D10Instruction d3d10
                || d3d10.GetDestinationParamIndex() is not int destinationIndex
                || d3d10.GetParamRegisterKey(destinationIndex)
                    is not D3D10RegisterKey { IsTempRegister: true } destination)
            {
                continue;
            }
            int writeMask = d3d10.GetWriteMask(destinationIndex);
            int madeDouble = FindDoublesMade(d3d10, destination, writeMask, live);
            if (madeDouble != 0)
            {
                _madeDoublePairs[d3d10] = madeDouble;
                if (d3d10.Opcode == D3D10Opcode.Mov)
                {
                    _movedDoublePairs[d3d10] = madeDouble;
                }
            }
            // Writing either component of a pair ends the double it held.
            for (int pair = 0; pair < 2; pair++)
            {
                if ((writeMask & (0b11 << (pair * 2))) == 0)
                {
                    continue;
                }
                if ((madeDouble & (1 << pair)) != 0)
                {
                    live.Add((destination, pair));
                    _doubleRegisterPairs.Add((destination, pair));
                }
                else
                {
                    live.Remove((destination, pair));
                }
            }
        }
    }

    /// <summary>
    /// Whether the value a structured store writes was a live double at the store.
    /// </summary>
    private bool StoresLiveDouble(D3D10Instruction instruction)
    {
        if (instruction.Opcode != D3D10Opcode.StoreStructured
            || instruction.GetParamRegisterKey(3) is not D3D10RegisterKey { IsTempRegister: true } value
            || !_liveDoublePairs.TryGetValue(instruction, out HashSet<(RegisterKey, int)> live))
        {
            return false;
        }
        int mask = DestinationMask(instruction);
        byte[] swizzle = instruction.GetSourceSwizzleComponents(3);
        for (int component = 0; component < 4; component += 2)
        {
            if ((mask & (0b11 << component)) == (0b11 << component)
                && swizzle[component] % 2 == 0
                && swizzle[component + 1] == swizzle[component] + 1
                && live.Contains((value, swizzle[component] / 2)))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// A dword of dword-addressed groupshared memory: `g0[i][offset / 4 + c]`.
    /// </summary>
    private string SharedDword(D3D10Instruction instruction, int bufferOperand, int component)
    {
        string dword = instruction.GetOperandType(2) == OperandType.Immediate32
            ? (instruction.GetParamInt(2, 0) / 4 + component).ToString(CultureInfo.InvariantCulture)
            : $"(uint)({GetOperandName(instruction, 2)}).x / 4 + {component}";
        return $"{GetOperandName(instruction, bufferOperand)}[{GetOperandName(instruction, 1)}][{dword}]";
    }

    /// <summary>
    /// The shadow of the double a mov copies into two components of a non-temp
    /// register (an output), or null.
    /// </summary>
    private string MovedDoubleShadow(D3D10Instruction instruction)
    {
        if (instruction.GetParamRegisterKey(0) is D3D10RegisterKey { IsTempRegister: true }
            || BitOperations.PopCount((uint)instruction.GetDestinationWriteMask()) != 2
            || instruction.GetParamRegisterKey(1) is not D3D10RegisterKey { IsTempRegister: true } source
            || !_liveDoublePairs.TryGetValue(instruction, out HashSet<(RegisterKey, int)> live))
        {
            return null;
        }
        int mask = instruction.GetDestinationWriteMask();
        byte[] swizzle = instruction.GetSourceSwizzleComponents(1);
        int low = FirstComponent(mask);
        int high = FirstComponent(mask & ~(1 << low));
        if (swizzle[low] % 2 != 0 || swizzle[high] != swizzle[low] + 1
            || !live.Contains((source, swizzle[low] / 2)))
        {
            return null;
        }
        string name = GetDoubleRegisterName(source);
        return GetDoublePairMask(source) == 1 ? name : $"{name}.{"xy"[swizzle[low] / 2]}";
    }

    /// <summary>
    /// Writes a double stored into a non-double member as `asuint(d, e.x, e.y)`.
    /// False otherwise, including a store into a double member (a plain assignment).
    /// </summary>
    private bool WriteDoubleBitsStore(D3D10Instruction instruction, string target)
    {
        if (IsDoubleStructuredComponent(instruction, 0) || !StoresLiveDouble(instruction))
        {
            return false;
        }
        WriteLine("asuint({0}, {1}.x, {1}.y);", GetOperandName(instruction, 3), target);
        return true;
    }

    // The pairs of the destination this instruction leaves holding a double.
    private int FindDoublesMade(D3D10Instruction instruction, RegisterKey destination,
        int writeMask, HashSet<(RegisterKey Register, int Pair)> live)
    {
        int made = 0;
        for (int pair = 0; pair < 2; pair++)
        {
            int low = pair * 2;
            if ((writeMask & (0b11 << low)) != (0b11 << low))
            {
                continue;
            }
            bool isDouble;
            if (instruction.WritesDoubles)
            {
                isDouble = true;
            }
            else if (instruction.Opcode == D3D10Opcode.LdStructured)
            {
                byte[] resourceSwizzle = instruction.GetSourceSwizzleComponents(3);
                int elementByteOffset = instruction.GetOperandType(2) == OperandType.Immediate32
                    ? instruction.GetParamInt(2, 0)
                    : 0;
                isDouble = _registers.IsDoubleStructuredMember(
                    instruction.GetParamRegisterKey(3),
                    elementByteOffset + resourceSwizzle[low] * 4)
                    // Or a double instruction reads the loaded pair: the load joins
                    // the bits with asdouble.
                    || _programOrder.IsReadAsDouble(instruction, destination, pair);
            }
            else if (instruction.Opcode == D3D10Opcode.Mov)
            {
                byte[] swizzle = instruction.GetSourceSwizzleComponents(1);
                bool isAlignedPair = swizzle[low] % 2 == 0
                    && swizzle[low + 1] == swizzle[low] + 1;
                isDouble = false;
                if (instruction.GetParamRegisterKey(1)
                    is D3D10RegisterKey { IsTempRegister: true } source)
                {
                    isDouble = isAlignedPair && live.Contains((source, swizzle[low] / 2));
                }
                // Two integers (e.g. a uint2 uniform) moved into a pair a double
                // instruction reads: the mov joins the double's two words. Integers
                // only, since whether a float register holds bits is not recorded.
                else if (_hasDoubleInstructions && isAlignedPair
                    && _programOrder.IsReadAsDouble(instruction, destination, pair)
                    && GetSourceStorage(instruction, 1) == ComponentStorage.Integer)
                {
                    isDouble = true;
                    _joinedDoubleMovs.Add(instruction);
                }
            }
            else
            {
                isDouble = false;
            }
            if (isDouble)
            {
                made |= 1 << pair;
            }
        }
        return made;
    }

    /// <summary>
    /// Splits a two-component operand name (`bits.xy`, or a whole two-wide variable)
    /// into its components for asdouble. Null if it is neither.
    /// </summary>
    private static (string Low, string High)? SplitTwoComponentName(string name)
    {
        int dot = name.LastIndexOf('.');
        string swizzle = dot < 0 ? "" : name[(dot + 1)..];
        // A dot inside a subscript (`buffer[i.x]`) is not a swizzle.
        if (swizzle.Length == 0 || !swizzle.All(c => "xyzw".Contains(c)))
        {
            return ($"{name}.x", $"{name}.y");
        }
        return swizzle.Length == 2
            ? ($"{name[..dot]}.{swizzle[0]}", $"{name[..dot]}.{swizzle[1]}")
            : null;
    }

    /// <summary>
    /// Whether a structured load of a non-double member fills a pair a double
    /// instruction then reads, so the load is written as an asdouble.
    /// </summary>
    private bool MakesDoubleFromBits(D3D10Instruction instruction)
    {
        if (instruction.Opcode != D3D10Opcode.LdStructured
            || !_madeDoublePairs.ContainsKey(instruction)
            || IsDoubleStructuredComponent(instruction, 3))
        {
            return false;
        }
        // Only one pair loaded from a plain vector element; struct members are unsupported.
        return instruction.GetDestinationWriteMask() is 0b0011 or 0b1100
            && _registers.FindStructuredBuffer(instruction.GetParamRegisterKey(3))
                ?.ElementType is { MemberInfo: null or { Count: 0 } };
    }

    /// <summary>
    /// Names a constant-buffer double by its element, not its components: a double2's
    /// second double at cb0[0].zw is `.y`. A double4 spills into the next register.
    /// </summary>
    private string GetDoubleConstantOperandName(D3D10Instruction instruction, int operandIndex)
    {
        if (!instruction.IsDoubleOperand(operandIndex))
        {
            return null;
        }
        RegisterKey registerKey = instruction.GetParamRegisterKey(operandIndex);
        if (registerKey is not D3D10RegisterKey { OperandType: OperandType.ConstantBuffer })
        {
            return null;
        }
        byte[] swizzle = instruction.GetSourceSwizzleComponents(operandIndex);
        var componentKey = new RegisterComponentKey(registerKey, swizzle[0]);
        if (_registers.GetConstantComponentsPerElement(componentKey) != 2)
        {
            return null;
        }
        string name = _registers.GetRegisterName(componentKey);
        int width = _registers.GetRegisterMaskedLength(componentKey);
        if (width <= 1)
        {
            return name;
        }
        int componentBase = _registers.GetConstantComponentBase(componentKey);
        string elements = string.Concat(Enumerable.Range(0, instruction.ValueCount)
            .Select(value => "xyzw"[(swizzle[instruction.GetValuePair(value) * 2] - componentBase) / 2]));
        return elements == "xyzw"[..width] ? name : $"{name}.{elements}";
    }

    /// <summary>
    /// The shadow variable naming an operand's doubles, or null if it holds none.
    /// A source names one double per value computed; fxc puts the nth at slot 2n.
    /// </summary>
    private string GetDoubleRegisterOperandName(D3D10Instruction instruction, int operandIndex)
    {
        bool isDestination = instruction.IsDestinationOperand(operandIndex);
        // Structured loads/stores carry doubles by element type, not by opcode.
        bool isStructuredDouble = instruction.Opcode switch
        {
            D3D10Opcode.LdStructured => operandIndex == 0
                && (IsDoubleStructuredComponent(instruction, 3)
                    || MakesDoubleFromBits(instruction)),
            // A double stored into a non-double member is split by asuint at the store.
            D3D10Opcode.StoreStructured => operandIndex == 3
                && (IsDoubleStructuredComponent(instruction, 0)
                    || StoresLiveDouble(instruction)),
            _ => false,
        };
        int movedPairs = _movedDoublePairs.TryGetValue(instruction, out int moved) ? moved : 0;
        // A joining mov has no shadow on its source side.
        bool isMovedDouble = movedPairs != 0
            && (operandIndex == 0
                || (operandIndex == 1 && !_joinedDoubleMovs.Contains(instruction)));
        if (!isStructuredDouble && !isMovedDouble
            && (isDestination ? !instruction.WritesDoubles : !instruction.IsDoubleOperand(operandIndex)))
        {
            return null;
        }
        RegisterKey registerKey = instruction.GetParamRegisterKey(operandIndex);
        if (registerKey is not D3D10RegisterKey { IsTempRegister: true })
        {
            return null;
        }
        var pairs = new List<int>();
        if (isMovedDouble)
        {
            byte[] movSwizzle = instruction.GetSourceSwizzleComponents(1);
            for (int pair = 0; pair < 2; pair++)
            {
                if ((movedPairs & (1 << pair)) == 0)
                {
                    continue;
                }
                pairs.Add(isDestination ? pair : movSwizzle[pair * 2] / 2);
            }
        }
        // A store's value is read at the destination mask's components.
        else if (isStructuredDouble && !isDestination)
        {
            int storeMask = DestinationMask(instruction) & 0b0101;
            byte[] storeSwizzle = instruction.GetSourceSwizzleComponents(operandIndex);
            for (int component = 0; component < 4; component += 2)
            {
                if ((storeMask & (1 << component)) != 0)
                {
                    pairs.Add(storeSwizzle[component] / 2);
                }
            }
        }
        else if (isDestination)
        {
            int mask = (operandIndex == 0 ? DestinationMask(instruction)
                : instruction.GetWriteMask(operandIndex)) & 0b0101;
            for (int component = 0; component < 4; component += 2)
            {
                if ((mask & (1 << component)) != 0)
                {
                    pairs.Add(component / 2);
                }
            }
        }
        else
        {
            byte[] swizzle = instruction.GetSourceSwizzleComponents(operandIndex);
            for (int value = 0; value < instruction.ValueCount; value++)
            {
                pairs.Add(swizzle[instruction.GetValuePair(value) * 2] / 2);
            }
        }
        if (pairs.Count == 0)
        {
            return null;
        }
        // A double instruction reading a pair that holds bits, not a double (a plain
        // mov shuffled them there): its shadow was never written, so join the bits.
        if (!isDestination && !isMovedDouble && !isStructuredDouble
            && !pairs.All(pair => _liveDoublePairs.TryGetValue(instruction, out var live)
                && live.Contains((registerKey, pair))))
        {
            byte[] bitsSwizzle = instruction.GetSourceSwizzleComponents(operandIndex);
            string register = _registers.GetRegisterName(registerKey);
            List<string> joined = [.. Enumerable.Range(0, pairs.Count).Select(value =>
            {
                int low = instruction.GetValuePair(value) * 2;
                return $"asdouble(asuint({register}.{"xyzw"[bitsSwizzle[low]]}), "
                    + $"asuint({register}.{"xyzw"[bitsSwizzle[low + 1]]}))";
            })];
            return joined.Count == 1 ? joined[0] : $"double{joined.Count}({string.Join(", ", joined)})";
        }
        string name = GetDoubleRegisterName(registerKey);
        // Only the .xy pair: declared a scalar double.
        if (GetDoublePairMask(registerKey) == 1)
        {
            return name;
        }
        string subscript = string.Concat(pairs.Select(pair => "xy"[pair]));
        return subscript == "xy" ? name : $"{name}.{subscript}";
    }

    /// <summary>
    /// Whether every write of a register is _pp, so it can be declared half. One
    /// full-precision write is enough to keep it float.
    /// </summary>
    private bool IsHalfTempRegister(RegisterKey registerKey)
    {
        bool written = false;
        foreach (D3D9Instruction instruction in _phaseShader.Instructions.OfType<D3D9Instruction>())
        {
            foreach (int destination in GetDestinationParamIndices(instruction))
            {
                if (!instruction.GetParamRegisterKey(destination).Equals(registerKey))
                {
                    continue;
                }
                if (!instruction.GetDestinationResultModifier()
                    .HasFlag(ResultModifier.PartialPrecision))
                {
                    return false;
                }
                written = true;
            }
        }
        return written;
    }

    // Only if no float ever lands in it: a float in an int register would truncate.
    private bool IsIntegerTempRegister(RegisterKey registerKey, int writeMask)
    {
        if (_integerOperandAnalysis == null || registerKey is not D3D10RegisterKey)
        {
            return false;
        }
        return _integerOperandAnalysis.IsIntegerDeclaredRegister(registerKey);
    }

    private ComponentStorage GetStorage(D3D10Instruction instruction, int operandIndex, int component)
    {
        return _integerOperandAnalysis.GetStorage(
            new RegisterComponentKey(instruction.GetParamRegisterKey(operandIndex), component));
    }

    // The storage of what an operand reads.
    private ComponentStorage GetSourceStorage(D3D10Instruction instruction, int operandIndex)
    {
        if (instruction.GetOperandType(operandIndex) == OperandType.IndexableTemp)
        {
            return IsIntegerIndexableTemp(instruction, operandIndex)
                ? ComponentStorage.Integer
                : ComponentStorage.Numeric;
        }
        // A constant buffer variable keeps its declared type.
        if (instruction.GetOperandType(operandIndex) == OperandType.ConstantBuffer)
        {
            return IsIntegerConstant(instruction, operandIndex)
                ? ComponentStorage.Integer
                : ComponentStorage.Numeric;
        }
        if (instruction.GetOperandType(operandIndex) is not (OperandType.Temp or OperandType.Input
            or OperandType.InputThreadID or OperandType.InputThreadGroupID
            or OperandType.InputThreadIDInGroup or OperandType.InputThreadIDInGroupFlattened
            or OperandType.InputPrimitiveID or OperandType.InputGSInstanceID))
        {
            return ComponentStorage.Numeric;
        }
        // All components share the register's declaration.
        ComponentStorage storage = ComponentStorage.Numeric;
        foreach (byte component in instruction.GetSourceSwizzleComponents(operandIndex).Distinct())
        {
            storage = GetStorage(instruction, operandIndex, component);
        }
        return storage;
    }

    // Whether the array an indexable temp operand names is declared int.
    private bool IsIntegerIndexableTemp(D3D10Instruction instruction, int operandIndex)
    {
        return _integerOperandAnalysis.IsIntegerIndexableTemp(
            instruction.GetParamRegisterKey(operandIndex).Number);
    }

    // Whether every component of the operand is an int register's bits.
    private bool HoldsFloatBits(D3D10Instruction instruction, int operandIndex)
    {
        if (instruction.GetOperandType(operandIndex) != OperandType.Temp)
        {
            return false;
        }
        RegisterKey registerKey = instruction.GetParamRegisterKey(operandIndex);
        IEnumerable<int> components = instruction.IsDestinationOperand(operandIndex)
            ? Enumerable.Range(0, 4).Where(c => (instruction.GetWriteMask(operandIndex) & (1 << c)) != 0)
            : instruction.GetSourceSwizzleComponents(operandIndex).Distinct().Select(c => (int)c);
        return components.All(c =>
            _integerOperandAnalysis.HoldsFloatBits(new RegisterComponentKey(registerKey, c)));
    }

    private ComponentStorage GetDestinationStorage(D3D10Instruction instruction, int operandIndex)
    {
        if (instruction.GetOperandType(operandIndex) == OperandType.IndexableTemp)
        {
            return IsIntegerIndexableTemp(instruction, operandIndex)
                ? ComponentStorage.Integer
                : ComponentStorage.Numeric;
        }
        // oMask is a uint; outputs are typed by their signature.
        if (instruction.GetOperandType(operandIndex)
            is not (OperandType.Temp or OperandType.OutputCoverageMask or OperandType.Output))
        {
            return ComponentStorage.Numeric;
        }
        int writeMask = instruction.GetWriteMask(operandIndex);
        ComponentStorage storage = ComponentStorage.Numeric;
        for (int component = 0; component < 4; component++)
        {
            if ((writeMask & (1 << component)) == 0)
            {
                continue;
            }
            storage = instruction.GetOperandType(operandIndex) == OperandType.Output
                ? _integerOperandAnalysis.IsIntegerOutputSignature(
                    new RegisterComponentKey(instruction.GetParamRegisterKey(operandIndex), component))
                    ? ComponentStorage.Integer
                    : ComponentStorage.Numeric
                : GetStorage(instruction, operandIndex, component);
        }
        return storage;
    }

    // What an instruction writes, where the opcode alone does not say.
    private ValueKind GetProducedKind(D3D10Instruction instruction)
    {
        return instruction.Opcode switch
        {
            D3D10Opcode.ResInfo => instruction.ResInfoReturnType == D3D10ResInfoReturnType.Uint
                ? ValueKind.Integer
                : ValueKind.Float,
            D3D10Opcode.LdStructured => _integerOperandAnalysis.GetStructuredElementKind(instruction),
            D3D10Opcode.LD or D3D10Opcode.LDMS => _integerOperandAnalysis.GetTypedLoadKind(instruction),
            _ => instruction.Opcode.ProducedKind(),
        };
    }

    // What an instruction reads an operand as, where the opcode alone does not say.
    private ValueKind GetConsumedKind(D3D10Instruction instruction, int operandIndex)
    {
        if (instruction.Opcode == D3D10Opcode.StoreStructured)
        {
            return operandIndex == 3
                ? _integerOperandAnalysis.GetStructuredElementKind(instruction)
                : ValueKind.Integer;
        }
        // A typed view's coordinate is an integer texel index.
        if (instruction.Opcode is D3D10Opcode.StoreUAVTyped or D3D10Opcode.LdUAVTyped
            && operandIndex == 1)
        {
            return ValueKind.Integer;
        }
        // The stored texel is whatever the view holds, not always a float.
        if (instruction.Opcode == D3D10Opcode.StoreUAVTyped && operandIndex == 2)
        {
            return _integerOperandAnalysis.GetTypedStoreKind(instruction);
        }
        if (instruction.Opcode == D3D10Opcode.MovC && operandIndex == 1)
        {
            return ValueKind.Bits;
        }
        // The evaluation place (sample index, or offset in 1/16 pixel) is an integer.
        if (instruction.Opcode is D3D10Opcode.EvalSampleIndex or D3D10Opcode.EvalSnapped)
        {
            return operandIndex == 2 ? ValueKind.Integer : ValueKind.Float;
        }
        return instruction.Opcode.ConsumedKind();
    }

    /// <summary>
    /// Whether a break_comp compares two def constants and is true. A false result
    /// still keeps the conditional break rather than dropping it.
    /// </summary>
    private bool IsSettledComparison(D3D9Instruction instruction)
    {
        if (DefinedConstantValue(instruction, 0) is not float left
            || DefinedConstantValue(instruction, 1) is not float right)
        {
            return false;
        }
        return instruction.Comparison switch
        {
            IfComparison.GT => left > right,
            IfComparison.EQ => left == right,
            IfComparison.GE => left >= right,
            IfComparison.LT => left < right,
            IfComparison.NE => left != right,
            IfComparison.LE => left <= right,
            _ => false,
        };
    }

    /// <summary>
    /// The value a source reads from a def constant, with its modifier applied.
    /// </summary>
    private float? DefinedConstantValue(D3D9Instruction instruction, int operandIndex)
    {
        if (instruction.GetParamRegisterKey(operandIndex)
            is not D3D9RegisterKey { Type: RegisterType.Const } key)
        {
            return null;
        }
        ConstantRegister definition = _registers.ConstantDefinitions
            .FirstOrDefault(c => c.RegisterIndex == key.Number);
        if (definition == null)
        {
            return null;
        }
        float value = definition[instruction.GetSourceSwizzleComponents(operandIndex)[0]];
        return instruction.GetSourceModifier(operandIndex) switch
        {
            SourceModifier.None => value,
            SourceModifier.Negate => -value,
            SourceModifier.Abs => Math.Abs(value),
            SourceModifier.AbsAndNegate => -Math.Abs(value),
            _ => null,
        };
    }

    private static string AsInt(string name)
    {
        return $"asint({name})";
    }

    /// <summary>
    /// A mov/movc source crossing between int and float declarations: an integer is
    /// converted, a float held as bits is read back. Putting a float into bits is
    /// left to <see cref="WriteResult"/>, so a saturate stays inside it.
    /// </summary>
    private string Moved(D3D10Instruction instruction, int sourceIndex, string name)
    {
        if (instruction.GetOperandType(sourceIndex) == OperandType.Immediate32)
        {
            // A float immediate into an int register is written as its bits;
            // `asint(1)` would read a whole float as the integer 1.
            return !IsIntegerImmediate(instruction)
                && GetDestinationStorage(instruction, 0) == ComponentStorage.Integer
                ? ImmediateBits(instruction, sourceIndex)
                : name;
        }
        // Double shadows are doubles whatever the register's declaration.
        if (GetDoubleRegisterOperandName(instruction, sourceIndex) != null)
        {
            return name;
        }
        // Same declaration: carry the bits as they are (reading them as floats
        // would flush small integers to zero).
        ComponentStorage source = GetSourceStorage(instruction, sourceIndex);
        ComponentStorage destination = GetDestinationStorage(instruction, 0);
        if (source == destination)
        {
            return name;
        }
        // If every source crosses, WriteResult converts the whole expression once.
        if (IsReinterpretedResult(instruction, 0))
        {
            return name;
        }
        if (IsFloatValue(instruction))
        {
            return source == ComponentStorage.Integer ? $"asfloat({name})" : AsInt(name);
        }
        // As wide as the move: `(float)vThreadID.xy` would be x twice.
        int length = MovedComponentCount(instruction);
        string size = length == 1 ? "" : length.ToString();
        return destination == ComponentStorage.Integer ? $"(int{size}){name}" : $"(float{size}){name}";
    }

    private static int MovedComponentCount(D3D10Instruction instruction)
    {
        return instruction.HasDestination ? instruction.GetDestinationMaskLength() : 1;
    }

    // Whether an instruction's immediates print as integers: by opcode, or for a
    // mov by its readers.
    private bool IsIntegerImmediate(D3D10Instruction instruction)
    {
        // Their only immediate is a sample index or offset.
        if (instruction.Opcode is D3D10Opcode.EvalSampleIndex or D3D10Opcode.EvalSnapped
            or D3D10Opcode.SamplePos)
        {
            return true;
        }
        if (instruction.Opcode is D3D10Opcode.Mov or D3D10Opcode.MovC)
        {
            ValueKind readAs = _integerOperandAnalysis.GetImmediateKindByReaders(instruction);
            if (readAs != ValueKind.Unknown)
            {
                return readAs == ValueKind.Integer;
            }
        }
        return _integerOperandAnalysis.IsIntegerOperand(instruction);
    }

    /// <summary>
    /// Whether an instruction writes a float's bits into an int register. A move
    /// between registers of one declaration does not.
    /// </summary>
    private bool IsReinterpretedResult(D3D10Instruction instruction, int destinationIndex)
    {
        if (GetDestinationStorage(instruction, destinationIndex) != ComponentStorage.Integer
            || instruction.GetOperandType(destinationIndex) != OperandType.Temp)
        {
            return false;
        }
        if (instruction.Opcode is D3D10Opcode.Mov or D3D10Opcode.MovC)
        {
            int firstValue = instruction.Opcode == D3D10Opcode.Mov ? 1 : 2;
            ComponentStorage destination = GetDestinationStorage(instruction, destinationIndex);
            // An immediate is already written as bits.
            if (Enumerable.Range(firstValue, instruction.OperandTokens.OperandCount - firstValue)
                .Any(value => instruction.GetOperandType(value) == OperandType.Immediate32))
            {
                return false;
            }
            // A temp source of the same declaration carries its bits as they are.
            if (Enumerable.Range(firstValue, instruction.OperandTokens.OperandCount - firstValue)
                .Any(value => instruction.GetOperandType(value) == OperandType.Temp
                    && GetSourceStorage(instruction, value) == destination))
            {
                return false;
            }
        }
        return IsFloatValue(instruction);
    }

    /// <summary>
    /// Whether an instruction writes a float; for a mov, decided by its sources.
    /// </summary>
    private bool IsFloatValue(D3D10Instruction instruction)
    {
        if (instruction.Opcode is not (D3D10Opcode.Mov or D3D10Opcode.MovC))
        {
            return GetProducedKind(instruction) == ValueKind.Float;
        }
        // Saturate implies a float.
        if (instruction.Saturate)
        {
            return true;
        }
        int firstValue = instruction.Opcode == D3D10Opcode.Mov ? 1 : 2;
        for (int value = firstValue; value < instruction.OperandTokens.OperandCount; value++)
        {
            if (instruction.GetOperandType(value) == OperandType.Immediate32)
            {
                if (IsIntegerImmediate(instruction))
                {
                    return false;
                }
                continue;
            }
            if (!HoldsFloatBits(instruction, value)
                && (GetSourceStorage(instruction, value) != ComponentStorage.Numeric
                    || HoldsIntegers(instruction, value)))
            {
                return false;
            }
        }
        return true;
    }

    // Whether a numeric operand is only ever touched by integer instructions.
    private bool HoldsIntegers(D3D10Instruction instruction, int operandIndex)
    {
        RegisterKey key = instruction.GetParamRegisterKey(operandIndex);
        IEnumerable<int> components = instruction.IsDestinationOperand(operandIndex)
            ? Enumerable.Range(0, 4).Where(c => (instruction.GetWriteMask(operandIndex) & (1 << c)) != 0)
            : instruction.GetSourceSwizzleComponents(operandIndex).Distinct().Select(c => (int)c);
        return components.All(c =>
        {
            var component = new RegisterComponentKey(key, c);
            return _integerOperandAnalysis.IsIntegerRegister(component)
                && !_integerOperandAnalysis.IsFloatTouched(component);
        });
    }

    /// <summary>
    /// The register an fcall's bodies all return into (fcall has no destination
    /// operand); null for other instructions.
    /// </summary>
    private RegisterKey InterfaceCallDestination(D3D10Instruction instruction)
    {
        if (instruction.Opcode != D3D10Opcode.InterfaceCall || _registers.Linkage == null)
        {
            return null;
        }
        return _registers.Linkage.MethodForCall(
            (int)instruction.OperandTokens.Tokens[2],
            (int)instruction.OperandTokens.Tokens[0]).ReturnRegister;
    }

    // The components of an fcall's return register its result fills.
    private int InterfaceCallMask(D3D10Instruction instruction)
    {
        return (1 << _registers.Linkage.MethodForCall(
            (int)instruction.OperandTokens.Tokens[2],
            (int)instruction.OperandTokens.Tokens[0]).ReturnComponents) - 1;
    }

    private Dictionary<RegisterKey, int> FindTemporaryRegisterAssignments(IList<Instruction> instructions)
    {
        var tempRegisters = new Dictionary<RegisterKey, int>();
        foreach (Instruction instruction in instructions)
        {
            if (instruction is D3D10Instruction call
                && InterfaceCallDestination(call) is RegisterKey result)
            {
                if (!tempRegisters.TryAdd(result, InterfaceCallMask(call)))
                {
                    tempRegisters[result] |= InterfaceCallMask(call);
                }
                continue;
            }
            foreach (int destIndex in GetDestinationParamIndices(instruction))
            {
                if (!IsDestinationTempRegister(instruction, destIndex))
                {
                    continue;
                }
                int writeMask = instruction is D3D10Instruction d3d10Instruction
                    ? d3d10Instruction.GetWriteMask(destIndex)
                    : instruction.GetDestinationWriteMask();

                var registerKey = instruction.GetParamRegisterKey(destIndex);
                if (!tempRegisters.TryAdd(registerKey, writeMask))
                {
                    tempRegisters[registerKey] |= writeMask;
                }
            }
        }
        return tempRegisters;
    }

    // udiv, sincos and imul write two registers, which HasDestination does not
    // describe; either may be null.
    private static IEnumerable<int> GetDestinationParamIndices(Instruction instruction)
    {
        if (instruction is D3D10Instruction d3d10
            && (d3d10.Opcode == D3D10Opcode.Udiv || d3d10.Opcode == D3D10Opcode.SinCos
                || d3d10.Opcode == D3D10Opcode.IMul))
        {
            for (int index = 0; index < 2; index++)
            {
                if (d3d10.GetOperandType(index) != OperandType.Null)
                {
                    yield return index;
                }
            }
            yield break;
        }
        if (instruction.HasDestination)
        {
            yield return instruction.GetDestinationParamIndex().Value;
        }
    }

    private bool IsDestinationTempRegister(Instruction instruction, int destIndex)
    {
        if (instruction is D3D9Instruction d3d9)
        {
            RegisterType type = d3d9.GetParamRegisterType(destIndex);
            // Addr and Texture share a type number: a0 in a vertex shader, tN in a pixel shader.
            return type == RegisterType.Temp
                || (type == RegisterType.Addr && _shader.Type != ShaderType.Pixel);
        }
        return instruction is D3D10Instruction d3d10 && d3d10.GetParamRegisterKey(destIndex).IsTempRegister;
    }

    private static String GetTempRegisterName(RegisterKey registerKey)
    {
        if (registerKey.IsTempRegister)
        {
            return "r" + registerKey.Number;
        }
        // `mova` writes the address register like a temp.
        if (registerKey is D3D9RegisterKey d3d9RegisterKey && d3d9RegisterKey.Type == RegisterType.Addr)
        {
            return "a" + registerKey.Number;
        }
        throw new NotImplementedException();
    }

    private string GetModifier(D3D9Instruction instruction)
    {
        string source = "{1}";
        ResultModifier resultModifier = instruction.GetDestinationResultModifier();
        if (resultModifier.HasFlag(ResultModifier.Saturate))
        {
            source = $"saturate({source})";
        }
        // Not into a register already declared half.
        if (resultModifier.HasFlag(ResultModifier.PartialPrecision)
            && !IsHalfDestination(instruction))
        {
            string size = instruction.GetDestinationMaskLength().ToString();
            size = size == "1" ? "" : size;
            source = $"half{size}({source})";
        }
        return "{0} = " + source + ";";
    }

    private bool IsHalfDestination(D3D9Instruction instruction)
    {
        int? destination = instruction.GetDestinationParamIndex();
        if (destination == null)
        {
            return false;
        }
        RegisterKey key = instruction.GetParamRegisterKey(destination.Value);
        if (key.IsTempRegister)
        {
            return IsHalfTempRegister(key);
        }
        // An output declared at partial precision is already half.
        return _registers.MethodOutputRegisters
            .Any(declaration => declaration.RegisterKey.Equals(key)
                && declaration.ResultModifier.HasFlag(ResultModifier.PartialPrecision));
    }

    private void WriteInstruction(D3D9Instruction instruction)
    {
        switch (instruction.Opcode)
        {
            case Opcode.Abs:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"abs({GetSourceName(instruction, 1)})");
                break;
            case Opcode.Add:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"{GetSourceName(instruction, 1)} + {GetSourceName(instruction, 2)}");
                break;
            case Opcode.BreakC:
                // fxc writes an unconditional break as `break_ne c11.y, -c11.y` over def 1.
                if (IsSettledComparison(instruction))
                {
                    WriteLine("break;");
                    break;
                }
                WriteLine("if ({0} {2} {1}) break;", GetSourceName(instruction, 0), GetSourceName(instruction, 1), instruction.Comparison.ToHlslString());
                break;
            case Opcode.Cmp:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"({GetSourceName(instruction, 1)} >= 0) ? {GetSourceName(instruction, 2)} : {GetSourceName(instruction, 3)}");
                break;
            case Opcode.Crs:
                // Always reads three components, whatever the mask.
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"cross({GetSourceName(instruction, 1, 3)}, {GetSourceName(instruction, 2, 3)})");
                break;
            case Opcode.DP2Add:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    // The addend is a scalar, not the vectors' width.
                    $"dot({GetSourceName(instruction, 1)}, {GetSourceName(instruction, 2)}) + {GetSourceName(instruction, 3, 1)}");
                break;
            case Opcode.Dp3:
            case Opcode.Dp4:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"dot({GetSourceName(instruction, 1)}, {GetSourceName(instruction, 2)})");
                break;
            case Opcode.Dst:
                {
                    // dst reads fixed components, so both sources are named at full width.
                    string first = GetSourceName(instruction, 1, 4);
                    string second = GetSourceName(instruction, 2, 4);
                    WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                        $"float4(1, {first}.y * {second}.y, {first}.z, {second}.w)");
                    break;
                }
            case Opcode.DSX:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"ddx({GetSourceName(instruction, 1)})");
                break;
            case Opcode.DSY:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"ddy({GetSourceName(instruction, 1)})");
                break;
            case Opcode.Else:
                indent = indent.Substring(0, indent.Length - 1);
                WriteLine("} else {");
                indent += "\t";
                break;
            case Opcode.Endif:
                indent = indent.Substring(0, indent.Length - 1);
                WriteLine("}");
                break;
            case Opcode.EndLoop:
            case Opcode.EndRep:
                indent = indent.Substring(0, indent.Length - 1);
                WriteLine("}");
                _loopVariableIndex--;
                break;
            case Opcode.Exp:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"exp2({GetSourceName(instruction, 1)})");
                break;
            case Opcode.Frc:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"frac({GetSourceName(instruction, 1)})");
                break;
            case Opcode.If:
                WriteBranchAttribute();
                WriteLine("if ({0}) {{", GetSourceName(instruction, 0));
                indent += "\t";
                break;
            case Opcode.IfC:
                WriteBranchAttribute();
                WriteLine("if ({0} {2} {1}) {{", GetSourceName(instruction, 0), GetSourceName(instruction, 1), instruction.Comparison.ToHlslString());
                indent += "\t";
                break;
            case Opcode.Lit:
                {
                    string source = GetSourceName(instruction, 1, 4);
                    WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                        $"lit({source}.x, {source}.y, {source}.w)");
                    break;
                }
            case Opcode.Log:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"log2({GetSourceName(instruction, 1)})");
                break;
            case Opcode.Loop:
                int loopRegisterNumber = instruction.GetParamRegisterNumber(1);
                ConstantIntRegister intRegister = _registers.FindConstantIntRegister(loopRegisterNumber);
                _loopVariableIndex++;
                if (WritesFlowAttributes || (intRegister == null && LoopSamplesWithGradients(instruction)))
                {
                    WriteLine("[loop]");
                }
                string loopVariable = "i" + _loopVariableIndex;
                if (intRegister == null)
                {
                    // No defi: the trip count is a uniform.
                    string count = _registers.GetRegisterName(
                        new D3D9RegisterKey(RegisterType.ConstInt, loopRegisterNumber));
                    WriteLine("for (int {1} = 0; {1} < {0}; {1}++) {{", count, loopVariable);
                }
                else if (intRegister.Value[2] == 1)
                {
                    WriteLine("for (int {2} = {0}; {2} < {1}; {2}++) {{",
                        intRegister.Value[1], intRegister.Value[0], loopVariable);
                }
                else
                {
                    WriteLine("for (int {3} = {0}; {3} < {1}; {3} += {2}) {{",
                        intRegister.Value[1], intRegister.Value[0], intRegister.Value[2], loopVariable);
                }
                indent += "\t";
                break;
            case Opcode.Lrp:
                // lrp is src2 + src0 * (src1 - src2): operands reversed for lerp.
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"lerp({GetSourceName(instruction, 3)}, {GetSourceName(instruction, 2)}, {GetSourceName(instruction, 1)})");
                break;
            case Opcode.Mad:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"{GetSourceName(instruction, 1)} * {GetSourceName(instruction, 2)} + {GetSourceName(instruction, 3)}");
                break;
            case Opcode.M3x2:
            case Opcode.M3x3:
            case Opcode.M3x4:
            case Opcode.M4x3:
            case Opcode.M4x4:
                {
                    // mNxM: N is the vector/row width, M the row count (consecutive registers).
                    int columns = instruction.Opcode is Opcode.M4x3 or Opcode.M4x4 ? 4 : 3;
                    int rows = instruction.Opcode switch
                    {
                        Opcode.M3x2 => 2,
                        Opcode.M3x3 or Opcode.M4x3 => 3,
                        _ => 4,
                    };
                    string vector = GetSourceName(instruction, 1, columns);
                    var products = Enumerable.Range(0, rows).Select(
                        row => $"dot({vector}, {GetSourceName(instruction, 2, columns, row)})");
                    WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                        $"float{rows}({string.Join(", ", products)})");
                    break;
                }
            case Opcode.Max:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"max({GetSourceName(instruction, 1)}, {GetSourceName(instruction, 2)})");
                break;
            case Opcode.Min:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"min({GetSourceName(instruction, 1)}, {GetSourceName(instruction, 2)})");
                break;
            case Opcode.Mov:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction), GetSourceName(instruction, 1));
                break;
            case Opcode.MovA:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction), GetSourceName(instruction, 1));
                break;
            case Opcode.Mul:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"{GetSourceName(instruction, 1)} * {GetSourceName(instruction, 2)}");
                break;
            case Opcode.Nrm:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"normalize({GetSourceName(instruction, 1)})");
                break;
            case Opcode.Pow:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"pow({GetSourceName(instruction, 1)}, {GetSourceName(instruction, 2)})");
                break;
            case Opcode.Rcp:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"1 / {GetSourceName(instruction, 1)}");
                break;
            case Opcode.Rep:
                int repRegisterNumber = instruction.GetParamRegisterNumber(0);
                ConstantIntRegister loopRegister = _registers.FindConstantIntRegister(repRegisterNumber);
                _loopVariableIndex++;
                object repCount = loopRegister != null
                    ? loopRegister[0]
                    : _registers.GetRegisterName(
                        new D3D9RegisterKey(RegisterType.ConstInt, repRegisterNumber));
                if (WritesFlowAttributes || (loopRegister == null && LoopSamplesWithGradients(instruction)))
                {
                    WriteLine("[loop]");
                }
                WriteLine("for (int {1} = 0; {1} < {0}; {1}++) {{", repCount, "i" + _loopVariableIndex);
                indent += "\t";
                break;
            case Opcode.Rsq:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"rsqrt({GetSourceName(instruction, 1)})");
                break;
            case Opcode.Sge:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"({GetSourceName(instruction, 1)} >= {GetSourceName(instruction, 2)}) ? 1 : 0");
                break;
            case Opcode.Sgn:
                // The other two sources are scratch registers.
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"sign({GetSourceName(instruction, 1)})");
                break;
            case Opcode.Slt:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"({GetSourceName(instruction, 1)} < {GetSourceName(instruction, 2)}) ? 1 : 0");
                break;
            case Opcode.SinCos:
                WriteSinCos(instruction);
                break;
            case Opcode.Sub:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"{GetSourceName(instruction, 1)} - {GetSourceName(instruction, 2)}");
                break;
            case Opcode.Tex:
                if ((_shader.MajorVersion == 1 && _shader.MinorVersion >= 4) || (_shader.MajorVersion > 1))
                {
                    ConstantDeclaration sampler = _registers.FindConstant(RegisterSet.Sampler, instruction.GetParamRegisterNumber(2));
                    int samplerDimension = sampler.GetSamplerDimension();
                    string samplerType = sampler.TypeInfo.ParameterType == ParameterType.SamplerCube ? "CUBE" : (samplerDimension + "D");
                    if (instruction.TexldControls.HasFlag(TexldControls.Project))
                    {
                        WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                            $"tex{samplerType}proj({GetSourceName(instruction, 2)}, {GetSourceName(instruction, 1, 4)})");
                    }
                    else if (instruction.TexldControls.HasFlag(TexldControls.Bias))
                    {
                        WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                            $"tex{samplerType}bias({GetSourceName(instruction, 2)}, {GetSourceName(instruction, 1, 4)})");
                    }
                    else
                    {
                        WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                            $"tex{samplerType}({GetSourceName(instruction, 2)}, {GetSourceName(instruction, 1, samplerDimension)})");
                    }
                }
                else
                {
                    WriteLine(GetModifier(instruction), GetDestinationName(instruction), "tex2D()");
                }
                break;
            case Opcode.TexLDL:
                {
                    ConstantDeclaration sampler = _registers.FindConstant(RegisterSet.Sampler, instruction.GetParamRegisterNumber(2));
                    int samplerDimension = sampler.GetSamplerDimension();
                    string samplerType = sampler.TypeInfo.ParameterType == ParameterType.SamplerCube ? "CUBE" : (samplerDimension + "D");
                    WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                        $"tex{samplerType}lod({GetSourceName(instruction, 2)}, {GetSourceName(instruction, 1, 4)})");
                    break;
                }
            case Opcode.TexLDD:
                {
                    ConstantDeclaration sampler = _registers.FindConstant(RegisterSet.Sampler, instruction.GetParamRegisterNumber(2));
                    int samplerDimension = sampler.GetSamplerDimension();
                    string samplerType = sampler.TypeInfo.ParameterType == ParameterType.SamplerCube ? "CUBE" : (samplerDimension + "D");
                    WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                        $"tex{samplerType}grad({GetSourceName(instruction, 2)}, {GetSourceName(instruction, 1, samplerDimension)}, {GetSourceName(instruction, 3, samplerDimension)}, {GetSourceName(instruction, 4, samplerDimension)})");
                    break;
                }
            case Opcode.TexKill:
                WriteLine("clip({0});", GetDestinationName(instruction));
                break;
            case Opcode.Def:
            case Opcode.DefB:
            case Opcode.DefI:
            case Opcode.Dcl:
            case Opcode.Comment:
            case Opcode.End:
                break;
            // Instructions fxc never emits (cnd, expp, logp, bem, call, setp, ...)
            // have no fixture to check a transcription against.
            default:
                throw new NotImplementedException(instruction.Opcode.ToString());
        }
    }

    /// <summary>
    /// firstbit_hi/shi count from the top, HLSL's firstbithigh from the bottom:
    /// written as 31 - firstbithigh, with -1 where no bit is found (0, and -1 for
    /// the signed form). A uint cast selects firstbit_hi over firstbit_shi.
    /// </summary>
    private void WriteFirstBitHigh(D3D10Instruction instruction, bool signed)
    {
        int length = instruction.GetDestinationMaskLength();
        string size = length == 1 ? "" : length.ToString();
        string source = GetOperandName(instruction, 1);
        string value = signed || IsUnsignedOperand(instruction, 1) ? source : $"(uint{size}){source}";
        string found = signed ? $"{source} != 0 && {source} != -1" : $"{value} != 0";
        WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
            $"{found} ? 31 - firstbithigh({value}) : -1");
    }

    // ftoi/ftou/itof/utof: the inner cast reinterprets the source, the outer one
    // converts; both as wide as the write (a bare (float) over a vector is X3014).
    private void WriteConversion(D3D10Instruction instruction, string readAs, string convertTo)
    {
        int length = instruction.GetDestinationMaskLength();
        string size = length == 1 ? "" : length.ToString();
        string source = GetOperandName(instruction, 1);
        // `(int)` on an int register is a no-op; `(uint)` is not (utof != itof).
        string reinterpreted = readAs == null
                || (readAs == "int" && GetSourceStorage(instruction, 1) == ComponentStorage.Integer)
            ? source
            : $"({readAs}{size}){source}";
        WriteResult(instruction, "{0} = {1};",
            GetOperandName(instruction, 0), $"({convertTo}{size}){reinterpreted}");
    }

    /// <summary>
    /// Double conversions, sized by value count rather than component count.
    /// </summary>
    private void WriteDoubleConversion(D3D10Instruction instruction, string convertTo)
    {
        int length = instruction.ValueCount;
        string size = length == 1 ? "" : length.ToString(_culture);
        WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
            $"({convertTo}{size}){GetOperandName(instruction, 1)}");
    }

    // A comparison writes all ones (-1) for true and 0 for false, a mask.
    private void WriteComparison(D3D10Instruction instruction, string op, bool unsigned = false)
    {
        string left = GetOperandName(instruction, 1);
        string right = GetOperandName(instruction, 2);
        // Temps are declared signed, so ult/uge cast each non-uint side to uint.
        // Immediates are left alone: `(uint)0 <= x` draws a tautology warning.
        if (unsigned)
        {
            int length = instruction.GetDestinationMaskLength();
            string size = length == 1 ? "" : length.ToString();
            if (!IsUnsignedOperand(instruction, 1)
                && instruction.GetOperandType(1) != OperandType.Immediate32)
            {
                left = $"(uint{size}){left}";
            }
            if (!IsUnsignedOperand(instruction, 2)
                && instruction.GetOperandType(2) != OperandType.Immediate32)
            {
                right = $"(uint{size}){right}";
            }
        }
        WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
            $"({left} {op} {right}) ? -1 : 0");
    }

    // The unsigned half of a signed/unsigned opcode pair.
    private static bool ReadsUnsignedImmediates(D3D10Instruction instruction)
    {
        return instruction.Opcode is D3D10Opcode.ULT or D3D10Opcode.UGE
            or D3D10Opcode.UMax or D3D10Opcode.UMin or D3D10Opcode.UShr;
    }

    // An unsigned iadd operand cast to int when the other is a negative immediate:
    // HLSL adds uint and int as uint, so `id.x + -4` would wrap.
    private string SignedAddend(D3D10Instruction instruction, int operandIndex, int otherIndex)
    {
        string name = GetOperandName(instruction, operandIndex);
        if (!IsUnsignedOperand(instruction, operandIndex)
            || instruction.GetOperandType(otherIndex) != OperandType.Immediate32)
        {
            return name;
        }
        int[] components = GetSourceComponents(instruction, otherIndex,
            instruction.GetSourceSwizzleComponents(otherIndex));
        if (!components.Any(c => instruction.GetParamInt(otherIndex, c) < 0))
        {
            return name;
        }
        int length = instruction.GetDestinationMaskLength();
        string size = length == 1 ? "" : length.ToString();
        return $"(int{size}){name}";
    }

    /// <summary>
    /// Whether HLSL already reads the operand as uint (uint cbuffer variable,
    /// uint input, thread id). Temps are always declared int.
    /// </summary>
    private bool IsUnsignedOperand(D3D10Instruction instruction, int operandIndex)
    {
        if (instruction.GetOperandType(operandIndex) is OperandType.Immediate32
            or OperandType.Temp or OperandType.IndexableTemp)
        {
            return false;
        }
        return DeclaredTypeOf(instruction, operandIndex) == DeclaredType.Uint;
    }

    // and/or/xor on a float register go through asint/asfloat: HLSL rejects
    // bitwise operators on floats (X3082), and a conversion would change the bits.
    private void WriteBitwise(D3D10Instruction instruction, string op)
    {
        bool reinterpret = !IsIntegerDestination(instruction);
        string left = Reinterpreted(instruction, 1, reinterpret);
        string right = Reinterpreted(instruction, 2, reinterpret);
        string expression = $"{left} {op} {right}";
        WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
            reinterpret ? $"asfloat({expression})" : expression);
    }

    private string Reinterpreted(D3D10Instruction instruction, int operandIndex, bool reinterpret)
    {
        if (instruction.GetOperandType(operandIndex) == OperandType.Immediate32)
        {
            // The immediate's bits as an integer; `asint(1)` would read 1.0f as 1.
            return ImmediateBits(instruction, operandIndex);
        }
        string name = GetOperandName(instruction, operandIndex);
        return HoldsInteger(instruction, operandIndex) ? name : AsInt(name);
    }

    /// <summary>
    /// A structured load of mixed float and int members into an int register, built
    /// as an int vector with the floats as asint. Null unless the members are mixed.
    /// </summary>
    private string MixedStructuredLoad(D3D10Instruction instruction, RegisterKey buffer,
        string element, int offset, List<int> read)
    {
        bool?[] integer = [.. read.Select(c => _registers.IsIntegerStructuredMember(buffer, offset + c * 4))];
        if (GetDestinationStorage(instruction, 0) != ComponentStorage.Integer
            || integer.Any(kind => kind == null) || integer.All(kind => kind == integer[0])
            || _registers.FindStructuredMemberRuns(buffer, element, offset, read) is not { } runs)
        {
            return null;
        }
        IEnumerable<string> members = runs.Select(run => integer[run.Values[0]] == true
            ? run.Name
            : $"asint({run.Name})");
        return $"int{read.Count}({string.Join(", ", members)})";
    }

    // An immediate stored to int groupshared memory, as integer bits (l(3) is a
    // denormal float, which prints as 0).
    private string StoredImmediateBits(D3D10Instruction instruction)
    {
        if (instruction.GetOperandType(3) != OperandType.Immediate32
            || !IsGroupSharedOperand(instruction, 0)
            || !_integerOperandAnalysis.IsIntegerThreadGroupSharedMemory(
                instruction.GetParamRegisterKey(0).Number))
        {
            return null;
        }
        return ImmediateBits(instruction, 3);
    }

    private string ImmediateBits(D3D10Instruction instruction, int operandIndex)
    {
        D3D10RegisterKey registerKey = instruction.GetParamRegisterKey(operandIndex);
        if (registerKey.ImmediateSingle.Length == 1)
        {
            return instruction.GetParamInt(operandIndex, 0).ToString(_culture);
        }
        byte[] swizzle = instruction.GetSourceSwizzleComponents(operandIndex);
        int[] components = GetSourceComponents(instruction, operandIndex, swizzle);
        string[] bits = [.. components.Select(s => instruction.GetParamInt(operandIndex, s).ToString(_culture))];
        return $"int{components.Length}(" + string.Join(", ", bits) + ")";
    }

    private bool IsIntegerDestination(D3D10Instruction instruction)
    {
        int? destinationIndex = instruction.GetDestinationParamIndex();
        if (destinationIndex == null)
        {
            return true;
        }
        // Outputs are typed by their signature.
        if (instruction.GetOperandType(destinationIndex.Value) == OperandType.Output)
        {
            return !IsFloatOutput(instruction, destinationIndex.Value);
        }
        return GetDestinationStorage(instruction, destinationIndex.Value) == ComponentStorage.Integer;
    }

    private void WriteMsadLanes(D3D10Instruction instruction)
    {
        string[] reference = OperandComponentNames(instruction, 1);
        string[] windows = OperandComponentNames(instruction, 2);
        string[] accumulators = OperandComponentNames(instruction, 3);
        var lanes = new List<string>();
        for (int i = 0; i < reference.Length; i++)
        {
            lanes.Add($"msad4({reference[i]}, uint2({windows[i]}, 0), uint4({accumulators[i]}, 0, 0, 0)).x");
        }
        string value = lanes.Count == 1 ? lanes[0] : $"uint{lanes.Count}({string.Join(", ", lanes)})";
        WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0), value);
    }

    // `packed.xw` as packed.x and packed.w, one per written component.
    private string[] OperandComponentNames(D3D10Instruction instruction, int operandIndex)
    {
        string name = GetOperandName(instruction, operandIndex);
        int dot = name.LastIndexOf('.');
        string swizzle = dot < 0 ? "" : name[(dot + 1)..];
        int count = BitOperations.PopCount((uint)instruction.GetDestinationWriteMask());
        if (swizzle.Length == 0 || swizzle.Any(c => "xyzw".IndexOf(c) < 0))
        {
            return [.. Enumerable.Repeat(name, count)];
        }
        string baseName = name[..dot];
        return [.. Enumerable.Range(0, count)
            .Select(i => $"{baseName}.{swizzle[Math.Min(i, swizzle.Length - 1)]}")];
    }

    private void WriteResult(D3D10Instruction instruction, string format, params object[] args)
    {
        WriteResult(instruction, instruction.GetDestinationParamIndex() ?? 0, format, args);
    }

    private void WriteResult(D3D10Instruction instruction, int destinationIndex, string format, params object[] args)
    {
        if (instruction.Saturate)
        {
            format = Wrapped(format, "saturate");
        }
        // An integer result into a float output is its bits.
        if (GetProducedKind(instruction) == ValueKind.Integer
            && IsFloatOutput(instruction, destinationIndex)
            && GetDoubleRegisterOperandName(instruction, destinationIndex) == null)
        {
            format = Wrapped(format, "asfloat");
        }
        // A loaded dword read as a float is a float's bits (fxc would emit a utof).
        if (IsLoadedBitsResult(instruction, destinationIndex))
        {
            format = Wrapped(format, "asfloat");
        }
        // A float into an int register as asint, outside the saturate. Never a
        // double: its shadow is declared double.
        if (IsReinterpretedResult(instruction, destinationIndex)
            && GetDoubleRegisterOperandName(instruction, destinationIndex) == null)
        {
            format = Wrapped(format, "asint");
        }
        WriteLine(format, args);
    }

    /// <summary>
    /// Wraps a result's expression in a call: `{0} = x;` becomes `{0} = f(x);`.
    /// Throws on any other format shape.
    /// </summary>
    private static string Wrapped(string format, string call)
    {
        const string assignment = "{0} = ";
        if (!format.StartsWith(assignment) || !format.EndsWith(";"))
        {
            throw new NotImplementedException(
                $"A result that is not `{assignment}<expression>;` cannot be wrapped "
                + $"in {call}(): {format}");
        }
        return $"{assignment}{call}({format[assignment.Length..^1]});";
    }

    /// <summary>
    /// A float stored into integer memory, as its bits (fxc would emit an ftou to
    /// convert). Null otherwise. The mirror of IsLoadedBitsResult.
    /// </summary>
    private string StoredBits(D3D10Instruction instruction, int operandIndex)
    {
        if (GetConsumedKind(instruction, operandIndex) != ValueKind.Integer
            || GetSourceStorage(instruction, operandIndex) != ComponentStorage.Numeric
            || !HoldsStoredFloat(instruction, operandIndex))
        {
            return null;
        }
        string name = GetOperandName(instruction, operandIndex);
        // asint for a signed member, avoiding fxc's X3203 mismatch warning.
        return instruction.Opcode == D3D10Opcode.StoreStructured
            && _registers.IsUnsignedStructuredMember(
                instruction.GetParamRegisterKey(0),
                // Element offset plus the mask's first component.
                instruction.GetParamInt(2, 0)
                    + FirstComponent(DestinationMask(instruction)) * 4) == false
            ? AsInt(name)
            : $"asuint({name})";
    }

    /// <summary>
    /// Whether every component an operand reads was last written with a float.
    /// Asked of the last writer, since fxc reuses registers; a mov answers no.
    /// </summary>
    private bool HoldsStoredFloat(D3D10Instruction instruction, int operandIndex)
    {
        if (instruction.GetParamRegisterKey(operandIndex)
            is not D3D10RegisterKey { IsTempRegister: true } register)
        {
            return false;
        }
        int[] components = GetSourceComponents(instruction, operandIndex,
            instruction.GetSourceSwizzleComponents(operandIndex));
        return components.Length != 0
            && components.All(component => IsWrittenAsFloat(instruction, register, component));
    }

    private bool IsWrittenAsFloat(D3D10Instruction before, RegisterKey register, int component)
    {
        return _programOrder.LastWriterOf(before, register, component) is D3D10Instruction previous
            && GetProducedKind(previous) == ValueKind.Float;
    }

    /// <summary>
    /// Whether the operand is a buffer/UAV/raw index, which HLSL takes as uint
    /// (a texture Load takes a signed int3).
    /// </summary>
    private static bool IsUnsignedIndexOperand(D3D10Instruction instruction, int operandIndex)
    {
        return operandIndex == 1
            && instruction.Opcode is D3D10Opcode.LdUAVTyped or D3D10Opcode.StoreUAVTyped
                or D3D10Opcode.LdStructured or D3D10Opcode.StoreStructured
                or D3D10Opcode.LdRaw or D3D10Opcode.StoreRaw;
    }

    /// <summary>
    /// Whether every component an operand reads was last written by an ftoi.
    /// </summary>
    private bool HoldsSignedInteger(D3D10Instruction instruction, int operandIndex)
    {
        if (instruction.GetParamRegisterKey(operandIndex)
            is not D3D10RegisterKey { IsTempRegister: true } register)
        {
            return false;
        }
        int[] components = GetSourceComponents(instruction, operandIndex,
            instruction.GetSourceSwizzleComponents(operandIndex));
        return components.Length != 0
            && components.All(component => _programOrder
                .LastWriterOf(instruction, register, component)?.Opcode == D3D10Opcode.Ftoi);
    }

    /// <summary>
    /// Whether a memory load into a float register loads a float's bits rather than
    /// a number. Only loads: an ftou's integer is a number the shader asked for.
    /// </summary>
    private bool IsLoadedBitsResult(D3D10Instruction instruction, int destinationIndex)
    {
        if (instruction.Opcode is not (D3D10Opcode.LdStructured or D3D10Opcode.LdRaw
                or D3D10Opcode.LD or D3D10Opcode.LDMS)
            || GetProducedKind(instruction) != ValueKind.Integer
            || instruction.GetOperandType(destinationIndex) != OperandType.Temp
            || GetDestinationStorage(instruction, destinationIndex) != ComponentStorage.Numeric
            || GetDoubleRegisterOperandName(instruction, destinationIndex) != null)
        {
            return false;
        }
        // Asked of this load's own readers, not of the register's other uses;
        // every written component must be read as a float.
        RegisterKey registerKey = instruction.GetParamRegisterKey(destinationIndex);
        int mask = DestinationMask(instruction);
        for (int component = 0; component < 4; component++)
        {
            if ((mask & (1 << component)) != 0
                && !IsReadAsFloat(instruction, registerKey, component))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Whether the first reader of a component after the given write reads a float.
    /// False if nothing reads it.
    /// </summary>
    private bool IsReadAsFloat(D3D10Instruction after, RegisterKey register, int component)
    {
        return _programOrder.FirstReaderOf(after, register, component)
                is (D3D10Instruction reader, int operand)
            && GetConsumedKind(reader, operand) == ValueKind.Float;
    }

    // Whether the destination is an output the signature does not type as integer.
    private bool IsFloatOutput(D3D10Instruction instruction, int destinationIndex)
    {
        if (instruction.GetOperandType(destinationIndex) != OperandType.Output)
        {
            return false;
        }
        int writeMask = instruction.GetWriteMask(destinationIndex);
        D3D10RegisterKey registerKey = instruction.GetParamRegisterKey(destinationIndex);
        for (int component = 0; component < 4; component++)
        {
            if ((writeMask & (1 << component)) != 0)
            {
                return !_integerOperandAnalysis.IsIntegerOutputSignature(
                    new RegisterComponentKey(registerKey, component));
            }
        }
        return false;
    }

    /// <summary>
    /// The components of the current statement while one instruction is written as
    /// several, e.g. `mov o1.xyz` over TEXCOORD at o1.xy and TEXCOORD1 at o1.z.
    /// </summary>
    private int? _destinationMaskOverride;

    private static int FirstComponent(int mask)
    {
        return Enumerable.Range(0, 4).First(c => (mask & (1 << c)) != 0);
    }

    /// <summary>
    /// The written components grouped by output declaration, or null if only one.
    /// </summary>
    private int[] SplitPackedOutputMasks(D3D10Instruction instruction)
    {
        int? destinationIndex = instruction.GetDestinationParamIndex();
        if (destinationIndex == null)
        {
            return null;
        }
        D3D10RegisterKey registerKey = instruction.GetParamRegisterKey(destinationIndex.Value);
        if (!registerKey.IsOutput)
        {
            return null;
        }
        int writeMask = instruction.GetWriteMask(destinationIndex.Value);
        var masks = new List<int>();
        string last = null;
        for (int component = 0; component < 4; component++)
        {
            if ((writeMask & (1 << component)) == 0)
            {
                continue;
            }
            string semantic = _registers
                .GetOutputDeclaration(new RegisterComponentKey(registerKey, component))
                .Semantic;
            if (semantic != last)
            {
                masks.Add(0);
                last = semantic;
            }
            masks[^1] |= 1 << component;
        }
        return masks.Count > 1 ? [.. masks] : null;
    }

    /// <summary>
    /// Splits a structured access covering a double and other members into one mask
    /// per run of each kind (the double goes through the shadow). Null if not mixed.
    /// </summary>
    private int[] SplitDoubleElementMasks(D3D10Instruction instruction)
    {
        int resourceOperand = instruction.Opcode switch
        {
            D3D10Opcode.LdStructured => 3,
            D3D10Opcode.StoreStructured => 0,
            _ => -1,
        };
        if (resourceOperand < 0
            || !_registers.HasDoubleStructuredMember(instruction.GetParamRegisterKey(resourceOperand)))
        {
            return null;
        }
        RegisterKey resourceKey = instruction.GetParamRegisterKey(resourceOperand);
        int elementByteOffset = instruction.GetOperandType(2) == OperandType.Immediate32
            ? instruction.GetParamInt(2, 0)
            : 0;
        byte[] swizzle = resourceOperand == 3
            ? instruction.GetSourceSwizzleComponents(3)
            : null;
        int mask = instruction.GetWriteMask(0);
        var masks = new List<int>();
        bool? last = null;
        for (int component = 0; component < 4; component++)
        {
            if ((mask & (1 << component)) == 0)
            {
                continue;
            }
            int elementComponent = swizzle != null ? swizzle[component] : component;
            int byteAddress = elementByteOffset + elementComponent * 4;
            bool isDouble = _registers.IsDoubleStructuredMember(resourceKey, byteAddress);
            if (isDouble != last)
            {
                masks.Add(0);
                last = isDouble;
            }
            masks[^1] |= 1 << component;
        }
        return masks.Count > 1 ? [.. masks] : null;
    }

    /// <summary>
    /// Splits a multi-dword raw groupshared access into one statement per dword,
    /// since raw groupshared is declared as a dword array. Null if not needed.
    /// </summary>
    private static int[] SplitGroupSharedRawMasks(D3D10Instruction instruction)
    {
        int resourceOperand = instruction.Opcode switch
        {
            D3D10Opcode.LdRaw => 2,
            D3D10Opcode.StoreRaw => 0,
            _ => -1,
        };
        if (resourceOperand < 0
            || instruction.GetOperandType(resourceOperand) != OperandType.ThreadGroupSharedMemory)
        {
            return null;
        }
        int mask = instruction.GetWriteMask(0);
        var masks = new List<int>();
        for (int component = 0; component < 4; component++)
        {
            if ((mask & (1 << component)) != 0)
            {
                masks.Add(1 << component);
            }
        }
        return masks.Count > 1 ? [.. masks] : null;
    }

    private void WriteInstruction(D3D10Instruction instruction)
    {
        // Only addresses class data, which is read by name.
        if (_registers.Linkage?.IsThisMove(instruction) == true)
        {
            return;
        }
        int[] split = SplitPackedOutputMasks(instruction)
            ?? SplitDoubleElementMasks(instruction)
            ?? SplitGroupSharedRawMasks(instruction);
        if (split == null)
        {
            WriteInstructionStatement(instruction);
            return;
        }
        foreach (int mask in split)
        {
            _destinationMaskOverride = mask;
            try
            {
                WriteInstructionStatement(instruction);
            }
            finally
            {
                _destinationMaskOverride = null;
            }
        }
    }

    private void WriteInstructionStatement(D3D10Instruction instruction)
    {
        switch (instruction.Opcode)
        {
            case D3D10Opcode.Add:
            case D3D10Opcode.IAdd:
                WriteResult(instruction, "{0} = {1} + {2};", GetOperandName(instruction, 0),
                    SignedAddend(instruction, 1, 2), SignedAddend(instruction, 2, 1));
                break;
            case D3D10Opcode.IShl:
                WriteResult(instruction, "{0} = {1} << {2};", GetOperandName(instruction, 0), ShiftOperand(instruction, 1), ShiftOperand(instruction, 2));
                break;
            // No bit field intrinsic: ubfe shifts a uint (no sign fill), ibfe shifts
            // the field to the top of an int and back.
            case D3D10Opcode.UBFE:
                {
                    int length = instruction.GetDestinationMaskLength();
                    string size = length == 1 ? "" : length.ToString();
                    WriteResult(instruction, "{0} = ((uint{4}){3} >> {2}) & ((1 << {1}) - 1);",
                        GetOperandName(instruction, 0), ShiftOperand(instruction, 1),
                        ShiftOperand(instruction, 2), ShiftOperand(instruction, 3), size);
                    break;
                }
            case D3D10Opcode.IBFE:
                WriteResult(instruction, "{0} = ({3} << (32 - {1} - {2})) >> (32 - {1});",
                    GetOperandName(instruction, 0), ShiftOperand(instruction, 1),
                    ShiftOperand(instruction, 2), ShiftOperand(instruction, 3));
                break;
            case D3D10Opcode.BFI:
                WriteResult(instruction, "{0} = ({4} & ~(((1 << {1}) - 1) << {2})) | (({3} << {2}) & (((1 << {1}) - 1) << {2}));",
                    GetOperandName(instruction, 0), ShiftOperand(instruction, 1),
                    ShiftOperand(instruction, 2), ShiftOperand(instruction, 3),
                    ShiftOperand(instruction, 4));
                break;
            case D3D10Opcode.IShr:
                WriteResult(instruction, "{0} = {1} >> {2};", GetOperandName(instruction, 0), ShiftOperand(instruction, 1), ShiftOperand(instruction, 2));
                break;
            case D3D10Opcode.UShr:
                {
                    // Temps are signed; >> on them would shift in the sign bit.
                    int length = instruction.GetDestinationMaskLength();
                    string size = length == 1 ? "" : length.ToString();
                    WriteResult(instruction, "{0} = {1} >> {2};", GetOperandName(instruction, 0),
                        $"(uint{size}){ShiftOperand(instruction, 1)}", ShiftOperand(instruction, 2));
                    break;
                }
            case D3D10Opcode.BreakC:
                WriteLine("if ({0}) break;", ZeroTest(instruction, 0));
                break;
            case D3D10Opcode.Cut:
            case D3D10Opcode.CutStream:
                WriteLine($"{_registers.StreamParameterName(instruction.Stream)}.RestartStrip();");
                break;
            case D3D10Opcode.EmitThenCut:
            case D3D10Opcode.EmitThenCutStream:
                WriteLine($"{_registers.StreamParameterName(instruction.Stream)}"
                    + $".Append({_registers.StreamVariableName(instruction.Stream)});");
                WriteLine($"{_registers.StreamParameterName(instruction.Stream)}.RestartStrip();");
                break;
            case D3D10Opcode.DerivRtx:
                WriteResult(instruction, "{0} = ddx({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.DerivRty:
                WriteResult(instruction, "{0} = ddy({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.DerivRtxCoarse:
            case D3D10Opcode.DerivRtxFine:
            case D3D10Opcode.DerivRtyCoarse:
            case D3D10Opcode.DerivRtyFine:
            case D3D10Opcode.Rcp:
                WriteResult(instruction, "{0} = " + instruction.Opcode switch
                {
                    D3D10Opcode.DerivRtxCoarse => "ddx_coarse",
                    D3D10Opcode.DerivRtxFine => "ddx_fine",
                    D3D10Opcode.DerivRtyCoarse => "ddy_coarse",
                    D3D10Opcode.DerivRtyFine => "ddy_fine",
                    _ => "rcp",
                } + "({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.Discard:
                // Not clip(): a comparison mask is NaN as a float, never negative.
                WriteLine("if ({0}) discard;", ZeroTest(instruction, 0));
                break;
            case D3D10Opcode.Dp2:
            case D3D10Opcode.Dp3:
            case D3D10Opcode.Dp4:
                WriteResult(instruction, "{0} = dot({1}, {2});", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.Emit:
            case D3D10Opcode.EmitStream:
                WriteLine($"{_registers.StreamParameterName(instruction.Stream)}"
                    + $".Append({_registers.StreamVariableName(instruction.Stream)});");
                break;
            case D3D10Opcode.If:
                WriteBranchAttribute();
                WriteLine("if ({0}) {{", ZeroTest(instruction, 0));
                indent += "\t";
                break;
            case D3D10Opcode.Else:
                indent = indent.Substring(0, indent.Length - 1);
                WriteLine("} else {");
                indent += "\t";
                break;
            case D3D10Opcode.EndIf:
                indent = indent.Substring(0, indent.Length - 1);
                WriteLine("}");
                break;
            case D3D10Opcode.Swtich:
                WriteLine("switch ({0}) {{", GetOperandName(instruction, 0));
                indent += "\t";
                break;
            case D3D10Opcode.Case:
                WriteLine("case {0}:", GetOperandName(instruction, 0));
                break;
            case D3D10Opcode.Default:
                WriteLine("default:");
                break;
            case D3D10Opcode.EndSwitch:
                indent = indent.Substring(0, indent.Length - 1);
                WriteLine("}");
                break;
            case D3D10Opcode.Break:
                WriteLine("break;");
                break;
            case D3D10Opcode.Continue:
                WriteLine("continue;");
                break;
            case D3D10Opcode.ContinueC:
                WriteLine("if ({0}) continue;", ZeroTest(instruction, 0));
                break;
            case D3D10Opcode.Div:
                WriteResult(instruction, "{0} = {1} / {2};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.Exp:
                WriteResult(instruction, "{0} = exp2({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.Max:
            case D3D10Opcode.IMax:
                WriteResult(instruction, "{0} = max({1}, {2});", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.IMin:
                WriteResult(instruction, "{0} = min({1}, {2});", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.UMax:
                WriteResult(instruction, "{0} = max({1}, {2});", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.UMin:
                WriteResult(instruction, "{0} = min({1}, {2});", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.Umad:
                WriteResult(instruction, "{0} = {1} * {2} + {3};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3));
                break;
            case D3D10Opcode.INeg:
                WriteResult(instruction, "{0} = -{1};", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.IMul:
                // Two destinations, high and low halves; only the low one is modelled.
                WriteResult(instruction, 1, "{0} = {1} * {2};", GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3));
                break;
            case D3D10Opcode.IMad:
                WriteResult(instruction, "{0} = {1} * {2} + {3};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3));
                break;
            case D3D10Opcode.RoundNi:
                WriteResult(instruction, "{0} = floor({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.LT:
                WriteComparison(instruction, "<");
                break;
            case D3D10Opcode.Ige:
                WriteComparison(instruction, ">=");
                break;
            case D3D10Opcode.UGE:
                WriteComparison(instruction, ">=", unsigned: true);
                break;
            case D3D10Opcode.ULT:
                WriteComparison(instruction, "<", unsigned: true);
                break;
            case D3D10Opcode.EndLoop:
                indent = indent.Substring(0, indent.Length - 1);
                WriteLine("}");
                break;
            case D3D10Opcode.GE:
                WriteComparison(instruction, ">=");
                break;
            case D3D10Opcode.Eq:
                WriteComparison(instruction, "==");
                break;
            case D3D10Opcode.Ne:
                WriteComparison(instruction, "!=");
                break;
            case D3D10Opcode.Ilt:
                WriteComparison(instruction, "<");
                break;
            case D3D10Opcode.IToF:
                WriteConversion(instruction, "int", "float");
                break;
            case D3D10Opcode.UTof:
                WriteConversion(instruction, "uint", "float");
                break;
            case D3D10Opcode.Ftoi:
                WriteConversion(instruction, null, "int");
                break;
            case D3D10Opcode.Ftou:
                WriteConversion(instruction, null, "uint");
                break;
            // On a counter buffer (not append/consume), the slot is an ordinary value.
            case D3D10Opcode.ImmAtomicAlloc:
                WriteResult(instruction, "{0} = {1}.IncrementCounter();",
                    GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.ImmAtomicConsume when !IsConsumeResource(instruction.GetParamRegisterKey(1)):
                WriteResult(instruction, "{0} = {1}.DecrementCounter();",
                    GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.ImmAtomicConsume:
                {
                    // Consume() takes the slot and reads the element; later loads read
                    // the variable it filled.
                    string consumed = $"consumed{_consumeCount++}";
                    string element = GetStructuredElementType(
                        _registers.ResourceDefinitions.First(d =>
                            d.BindPoint == instruction.GetParamRegisterNumber(1)
                            && d.ShaderInputType == D3DShaderInputType.UavConsumeStructured));
                    WriteLine("{0} {1} = {2}.Consume();", element, consumed,
                        GetOperandName(instruction, 1));
                    _consumedSlots[instruction.GetParamRegisterKey(0)] = consumed;
                    break;
                }
            case D3D10Opcode.LdStructured
                when _consumedSlots.TryGetValue(instruction.GetParamRegisterKey(1), out string consumedElement)
                    && IsConsumeResource(instruction.GetParamRegisterKey(3)):
                {
                    // By member where the element is a struct (`consumed0.x` is X3018).
                    int component = instruction.GetParamInt(2, 0) / 4;
                    string member = _registers.NameStructuredMembers(
                        instruction.GetParamRegisterKey(3), consumedElement, 0, [component]);
                    WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
                        member ?? $"{consumedElement}.{"xyzw"[component]}");
                    break;
                }
            case D3D10Opcode.LdStructured:
                {
                    string element = $"{GetOperandName(instruction, 3)}[{GetOperandName(instruction, 1)}]";
                    RegisterKey buffer = instruction.GetParamRegisterKey(3);
                    int offset = instruction.GetParamInt(2, 0);
                    byte[] elementSwizzle = instruction.GetSourceSwizzleComponents(3);
                    int writeMask = DestinationMask(instruction);
                    // One entry per value read: a double takes two components.
                    int loadPerElement = IsDoubleStructuredComponent(instruction, 3) ? 2 : 1;
                    List<int> loaded = [.. Enumerable.Range(0, 4)
                        .Where(c => (writeMask & (1 << c)) != 0)];
                    List<int> read = [];
                    for (int value = 0; value < loaded.Count; value += loadPerElement)
                    {
                        read.Add(elementSwizzle[loaded[value]]);
                    }
                    // Groupshared reached at a computed offset is declared as dwords.
                    if (_registers.IsDwordAddressedThreadGroupSharedMemory(buffer))
                    {
                        List<string> dwords = [.. read.Select(c => SharedDword(instruction, 3, c))];
                        string sharedType = _registers.ThreadGroupSharedTypes.TryGetValue(
                            buffer.Number, out string declared) ? declared : "float";
                        WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
                            dwords.Count == 1 ? dwords[0] : $"{sharedType}{dwords.Count}({string.Join(", ", dwords)})");
                        break;
                    }
                    // Groupshared wider than a register is a struct; the offset picks the member.
                    if (_registers.ThreadGroupSharedMember(buffer, offset)
                        is var (sharedName, sharedComponents, sharedBase))
                    {
                        // Starting in one member and ending in the next: per component.
                        var lanes = read.Select(c => _registers.ThreadGroupSharedMember(buffer, offset + c * 4)).ToList();
                        if (lanes.Any(lane => lane?.Name != sharedName))
                        {
                            List<string> parts = [.. lanes.Select(lane => lane.Value.Components == 1
                                ? $"{element}.{lane.Value.Name}"
                                : $"{element}.{lane.Value.Name}.{"xyzw"[lane.Value.ComponentBase]}")];
                            WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
                                parts.Count == 1 ? parts[0] : $"float{parts.Count}({string.Join(", ", parts)})");
                            break;
                        }
                        string sharedSwizzle = sharedComponents == 1
                            ? ""
                            : Rebased(
                                instruction.GetSourceSwizzleNameForMask(3,
                                    instruction.GetDestinationWriteMask()),
                                sharedBase, sharedComponents);
                        // Drop a whole-member swizzle (`.m0.xyzw`).
                        if (sharedSwizzle == "." + "xyzw"[..sharedComponents])
                        {
                            sharedSwizzle = "";
                        }
                        WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
                            $"{element}.{sharedName}{sharedSwizzle}");
                        break;
                    }
                    if (loadPerElement == 1
                        && MixedStructuredLoad(instruction, buffer, element, offset, read) is string mixed)
                    {
                        WriteLine("{0} = {1};", GetOperandName(instruction, 0), mixed);
                        break;
                    }
                    string readElement = _registers.NameStructuredMembers(buffer, element, offset, read)
                        ?? _registers.ApplyStructuredElementRow(buffer, element, offset);
                    // A scalar/vector element: the byte offset selects components (offset
                    // 12 of a uint4 is .w; offset 8 of a double2 is .y).
                    if (readElement == element)
                    {
                        var names = new List<char>();
                        foreach (int component in read)
                        {
                            names.Add("xyzw"[(component + offset / 4) % 4 / loadPerElement]);
                        }
                        // Omitted only when it is the identity swizzle.
                        string picked = new string([.. names]);
                        int width = _registers.GetRegisterMaskedLength(buffer) / loadPerElement;
                        if (picked != "xyzw"[..Math.Min(width, 4)])
                        {
                            readElement += "." + picked;
                        }
                    }
                    // Two words later read as a double are joined into the shadow here.
                    if (MakesDoubleFromBits(instruction) && read.Count == 2)
                    {
                        readElement = string.Format("asdouble({0}.{1}, {0}.{2})", element,
                            "xyzw"[(read[0] + offset / 4) % 4],
                            "xyzw"[(read[1] + offset / 4) % 4]);
                    }
                    WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0), readElement);
                    break;
                }
            case D3D10Opcode.LdRaw when IsGroupSharedOperand(instruction, 2):
                {
                    // Groupshared is an array: a subscript per dword (already split).
                    int readMask = DestinationMask(instruction);
                    if (BitOperations.PopCount((uint)readMask) != 1)
                    {
                        throw new NotImplementedException(
                            "a raw groupshared load of more than one dword");
                    }
                    byte[] sharedSwizzle = instruction.GetSourceSwizzleComponents(2);
                    int dword = sharedSwizzle[BitOperations.TrailingZeroCount((uint)readMask)];
                    WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
                        _registers.GroupSharedElementReference(
                            instruction.GetParamRegisterKey(2),
                            GetOperandName(instruction, 2),
                            GroupSharedElement(instruction, 1, dword)));
                    break;
                }
            case D3D10Opcode.LdRaw:
                {
                    // LoadN up to the highest component read, then the swizzle.
                    byte[] swizzle = instruction.GetSourceSwizzleComponents(2);
                    int[] read = GetSourceComponents(instruction, 2, swizzle);
                    int width = read.Max() + 1;
                    string method = width == 1 ? "Load" : $"Load{width}";
                    string picked = width == 1 || read.SequenceEqual(Enumerable.Range(0, width))
                        ? ""
                        : "." + string.Concat(read.Select(c => "xyzw"[c]));
                    WriteResult(instruction, "{0} = {2}.{3}({1}){4};", GetOperandName(instruction, 0),
                        GetOperandName(instruction, 1), GetOperandName(instruction, 2), method, picked);
                    break;
                }
            case D3D10Opcode.Loop:
                if (WritesFlowAttributes || LoopSamplesWithGradients(instruction))
                {
                    WriteLine("[loop]");
                }
                WriteLine("while (true) {");
                indent += "\t";
                break;
            case D3D10Opcode.Mad:
                WriteResult(instruction, "{0} = {1} * {2} + {3};", GetOperandName(instruction, 0),
                    GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3));
                break;
            // msad4 takes two words; the instruction takes four sliding windows. The
            // first window is the low word; the fourth >> 8 gives the high word's
            // low three bytes, the only ones msad4 reads.
            case D3D10Opcode.MSAD:
                {
                    // Fewer lanes: fxc packs them, so each becomes lane 0 of its own call.
                    if (instruction.GetDestinationWriteMask() != 0xF)
                    {
                        WriteMsadLanes(instruction);
                        break;
                    }
                    byte[] windowSwizzle = instruction.GetSourceSwizzleComponents(2);
                    string windows = GetOperandName(instruction, 2).Split('.')[0];
                    string low = $"{windows}.{"xyzw"[windowSwizzle[0]]}";
                    string high = $"{windows}.{"xyzw"[windowSwizzle[3]]}";
                    WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
                        $"msad4({GetOperandName(instruction, 1)}, "
                        + $"uint2({low}, {high} >> 8), {GetOperandName(instruction, 3)})");
                    break;
                }
            case D3D10Opcode.Mov:
                // Joins two 32-bit words into the double shadow.
                if (_joinedDoubleMovs.Contains(instruction)
                    && SplitTwoComponentName(GetOperandName(instruction, 1))
                        is (string lowWord, string highWord))
                {
                    WriteResult(instruction, "{0} = asdouble({1}, {2});",
                        GetOperandName(instruction, 0), lowWord, highWord);
                    break;
                }
                // A double's words moved out of its shadow into an output register.
                if (MovedDoubleShadow(instruction) is string movedDouble)
                {
                    WriteLine("asuint({0}, {1}.x, {1}.y);", movedDouble, GetOperandName(instruction, 0));
                    break;
                }
                WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
                    Moved(instruction, 1, GetOperandName(instruction, 1)));
                break;
            case D3D10Opcode.MovC:
                WriteResult(instruction, "{0} = ({1}) ? {2} : {3};", GetOperandName(instruction, 0),
                    ZeroTest(instruction, 1, true),
                    Moved(instruction, 2, GetOperandName(instruction, 2)),
                    Moved(instruction, 3, GetOperandName(instruction, 3)));
                break;
            case D3D10Opcode.Mul:
                WriteResult(instruction, "{0} = {1} * {2};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            // Double arithmetic, over the shadow variables.
            case D3D10Opcode.DAdd:
                WriteResult(instruction, "{0} = {1} + {2};", GetOperandName(instruction, 0),
                    GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.DMul:
                WriteResult(instruction, "{0} = {1} * {2};", GetOperandName(instruction, 0),
                    GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.DDiv:
                WriteResult(instruction, "{0} = {1} / {2};", GetOperandName(instruction, 0),
                    GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.DMax:
                WriteResult(instruction, "{0} = max({1}, {2});", GetOperandName(instruction, 0),
                    GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.DMin:
                WriteResult(instruction, "{0} = min({1}, {2});", GetOperandName(instruction, 0),
                    GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.DFMA:
                WriteResult(instruction, "{0} = fma({1}, {2}, {3});", GetOperandName(instruction, 0),
                    GetOperandName(instruction, 1), GetOperandName(instruction, 2),
                    GetOperandName(instruction, 3));
                break;
            case D3D10Opcode.DRCP:
                WriteResult(instruction, "{0} = rcp({1});", GetOperandName(instruction, 0),
                    GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.DMov:
                WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
                    GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.DMovC:
                WriteResult(instruction, "{0} = ({1}) ? {2} : {3};", GetOperandName(instruction, 0),
                    ZeroTest(instruction, 1, true),
                    GetOperandName(instruction, 2), GetOperandName(instruction, 3));
                break;
            case D3D10Opcode.DEq:
                WriteComparison(instruction, "==");
                break;
            case D3D10Opcode.DNe:
                WriteComparison(instruction, "!=");
                break;
            case D3D10Opcode.DLt:
                WriteComparison(instruction, "<");
                break;
            case D3D10Opcode.DGe:
                WriteComparison(instruction, ">=");
                break;
            case D3D10Opcode.DToF:
                WriteDoubleConversion(instruction, "float");
                break;
            case D3D10Opcode.DToI:
                WriteDoubleConversion(instruction, "int");
                break;
            case D3D10Opcode.DToU:
                WriteDoubleConversion(instruction, "uint");
                break;
            case D3D10Opcode.FToD:
            case D3D10Opcode.IToD:
            case D3D10Opcode.UToD:
                WriteDoubleConversion(instruction, "double");
                break;
            case D3D10Opcode.Rsq:
                // Not `1 / sqrt(x)`: fxc would not fold it back into rsq.
                WriteResult(instruction, "{0} = rsqrt({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.Sample:
                WriteResult(instruction, "{0} = {2}.Sample({3}, {1}{4}){5};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3), GetSampleOffset(instruction), GetResourceSwizzle(instruction));
                break;
            // The resource swizzle picks clamped or unclamped, not a channel.
            case D3D10Opcode.Lod:
                WriteResult(instruction, "{0} = {2}.{4}({3}, {1});",
                    GetOperandName(instruction, 0), GetOperandName(instruction, 1),
                    GetOperandName(instruction, 2), GetOperandName(instruction, 3),
                    instruction.GetSourceSwizzleComponents(2)[0] == 1
                        ? "CalculateLevelOfDetailUnclamped"
                        : "CalculateLevelOfDetail");
                break;
            case D3D10Opcode.RoundZ:
                WriteResult(instruction, "{0} = trunc({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.Frc:
                WriteResult(instruction, "{0} = frac({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.RoundNe:
                WriteResult(instruction, "{0} = round({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.Gather4:
                {
                    // The sampler swizzle picks the channel gathered.
                    string method = instruction.GetSourceSwizzleComponents(3)[0] switch
                    {
                        1 => "GatherGreen",
                        2 => "GatherBlue",
                        3 => "GatherAlpha",
                        _ => "Gather",
                    };
                    WriteResult(instruction, "{0} = {2}.{4}({3}, {1}{5}){6};", GetOperandName(instruction, 0),
                        GetOperandName(instruction, 1), GetOperandName(instruction, 2),
                        GetOperandName(instruction, 3), method, GetSampleOffset(instruction),
                        GetResourceSwizzle(instruction));
                    break;
                }
            case D3D10Opcode.Gather4Po:
            case D3D10Opcode.Gather4PoC:
                {
                    // The offset is operand 2, shifting texture and sampler along.
                    bool compares = instruction.Opcode == D3D10Opcode.Gather4PoC;
                    string method = instruction.GetSourceSwizzleComponents(4)[0] switch
                    {
                        1 => compares ? "GatherCmpGreen" : "GatherGreen",
                        2 => compares ? "GatherCmpBlue" : "GatherBlue",
                        3 => compares ? "GatherCmpAlpha" : "GatherAlpha",
                        _ => compares ? "GatherCmp" : "Gather",
                    };
                    string compared = compares ? $", {GetOperandName(instruction, 5)}" : "";
                    WriteResult(instruction, "{0} = {1}.{2}({3}, {4}{5}, {6}){7};",
                        GetOperandName(instruction, 0), GetOperandName(instruction, 3),
                        method, GetOperandName(instruction, 4), GetOperandName(instruction, 1),
                        compared, GetOperandName(instruction, 2),
                        GetResourceSwizzle(instruction));
                    break;
                }
            case D3D10Opcode.Gather4C:
                {
                    string method = instruction.GetSourceSwizzleComponents(3)[0] switch
                    {
                        1 => "GatherCmpGreen",
                        2 => "GatherCmpBlue",
                        3 => "GatherCmpAlpha",
                        _ => "GatherCmp",
                    };
                    WriteResult(instruction, "{0} = {2}.{5}({3}, {1}, {4}{6}){7};",
                        GetOperandName(instruction, 0), GetOperandName(instruction, 1),
                        GetOperandName(instruction, 2), GetOperandName(instruction, 3),
                        GetOperandName(instruction, 4), method, GetSampleOffset(instruction),
                        GetResourceSwizzle(instruction));
                    break;
                }
            case D3D10Opcode.SampleL:
                WriteResult(instruction, "{0} = {2}.SampleLevel({3}, {1}, {4}{5}){6};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3), GetOperandName(instruction, 4), GetSampleOffset(instruction), GetResourceSwizzle(instruction));
                break;
            case D3D10Opcode.SampleB:
                WriteResult(instruction, "{0} = {2}.SampleBias({3}, {1}, {4}{5}){6};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3), GetOperandName(instruction, 4), GetSampleOffset(instruction), GetResourceSwizzle(instruction));
                break;
            case D3D10Opcode.SampleC:
                WriteResult(instruction, "{0} = {2}.SampleCmp({3}, {1}, {4}{5});", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3), GetOperandName(instruction, 4), GetSampleOffset(instruction));
                break;
            case D3D10Opcode.SampleCLZ:
                WriteResult(instruction, "{0} = {2}.SampleCmpLevelZero({3}, {1}, {4}{5});", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3), GetOperandName(instruction, 4), GetSampleOffset(instruction));
                break;
            case D3D10Opcode.SampleD:
                WriteResult(instruction, "{0} = {2}.SampleGrad({3}, {1}, {4}, {5}{6}){7};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3), GetOperandName(instruction, 4), GetOperandName(instruction, 5), GetSampleOffset(instruction), GetResourceSwizzle(instruction));
                break;
            case D3D10Opcode.LD:
                // A tbuffer has no Load; the address picks a variable.
                if (TextureBufferVariable(instruction) is string bufferVariable)
                {
                    WriteResult(instruction, "{0} = {1}{2};", GetOperandName(instruction, 0),
                        bufferVariable, GetResourceSwizzle(instruction));
                    break;
                }
                WriteResult(instruction, "{0} = {2}.Load({1}{3}){4};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetSampleOffset(instruction), GetResourceSwizzle(instruction));
                break;
            case D3D10Opcode.LDMS:
                WriteResult(instruction, "{0} = {2}.Load({1}, {3}{4}){5};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3), GetSampleOffset(instruction), GetResourceSwizzle(instruction));
                break;
            case D3D10Opcode.ResInfo:
                WriteResourceInfo(instruction);
                break;
            case D3D10Opcode.BufInfo:
                WriteBufferInfo(instruction);
                break;
            case D3D10Opcode.EvalSampleIndex:
                WriteResult(instruction, "{0} = EvaluateAttributeAtSample({1}, {2});",
                    GetOperandName(instruction, 0), GetOperandName(instruction, 1),
                    GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.EvalSnapped:
                WriteResult(instruction, "{0} = EvaluateAttributeSnapped({1}, {2});",
                    GetOperandName(instruction, 0), GetOperandName(instruction, 1),
                    GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.EvalCentroid:
                WriteResult(instruction, "{0} = EvaluateAttributeCentroid({1});",
                    GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.SampleInfo:
                WriteSampleInfo(instruction);
                break;
            // The render target (rasterizer operand) has its own function.
            case D3D10Opcode.SamplePos:
                if (instruction.GetOperandType(1) == OperandType.Rasterizer)
                {
                    WriteResult(instruction, "{0} = GetRenderTargetSamplePosition({1});",
                        GetOperandName(instruction, 0), GetOperandName(instruction, 2));
                    break;
                }
                WriteResult(instruction, "{0} = {1}.GetSamplePosition({2});",
                    GetOperandName(instruction, 0),
                    _registers.GetRegisterName(instruction.GetParamRegisterKey(1)),
                    GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.Log:
                WriteResult(instruction, "{0} = log2({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.Min:
                WriteResult(instruction, "{0} = min({1}, {2});", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.RoundPi:
                WriteResult(instruction, "{0} = ceil({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.Ieq:
                WriteComparison(instruction, "==");
                break;
            case D3D10Opcode.Ine:
                WriteComparison(instruction, "!=");
                break;
            case D3D10Opcode.And:
                WriteBitwise(instruction, "&");
                break;
            case D3D10Opcode.Udiv:
                {
                    // Quotient and remainder (either may be null), explicitly unsigned:
                    // a signed % would get its own sign handling from fxc.
                    string dividend = AsUint(instruction, 2);
                    string divisor = AsUint(instruction, 3);
                    bool wantsQuotient = instruction.GetOperandType(0) != OperandType.Null;
                    bool wantsRemainder = instruction.GetOperandType(1) != OperandType.Null;
                    // The quotient may overwrite an operand the remainder still reads
                    // (`udiv r1, r2, r1, r2`), so keep those operands first.
                    if (wantsQuotient && wantsRemainder)
                    {
                        dividend = KeptOperand(instruction, 2, 0, "uint", "dividend", dividend);
                        divisor = KeptOperand(instruction, 3, 0, "uint", "divisor", divisor);
                    }
                    if (wantsQuotient)
                    {
                        WriteResult(instruction, "{0} = {1} / {2};", GetOperandName(instruction, 0), dividend, divisor);
                    }
                    if (wantsRemainder)
                    {
                        // fxc bug: folds `asfloat(u % 5)` to zero; `u - u / 5 * 5` works.
                        bool reinterpreted = IsFloatOutput(instruction, 1);
                        string remainder = reinterpreted && instruction.GetOperandType(3) == OperandType.Immediate32
                            ? $"{dividend} - {dividend} / {divisor} * {divisor}"
                            : $"{dividend} % {divisor}";
                        WriteResult(instruction, 1, "{0} = {1};", GetOperandName(instruction, 1), remainder);
                    }
                    break;
                }
            case D3D10Opcode.Or:
                WriteBitwise(instruction, "|");
                break;
            case D3D10Opcode.Xor:
                WriteBitwise(instruction, "^");
                break;
            case D3D10Opcode.Not:
                {
                    bool reinterpret = !IsIntegerDestination(instruction);
                    string expression = "~" + Reinterpreted(instruction, 1, reinterpret);
                    WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
                        reinterpret ? $"asfloat({expression})" : expression);
                }
                break;
            // Either half can be dropped: `sincos r0.x, null, r0.y` is a sin alone.
            case D3D10Opcode.SinCos:
                {
                    string angle = GetOperandName(instruction, 2);
                    bool wantsSine = instruction.GetOperandType(0) != OperandType.Null;
                    bool wantsCosine = instruction.GetOperandType(1) != OperandType.Null;
                    // The sine may overwrite the angle before the cosine reads it.
                    if (wantsSine && wantsCosine)
                    {
                        angle = KeptOperand(instruction, 2, 0, "float", "angle", angle);
                    }
                    if (wantsSine)
                    {
                        WriteResult(instruction, "{0} = sin({1});", GetOperandName(instruction, 0), angle);
                    }
                    if (wantsCosine)
                    {
                        WriteResult(instruction, 1, "{0} = cos({1});", GetOperandName(instruction, 1), angle);
                    }
                    break;
                }
            case D3D10Opcode.Sqrt:
                WriteResult(instruction, "{0} = sqrt({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.CountBits:
                WriteResult(instruction, "{0} = countbits({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.FirstBitLo:
                WriteResult(instruction, "{0} = firstbitlow({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.FirstBitHi:
                WriteFirstBitHigh(instruction, signed: false);
                break;
            case D3D10Opcode.FirstBitSHi:
                WriteFirstBitHigh(instruction, signed: true);
                break;
            case D3D10Opcode.BFRev:
                WriteResult(instruction, "{0} = reversebits({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.F32ToF16:
                WriteResult(instruction, "{0} = f32tof16({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.F16ToF32:
                WriteResult(instruction, "{0} = f16tof32({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.LdUAVTyped:
                {
                    // Swizzled to the mask (else X3206 truncation); `.xyzw` is dropped.
                    string texel = instruction.GetSourceSwizzleNameForMask(
                        2, instruction.GetDestinationWriteMask());
                    WriteResult(instruction, "{0} = {1}[{2}]{3};", GetOperandName(instruction, 0),
                        GetOperandName(instruction, 2), GetOperandName(instruction, 1),
                        texel == ".xyzw" ? "" : texel);
                    break;
                }
            case D3D10Opcode.StoreUAVTyped:
                WriteLine("{0}[{1}] = {2};", GetOperandName(instruction, 0),
                    GetOperandName(instruction, 1),
                    StoredBits(instruction, 2) ?? GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.StoreStructured:
                {
                    // A struct element is written a member at a time.
                    RegisterKey buffer = instruction.GetParamRegisterKey(0);
                    string element = $"{GetOperandName(instruction, 0)}[{GetOperandName(instruction, 1)}]";
                    int writeMask = DestinationMask(instruction);
                    int storePerElement = IsDoubleStructuredComponent(instruction, 0) ? 2 : 1;
                    List<int> masked = [.. Enumerable.Range(0, 4).Where(c => (writeMask & (1 << c)) != 0)];
                    List<int> written = [];
                    for (int value = 0; value < masked.Count; value += storePerElement)
                    {
                        written.Add(masked[value]);
                    }
                    if (_registers.IsDwordAddressedThreadGroupSharedMemory(buffer))
                    {
                        // The value's name is masked to the components written, in order.
                        string stored = GetOperandName(instruction, 3);
                        for (int k = 0; k < written.Count; k++)
                        {
                            WriteLine("{0} = {1};", SharedDword(instruction, 0, written[k]),
                                written.Count == 1 ? stored : $"({stored}).{"xyzw"[k]}");
                        }
                        break;
                    }
                    IList<(string Name, int[] Values)> runs = _registers.FindStructuredMemberRuns(
                        buffer, element, instruction.GetParamInt(2, 0), written);
                    if (runs == null)
                    {
                        // A groupshared struct member, as for the load.
                        string storedMember = _registers.ThreadGroupSharedMember(
                                buffer, instruction.GetParamInt(2, 0))
                            is var (storeName, _, _)
                            ? $".{storeName}"
                            : "";
                        if (WriteDoubleBitsStore(instruction, $"{element}{storedMember}"))
                        {
                            break;
                        }
                        WriteLine("{0}{1} = {2};", element, storedMember,
                            StoredImmediateBits(instruction)
                                ?? StoredBits(instruction, 3)
                                ?? GetOperandName(instruction, 3));
                        break;
                    }
                    // Only for one member, or the split would repeat per run.
                    if (runs.Count == 1 && WriteDoubleBitsStore(instruction, runs[0].Name))
                    {
                        break;
                    }
                    byte[] valueSwizzle = instruction.GetSourceSwizzleComponents(3);
                    foreach ((string name, int[] values) in runs)
                    {
                        // Picked by value: a shadow's second double is .y, not .z; a
                        // scalar double shadow takes no swizzle.
                        string valueName = GetOperandName(instruction, 3);
                        bool isWholeShadow = storePerElement == 2
                            && GetDoublePairMask(instruction.GetParamRegisterKey(3)) == 1;
                        string picked = isWholeShadow
                            ? ""
                            : "." + string.Concat(
                                values.Select(v => "xyzw"[valueSwizzle[written[v]] / storePerElement]));
                        WriteLine("{0} = {1}{2};", name,
                            isWholeShadow ? valueName : valueName.Split('.')[0], picked);
                    }
                    break;
                }
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
                {
                    // Raw buffers use methods, others free functions over the element.
                    // imm_ forms have the original-value register first; HLSL takes
                    // it as the trailing out parameter.
                    string method = instruction.Opcode.AtomicMethodName();
                    bool keepsOriginal = instruction.Opcode.IsImmediateAtomic();
                    int first = keepsOriginal ? 1 : 0;
                    string resource = GetOperandName(instruction, first);
                    string address = GetOperandName(instruction, first + 1);
                    bool hasCompare = instruction.Opcode
                        is D3D10Opcode.AtomicCmpStore or D3D10Opcode.ImmAtomicCmpExch;
                    int valueIndex = first + (hasCompare ? 3 : 2);
                    string bits = StoredBits(instruction, valueIndex);
                    string value = bits ?? GetOperandName(instruction, valueIndex);
                    // InterlockedMax/Min pick signedness from the value's type.
                    if (bits == null
                        && instruction.Opcode is D3D10Opcode.AtomicUMax or D3D10Opcode.AtomicUMin
                            or D3D10Opcode.ImmAtomicUMax or D3D10Opcode.ImmAtomicUMin)
                    {
                        value = $"(uint){value}";
                    }
                    string arguments = hasCompare
                        ? $"{GetOperandName(instruction, first + 2)}, {value}"
                        : value;
                    if (keepsOriginal)
                    {
                        arguments += $", {GetOperandName(instruction, 0)}";
                    }
                    if (_registers.IsRawResource(instruction.GetParamRegisterKey(first)))
                    {
                        WriteLine("{0}.{1}({2}, {3});", resource, method, address, arguments);
                        break;
                    }
                    string target = _registers.GroupSharedElementReference(
                        instruction.GetParamRegisterKey(first), resource, address);
                    // On a struct element, address.y's byte offset picks the member
                    // (without it, X3013).
                    int? memberOffset =
                        instruction.GetOperandType(first + 1) == OperandType.Immediate32
                            ? instruction.GetParamInt(first + 1, 1)
                            : TryGetAddressOffsetConstant(instruction, first + 1, out int held)
                                ? held
                                : null;
                    if (memberOffset != null)
                    {
                        IList<(string Name, int[] Values)> memberRuns =
                            _registers.FindStructuredMemberRuns(
                                instruction.GetParamRegisterKey(first), target,
                                memberOffset.Value, [0]);
                        if (memberRuns != null && memberRuns.Count == 1)
                        {
                            target = memberRuns[0].Name;
                        }
                    }
                    WriteLine("{0}({1}, {2});", method, target, arguments);
                    break;
                }
            case D3D10Opcode.StoreRaw when IsGroupSharedOperand(instruction, 0):
                {
                    int storeMask = DestinationMask(instruction);
                    if (BitOperations.PopCount((uint)storeMask) != 1)
                    {
                        throw new NotImplementedException(
                            "a raw groupshared store of more than one dword");
                    }
                    int dword = BitOperations.TrailingZeroCount((uint)storeMask);
                    string storedValue;
                    if (instruction.GetOperandType(2) == OperandType.Immediate32)
                    {
                        storedValue = instruction.GetParamInt(2, dword).ToString(_culture);
                    }
                    else
                    {
                        // Raw groupshared is integer: a float-written value is stored
                        // as its bits, as StoredBits does.
                        storedValue = GetOperandName(instruction, 2);
                        if (instruction.GetOperandType(2) == OperandType.Temp
                            && GetSourceStorage(instruction, 2) == ComponentStorage.Numeric
                            && instruction.GetParamRegisterKey(2)
                                is D3D10RegisterKey { IsTempRegister: true } source
                            && IsWrittenAsFloat(instruction, source,
                                instruction.GetSourceSwizzleComponents(2)[dword]))
                        {
                            string cast = _integerOperandAnalysis.IsUnsignedThreadGroupSharedMemory(
                                instruction.GetParamRegisterKey(0).Number) ? "asuint" : "asint";
                            storedValue = $"{cast}({storedValue})";
                        }
                    }
                    WriteLine("{0} = {1};",
                        _registers.GroupSharedElementReference(
                            instruction.GetParamRegisterKey(0),
                            GetOperandName(instruction, 0),
                            GroupSharedElement(instruction, 1, dword)),
                        storedValue);
                    break;
                }
            case D3D10Opcode.StoreRaw:
                {
                    int width = instruction.GetDestinationMaskLength();
                    string method = width == 1 ? "Store" : $"Store{width}";
                    WriteLine("{0}.{3}({1}, {2});", GetOperandName(instruction, 0), GetOperandName(instruction, 1),
                        GetOperandName(instruction, 2), method);
                    break;
                }
            case D3D10Opcode.Sync:
                WriteLine("{0}();", SyncStatement.GetIntrinsicName(instruction.SyncFlags));
                break;
            case D3D10Opcode.DclConstantBuffer:
            case D3D10Opcode.DclGlobalFlags:
            case D3D10Opcode.DclIndexRange:
            // Phases are unrolled per instance before this point.
            case D3D10Opcode.DclHSForkPhaseInstanceCount:
            case D3D10Opcode.DclHSJoinPhaseInstanceCount:
            case D3D10Opcode.DclGSInputPrimitive:
            case D3D10Opcode.DclGSMaxOutputVertexCount:
            case D3D10Opcode.DclGSInstanceCount:
            case D3D10Opcode.DclInput:
            case D3D10Opcode.DclInputPS:
            case D3D10Opcode.DclInputPSSgv:
            case D3D10Opcode.DclInputPSSiv:
            case D3D10Opcode.DclInputSgv:
            case D3D10Opcode.DclOutputSgv:
            case D3D10Opcode.DclInputSiv:
            case D3D10Opcode.DclOutput:
            // Tessellation declarations are written as attributes with the signature.
            case D3D10Opcode.DclInputControlPointCount:
            case D3D10Opcode.DclOutputControlPointCount:
            case D3D10Opcode.DclTessDomain:
            case D3D10Opcode.DclTessPartitioning:
            case D3D10Opcode.DclTessOutputPrimitive:
            case D3D10Opcode.DclHSMaxTessFactor:
            case D3D10Opcode.HsDecls:
            case D3D10Opcode.HsControlPointPhase:
            case D3D10Opcode.HsForkPhase:
            case D3D10Opcode.HsJoinPhase:
            case D3D10Opcode.DclGSOutputPrimitiveTopology:
            case D3D10Opcode.DclStream:
            case D3D10Opcode.DclOutputSiv:
            case D3D10Opcode.DclResource:
            case D3D10Opcode.DclUnorderedAccessViewTyped:
            case D3D10Opcode.DclResourceStructured:
            case D3D10Opcode.DclResourceRaw:
            case D3D10Opcode.DclUnorderedAccessViewRaw:
            case D3D10Opcode.CustomData:
            case D3D10Opcode.DclSampler:
            case D3D10Opcode.DclTemps:
            case D3D10Opcode.DclIndexableTemp:
            case D3D10Opcode.DclThreadGroup:
            case D3D10Opcode.DclUnorderedAccessViewStructured:
            case D3D10Opcode.DclThreadGroupSharedMemoryRaw:
            case D3D10Opcode.DclThreadGroupSharedMemoryStructured:
            case D3D10Opcode.DclFunctionBody:
            case D3D10Opcode.DclFunctionTable:
            case D3D10Opcode.DclInterface:
                break;
            case D3D10Opcode.InterfaceCall:
                WriteInterfaceCall(instruction);
                break;
            case D3D10Opcode.RetC:
                WriteLine("if ({0}) return{1};", ZeroTest(instruction, 0),
                    _registers.MethodOutputRegisters.Count != 0 && _shader.Type != ShaderType.Geometry
                        ? " o"
                        : "");
                break;
            case D3D10Opcode.Ret:
                // Only main's last ret is the closing return, written after the body.
                if (!ReferenceEquals(instruction, LastMainInstruction()))
                {
                    WriteLine(_registers.MethodOutputRegisters.Count != 0 && _shader.Type != ShaderType.Geometry
                        ? $"return {_registers.OutputVariableName};"
                        : "return;");
                }
                break;
            default:
                throw new NotImplementedException(instruction.Opcode.ToString());
        }
    }

    // sm3 sincos writes cos to x and sin to y; HLSL's sincos(v, sin, cos) is the
    // other way round.
    private void WriteSinCos(D3D9Instruction instruction)
    {
        const int CosineComponent = 1;
        const int SineComponent = 2;
        int writeMask = instruction.GetDestinationWriteMask();
        string register = GetDestinationRegisterName(instruction);
        // A scalar source: sincos replicates it.
        string value = GetSourceName(instruction, 1, 1);
        if ((writeMask & CosineComponent) != 0 && (writeMask & SineComponent) != 0)
        {
            WriteLine("sincos({0}, {1}.y, {1}.x);", value, register);
        }
        else if ((writeMask & SineComponent) != 0)
        {
            WriteLine("{0}.y = sin({1});", register, value);
        }
        else
        {
            WriteLine("{0}.x = cos({1});", register, value);
        }
    }

    private string GetDestinationRegisterName(D3D9Instruction instruction)
    {
        int destinationIndex = instruction.GetDestinationParamIndex().Value;
        return _registers.GetRegisterName(instruction.GetParamRegisterKey(destinationIndex));
    }

    private string GetDestinationName(D3D9Instruction instruction)
    {
        int destIndex = instruction.GetDestinationParamIndex().Value;
        D3D9RegisterKey registerKey = instruction.GetParamRegisterKey(destIndex);

        string registerName;
        // Addr/Texture told apart by shader type, not opcode: vs_1_1 writes a0 with mov.
        if (registerKey.Type == RegisterType.Addr && _shader.Type == ShaderType.Vertex)
        {
            registerName = "a" + registerKey.Number;
        }
        else
        {
            registerName = _registers.GetRegisterName(registerKey);
        }
        int registerLength = _registers.GetRegisterMaskedLength(registerKey);
        string writeMaskName = instruction.GetDestinationWriteMaskName(registerLength);

        return string.Format("{0}{1}", registerName, writeMaskName);
    }

    // rowOffset selects a later register, for the rows of a matrix product.
    private string GetSourceName(D3D9Instruction instruction, int srcIndex, int? destinationLength = null,
        int rowOffset = 0)
    {
        string sourceRegisterName;

        var registerKey = instruction.GetParamRegisterKey(srcIndex);
        if (rowOffset != 0)
        {
            registerKey = new D3D9RegisterKey(registerKey.Type, registerKey.Number + rowOffset);
        }
        switch (registerKey.Type)
        {
            case RegisterType.Const:
            case RegisterType.Const2:
            case RegisterType.Const3:
            case RegisterType.Const4:
            case RegisterType.ConstBool:
            case RegisterType.ConstInt:
                // A relatively addressed def is an array base, not a literal.
                if (!instruction.Params.HasRelativeAddressing(srcIndex))
                {
                    string constantValue = GetSourceConstantValue(instruction, srcIndex, destinationLength, rowOffset);
                    if (constantValue != null)
                    {
                        return constantValue;
                    }
                }

                if (IsPreshaderOutput(registerKey))
                {
                    sourceRegisterName = GetPreshaderOutputName(registerKey);
                    break;
                }

                if (_registers.FindConstantArray(registerKey) is ConstantArray literals)
                {
                    sourceRegisterName = literals.Name;
                    break;
                }

                // A struct member is swizzled itself; a scalar is named whole and
                // broadcast. Relative addressing is handled below.
                if (!instruction.Params.HasRelativeAddressing(srcIndex)
                    && _registers.TryGetConstantMember(
                        new RegisterComponentKey(
                            registerKey, instruction.GetSourceSwizzleComponents(srcIndex)[0]),
                        out StructMemberAccess member))
                {
                    string memberSource = member.Width <= 1
                        ? member.Name
                        : member.Name + instruction.GetSourceSwizzleName(srcIndex, destinationLength);
                    return ApplyModifier(instruction.GetSourceModifier(srcIndex), memberSource);
                }

                ConstantDeclaration decl = _registers.FindConstant(registerKey);
                if (decl == null)
                {
                    // Constant register not found in def statements nor the constant table
                    throw new NotImplementedException();
                }

                // Struct array indexed at run time: a0 counts registers, so the element
                // is a0 / stride and the remaining offset picks the member.
                if (decl.TypeInfo.MemberInfo != null
                    && instruction.Params.HasRelativeAddressing(srcIndex))
                {
                    int registerOffset = registerKey.Number - decl.RegisterIndex;
                    int stride = Math.Max(decl.RegistersPerElement, 1);
                    string element = $"{GetRelativeAddressIndex(instruction, srcIndex)} / {stride}";
                    if (registerOffset / stride != 0)
                    {
                        element += $" + {registerOffset / stride}";
                    }
                    if (RegisterState.TryGetStructMemberAtRegister(
                            decl, $"{decl.Name}[{element}]", registerOffset % stride,
                            out StructMemberAccess dynamicMember))
                    {
                        // A matrix comes back whole; the register picks its row.
                        if (dynamicMember.IsMatrix)
                        {
                            dynamicMember = RegisterState.MatrixRowOf(
                                dynamicMember, registerOffset % stride);
                        }
                        string dynamicSource = dynamicMember.Width <= 1
                            ? dynamicMember.Name
                            : dynamicMember.Name
                                + instruction.GetSourceSwizzleName(srcIndex, destinationLength);
                        return ApplyModifier(
                            instruction.GetSourceModifier(srcIndex), dynamicSource);
                    }
                }

                // Matrix array: element = offset / registers per element, then the row.
                if (decl.TypeInfo.Rows > 1 && decl.TypeInfo.NumElements > 1)
                {
                    int offset = registerKey.Number - decl.RegisterIndex;
                    int rows = decl.RegistersPerElement;
                    string element = instruction.Params.HasRelativeAddressing(srcIndex)
                        ? $"{GetRelativeAddressIndex(instruction, srcIndex)} / {rows}"
                        : (offset / rows).ToString(_culture);
                    string matrix = RegisterState.MatrixRegisterName(
                        decl.TypeInfo, $"{decl.Name}[{element}]", offset % rows);
                    return ApplyModifier(instruction.GetSourceModifier(srcIndex),
                        matrix + instruction.GetSourceSwizzleName(srcIndex, destinationLength));
                }

                if (decl.TypeInfo.ParameterClass is ParameterClass.MatrixRows
                    or ParameterClass.MatrixColumns)
                {
                    sourceRegisterName = RegisterState.MatrixRegisterName(
                        decl.TypeInfo, decl.Name, registerKey.Number - decl.RegisterIndex);
                }
                else if (decl.TypeInfo.NumElements > 1)
                {
                    // With relative addressing the offset folds into the subscript.
                    sourceRegisterName = instruction.Params.HasRelativeAddressing(srcIndex)
                        ? decl.Name
                        : $"{decl.Name}[{registerKey.Number - decl.RegisterIndex}]";
                }
                else
                {
                    sourceRegisterName = decl.Name;
                }
                break;
            default:
                sourceRegisterName = _registers.GetRegisterName(registerKey);
                break;
        }

        sourceRegisterName += GetRelativeAddressingName(instruction, srcIndex);
        sourceRegisterName += instruction.GetSourceSwizzleName(srcIndex, destinationLength);
        return ApplyModifier(instruction.GetSourceModifier(srcIndex), sourceRegisterName);
    }

    // x0[3], x0[r0.x], x0[r0.x + 1].
    private static string GetIndexableTempOperandName(
        D3D10Instruction instruction,
        int operandIndex,
        D3D10OperandTokenCollection.OperandIndex[] operandIndices)
    {
        const int ElementIndex = 1;
        D3D10OperandTokenCollection.OperandIndex element = operandIndices[ElementIndex];
        string index;
        if (!element.IsRelative)
        {
            index = element.Immediate.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            (OperandType indexType, int indexNumber, byte indexComponent) =
                instruction.OperandTokens.GetRelativeIndexOperand(operandIndex, ElementIndex);
            if (indexType != OperandType.Temp)
            {
                throw new NotImplementedException(indexType.ToString());
            }
            index = $"r{indexNumber}.{"xyzw"[indexComponent]}";
            if (element.Immediate != 0)
            {
                index += $" + {element.Immediate.ToString(CultureInfo.InvariantCulture)}";
            }
        }
        return $"x{operandIndices[0].Immediate}[{index}]";
    }

    private string GetDynamicOperandName(
        D3D10Instruction instruction,
        int operandIndex,
        D3D10OperandTokenCollection.OperandIndex[] operandIndices)
    {
        return GetDynamicOperandName(instruction, operandIndex, operandIndices, out _);
    }

    /// <param name="member">The struct member named in a run-time indexed struct
    /// array; the caller rebases the swizzle onto it.</param>
    private string GetDynamicOperandName(
        D3D10Instruction instruction,
        int operandIndex,
        D3D10OperandTokenCollection.OperandIndex[] operandIndices,
        out StructMemberAccess member)
    {
        member = null;
        int relativeIndex = Array.FindIndex(operandIndices, i => i.IsRelative);
        (OperandType indexType, int indexNumber, byte indexComponent) =
            instruction.OperandTokens.GetRelativeIndexOperand(operandIndex, relativeIndex);
        if (indexType != OperandType.Temp)
        {
            throw new NotImplementedException(indexType.ToString());
        }
        string index = $"r{indexNumber}.{"xyzw"[indexComponent]}";

        OperandType operandType = instruction.GetOperandType(operandIndex);
        if (operandType == OperandType.ImmediateConstantBuffer)
        {
            // The immediate beside the index picks which array in the icb is read.
            return $"{_registers.ImmediateConstantBufferName((int)operandIndices[0].Immediate)}"
                + $"[{index}]";
        }
        // An indexed output range is only written by an instanced hull phase, which
        // was unrolled: the index is the instance, so name the register directly.
        if (operandType == OperandType.Output && instruction.ForkInstance is int instance)
        {
            var writtenKey = new D3D10RegisterKey(
                OperandType.Output, (int)operandIndices[0].Immediate + instance);
            int outputMask = instruction.GetWriteMask(operandIndex);
            return _registers.GetRegisterName(
                new RegisterComponentKey(writtenKey, FirstComponent(outputMask)));
        }
        // Likewise a patch constant an unrolled join phase reads by its instance.
        if (operandType == OperandType.InputPatchConstant && instruction.ForkInstance is int readInstance)
        {
            var readKey = new D3D10RegisterKey(
                OperandType.InputPatchConstant, (int)operandIndices[0].Immediate + readInstance);
            return _registers.GetRegisterName(new RegisterComponentKey(readKey,
                instruction.GetSourceSwizzleComponents(operandIndex)[0]));
        }
        // Control points are indexed like geometry shader vertices.
        if (operandType is OperandType.Input or OperandType.InputControlPoint
            or OperandType.OutputControlPoint)
        {
            // One index: a dcl_indexrange input array starting at the immediate.
            // Two: vertex, then register.
            if (operandIndices.Length == 1)
            {
                var arrayKey = new D3D10RegisterKey(
                    OperandType.Input, (int)operandIndices[0].Immediate);
                return $"{_registers.GetRegisterName(new RegisterComponentKey(arrayKey, 0))}[{index}]";
            }
            // The vertex is the relative register plus the immediate (`vocp[r0.x + 3]`).
            if (operandIndices[0].Immediate != 0)
            {
                index = $"{index} + {operandIndices[0].Immediate}";
            }
            // The member is found by component: two semantics can share a register.
            bool isOutputPatch = operandType == OperandType.OutputControlPoint;
            var vertexKey = isOutputPatch
                ? D3D10RegisterKey.CreateControlPointOutput((int)operandIndices[1].Immediate, 0)
                : D3D10RegisterKey.CreateGSInput((int)operandIndices[1].Immediate, 0);
            byte vertexComponent = instruction.GetSourceSwizzleComponents(operandIndex)[0];
            RegisterDeclaration vertexDeclaration =
                _registers.FindInputDeclaration(vertexKey, vertexComponent)
                ?? _registers.RegisterDeclarations[vertexKey];
            string vertexArray = isOutputPatch ? RegisterState.OutputPatchName : _registers.InputArrayName;
            return $"{vertexArray}[{index}].{vertexDeclaration.Name}";
        }

        var registerKey = new D3D10RegisterKey(
            OperandType.ConstantBuffer,
            (int)operandIndices[0].Immediate,
            (int)operandIndices[1].Immediate);
        ConstantDeclaration declaration = _registers.FindConstant(registerKey, 0);
        int elementOffset = _registers.GetConstantBufferElementOffset(registerKey, declaration);
        if (declaration.TypeInfo.MemberInfo != null && declaration.TypeInfo.NumElements > 1)
        {
            // Element = index / stride; the constant offset picks the member.
            int stride = declaration.RegistersPerElement;
            string element = elementOffset / stride == 0
                ? $"{index} / {stride}"
                : $"{index} / {stride} + {elementOffset / stride}";
            byte component = instruction.GetSourceSwizzleComponents(operandIndex)[0];
            if (RegisterState.TryGetStructMemberAt(declaration, $"{declaration.Name}[{element}]",
                elementOffset % stride, component, out member))
            {
                if (member.IsMatrix)
                {
                    int row = (elementOffset % stride) - member.StartOffset / 4;
                    string matrixRow = member.MatrixRow(row);
                    member = null;
                    return matrixRow;
                }
                return member.Name;
            }
        }
        if (declaration.TypeInfo.Rows > 1)
        {
            // Matrix array: the index counts registers, so divide by the row count.
            return RegisterState.MatrixRegisterName(
                declaration.TypeInfo,
                $"{declaration.Name}[{index} / {declaration.RegistersPerElement}]",
                elementOffset);
        }
        return elementOffset == 0
            ? $"{declaration.Name}[{index}]"
            : $"{declaration.Name}[{index} + {elementOffset}]";
    }

    private static string AddressTypeName(int writeMask)
    {
        int components = 0;
        for (int component = 0; component < 4; component++)
        {
            if ((writeMask & (1 << component)) != 0)
            {
                components = component + 1;
            }
        }
        return components > 1 ? "int" + components : "int";
    }

    // aL is the enclosing loop counter; a wide a0 keeps its component.
    private string GetRelativeAddressIndex(D3D9Instruction instruction, int srcIndex)
    {
        if (instruction.GetRelativeParamRegisterType(srcIndex) == RegisterType.Loop)
        {
            return $"i{_loopVariableIndex}";
        }

        int number = instruction.GetRelativeParamRegisterNumber(srcIndex);
        var addressKey = new D3D9RegisterKey(RegisterType.Addr, number);
        string component = IsWideAddressRegister(addressKey)
            ? "." + "xyzw"[instruction.GetRelativeParamComponent(srcIndex)]
            : "";
        return $"a{number}{component}";
    }

    private Dictionary<RegisterKey, int> _addressWriteMasks;

    private bool IsWideAddressRegister(RegisterKey addressKey)
    {
        _addressWriteMasks ??= FindTemporaryRegisterAssignments(_phaseShader.Instructions);
        return _addressWriteMasks.TryGetValue(addressKey, out int writeMask)
            && AddressTypeName(writeMask) != "int";
    }

    private string GetRelativeAddressingName(D3D9Instruction instruction, int srcIndex)
    {
        if (instruction.Params.HasRelativeAddressing(srcIndex))
        {
            string index = GetRelativeAddressIndex(instruction, srcIndex);

            // `floats[i + 2]` reads c2[a0.x] when floats starts at c0.
            var registerKey = instruction.GetParamRegisterKey(srcIndex);
            int elementOffset = 0;
            if (_registers.FindConstantArray(registerKey) is ConstantArray literals)
            {
                elementOffset = registerKey.Number - literals.BaseRegisterIndex;
            }
            // Not matrix arrays: their row offset was handled by the caller.
            else if (_registers.FindConstant(registerKey) is ConstantDeclaration declared
                && declared.TypeInfo.NumElements > 1
                && declared.TypeInfo.Rows == 1)
            {
                elementOffset = registerKey.Number - declared.RegisterIndex;
            }
            if (elementOffset != 0)
            {
                index += $" + {elementOffset}";
            }
            return $"[{index}]";
        }
        return string.Empty;
    }

    private string GetSourceConstantValue(D3D9Instruction instruction, int srcIndex, int? destinationLength = null,
        int rowOffset = 0)
    {
        var registerType = instruction.GetParamRegisterType(srcIndex);
        int registerNumber = instruction.GetParamRegisterNumber(srcIndex) + rowOffset;
        byte[] swizzle = instruction.GetSourceSwizzleComponents(srcIndex);

        // Swizzle entries are picked by which components are written (`c0.xyyx`
        // under .zw is (y, x)), as in GetSourceSwizzleName; a dot product reads
        // its full width whatever it writes.
        int? dotWidth = instruction.Opcode switch
        {
            Opcode.Dp3 => 3,
            Opcode.Dp4 => 4,
            Opcode.DP2Add when srcIndex < 3 => 2,
            _ => null,
        };

        int[] components;
        if (destinationLength != null)
        {
            components = [.. swizzle.Take(destinationLength.Value).Select(c => (int)c)];
        }
        else if (dotWidth != null)
        {
            components = [.. swizzle.Take(dotWidth.Value).Select(c => (int)c)];
        }
        else if (instruction.HasDestination)
        {
            int writeMask = instruction.GetDestinationWriteMask();
            components = [.. Enumerable.Range(0, 4)
                .Where(i => (writeMask & (1 << i)) != 0)
                .Select(i => (int)swizzle[i])];
        }
        else
        {
            components = [.. swizzle.Select(c => (int)c)];
        }

        switch (registerType)
        {
            case RegisterType.ConstBool:
                // defb literals are not modelled; the caller names the uniform.
                return null;
            case RegisterType.ConstInt:
                {
                    var constantInt = _registers.ConstantIntDefinitions.FirstOrDefault(x => x.RegisterIndex == registerNumber);
                    if (constantInt == null)
                    {
                        return null;
                    }

                    uint[] constant = components
                        .Select(s => constantInt[s]).ToArray();

                    // Source modifiers on an integer constant are not supported.
                    if (instruction.GetSourceModifier(srcIndex) != SourceModifier.None)
                    {
                        throw new NotImplementedException();
                    }

                    if (constant.Skip(1).All(c => constant[0] == c))
                    {
                        return constant[0].ToString(_culture);
                    }
                    string size = constant.Length == 1 ? "" : constant.Length.ToString();
                    return $"int{size}({string.Join(", ", constant)})";
                }
            case RegisterType.Const:
            case RegisterType.Const2:
            case RegisterType.Const3:
            case RegisterType.Const4:
                {
                    var constantRegister = _registers.ConstantDefinitions.FirstOrDefault(x => x.RegisterIndex == registerNumber);
                    if (constantRegister == null)
                    {
                        return null;
                    }

                    float[] constant = components
                        .Select(s => constantRegister[s]).ToArray();

                    switch (instruction.GetSourceModifier(srcIndex))
                    {
                        case SourceModifier.None:
                            break;
                        case SourceModifier.Negate:
                            for (int i = 0; i < constant.Length; i++)
                            {
                                constant[i] = -constant[i];
                            }
                            break;
                        case SourceModifier.Abs:
                            for (int i = 0; i < constant.Length; i++)
                            {
                                constant[i] = Math.Abs(constant[i]);
                            }
                            break;
                        case SourceModifier.AbsAndNegate:
                            for (int i = 0; i < constant.Length; i++)
                            {
                                constant[i] = -Math.Abs(constant[i]);
                            }
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    if (constant.Skip(1).All(c => constant[0] == c))
                    {
                        return constant[0].ToString(_culture);
                    }
                    string size = constant.Length == 1 ? "" : constant.Length.ToString();
                    return $"float{size}({string.Join(", ", constant.Select(c => c.ToString(_culture)))})";
                }
            default:
                throw new NotImplementedException();
        }
    }

    private static bool IsGroupSharedOperand(D3D10Instruction instruction, int operandIndex)
    {
        return instruction.GetOperandType(operandIndex) == OperandType.ThreadGroupSharedMemory;
    }

    /// <summary>
    /// The dword element a raw groupshared access names; folded for a literal address.
    /// </summary>
    private string GroupSharedElement(D3D10Instruction instruction, int addressOperand, int dword)
    {
        if (instruction.GetOperandType(addressOperand) == OperandType.Immediate32)
        {
            int bytes = instruction.GetParamInt(addressOperand, 0);
            if (bytes % 4 == 0)
            {
                return (bytes / 4 + dword).ToString(_culture);
            }
        }
        string element = $"{GetOperandName(instruction, addressOperand)} / 4";
        return dword == 0 ? element : $"{element} + {dword}";
    }

    private string GetOperandName(D3D10Instruction instruction, int operandIndex)
    {
        D3D10RegisterKey registerKey = instruction.GetParamRegisterKey(operandIndex);
        // cb[this.x][this.y + row] reads the class's own members.
        LinkageModel.ClassInfo classInfo = null;
        int classRow = 0;
        bool isClassData = _registers.Linkage?.TryGetClassData(
            instruction, operandIndex, out classInfo, out classRow) == true;
        if (isClassData)
        {
            registerKey = new D3D10RegisterKey(OperandType.ConstantBuffer, classInfo.DataRegister, classRow);
        }

        // Each unrolled copy of an instanced phase knows its own instance number.
        if (registerKey.OperandType is OperandType.InputForkInstanceID or OperandType.InputJoinInstanceID)
        {
            return (instruction.ForkInstance ?? 0).ToString(_culture);
        }

        if (registerKey.OperandType == OperandType.Immediate64)
        {
            // One double per value computed, at the slot pair the swizzle names.
            byte[] doubleSwizzle = instruction.GetSourceSwizzleComponents(operandIndex);
            string[] doubles = [.. Enumerable.Range(0, instruction.ValueCount)
                .Select(value => ConstantFormatter.Format(instruction.GetParamDouble(
                    operandIndex, doubleSwizzle[instruction.GetValuePair(value) * 2] / 2)))];
            if (doubles.All(d => d == doubles[0]))
            {
                return doubles[0];
            }
            return $"double{doubles.Length}({string.Join(", ", doubles)})";
        }

        if (registerKey.OperandType == OperandType.Immediate32)
        {
            // Typed by the instruction using them; for a mov, by its readers.
            bool isInteger = IsIntegerImmediate(instruction) || instruction.IsAddressOperand(operandIndex);
            // Any asint is added by Moved or WriteResult instead.
            bool asBits = false;
            if (registerKey.ImmediateSingle.Length == 1)
            {
                string scalar = isInteger
                    ? instruction.GetParamInt(operandIndex, 0).ToString(_culture)
                    : ConstantFormatter.Format(registerKey.ImmediateSingle[0]);
                return asBits ? AsInt(scalar) : scalar;
            }
            byte[] swizzle = instruction.GetSourceSwizzleComponents(operandIndex);
            // `mov o0.zw, l(0, 0, 1, 2)` writes 1 and 2.
            int[] components = GetSourceComponents(instruction, operandIndex, swizzle);
            string[] constant = [.. components
                .Select(s => isInteger
                    ? instruction.GetParamInt(operandIndex, s).ToString(_culture)
                    : ConstantFormatter.Format(registerKey.ImmediateSingle[s]))];
            // A vector constructor has a sign of its own, unlike a bare literal.
            string immediateType = isInteger
                ? (ReadsUnsignedImmediates(instruction) ? "uint" : "int")
                : "float";
            // A scalar, not `int1(0)`, which is invalid as a subscript.
            string vector = components.Length == 1
                ? constant[0]
                : $"{immediateType}{components.Length}(" + string.Join(", ", constant) + ")";
            return asBits ? AsInt(vector) : vector;
        }

        D3D10OperandModifier modifier = instruction.GetOperandModifier(operandIndex);
        if (GetDoubleRegisterOperandName(instruction, operandIndex) is string doubleRegister)
        {
            return ApplyModifier(modifier, doubleRegister);
        }
        if (GetDoubleConstantOperandName(instruction, operandIndex) is string doubleConstant)
        {
            return ApplyModifier(modifier, doubleConstant);
        }
        // A relatively addressed operand's decoded register number is meaningless.
        D3D10OperandTokenCollection.OperandIndex[] operandIndices =
            instruction.OperandTokens.GetOperandIndices(operandIndex);
        string registerName;
        bool isPackedScalar = false;
        StructMemberAccess dynamicMember = null;
        if (registerKey.OperandType == OperandType.IndexableTemp)
        {
            registerName = GetIndexableTempOperandName(instruction, operandIndex, operandIndices);
        }
        else if (!isClassData && operandIndices.Any(i => i.IsRelative))
        {
            registerName = GetDynamicOperandName(instruction, operandIndex, operandIndices, out dynamicMember);
            isPackedScalar = dynamicMember != null && dynamicMember.Width == 1;
            // An indexed vertex member is as wide as its declaration.
            if (registerKey.OperandType is OperandType.Input or OperandType.InputControlPoint
                    or OperandType.OutputControlPoint
                && operandIndices.Length > 1
                && !operandIndices[1].IsRelative)
            {
                // Keyed by the immediate register index, not the decoded number.
                var vertexRegister = new D3D10RegisterKey(
                    registerKey.OperandType == OperandType.OutputControlPoint
                        ? OperandType.OutputControlPoint
                        : OperandType.Input,
                    (int)operandIndices[1].Immediate);
                isPackedScalar = _registers.GetRegisterMaskedLength(new RegisterComponentKey(
                    vertexRegister, instruction.GetSourceSwizzleComponents(operandIndex)[0])) == 1;
            }
        }
        else if (registerKey.OperandType == OperandType.ConstantBuffer)
        {
            // Variables share registers: the component picks the variable (or member).
            byte component = instruction.GetSourceSwizzleComponents(operandIndex)[0];
            registerName = _registers.GetRegisterName(new RegisterComponentKey(registerKey, component));
            isPackedScalar = _registers.GetRegisterMaskedLength(
                new RegisterComponentKey(registerKey, component)) == 1;
            // One read may span several packed variables.
            if (_destinationMaskOverride == null
                && TryNamePackedComponents(instruction, operandIndex, registerKey, out string packed))
            {
                return ApplyModifier(modifier, packed);
            }
        }
        else if (registerKey.OperandType is OperandType.Input or OperandType.OutputControlPoint)
        {
            // Inputs are packed too (TEXCOORD0 at v2.xy, TEXCOORD1 at v2.z).
            byte component = instruction.GetSourceSwizzleComponents(operandIndex)[0];
            var inputComponentKey = new RegisterComponentKey(registerKey, component);
            registerName = _registers.GetRegisterName(inputComponentKey);
            isPackedScalar = _registers.GetRegisterMaskedLength(inputComponentKey) == 1;
            if (_destinationMaskOverride == null
                && TryNamePackedComponents(instruction, operandIndex, registerKey, out string packedInputs))
            {
                return ApplyModifier(modifier, packedInputs);
            }
        }
        else if (registerKey.IsOutput && instruction.IsDestinationOperand(operandIndex))
        {
            // Outputs are packed too; the first written component picks the field.
            int outputMask = _destinationMaskOverride ?? instruction.GetWriteMask(operandIndex);
            var outputComponentKey = new RegisterComponentKey(registerKey,
                Enumerable.Range(0, 4).First(c => (outputMask & (1 << c)) != 0));
            registerName = _registers.GetRegisterName(outputComponentKey);
            isPackedScalar = _registers.IsPackedOutputComponent(outputComponentKey)
                && _registers.GetRegisterMaskedLength(outputComponentKey) == 1;
        }
        else
        {
            registerName = _registers.GetRegisterName(registerKey);
        }
        string writeMaskName;
        // A buffer is named, never masked or swizzled.
        if ((instruction.Opcode == D3D10Opcode.LdStructured && operandIndex == 3)
            || (instruction.Opcode == D3D10Opcode.LdRaw && operandIndex == 2)
            || (instruction.Opcode == D3D10Opcode.StoreRaw && operandIndex == 0)
            || (instruction.Opcode == D3D10Opcode.StoreStructured && operandIndex == 0)
            || (instruction.Opcode == D3D10Opcode.StoreUAVTyped && operandIndex == 0)
            || (instruction.Opcode == D3D10Opcode.LdUAVTyped && operandIndex == 2)
            || (instruction.Opcode == D3D10Opcode.BufInfo && operandIndex == 1)
            || (instruction.Opcode == D3D10Opcode.ImmAtomicAlloc && operandIndex == 1)
            || (instruction.Opcode.IsAtomic() && operandIndex == 0)
            || (instruction.Opcode.IsImmediateAtomic() && operandIndex == 1))
        {
            writeMaskName = "";
        }
        else if (instruction.IsDestinationOperand(operandIndex))
        {
            // The mask within the field, not the register (CENTRE at o0.yzw: z is .y).
            int mask = _destinationMaskOverride ?? instruction.GetWriteMask(operandIndex);
            var componentKey = new RegisterComponentKey(registerKey, FirstComponent(mask));
            int fieldBase = _registers.GetOutputComponentBase(componentKey);
            writeMaskName = instruction.GetWriteMaskName(
                operandIndex,
                _registers.GetRegisterMaskedLength(componentKey),
                mask >> fieldBase);
        }
        else
        {
            // Textures, samplers and UAVs are never swizzled: their swizzle applies
            // to the result (or a gather's channel).
            if (registerKey.OperandType == OperandType.Resource
                || registerKey.OperandType == OperandType.Sampler
                || registerKey.OperandType == OperandType.UnorderedAccessView)
            {
                return ApplyModifier(modifier, registerName);
            }

            // A split statement reads only the components it writes.
            if (_destinationMaskOverride != null && GetSourceLength(instruction, operandIndex) == null)
            {
                return ApplyModifier(modifier, registerName
                    + instruction.GetSourceSwizzleNameForMask(operandIndex, _destinationMaskOverride.Value));
            }

            int? maskedLength = GetSourceLength(instruction, operandIndex);
            if (dynamicMember != null)
            {
                maskedLength = dynamicMember.Width;
            }
            writeMaskName = isPackedScalar
                ? ""
                : Rebased(
                    instruction.GetSourceSwizzleName(operandIndex, maskedLength),
                    dynamicMember?.ComponentBase ?? GetConstantComponentBase(instruction, operandIndex),
                    maskedLength);
            // An integer op into an int register must compute in integers: a float
            // 3 + 4 is 7.0f, whose bits are not 7.
            if (GetConsumedKind(instruction, operandIndex) == ValueKind.Integer
                && GetSourceStorage(instruction, operandIndex) == ComponentStorage.Numeric
                && !IsIntegerConstant(instruction, operandIndex)
                && instruction.HasDestination
                && GetProducedKind(instruction) == ValueKind.Integer
                && GetDestinationStorage(instruction, instruction.GetDestinationParamIndex() ?? 0)
                    == ComponentStorage.Integer)
            {
                // As wide as the operand (a scalar cast would broadcast .x).
                int length = maskedLength ?? instruction.GetDestinationMaskLength();
                string size = length == 1 ? "" : length.ToString();
                return ApplyModifier(modifier,
                    $"(int{size}){string.Format("{0}{1}", registerName, writeMaskName)}");
            }
            // An ftoi result used as a uint index: cast to int so a negative wraps
            // rather than clamping to zero in the float-to-uint conversion.
            if (GetConsumedKind(instruction, operandIndex) == ValueKind.Integer
                && GetSourceStorage(instruction, operandIndex) == ComponentStorage.Numeric
                && !IsIntegerConstant(instruction, operandIndex)
                && IsUnsignedIndexOperand(instruction, operandIndex)
                && HoldsSignedInteger(instruction, operandIndex))
            {
                int indexLength = maskedLength ?? 1;
                string indexSize = indexLength == 1 ? "" : indexLength.ToString();
                return ApplyModifier(modifier,
                    $"(int{indexSize}){string.Format("{0}{1}", registerName, writeMaskName)}");
            }
            // A float read of float bits held in an int register, or of an integer
            // uniform/input without a utof/itof, is an asfloat.
            if (GetConsumedKind(instruction, operandIndex) == ValueKind.Float
                && (HoldsFloatBits(instruction, operandIndex)
                    || IsIntegerUniformOrInput(instruction, operandIndex)))
            {
                return ApplyModifier(modifier,
                    $"asfloat({string.Format("{0}{1}", registerName, writeMaskName)})");
            }
            // A modifier on float bits a mov carries applies to the float:
            // asint(-asfloat(x)).
            if (modifier is not D3D10OperandModifier.None
                && GetConsumedKind(instruction, operandIndex) != ValueKind.Integer
                && HoldsFloatBits(instruction, operandIndex))
            {
                return AsInt(ApplyModifier(modifier,
                    $"asfloat({string.Format("{0}{1}", registerName, writeMaskName)})"));
            }
        }

        return ApplyModifier(modifier, string.Format("{0}{1}", registerName, writeMaskName));
    }

    // A shift operand as an int (HLSL will not shift a float, X3082), cast as wide
    // as the write (a bare (int) over a vector is X3014).
    private string ShiftOperand(D3D10Instruction instruction, int operandIndex)
    {
        string name = GetOperandName(instruction, operandIndex);
        if (instruction.GetOperandType(operandIndex) == OperandType.Immediate32
            || HoldsInteger(instruction, operandIndex))
        {
            return name;
        }
        // A float temp holds an integer as a number: convert. A declared float: its bits.
        if (!instruction.GetParamRegisterKey(operandIndex).IsTempRegister)
        {
            return AsInt(name);
        }
        int length = instruction.GetDestinationMaskLength();
        string size = length == 1 ? "" : length.ToString();
        return $"(int{size}){name}";
    }

    private int _keptOperandCount;

    private static bool IsSameRegister(D3D10Instruction instruction, int operandIndex, RegisterKey registerKey)
    {
        return instruction.GetOperandType(operandIndex) != OperandType.Immediate32
            && instruction.GetParamRegisterKey(operandIndex).Equals(registerKey);
    }

    /// <summary>
    /// Copies an operand into a local when the first of two results overwrites it
    /// (`sincos r0.x, r1.x, r0.x`); otherwise returns the expression unchanged.
    /// </summary>
    private string KeptOperand(D3D10Instruction instruction, int operandIndex,
        int writtenIndex, string typeName, string baseName, string expression)
    {
        if (!IsSameRegister(instruction, operandIndex, instruction.GetParamRegisterKey(writtenIndex)))
        {
            return expression;
        }
        int length = instruction.GetDestinationMaskLength();
        string name = $"{baseName}{_keptOperandCount++}";
        WriteLine("{0}{1} {2} = {3};", typeName, length == 1 ? "" : length.ToString(), name, expression);
        return name;
    }

    // An operand read as uint; the cast is as wide as the write (X3014).
    private string AsUint(D3D10Instruction instruction, int operandIndex)
    {
        string name = GetOperandName(instruction, operandIndex);
        if (instruction.GetOperandType(operandIndex) == OperandType.Immediate32)
        {
            return name;
        }
        if (name.StartsWith("asint("))
        {
            return "asuint(" + name["asint(".Length..];
        }
        int length = instruction.GetDestinationMaskLength();
        string size = length == 1 ? "" : length.ToString();
        return $"(uint{size}){name}";
    }

    /// <summary>
    /// Whether a loop needs [loop] to compile: it uses implicit derivatives and is
    /// not counted (no breakc on a comparison with an immediate).
    /// </summary>
    private bool LoopSamplesWithGradients(Instruction loop)
    {
        int depth = 0;
        bool samples = false;
        bool counted = false;
        D3D10Instruction lastComparison = null;
        for (int i = _phaseShader.Instructions.IndexOf(loop); i < _phaseShader.Instructions.Count; i++)
        {
            Instruction instruction = _phaseShader.Instructions[i];
            bool opens = instruction is D3D10Instruction { Opcode: D3D10Opcode.Loop }
                || instruction is D3D9Instruction { Opcode: Opcode.Loop or Opcode.Rep };
            bool closes = instruction is D3D10Instruction { Opcode: D3D10Opcode.EndLoop }
                || instruction is D3D9Instruction { Opcode: Opcode.EndLoop or Opcode.EndRep };
            if (opens)
            {
                depth++;
            }
            else if (closes && --depth == 0)
            {
                break;
            }
            else if (instruction is D3D10Instruction { Opcode: D3D10Opcode.Sample or D3D10Opcode.SampleB
                    or D3D10Opcode.SampleC or D3D10Opcode.DerivRtx or D3D10Opcode.DerivRty
                    or D3D10Opcode.DerivRtxCoarse or D3D10Opcode.DerivRtxFine
                    or D3D10Opcode.DerivRtyCoarse or D3D10Opcode.DerivRtyFine }
                || instruction is D3D9Instruction { Opcode: Opcode.Tex or Opcode.DSX or Opcode.DSY })
            {
                samples = true;
            }
            else if (depth == 1 && instruction is D3D10Instruction { Opcode: D3D10Opcode.Ige or D3D10Opcode.Ilt
                or D3D10Opcode.UGE or D3D10Opcode.ULT or D3D10Opcode.Ieq or D3D10Opcode.Ine
                or D3D10Opcode.GE or D3D10Opcode.LT or D3D10Opcode.Eq or D3D10Opcode.Ne } comparison)
            {
                lastComparison = comparison;
            }
            else if (depth == 1 && instruction is D3D10Instruction { Opcode: D3D10Opcode.BreakC } && lastComparison != null)
            {
                counted |= Enumerable.Range(1, lastComparison.OperandTokens.OperandCount - 1)
                    .Any(operand => lastComparison.GetOperandType(operand) == OperandType.Immediate32);
                lastComparison = null;
            }
        }
        return samples && !counted;
    }

    // A zero test is of the bits: -0.0f and a comparison mask (NaN) are non-zero.
    private string ZeroTest(D3D10Instruction instruction, int operandIndex, bool nonZero = false)
    {
        string name = instruction.GetOperandType(operandIndex) == OperandType.Immediate32
            ? ImmediateBits(instruction, operandIndex)
            : GetOperandName(instruction, operandIndex);
        if (instruction.GetOperandType(operandIndex) != OperandType.Immediate32
            && GetSourceStorage(instruction, operandIndex) != ComponentStorage.Integer
            && !IsIntegerConstant(instruction, operandIndex))
        {
            name = AsInt(name);
        }
        string test = nonZero || instruction.TestNonZero ? "!=" : "==";
        return $"{name} {test} 0";
    }

    private DeclaredType DeclaredTypeOf(D3D10Instruction instruction, int operandIndex)
    {
        return _registers.GetDeclaredType(new RegisterComponentKey(
            instruction.GetParamRegisterKey(operandIndex),
            instruction.GetSourceSwizzleComponents(operandIndex)[0]));
    }

    private bool IsIntegerConstant(D3D10Instruction instruction, int operandIndex)
    {
        return instruction.GetOperandType(operandIndex) == OperandType.ConstantBuffer
            && DeclaredTypeOf(instruction, operandIndex) is DeclaredType.Bool or DeclaredType.Int or DeclaredType.Uint;
    }

    // Only in float arithmetic, and not bools (fxc selects 1.0 for those).
    private bool IsIntegerUniformOrInput(D3D10Instruction instruction, int operandIndex)
    {
        if (instruction.Opcode is not (D3D10Opcode.Add or D3D10Opcode.Mul or D3D10Opcode.Mad
            or D3D10Opcode.Div or D3D10Opcode.Min or D3D10Opcode.Max
            or D3D10Opcode.Dp2 or D3D10Opcode.Dp3 or D3D10Opcode.Dp4
            or D3D10Opcode.LT or D3D10Opcode.GE or D3D10Opcode.Eq or D3D10Opcode.Ne))
        {
            return false;
        }
        return instruction.GetOperandType(operandIndex) is OperandType.ConstantBuffer or OperandType.Input
            && DeclaredTypeOf(instruction, operandIndex) is DeclaredType.Int or DeclaredType.Uint;
    }

    /// <summary>
    /// Whether an operand holds an integer: a temp by its storage, anything else by
    /// its declared type.
    /// </summary>
    private bool HoldsInteger(D3D10Instruction instruction, int operandIndex)
    {
        RegisterKey key = instruction.GetParamRegisterKey(operandIndex);
        if (key.IsTempRegister)
        {
            return GetSourceStorage(instruction, operandIndex) == ComponentStorage.Integer;
        }
        return DeclaredTypeOf(instruction, operandIndex) is DeclaredType.Bool or DeclaredType.Int or DeclaredType.Uint;
    }

    private int _resourceInfoCount;

    // GetDimensions fills out parameters into a local, then the resource swizzle
    // moves it into the register.
    private void WriteResourceInfo(D3D10Instruction instruction)
    {
        string type = instruction.ResInfoReturnType == D3D10ResInfoReturnType.Uint ? "uint" : "float";
        string resource = GetOperandName(instruction, 2);
        string mipLevel = instruction.GetOperandType(1) == OperandType.Immediate32
            ? instruction.GetParamInt(1, 0).ToString(_culture)
            : GetOperandName(instruction, 1);
        string dimensions = $"dimensions{_resourceInfoCount++}";

        int written = instruction.GetDestinationWriteMask();
        bool[] read = new bool[4];
        byte[] swizzle = instruction.GetSourceSwizzleComponents(2);
        for (int component = 0; component < 4; component++)
        {
            if ((written & (1 << component)) != 0)
            {
                read[swizzle[component]] = true;
            }
        }
        // Multisampled textures have no mip overload.
        if (IsMultisampled(instruction, 2))
        {
            WriteMultisampledDimensions(instruction, 2, type, dimensions);
        }
        else
        {
            // The mip form (level in, mip count out to w) when a level is given or
            // the count is read. The shape decides the other outputs: 1D width only,
            // 2D arrays, cube arrays and 3D also z.
            ResourceDimension? dimension = ResourceDimensionOf(instruction, 2);
            bool hasDepth = dimension is ResourceDimension.Texture2DArray
                or ResourceDimension.TextureCubeArray or ResourceDimension.Texture3D;
            bool is1D = dimension == ResourceDimension.Texture1D;
            // Reading past what the no-mip form reports needs the mip form.
            int noMipComponents = is1D ? 1 : hasDepth ? 3 : 2;
            bool mipForm = mipLevel != "0" || read.Skip(noMipComponents).Any(r => r);
            if (!mipForm && !hasDepth && !is1D)
            {
                WriteLine($"{type}2 {dimensions};");
                WriteLine($"{resource}.GetDimensions({dimensions}.x, {dimensions}.y);");
            }
            else
            {
                string width = !mipForm && hasDepth ? "3" : is1D && !mipForm ? "2" : "4";
                string arguments = mipForm
                    ? is1D ? $"{mipLevel}, {dimensions}.x, {dimensions}.w"
                    : hasDepth ? $"{mipLevel}, {dimensions}.x, {dimensions}.y, {dimensions}.z, {dimensions}.w"
                    : $"{mipLevel}, {dimensions}.x, {dimensions}.y, {dimensions}.w"
                    : hasDepth ? $"{dimensions}.x, {dimensions}.y, {dimensions}.z"
                    : $"{dimensions}.x";
                WriteLine($"{type}{width} {dimensions} = 0;");
                WriteLine($"{resource}.GetDimensions({arguments});");
            }
        }
        WriteResult(instruction, "{0} = {1}{2};", GetOperandName(instruction, 0), dimensions, GetResourceSwizzle(instruction));
    }

    /// <summary>
    /// bufinfo via GetDimensions; a structured buffer also reports a stride, unread.
    /// </summary>
    private void WriteBufferInfo(D3D10Instruction instruction)
    {
        string resource = GetOperandName(instruction, 1);
        string dimensions = $"dimensions{_resourceInfoCount++}";
        bool reportsStride = _registers.IsStructuredResource(instruction.GetParamRegisterKey(1));
        if (reportsStride)
        {
            WriteLine($"uint2 {dimensions};");
            WriteLine($"{resource}.GetDimensions({dimensions}.x, {dimensions}.y);");
        }
        else
        {
            WriteLine($"uint {dimensions};");
            WriteLine($"{resource}.GetDimensions({dimensions});");
        }
        WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
            reportsStride ? $"{dimensions}.x" : dimensions);
    }

    /// <summary>
    /// sampleinfo via GetDimensions, or GetRenderTargetSampleCount for the rasterizer.
    /// </summary>
    private void WriteSampleInfo(D3D10Instruction instruction)
    {
        if (instruction.GetOperandType(1) == OperandType.Rasterizer)
        {
            WriteResult(instruction, "{0} = GetRenderTargetSampleCount();",
                GetOperandName(instruction, 0));
            return;
        }
        string type = instruction.ResInfoReturnType == D3D10ResInfoReturnType.Uint ? "uint" : "float";
        string dimensions = $"dimensions{_resourceInfoCount++}";
        WriteMultisampledDimensions(instruction, 1, type, dimensions);
        // An array's element count pushes the sample count to w.
        string component = IsMultisampledArray(instruction, 1) ? "w" : "z";
        WriteResult(instruction, "{0} = {1}.{2};", GetOperandName(instruction, 0), dimensions, component);
    }

    /// <summary>
    /// Width, height, [elements,] sample count; multisampled textures have no mips.
    /// </summary>
    private void WriteMultisampledDimensions(
        D3D10Instruction instruction, int resourceOperand, string type, string dimensions)
    {
        string resource = GetOperandName(instruction, resourceOperand);
        if (IsMultisampledArray(instruction, resourceOperand))
        {
            WriteLine($"{type}4 {dimensions};");
            WriteLine($"{resource}.GetDimensions({dimensions}.x, {dimensions}.y, {dimensions}.z, {dimensions}.w);");
            return;
        }
        WriteLine($"{type}3 {dimensions};");
        WriteLine($"{resource}.GetDimensions({dimensions}.x, {dimensions}.y, {dimensions}.z);");
    }

    private bool IsMultisampledArray(D3D10Instruction instruction, int operandIndex)
    {
        return ResourceDimensionOf(instruction, operandIndex) == ResourceDimension.Texture2DmsArray;
    }

    private bool IsMultisampled(D3D10Instruction instruction, int operandIndex)
    {
        return ResourceDimensionOf(instruction, operandIndex) is ResourceDimension.Texture2Dms
            or ResourceDimension.Texture2DmsArray;
    }

    private ResourceDimension? ResourceDimensionOf(D3D10Instruction instruction, int operandIndex)
    {
        return _registers.GetTextureDefinition(
            instruction.GetParamRegisterKey(operandIndex))?.Dimension;
    }

    // The texture operand's swizzle picks the channel of each result component
    // (`t2.yzxw` puts green in x). Comparison samples return one value.
    private static string GetResourceSwizzle(D3D10Instruction instruction)
    {
        if (instruction.Opcode is D3D10Opcode.SampleC or D3D10Opcode.SampleCLZ
            or D3D10Opcode.Gather4PoC)
        {
            return "";
        }
        return instruction.GetSourceSwizzleName(TextureOperandIndex(instruction));
    }

    private string GetSampleOffset(D3D10Instruction instruction)
    {
        if (instruction.SampleOffsets == null)
        {
            return "";
        }

        int dimension = GetTextureDimension(instruction);
        string offsets = string.Join(", ", instruction.SampleOffsets.Take(dimension).Select(o => o.ToString(_culture)));
        return dimension > 1
            ? $", int{dimension}({offsets})"
            : $", {offsets}";
    }

    private bool IsBufferResource(D3D10Instruction instruction)
    {
        return LoadedResource(instruction)?.Dimension == ResourceDimension.Buffer;
    }

    private int GetTextureDimension(D3D10Instruction instruction)
    {
        return LoadedResource(instruction).GetDimensionSize();
    }

    /// <summary>
    /// The tbuffer variable an ld reads (one per 16-byte element); null if not a tbuffer.
    /// </summary>
    private string TextureBufferVariable(D3D10Instruction instruction)
    {
        ResourceDefinition buffer = LoadedResource(instruction);
        if (buffer?.ShaderInputType != D3DShaderInputType.TBuffer)
        {
            return null;
        }
        var declarations = _registers.ConstantDeclarations
            .OfType<D3D10ConstantDeclaration>()
            .Where(d => d.IsTextureBuffer && d.BufferName == buffer.Name);
        if (instruction.GetOperandType(1) != OperandType.Immediate32)
        {
            // A computed address subscripts the block's array (the block name is
            // not an identifier).
            D3D10ConstantDeclaration array = declarations
                .FirstOrDefault(d => d.TypeInfo.NumElements > 1);
            if (array == null)
            {
                return null;
            }
            // The address counts registers, not elements.
            string index = GetOperandName(instruction, 1);
            if (array.RegistersPerElement > 1)
            {
                index = $"{index} / {array.RegistersPerElement}";
            }
            return $"{array.Name}[{index}]";
        }
        int register = instruction.GetParamInt(1, 0);
        return declarations
            .FirstOrDefault(d => d.VariableOffset / 16 == register)
            ?.Name;
    }

    // Includes tbuffers, which ld reads like any other buffer.
    private ResourceDefinition LoadedResource(D3D10Instruction instruction)
    {
        return _registers.ResourceDefinitions
            .Where(d => d.ShaderInputType is D3DShaderInputType.Texture
                or D3DShaderInputType.TBuffer)
            .FirstOrDefault(d => d.BindPoint
                == instruction.GetParamRegisterNumber(TextureOperandIndex(instruction)));
    }

    /// <summary>
    /// Operand 2, or 3 for gather4_po whose register offset shifts the rest along.
    /// </summary>
    private static int TextureOperandIndex(D3D10Instruction instruction)
    {
        return instruction.Opcode is D3D10Opcode.Gather4Po or D3D10Opcode.Gather4PoC ? 3 : 2;
    }

    // Identifies a packed variable by its starting component, which is unique.
    private int GetPackedComponentBase(D3D10RegisterKey registerKey, int component)
    {
        var key = new RegisterComponentKey(registerKey, component);
        return registerKey.OperandType switch
        {
            OperandType.ConstantBuffer => _registers.GetConstantComponentBase(key),
            OperandType.Input or OperandType.OutputControlPoint => _registers.GetInputComponentBase(key),
            _ => 0,
        };
    }

    /// <summary>
    /// A constructor over the packed variables one read spans, e.g.
    /// `float3(uv2, uv1.x)`; each run of components is swizzled as its own variable.
    /// </summary>
    private bool TryNamePackedComponents(
        D3D10Instruction instruction,
        int operandIndex,
        D3D10RegisterKey registerKey,
        out string name)
    {
        name = null;
        byte[] swizzle = instruction.GetSourceSwizzleComponents(operandIndex);
        int[] components = GetSourceComponents(instruction, operandIndex, swizzle);
        if (components.Length < 2)
        {
            return false;
        }

        int[] bases = [.. components.Select(c => GetPackedComponentBase(registerKey, c))];

        var runs = new List<List<int>>();
        var runBases = new List<int>();
        for (int i = 0; i < components.Length; i++)
        {
            if (runs.Count > 0 && runBases[^1] == bases[i])
            {
                runs[^1].Add(components[i]);
            }
            else
            {
                runs.Add([components[i]]);
                runBases.Add(bases[i]);
            }
        }
        if (runs.Count < 2)
        {
            return false;
        }

        var parts = new List<string>();
        for (int r = 0; r < runs.Count; r++)
        {
            int variableBase = runBases[r];
            List<int> run = runs[r];
            string variable = _registers.GetRegisterName(
                new RegisterComponentKey(registerKey, run[0]));
            string letters = "";
            foreach (int c in run)
            {
                letters += "xyzw"[c - variableBase];
            }
            int maskedLength = _registers.GetRegisterMaskedLength(
                new RegisterComponentKey(registerKey, run[0]));
            if (letters != "xyzw"[..maskedLength])
            {
                variable += "." + letters;
            }
            parts.Add(variable);
        }

        string type = _integerOperandAnalysis.IsIntegerOperand(instruction) ? "int" : "float";
        name = $"{type}{components.Length}({string.Join(", ", parts)})";
        return true;
    }

    /// <summary>
    /// Rebases a register swizzle onto a packed variable: a float3 at .yzw reads .xyz.
    /// </summary>
    private static string Rebased(string swizzleName, int componentBase, int? maskedLength)
    {
        if (componentBase == 0 || swizzleName.Length < 2)
        {
            return swizzleName;
        }

        string rebased = "";
        foreach (char component in swizzleName[1..])
        {
            rebased += "xyzw"["xyzw".IndexOf(component) - componentBase];
        }
        if (maskedLength != null && rebased == "xyzw"[..maskedLength.Value])
        {
            return "";
        }
        return "." + rebased;
    }

    private int GetConstantComponentBase(D3D10Instruction instruction, int operandIndex)
    {
        byte component = instruction.GetSourceSwizzleComponents(operandIndex)[0];
        var registerComponentKey = new RegisterComponentKey(
            instruction.GetParamRegisterKey(operandIndex), component);
        return instruction.GetOperandType(operandIndex) switch
        {
            OperandType.ConstantBuffer => _registers.GetConstantComponentBase(registerComponentKey),
            OperandType.Input or OperandType.InputPatchConstant or OperandType.OutputControlPoint =>
                _registers.GetInputComponentBase(registerComponentKey),
            _ => 0,
        };
    }

    // The swizzle entries read: by the destination mask unless the operand has a
    // width of its own.
    private int[] GetSourceComponents(D3D10Instruction instruction, int operandIndex, byte[] swizzle)
    {
        int? length = GetSourceLength(instruction, operandIndex);
        if (length != null)
        {
            return [.. swizzle.Take(length.Value).Select(c => (int)c)];
        }
        if (!instruction.HasDestination)
        {
            return [.. swizzle.Select(c => (int)c)];
        }

        int writeMask = instruction.GetDestinationWriteMask();
        return [.. Enumerable.Range(0, 4)
            .Where(i => (writeMask & (1 << i)) != 0)
            .Select(i => (int)swizzle[i])];
    }

    // An operand's own width, where it does not follow the destination mask;
    // null where it does.
    private int? GetSourceLength(D3D10Instruction instruction, int operandIndex)
    {
        // Atomics read scalars; the address width depends on the resource (the
        // byte offset is not part of a structured subscript).
        if (instruction.Opcode.IsAtomic() || instruction.Opcode.IsImmediateAtomic())
        {
            bool keepsOriginal = instruction.Opcode.IsImmediateAtomic();
            int resourceOperand = keepsOriginal ? 1 : 0;
            if (operandIndex == resourceOperand + 1)
            {
                return _registers.GetAtomicAddressWidth(
                    instruction.GetParamRegisterKey(resourceOperand));
            }
            if (keepsOriginal ? operandIndex > 1 : operandIndex != 0)
            {
                return 1;
            }
        }
        // A non-double operand of a double instruction (dmovc condition, ftod
        // source) has one component per value, not two.
        if (instruction.HasDoubleOperands && !instruction.IsDoubleOperand(operandIndex))
        {
            return instruction.ValueCount;
        }
        // Gradients are as wide as the texture.
        if (instruction.Opcode == D3D10Opcode.SampleD && operandIndex is 4 or 5)
        {
            return GetTextureDimension(instruction);
        }
        // LOD, bias and comparison value are scalars.
        if (operandIndex == 4 && instruction.Opcode is D3D10Opcode.SampleL
            or D3D10Opcode.SampleB or D3D10Opcode.SampleC or D3D10Opcode.SampleCLZ
            or D3D10Opcode.Gather4C)
        {
            return 1;
        }
        if (operandIndex == 2
            && instruction.Opcode is D3D10Opcode.Gather4Po or D3D10Opcode.Gather4PoC)
        {
            return GetTextureDimension(instruction);
        }
        // A snapped offset is int2; a sample index is a scalar.
        if (operandIndex == 2)
        {
            if (instruction.Opcode == D3D10Opcode.EvalSnapped)
            {
                return 2;
            }
            if (instruction.Opcode is D3D10Opcode.EvalSampleIndex or D3D10Opcode.SamplePos)
            {
                return 1;
            }
        }
        if (operandIndex is 1 or 2)
        {
            switch (instruction.Opcode)
            {
                case D3D10Opcode.Dp2:
                    return 2;
                case D3D10Opcode.Dp3:
                    return 3;
                case D3D10Opcode.Dp4:
                    return 4;
            }
        }

        if (operandIndex != 1)
        {
            return null;
        }

        if (IsSamplingOpcode(instruction.Opcode))
        {
            return GetTextureDimension(instruction);
        }

        // Typed UAVs: one coordinate per dimension, no mip.
        if (instruction.Opcode == D3D10Opcode.StoreUAVTyped)
        {
            return _registers.GetResourceDimensionSize(instruction.GetParamRegisterKey(0));
        }
        if (instruction.Opcode == D3D10Opcode.LdUAVTyped)
        {
            return _registers.GetResourceDimensionSize(instruction.GetParamRegisterKey(2));
        }
        if (instruction.Opcode == D3D10Opcode.LD)
        {
            // A buffer is addressed by its index alone; a texture takes a mip too.
            return IsBufferResource(instruction) ? 1 : GetTextureDimension(instruction) + 1;
        }
        if (instruction.Opcode == D3D10Opcode.LDMS)
        {
            // No mip: the sample index is an operand of its own.
            return GetTextureDimension(instruction);
        }
        if (instruction.Opcode is D3D10Opcode.LdRaw or D3D10Opcode.StoreRaw)
        {
            return 1;
        }
        // Element index and byte offset are scalars.
        if (instruction.Opcode is D3D10Opcode.LdStructured or D3D10Opcode.StoreStructured
            && operandIndex is 1 or 2)
        {
            return 1;
        }
        return null;
    }

    // Coordinate at operand 1, resource at 2. Not ld, whose address carries a mip.
    private static bool IsSamplingOpcode(D3D10Opcode opcode)
    {
        switch (opcode)
        {
            case D3D10Opcode.Sample:
            case D3D10Opcode.SampleL:
            case D3D10Opcode.SampleB:
            case D3D10Opcode.SampleD:
            case D3D10Opcode.SampleC:
            case D3D10Opcode.SampleCLZ:
            case D3D10Opcode.Gather4:
            case D3D10Opcode.Gather4C:
            case D3D10Opcode.Gather4Po:
            case D3D10Opcode.Gather4PoC:
            case D3D10Opcode.Lod:
                return true;
            default:
                return false;
        }
    }

    private static string ApplyModifier(SourceModifier modifier, string value)
    {
        return modifier switch
        {
            SourceModifier.None => value,
            SourceModifier.Negate => $"-{value}",
            SourceModifier.Bias => $"{value}_bias",
            SourceModifier.BiasAndNegate => $"-{value}_bias",
            SourceModifier.Sign => $"{value}_bx2",
            SourceModifier.SignAndNegate => $"-{value}_bx2",
            SourceModifier.Complement => throw new NotImplementedException(),
            SourceModifier.X2 => $"(2 * {value})",
            SourceModifier.X2AndNegate => $"(-2 * {value})",
            SourceModifier.DivideByZ => $"{value}_dz",
            SourceModifier.DivideByW => $"{value}_dw",
            SourceModifier.Abs => $"abs({value})",
            SourceModifier.AbsAndNegate => $"-abs({value})",
            SourceModifier.Not => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };
    }

    private static string ApplyModifier(D3D10OperandModifier modifier, string value)
    {
        if (modifier.HasFlag(D3D10OperandModifier.Abs))
        {
            value = $"abs({value})";
        }
        if (modifier.HasFlag(D3D10OperandModifier.Neg))
        {
            value = $"-({value})";
        }
        return value;
    }
}