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
    // Over the phase being written rather than the whole shader: what an immediate
    // means is decided by what reads it, and a hull shader's phases read their own.
    private IntegerOperandAnalysis _integerOperandAnalysis;

    public HlslSimpleWriter(ShaderModel shader)
        : base(shader)
    {
    }

    protected override bool WritesInstructions => true;

    protected override void WriteMethodBody()
    {
        _integerOperandAnalysis = new IntegerOperandAnalysis(_phaseShader);
        // Before the walk below, which asks it what reads a pair it has just seen
        // filled.
        _programOrder = new ProgramOrder(_phaseShader,
            (instruction, operand) => GetSourceComponents(
                instruction, operand, instruction.GetSourceSwizzleComponents(operand)));
        FindMovedDoubles();
        // Per function, not per shader: a hull shader's two functions are written
        // through one writer, and neither inherits the other's loop counters or the
        // slots it was still holding.
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

        // A shader with dynamic linkage runs its bodies as methods of their own
        // class: main reaches the first label and no further, and the temps main
        // needs are only the ones written before it.
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
                // The one place a statement is two instructions rather than one.
                // An append buffer has no spelling for half of it: no subscript to
                // store through, and no counter to keep the slot in - so
                // imm_atomic_alloc and the store that fills the slot are written as
                // the Append they came from, or neither of them can be written at
                // all. The store is not always the instruction after: what is
                // appended is computed between the two whenever it reads nothing the
                // alloc needed, so the slot waits here for the store that fills it.
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
    /// Whether a store appends the element the alloc before it took the slot for:
    /// the same buffer, addressed by the register the alloc wrote.
    /// </summary>
    private static bool IsAppendPair(D3D10Instruction alloc, D3D10Instruction store)
    {
        return store.Opcode == D3D10Opcode.StoreStructured
            && store.GetParamRegisterNumber(0) == alloc.GetParamRegisterNumber(1)
            && store.GetOperandType(1) != OperandType.Immediate32
            && store.GetParamRegisterKey(1).Equals(alloc.GetParamRegisterKey(0));
    }

    // The alloc that took each slot, by the register it left the slot in, waiting
    // for the store that fills it.
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
    /// The constant registers an fx_2_0 preshader fills, as locals filled the same
    /// way. The preshader runs before the shader, so these come first, and the
    /// instructions that read c1 read the local c1 rather than a uniform: nothing
    /// declares one there, because the effect compiler moved its arithmetic out of
    /// the shader and left only the register.
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
    /// The line a temp variable is declared by: as wide as it is written and of the
    /// type it is written as, with the doubles it holds shadowed beside it. The body
    /// of a linkage method declares its locals the same way main declares its temps.
    /// </summary>
    private void WriteTemporaryVariableDeclaration(RegisterKey registerKey, int writeMask)
    {
        // An array subscript has to be an integer, and the address register is
        // only ever used as one.
        bool isAddressRegister = registerKey is D3D9RegisterKey addressKey
            && addressKey.Type == RegisterType.Addr;
        // A register holding only integers has to be declared as one: a shift or a
        // bitwise operator will not take a float, however the bits got there.
        string scalarType = isAddressRegister || IsIntegerTempRegister(registerKey, writeMask)
            ? "int"
            : IsHalfTempRegister(registerKey) ? "half" : "float";
        // The address register is as wide as it is written: `mova a0.xy`
        // loads two indices, and reading them both out of a scalar is not
        // possible.
        string writeMaskName = isAddressRegister ? AddressTypeName(writeMask) : writeMask switch
        {
            0x1 => scalarType,
            0x3 => scalarType + "2",
            0x7 => scalarType + "3",
            0xF => scalarType + "4",
            _ => scalarType + "4",// TODO
        };
        // A register nothing but doubles is written to needs no float variable
        // beside their shadow: every value in it is named through the shadow, and
        // the float2 a StructuredBuffer<double> loads through was declared and
        // never read. Asked of the writes rather than of the mask, because fxc
        // reuses a register freely and one holding a double at .xy and a
        // comparison mask at .x needs both.
        if (FindFloatWrittenComponents(registerKey) != 0)
        {
            WriteLine("{0} {1};", writeMaskName, GetTempRegisterName(registerKey));
        }
        WriteDoubleRegisterDeclaration(registerKey);
    }

    /// <summary>
    /// The shadow variable a register's doubles are kept in. A double takes two
    /// components, so a register holds two of them where it holds four floats, and
    /// each one is declared alongside the register: `double2 d0` beside `float4 r0`,
    /// with r0.xy as d0.x and r0.zw as d0.y. Transcribed as the two float components
    /// they really are, the arithmetic would be done in floats over each half of
    /// the number separately, which is not the number at all.
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
    /// The instruction main ends at: the last one there is, or the one before the
    /// first label once dynamic linkage starts writing bodies of their own.
    /// </summary>
    private Instruction LastMainInstruction()
    {
        return _registers.Linkage != null && _registers.Linkage.HasLinkage
            ? _phaseShader.Instructions[_registers.Linkage.MainInstructionCount - 1]
            : _phaseShader.Instructions[^1];
    }

    /// <summary>
    /// A call through an interface: the result lands in the register every body it
    /// could run writes, and the arguments are the registers any of those bodies
    /// reads before writing them - named as main names them, which is how they reach
    /// the body under its own parameter names.
    /// </summary>
    private void WriteBranchAttribute()
    {
        if (WritesFlowAttributes)
        {
            WriteLine("[branch]");
        }
    }

    /// <summary>
    /// An append of a struct element: the struct put together a member at a time
    /// from the register's components, and appended. The register itself is a
    /// float4 and not the struct, which fxc refused (X3017). False where the
    /// element is not a struct, or the store does not write it whole from offset 0.
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

    private void WriteInterfaceCall(D3D10Instruction instruction)
    {
        int function = (int)instruction.OperandTokens.Tokens[0];
        int interfaceNumber = (int)instruction.OperandTokens.Tokens[2];
        int instance = (int)instruction.OperandTokens.Tokens[3];
        LinkageModel.InterfaceInfo iface = _registers.Linkage.InterfaceByNumber(interfaceNumber);
        LinkageModel.MethodInfo method = iface.Methods[function];
        string arguments = string.Join(", ", method.Parameters.Select(GetLinkageArgumentName));
        WriteLine("{0} = {1}.{2}({3});", GetTempRegisterName(method.ReturnRegister),
            iface.InstanceExpression(instance), method.Name, arguments);
    }

    private string GetLinkageArgumentName(RegisterKey parameter)
    {
        if (parameter is D3D10RegisterKey { OperandType: OperandType.Input } input)
        {
            return _registers.GetRegisterName(new RegisterComponentKey(input, 0));
        }
        return GetTempRegisterName(parameter);
    }

    /// <summary>
    /// A body transcribed instruction by instruction: its locals declared the way
    /// main's temps are, its ret replaced by the return of the one register it
    /// writes, and the registers it reads before writing them already the
    /// parameter names the signature declares.
    /// </summary>
    protected override void WriteLinkageMethodBody(LinkageModel.FunctionBodyInfo body)
    {
        // The classes are written before main, and a body writes instructions the
        // way main writes them - which asks for the analyses main's method body
        // would have built by then. The same phase, so the same answers.
        _integerOperandAnalysis ??= new IntegerOperandAnalysis(_phaseShader);
        _programOrder ??= new ProgramOrder(_phaseShader,
            (instruction, operand) => GetSourceComponents(
                instruction, operand, instruction.GetSourceSwizzleComponents(operand)));
        FindMovedDoubles();

        foreach ((RegisterKey register, int writeMask) in FindTemporaryRegisterAssignments(
            [.. _phaseShader.Instructions.Skip(body.First).Take(body.Last - body.First)]))
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
        WriteLine("return {0};", GetTempRegisterName(body.ReturnRegister));
    }

    /// <summary>
    /// Whether the instruction is the half of a call the writer folds into
    /// the other half: the slot an append buffer took and the one a
    /// consume buffer takes are written as part of Append and Consume, so
    /// neither leaves an assignment behind to declare a register for.
    /// </summary>
    private bool IsFoldedIntoCall(D3D10Instruction instruction)
    {
        return (instruction.Opcode == D3D10Opcode.ImmAtomicAlloc
                && IsAppendResource(instruction.GetParamRegisterKey(1)))
            || (instruction.Opcode == D3D10Opcode.ImmAtomicConsume
                && IsConsumeResource(instruction.GetParamRegisterKey(1)));
    }

    /// <summary>
    /// The constant a register component holds, where that is knowable: one
    /// write in the whole program, and that write a mov of an immediate. A
    /// structured atomic names its member by the byte offset beside the element
    /// index in its address, and fxc puts that offset in a register as readily
    /// as in an immediate - `mov r0.y, l(0)` beside `imm_atomic_cmp_exch r1.x,
    /// u0, r0.xyxx, ..`, where the zero names the first member.
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
                    // Written twice: which of them this read sees is a
                    // question a transcription cannot answer.
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
    /// The components of a register written by anything that is not a double, as a
    /// write mask. A write that goes through the shadow variable leaves the float
    /// components of the register untouched, so a register only doubles are written
    /// to answers zero and needs no float variable at all.
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
            // A slot the writer folds into the call it belongs to is written
            // by no line of the output, so the register holding it needs no
            // declaration either: an Append carries its own slot and a
            // Consume carries its own, and `float r1;` stood for a value
            // nothing else in the function mentions.
            if (IsFoldedIntoCall(d3d10))
            {
                continue;
            }
            // The register a call through an interface returns into is written
            // by that call, floats, whatever its operand tokens say.
            if (InterfaceCallDestination(d3d10) is RegisterKey returned
                && Equals(returned, registerKey))
            {
                mask |= 0b1111;
                continue;
            }
            foreach (int destination in GetDestinationParamIndices(d3d10))
            {
                if (!Equals(d3d10.GetParamRegisterKey(destination), registerKey))
                {
                    continue;
                }
                // Per statement, not per instruction: a load that takes a double and
                // a uint of the element is written as two, and only the double's half
                // of it goes through the shadow. Counted whole, the uint's component
                // was held to be a double's and the register went undeclared.
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

    // Which halves of a register ever hold a double: bit 0 for the pair at .xy and
    // bit 1 for the pair at .zw.
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

    /// <summary>
    /// The shadow variable an operand's doubles are named by, or null where the
    /// operand holds none. Which halves of the register it covers is asked of the
    /// instruction rather than of the operand: a source names one double for each
    /// value the instruction computes, and fxc repeats the pair across the swizzle
    /// so that the nth of them is at slot 2n.
    /// </summary>
    // The components of the destination this statement writes, which for an
    // instruction written as several is the part of it being written now.
    private int DestinationMask(D3D10Instruction instruction)
    {
        return _destinationMaskOverride ?? instruction.GetWriteMask(0);
    }

    /// <summary>
    /// Whether the member a structured load or store reaches is a double. Asked at
    /// the first component the mask names rather than of the element as a whole: an
    /// element with a double in it has other members beside, and a load of the float
    /// among them is a load of a float.
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
    /// Which halves of its destination each mov copies a double into, by instruction:
    /// bit 0 for the pair at .xy and bit 1 for the pair at .zw. fxc assembles a
    /// double vector out of movs of the raw halves - `mov r0.zw, r0.xxxy` puts the
    /// pair at r0.xy into the pair at r0.zw - and nothing about the instruction says
    /// it carries one number rather than two floats. The opcode cannot say and
    /// neither can a set of register components: fxc uses one for a double here and
    /// a float there. What says is where the walk below has got to.
    /// </summary>
    private Dictionary<D3D10Instruction, int> _movedDoublePairs =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Every register pair the walk ever found holding a double, which is what the
    /// shadow variables are declared from. Asked of the walk rather than of a scan
    /// for double instructions, because a shader can hold doubles without one: a
    /// load of a double member and a store of it back is the whole of what some do,
    /// and their shadow was named and never declared.
    /// </summary>
    private HashSet<(RegisterKey Register, int Pair)> _doubleRegisterPairs = [];

    /// <summary>
    /// What the register pairs held when each instruction was reached, for the
    /// instructions reached with any of them holding a double. A source operand
    /// reads what was there before the instruction, and whether that was a double
    /// is a question about the moment and not about the register: fxc uses a pair
    /// for a double here and for two numbers there. Empty for every shader without
    /// a double in it, which is all but a handful.
    /// </summary>
    private Dictionary<D3D10Instruction, HashSet<(RegisterKey Register, int Pair)>> _liveDoublePairs =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Which pairs of its destination each instruction leaves holding a double,
    /// whatever the instruction was. The mov record above is this one narrowed to
    /// the opcode that needs it for its operands; a load needs it to know that the
    /// pair it filled is about to be read as a double.
    /// </summary>
    private Dictionary<D3D10Instruction, int> _madeDoublePairs =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The movs that make a double out of two 32-bit values rather than carry one
    /// that was already a double. Both kinds are in the mov record above, because
    /// both leave a double in the destination pair - what this adds is which side
    /// of the mov the shadow is on. A mov that carries one reads a shadow and
    /// writes a shadow; one that makes a double reads two numbers and writes a
    /// shadow, and naming a shadow on its source side named a pair that is not one.
    ///
    /// Per mov rather than per pair, which is as fine as the shape needs: a mov
    /// that joined one of its pairs and carried the other would have to read a
    /// uint2 and a double out of one operand, and fxc writes a double uniform as
    /// the pair it already is rather than moving it anywhere.
    /// </summary>
    private HashSet<D3D10Instruction> _joinedDoubleMovs =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Whether any instruction passes doubles at all, which is what makes the look
    /// forward below worth doing. Every shader without one answers no and never
    /// scans.
    /// </summary>
    private bool _hasDoubleInstructions;

    /// <summary>What wrote a register component and what reads it, which is the whole
    /// of what this writer knows about a value: see ProgramOrder.</summary>
    private ProgramOrder _programOrder;

    /// <summary>
    /// Walks the instructions in order keeping track of which register pairs hold a
    /// double, so that a mov between two of them can be told from a mov of two
    /// floats. A pair holds one from where a double instruction or a load of a double
    /// member wrote it until something else writes either of its components; a mov
    /// out of a pair that holds one carries the double on.
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
            // Before the destination is looked at, and before the write below
            // changes what is live: a store has no temp destination and is exactly
            // the instruction that has to know what the pair it reads was holding.
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
            // Every pair the instruction touches stops holding what it held, and the
            // ones it just made doubles start. A write to one component of a pair is
            // enough to lose the number: half a double is not one.
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
    /// Whether the value a structured store writes was a double when the store was
    /// reached. The opcode cannot say and neither can the element: a store of a
    /// double into a member that is not one is how a shader writes the number's two
    /// words out, and there is no instruction for the split. What says is where the
    /// walk above had got to.
    /// </summary>
    private bool StoresLiveDouble(D3D10Instruction instruction)
    {
        if (instruction.Opcode != D3D10Opcode.StoreStructured
            || instruction.GetParamRegisterKey(3) is not D3D10RegisterKey { IsTempRegister: true } value
            || !_liveDoublePairs.TryGetValue(instruction, out HashSet<(RegisterKey, int)> live))
        {
            return false;
        }
        // Both halves of an aligned pair, or there is no double being stored.
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
    /// A store of a double into something that is not one, written as the asuint
    /// that takes it apart. The two words come back through out parameters - there
    /// is no expression for one of them - and fxc takes a buffer element's
    /// components as those parameters directly, so no name has to be invented for
    /// the pair on the way.
    ///
    /// False where the store is of something else, which leaves the assignment the
    /// caller was about to write. A store into a member that *is* a double is one
    /// of those: the element holds the number, and assigning the shadow to it is
    /// the whole of what the instruction did.
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
            // Both halves, or it is not a double being written.
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
                    // Or the member is not a double and a double instruction reads
                    // the pair all the same, which makes the two components the
                    // number's bits: the load is where they enter, so it is where
                    // the asdouble that joins them goes. Left as a load of two
                    // floats, the arithmetic read a shadow nothing had assigned to.
                    || _programOrder.IsReadAsDouble(instruction, destination, pair);
            }
            else if (instruction.Opcode == D3D10Opcode.Mov)
            {
                // The pair the mov reads for this pair of its destination, which the
                // swizzle gives at the destination's own position.
                byte[] swizzle = instruction.GetSourceSwizzleComponents(1);
                bool isAlignedPair = swizzle[low] % 2 == 0
                    && swizzle[low + 1] == swizzle[low] + 1;
                isDouble = false;
                if (instruction.GetParamRegisterKey(1)
                    is D3D10RegisterKey { IsTempRegister: true } source)
                {
                    isDouble = isAlignedPair && live.Contains((source, swizzle[low] / 2));
                }
                // Two integers moved out of something that is not a register, into a
                // pair a double instruction goes on to read: the two words of a
                // double. fxc reads a double uniform straight off the pair it
                // occupies and a uint2 uniform it cannot, so it movs that one into a
                // register first, and the words enter the shader at the mov.
                //
                // Integers, because what the join needs is their bits: a pair of
                // floats held in a register declared float would have to be read
                // back through asuint, and whether such a register holds a number or
                // a float's bits is the one thing this writer does not record - see
                // the entries in EquivalenceTests.KnownDifferences that turn on it.
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
    /// The two 32-bit values a two component operand names, one at a time, so that
    /// the asdouble joining them can name each. Either a swizzle of two letters off
    /// a wider variable - `bits.xy` - or a two wide one named whole, which a pair
    /// that is the whole of what it is on comes out as.
    ///
    /// Null where the name is neither, which leaves the plain move the caller was
    /// about to write rather than an asdouble of something that is not two values.
    /// </summary>
    private static (string Low, string High)? SplitTwoComponentName(string name)
    {
        int dot = name.LastIndexOf('.');
        string swizzle = dot < 0 ? "" : name[(dot + 1)..];
        // A dot inside a subscript is not a swizzle: `buffer[i.x]` names one value
        // and ends in a letter all the same.
        if (swizzle.Length == 0 || !swizzle.All(c => "xyzw".Contains(c)))
        {
            return ($"{name}.x", $"{name}.y");
        }
        return swizzle.Length == 2
            ? ($"{name[..dot]}.{swizzle[0]}", $"{name[..dot]}.{swizzle[1]}")
            : null;
    }

    /// <summary>
    /// Whether a structured load fills a register pair with a double built out of
    /// the two components of the element it read, rather than with the element's
    /// own two numbers. The member is not a double - the element says nothing - and
    /// what makes it one is that a double instruction goes on to read the pair.
    /// </summary>
    private bool MakesDoubleFromBits(D3D10Instruction instruction)
    {
        if (instruction.Opcode != D3D10Opcode.LdStructured
            || !_madeDoublePairs.ContainsKey(instruction)
            || IsDoubleStructuredComponent(instruction, 3))
        {
            return false;
        }
        // The one shape the join can be written in: the whole of what the load
        // writes is one pair, and the element is a vector whose components can be
        // named one at a time. An element with members of its own puts the two words
        // who knows where in it, and `asdouble` of a member is not something this
        // writer can spell.
        return instruction.GetDestinationWriteMask() is 0b0011 or 0b1100
            && _registers.FindStructuredBuffer(instruction.GetParamRegisterKey(3))
                ?.ElementType is { MemberInfo: null or { Count: 0 } };
    }

    /// <summary>
    /// A double a constant buffer holds, named by which element of its variable it is
    /// rather than by the components it takes: the second double of a double2 is at
    /// cb0[0].zw, and the swizzle a register read would give it calls it `.zw`. A
    /// double4 spills into the next register, and the base counts back over the ones
    /// the variable has already filled, so its `.z` is the second register's first
    /// pair.
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

    private string GetDoubleRegisterOperandName(D3D10Instruction instruction, int operandIndex)
    {
        bool isDestination = instruction.IsDestinationOperand(operandIndex);
        // A structured load or store carries a double as plainly as the arithmetic
        // does, and the element says so where the opcode cannot: a load from a
        // StructuredBuffer<double> fills a pair, and written to the register's two
        // float components the arithmetic that follows read a shadow nothing had
        // assigned to.
        bool isStructuredDouble = instruction.Opcode switch
        {
            D3D10Opcode.LdStructured => operandIndex == 0
                && (IsDoubleStructuredComponent(instruction, 3)
                    || MakesDoubleFromBits(instruction)),
            // And a store of a double into a member that is not one, which is how
            // a shader writes the number's two words out: the value is still the
            // double in the shadow, and the asuint that splits it is written where
            // the store is.
            D3D10Opcode.StoreStructured => operandIndex == 3
                && (IsDoubleStructuredComponent(instruction, 0)
                    || StoresLiveDouble(instruction)),
            _ => false,
        };
        // And a mov says so only through the walk: which of its halves carry a
        // double was decided by what the registers held when it was reached.
        int movedPairs = _movedDoublePairs.TryGetValue(instruction, out int moved) ? moved : 0;
        // The destination of either kind of mov, and the source of one that carries
        // a double rather than making one out of two numbers: there is no shadow on
        // the reading side of a join.
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
        // A mov names the pairs the walk found on both sides of it: the destination's
        // own, and for the source whichever pair the swizzle reads for each of them.
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
        // The value a structured store carries is read at the components its mask
        // names, which is the destination's mask and not this operand's swizzle.
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
        string name = GetDoubleRegisterName(registerKey);
        // A register with only the one pair of doubles in it is declared a double
        // and has no halves to name.
        if (GetDoublePairMask(registerKey) == 1)
        {
            return name;
        }
        string subscript = string.Concat(pairs.Select(pair => "xy"[pair]));
        return subscript == "xy" ? name : $"{name}.{subscript}";
    }

    // Only when every written component is an integer no float instruction ever
    // touches. fxc reuses a register freely, and one it uses for a loop counter and
    // later for an angle is a float register holding an integer for a while, not
    // an int register holding a float - the float would truncate. What such a
    // component holds, and how the integer instructions get at it, is the
    // register's storage: see IntegerOperandAnalysis.GetStorage.
    /// <summary>
    /// Whether every write of a register asks for partial precision, which is what
    /// lets it be declared half and the half cast come off each of those writes.
    ///
    /// Every write, because the modifier is a property of the instruction and not of
    /// the register: fxc fills one with a mov_sat at full precision and overwrites it
    /// with a mul_pp, and declaring that half would narrow the write that did not ask
    /// for it.
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
        // A constant buffer variable is kept the way it was declared: an int
        // index moved into an int register is a move, not a conversion, and
        // `(int)idx` over an int said otherwise.
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
        // Every component of a register shares its declaration, so the first one
        // read answers for all of them.
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
        // oMask joins the temps because the analysis has a type for it - a uint -
        // where a register outside them usually holds whatever number it is given.
        // Outputs join them too, typed by their signature: `mov o1.x, vPrim` moves
        // the primitive id whole, and a float carried on the way into a uint output
        // keeps its bits only below 2^24. The same answer IsFloatOutput already
        // gives of them - the instructions say nothing about what an output holds,
        // and left untyped, an integer reaching an int output was always a crossing
        // into a float one.
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
        // A typed view is addressed by a coordinate: a texel index, whatever the
        // texel it names is kept as. Read as a float, an integer coordinate in a
        // register of bits was reinterpreted - `tex[asfloat(coordinate)]`, which
        // truncates the bits of a float back into an index of a texel nobody asked
        // for.
        if (instruction.Opcode is D3D10Opcode.StoreUAVTyped or D3D10Opcode.LdUAVTyped
            && operandIndex == 1)
        {
            return ValueKind.Integer;
        }
        // And the texel itself is whatever the view holds. Taken for the float a
        // texel usually is, an integer one was stored through asfloat(), which HLSL
        // converts back by value: the texel became the number its bits spell.
        if (instruction.Opcode == D3D10Opcode.StoreUAVTyped && operandIndex == 2)
        {
            return _integerOperandAnalysis.GetTypedStoreKind(instruction);
        }
        if (instruction.Opcode == D3D10Opcode.MovC && operandIndex == 1)
        {
            return ValueKind.Bits;
        }
        // An attribute is a float and where it is evaluated is not: a sample index,
        // or an offset counted in sixteenths of a pixel.
        if (instruction.Opcode is D3D10Opcode.EvalSampleIndex or D3D10Opcode.EvalSnapped)
        {
            return operandIndex == 2 ? ValueKind.Integer : ValueKind.Float;
        }
        return instruction.Opcode.ConsumedKind();
    }

    /// <summary>
    /// Whether a conditional break's comparison is one the shader itself settled:
    /// both sides constants it defined, and the comparison between them true.
    ///
    /// True only, never false. fxc does not emit a break that never breaks, so a
    /// false answer here means this has misread something - and dropping the break
    /// on that reading is a larger claim than writing the `break;` on a true one.
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
    /// The one value a source operand reads, where it is a constant the shader
    /// defined with def and carries a modifier that can be applied to it here.
    /// Null for a register the shader computes or a uniform the application sets,
    /// neither of which has a value to read at this point.
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
    /// A value carried by a mov or a movc out of a register of one declaration into
    /// one of the other. An integer moving between them is converted - it is a
    /// number on both sides - and a float held as bits is read back out of them.
    /// Putting one *into* bits is left to <see cref="WriteResult"/>, which wraps the
    /// whole expression and so keeps a saturate inside the reinterpretation rather
    /// than around it.
    /// </summary>
    private string Moved(D3D10Instruction instruction, int sourceIndex, string name)
    {
        if (instruction.GetOperandType(sourceIndex) == OperandType.Immediate32)
        {
            // An integer immediate is a number in either declaration; a float one
            // goes into an int register as its bits, written as the integer they
            // are. `asint(1)` is not them: a whole float prints without a decimal
            // point, and HLSL reads that as the integer 1.
            return !IsIntegerImmediate(instruction)
                && GetDestinationStorage(instruction, 0) == ComponentStorage.Integer
                ? ImmediateBits(instruction, sourceIndex)
                : name;
        }
        // Between registers of one declaration a move carries what is there, bits
        // and all: a movc selecting between two sets of bits selects bits, and
        // reading them as floats to select between them would flush a small integer
        // to zero. Only a move between the two declarations converts, and then a
        // float is reinterpreted where an integer is converted. Putting a float
        // *into* bits is left to WriteResult, which wraps the whole expression and
        // so keeps a saturate inside the reinterpretation rather than around it.
        ComponentStorage source = GetSourceStorage(instruction, sourceIndex);
        ComponentStorage destination = GetDestinationStorage(instruction, 0);
        if (source == destination)
        {
            return name;
        }
        // Where every source crosses, the conversion goes on once around the whole
        // expression in WriteResult; where only some do, each of those carries its
        // own, since the halves of a select have to meet as the same kind.
        if (IsReinterpretedResult(instruction, 0))
        {
            return name;
        }
        if (IsFloatValue(instruction))
        {
            return source == ComponentStorage.Integer ? $"asfloat({name})" : AsInt(name);
        }
        // As wide as what is being moved. A scalar cast over two components takes
        // the first and spreads it: `(float)vThreadID.xy` is the x of it twice, and
        // an address built that way reads the wrong texel - which is what
        // `mov r0.xy, vThreadID.xy` came out as.
        int length = MovedComponentCount(instruction);
        string size = length == 1 ? "" : length.ToString();
        return destination == ComponentStorage.Integer ? $"(int{size}){name}" : $"(float{size}){name}";
    }

    /// <summary>
    /// How many components a move carries: the ones its destination mask names.
    /// </summary>
    private static int MovedComponentCount(D3D10Instruction instruction)
    {
        return instruction.HasDestination ? instruction.GetDestinationMaskLength() : 1;
    }

    // How an immediate an instruction reads is printed: as an integer where the
    // opcode says so, or where a mov's readers do.
    private bool IsIntegerImmediate(D3D10Instruction instruction)
    {
        // The only immediate an eval takes is the place it evaluates the attribute
        // at - a sample index, or an offset in sixteenths of a pixel - whatever the
        // attribute itself is made of. A samplepos takes one immediate too, and it
        // is the sample it asks about, however much of a float2 it answers with.
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
    /// Whether what an instruction writes goes into an int register as a float's
    /// bits. A move between registers of one declaration is not that: it carries
    /// what is there as it is.
    /// </summary>
    private bool IsReinterpretedResult(D3D10Instruction instruction, int destinationIndex)
    {
        // The register's declaration and not the component's history: a load writing
        // a whole register writes a float into every component of it, whatever each
        // of them holds elsewhere in the shader.
        if (GetDestinationStorage(instruction, destinationIndex) != ComponentStorage.Integer
            || instruction.GetOperandType(destinationIndex) != OperandType.Temp)
        {
            return false;
        }
        if (instruction.Opcode is D3D10Opcode.Mov or D3D10Opcode.MovC)
        {
            int firstValue = instruction.Opcode == D3D10Opcode.Mov ? 1 : 2;
            ComponentStorage destination = GetDestinationStorage(instruction, destinationIndex);
            // An immediate carries its own reinterpretation, as the bits it is.
            if (Enumerable.Range(firstValue, instruction.OperandTokens.OperandCount - firstValue)
                .Any(value => instruction.GetOperandType(value) == OperandType.Immediate32))
            {
                return false;
            }
            // A register of the same declaration on either side carries what is
            // there; only a temp answers for its own declaration, an input or a
            // constant being a value whatever the register rule makes of it.
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
    /// Whether what an instruction writes is a float. A mov carries whatever it is
    /// given, so the operand it carries answers for it.
    /// </summary>
    private bool IsFloatValue(D3D10Instruction instruction)
    {
        if (instruction.Opcode is not (D3D10Opcode.Mov or D3D10Opcode.MovC))
        {
            return GetProducedKind(instruction) == ValueKind.Float;
        }
        // A saturating move clamps to [0, 1], which is a float's range and nothing
        // an integer is put through.
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

    // Whether a numeric operand only ever holds integers: read and written by
    // integer instructions and never by a float one.
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
    /// The register a call through an interface leaves its result in: not an operand
    /// of the fcall - it has none - but the one register every body it could run
    /// writes. Null for every other instruction.
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

    private Dictionary<RegisterKey, int> FindTemporaryRegisterAssignments(IList<Instruction> instructions)
    {
        var tempRegisters = new Dictionary<RegisterKey, int>();
        foreach (Instruction instruction in instructions)
        {
            if (instruction is D3D10Instruction call
                && InterfaceCallDestination(call) is RegisterKey result)
            {
                if (!tempRegisters.TryAdd(result, 0xF))
                {
                    tempRegisters[result] |= 0xF;
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

    // udiv and sincos write two registers, and HasDestination does not describe them
    // - it answers for the one destination the rest of the model assumes. Either of
    // the two may be null where the shader wants only one of the results.
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
            // Addr and Texture are one register type number. In a vertex shader it is
            // the address register, which needs declaring like a temp; in a pixel
            // shader it is a texture coordinate input, which does not.
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
        // `mova` writes the address register like a temp, so it is collected as
        // one and needs a name and a declaration for the same reason.
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
        // Not where the register it is written to is declared half: the declaration
        // asks for the same precision, once, for every write of it.
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
        // An output the signature declares at partial precision is a half already -
        // `half4 o;` - so a half cast on the way into it says the same thing twice.
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
                // fxc writes an unconditional break as a comparison it already knows
                // the answer to - `break_ne c11.y, -c11.y` over a defined 1 - because
                // a break inside an if is what shader model 3 has instead of one.
                // Written out literally that reads `if (1 != -1) break;`, which is
                // every loop in the profile that breaks. The ast writer evaluates the
                // comparison and says `break;`, and this is the same answer.
                if (IsSettledComparison(instruction))
                {
                    WriteLine("break;");
                    break;
                }
                WriteLine("if ({0} {2} {1}) break;", GetSourceName(instruction, 0), GetSourceName(instruction, 1), instruction.Comparison.ToHlslString());
                break;
            case Opcode.Cmp:
                // TODO: should be per-component
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"({GetSourceName(instruction, 1)} >= 0) ? {GetSourceName(instruction, 2)} : {GetSourceName(instruction, 3)}");
                break;
            case Opcode.Crs:
                // A cross product reads three components to write any one of them, and
                // writes the three the destination mask covers, the way nrm does.
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"cross({GetSourceName(instruction, 1, 3)}, {GetSourceName(instruction, 2, 3)})");
                break;
            case Opcode.DP2Add:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    // The two vectors are two components wide whatever is written; the
                    // addend is a scalar and must not take that width too.
                    $"dot({GetSourceName(instruction, 1)}, {GetSourceName(instruction, 2)}) + {GetSourceName(instruction, 3, 1)}");
                break;
            case Opcode.Dp3:
            case Opcode.Dp4:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"dot({GetSourceName(instruction, 1)}, {GetSourceName(instruction, 2)})");
                break;
            case Opcode.Dst:
                {
                    // The fixed-function distance step: x is 1, y is the product of
                    // the two y's, z and w come one from each source. Both sources
                    // are read at fixed components, so both are named at full width.
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
                    // n.l, n.h and the specular power come from x, y and w of the one
                    // source, so it is named at full width and indexed three times.
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
                    // The trip count is a uniform, declared rather than defined by a defi.
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
                // lrp is dst = src2 + src0 * (src1 - src2), so src2 is what it
                // blends from and src1 what it blends to. Naming them in bytecode
                // order blended the wrong way round.
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
                    // A dot product per row against consecutive registers, the matrix
                    // operand naming the first of them. The first number in the
                    // mnemonic is how wide the vector and each row are, the second how
                    // many rows there are - and the rows are the components written,
                    // so neither operand is as wide as the destination mask.
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
                // As with loop, the trip count is a uniform when there is no defi.
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
                // The two registers after the value are scratch space the hardware
                // wanted and nothing reads, so they are no part of the result.
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
            // What reaches here is the part of the shader model 1 to 3 instruction set
            // that fxc does not emit: the matrix multiplies m4x4 to m3x2, crs, sgn, dst,
            // cnd, expp, logp, bem, the ps_1_x texture addressing instructions, and
            // call, callnz and setp. The expression writer handles several of them
            // already, so a shader assembled by hand - which is where they come from,
            // there being no HLSL that compiles to them - decompiles as an expression
            // and throws here. Filling them in is not the difficulty; having nothing to
            // check it against is. This fxc refuses ps_1_x outright (X3539) and never
            // chooses the others, so no fixture can be made for any of them the way
            // every other one here was.
            default:
                throw new NotImplementedException(instruction.Opcode.ToString());
        }
    }

    // ftoi, ftou, itof and utof each say how to read their source and what to make
    // of it, and a plain move says neither. A temp register is declared with one
    // integer type, so utof on a register holding 0xFFFFFFFF read it as -1 where
    // the original reads 4294967295. The reading is a reinterpretation of the same
    // bits; only the outer cast converts. Both are as wide as what is written,
    // since a bare (float) over two components is X3014.
    /// <summary>
    /// firstbit_hi and firstbit_shi, which HLSL has one function for and counts
    /// the other way round: firstbithigh gives the position of the bit from the
    /// bottom, and both instructions give the number of bits above it. So the
    /// statement is the subtraction, and the case each instruction has no answer
    /// for has to be said as well - a word with no bit to find answers 0xffffffff
    /// rather than the 32 the subtraction would give. The signed form has two such
    /// words, 0 and -1, being the two whose bits are all their own sign.
    ///
    /// The cast is what picks the instruction: firstbithigh over a uint is
    /// firstbit_hi and over an int it is firstbit_shi, and a temp is declared int.
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

    private void WriteConversion(D3D10Instruction instruction, string readAs, string convertTo)
    {
        int length = instruction.GetDestinationMaskLength();
        string size = length == 1 ? "" : length.ToString();
        string source = GetOperandName(instruction, 1);
        // An int register holds the integer already, and `(int)` on top of that would
        // convert it to itself. `(uint)` is not the same nothing: it says which half
        // of the range the bits mean, and utof of a negative int is not itof of it.
        string reinterpreted = readAs == null
                || (readAs == "int" && GetSourceStorage(instruction, 1) == ComponentStorage.Integer)
            ? source
            : $"({readAs}{size}){source}";
        WriteResult(instruction, "{0} = {1};",
            GetOperandName(instruction, 0), $"({convertTo}{size}){reinterpreted}");
    }

    /// <summary>
    /// dtof and dtoi, and ftod and itod the other way about: as wide as the values
    /// converted rather than as the components they take, one side of the conversion
    /// filling two components for each value where the other fills one.
    /// </summary>
    private void WriteDoubleConversion(D3D10Instruction instruction, string convertTo)
    {
        int length = instruction.ValueCount;
        string size = length == 1 ? "" : length.ToString(_culture);
        WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
            $"({convertTo}{size}){GetOperandName(instruction, 1)}");
    }

    // A comparison writes all ones for true and all zeroes for false, which is what
    // lets an and with it act as a mask. A mask is bits, so the register holding it
    // is an int one, where -1 and 0 are the numbers wanted; where the mask goes into
    // a float register instead, nothing integer ever touches it and its readers only
    // test it, for which -1 and 0 do as well.
    private void WriteComparison(D3D10Instruction instruction, string op, bool unsigned = false)
    {
        string left = GetOperandName(instruction, 1);
        string right = GetOperandName(instruction, 2);
        // A temp register is declared signed, and `<` between signed values orders
        // them by sign first: ult held 0x80000000 to be below 1, and written without
        // this the comparison said the opposite. One side is enough - the other is
        // promoted to match whichever is unsigned - so a register already declared
        // one needs nothing, and the cast goes on the side that is not an immediate,
        // where it says something.
        if (unsigned)
        {
            int length = instruction.GetDestinationMaskLength();
            string size = length == 1 ? "" : length.ToString();
            // Whichever side is not unsigned already, rather than one of them where
            // neither is. One was enough to make the comparison unsigned - HLSL
            // promotes the other to match - but a signed operand beside an unsigned
            // one is a mismatch it resolves by assuming unsigned and warns about, and
            // this writer says what it means everywhere else. An immediate is left
            // alone: a literal takes the type it is compared against, and `(uint)0 <=
            // x` is a tautology fxc warns about in its own right.
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

    // Whether the instruction reads its integer operands as unsigned. Only the
    // opcodes that come in both forms need asking: the unsigned one of a pair says
    // what its operands are, where an opcode with no signed counterpart says nothing
    // about them either way.
    private static bool ReadsUnsignedImmediates(D3D10Instruction instruction)
    {
        return instruction.Opcode is D3D10Opcode.ULT or D3D10Opcode.UGE
            or D3D10Opcode.UMax or D3D10Opcode.UMin or D3D10Opcode.UShr;
    }

    /// <summary>
    /// Whether the operand is a register HLSL already reads as unsigned - a uint
    /// constant buffer variable, an input the signature types one, a thread id. A
    /// temp is never one: the writer declares them all int.
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

    // and, or and xor work on the bits, whatever the register holding them is
    // declared as. A register holding what a float comparison wrote is declared
    // float, and HLSL will not apply a bitwise operator to one - X3082 - so where
    // that happens the bits are named directly. asint and asfloat reinterpret
    // rather than convert, which is what makes this the same operation and not a
    // rounding of it: the mask anded here is 0x3f800000, the bits of 1.0f, and
    // converting it to an integer would make it 1065353216.
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
            // What a bitwise operator ands is the immediate's bits, whatever the
            // register holding the other operand is declared as, and they are
            // written as the integer they are. `asint(1)` is not them: a whole
            // float prints without a decimal point, and HLSL reads that as the
            // integer 1.
            return ImmediateBits(instruction, operandIndex);
        }
        string name = GetOperandName(instruction, operandIndex);
        // An integer is one already; a float has to be reinterpreted, HLSL not
        // applying a bitwise operator to one at all (X3082).
        return HoldsInteger(instruction, operandIndex) ? name : AsInt(name);
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
        // An output register is typed by its signature, not by the storage analysis -
        // which files every output as numeric, since a float register holds whatever
        // it is given. A bitwise write to an int output is an integer write: it wants
        // `o.z = x ^ y`, and `o.z = asfloat(x ^ y)` converts the bits it was writing
        // into the number they happen to make.
        if (instruction.GetOperandType(destinationIndex.Value) == OperandType.Output)
        {
            return !IsFloatOutput(instruction, destinationIndex.Value);
        }
        return GetDestinationStorage(instruction, destinationIndex.Value) == ComponentStorage.Integer;
    }

    // A D3D10 result can be clamped to [0, 1] by a bit on the instruction rather
    // than by anything in the operands, and dropping it is not visible in the
    // output - dp3_sat feeding a log became log2 of a negative number. Every case
    // here assigns, in the one shape `{0} = <expression>;`, so the clamp goes on
    // around the expression.
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
        // An output register the signature types as a float holds an integer result
        // as its bits: a shader packing two half floats ends with `iadd o0.x, ...`,
        // and that is a float's bit pattern and not the number the bits add up to.
        if (GetProducedKind(instruction) == ValueKind.Integer
            && IsFloatOutput(instruction, destinationIndex)
            && GetDoubleRegisterOperandName(instruction, destinationIndex) == null)
        {
            format = Wrapped(format, "asfloat");
        }
        // And a dword a buffer handed back, into a register declared float: those
        // bits are a float's. A buffer holds whatever was stored in it, and fxc
        // converts with an instruction of its own - a utof behind the load - so a
        // load with no conversion behind it whose register a float instruction
        // reads is a load of a float's bit pattern. Converted instead, it came back
        // as the number those bits happen to make, which for a texel below 2^-126
        // is zero.
        if (IsLoadedBitsResult(instruction, destinationIndex))
        {
            format = Wrapped(format, "asfloat");
        }
        // A float result into an int register keeps its bits the same way, which is
        // how that register holds a float at all. Around the saturate above and not
        // inside it: what is clamped to [0, 1] is the number, not its bits. Never
        // onto a double: the shadow variable holding one is declared double, so the
        // register's own storage says nothing about it, and `asint` of a double is
        // not a conversion the language has.
        if (IsReinterpretedResult(instruction, destinationIndex)
            && GetDoubleRegisterOperandName(instruction, destinationIndex) == null)
        {
            format = Wrapped(format, "asint");
        }
        WriteLine(format, args);
    }

    /// <summary>
    /// A result's expression wrapped in a call: `{0} = x;` becomes `{0} = f(x);`.
    ///
    /// Every format reaching WriteResult assigns one expression to the destination,
    /// which is what lets a wrapper go round the middle of it rather than round the
    /// whole statement - `asint({0} = x;)` is not a thing. Four rules wrap one in
    /// turn, each of them with its own copy of the two slices that take the
    /// expression out and put it back; this is that, once, with the shape they all
    /// assumed said out loud.
    ///
    /// Said out loud and checked, because a format of another shape would otherwise
    /// be sliced into nonsense and written out as if nothing were wrong. It can only
    /// be reached by a caller that is already wrong, so nothing in the corpus does.
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
    /// The value a store or an interlocked operation writes, where a float register
    /// is holding it and the memory it goes into is integer: the float's bits, which
    /// is what the instruction writes. Null where that is not the case, which leaves
    /// the operand as the caller would have named it.
    ///
    /// The mirror of IsLoadedBitsResult on the way out. fxc converts with an
    /// instruction of its own - an ftou in front of the store - so a store with none
    /// writes the bits that are in the register, and converting them wrote the whole
    /// number nearest the float instead. Which for a depth in [0, 1] is nought or
    /// one, so every tile came out with bounds of zero.
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
        // Signed where the element says so, which keeps fxc's X3203 away: it
        // resolves a mismatch by assuming unsigned and warns, and the shader this
        // came from had no such warning.
        return instruction.Opcode == D3D10Opcode.StoreStructured
            && _registers.IsUnsignedStructuredMember(
                instruction.GetParamRegisterKey(0),
                // The member this store reaches: the element's offset plus where in
                // the element the mask starts, the way IsDoubleStructuredComponent
                // counts. The element's own base answers for a scalar element and
                // for the first member of a struct, and for nothing further in.
                instruction.GetParamInt(2, 0)
                    + FirstComponent(DestinationMask(instruction)) * 4) == false
            ? AsInt(name)
            : $"asuint({name})";
    }

    /// <summary>
    /// Whether what the register components an operand reads were last written with
    /// is a float, rather than an integer that a float register happens to be
    /// holding. Asked of the writer and not of the register, for the reason
    /// IsReadAsFloat is asked of the readers: fxc fills one register with a texel
    /// address here and a colour there, and only what put the value in says which it
    /// is. A mov says nothing - its own kind is unknown - so a value moved in is left
    /// to convert, which is what it did before.
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
        // Every component, so that a store of a register holding a float and an
        // integer at once is left alone rather than reinterpreted whole.
        return components.Length != 0
            && components.All(component => IsWrittenAsFloat(instruction, register, component));
    }

    /// <summary>
    /// Whether the last instruction to write a register component wrote a float.
    /// Read backwards in program order, the way IsReadAsFloat reads forwards.
    /// </summary>
    private bool IsWrittenAsFloat(D3D10Instruction before, RegisterKey register, int component)
    {
        return _programOrder.LastWriterOf(before, register, component) is D3D10Instruction previous
            && GetProducedKind(previous) == ValueKind.Float;
    }

    /// <summary>
    /// Whether an operand is an index HLSL takes unsigned. A subscript of a buffer
    /// or of a typed view takes a uint, and a byte address buffer's offset is one
    /// too; a texture's Load takes a signed int3 and is not one of these. Each of
    /// them carries its index as its second operand, loads and stores alike.
    /// </summary>
    private static bool IsUnsignedIndexOperand(D3D10Instruction instruction, int operandIndex)
    {
        return operandIndex == 1
            && instruction.Opcode is D3D10Opcode.LdUAVTyped or D3D10Opcode.StoreUAVTyped
                or D3D10Opcode.LdStructured or D3D10Opcode.StoreStructured
                or D3D10Opcode.LdRaw or D3D10Opcode.StoreRaw;
    }

    /// <summary>
    /// Whether the register components an operand reads were last written with a
    /// signed integer, in a register declared float.
    ///
    /// Only an ftoi says so. Every other way a number reaches a float register
    /// leaves a float there, or an unsigned integer an ftou made, or whatever a mov
    /// carried in from somewhere this does not follow - and calling any of those
    /// signed would be a claim about them rather than a reading of them.
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
        // Every component, so that an address built half one way and half the other
        // is left as it was rather than called signed whole.
        return components.Length != 0
            && components.All(component => _programOrder
                .LastWriterOf(instruction, register, component)?.Opcode == D3D10Opcode.Ftoi);
    }

    /// <summary>
    /// Whether what a memory load writes into a register declared float is the bits
    /// of a float rather than a number to convert. The mirror of
    /// IntegerOperandAnalysis.HoldsFloatBits, which is the same question about an
    /// int register: there a float's bits are what the register holds all along,
    /// and here they arrive as a dword out of a buffer.
    ///
    /// Only a load of memory, not any integer-producing instruction. An ftou puts
    /// an integer in a register because the shader asked for the number; a buffer
    /// hands back whatever was stored in it, and nothing but a conversion
    /// instruction would have made that a number.
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
        // Asked of this load's own readers, not of the register. Whether a float
        // instruction ever touches the component says nothing about the dword this
        // load put there: `ld r0, r0.x, t1` over a Buffer&lt;uint&gt; of indices
        // writes a register a mul wrote before it and a mul reads after it, and
        // what it loaded is an index the next ld addresses a texel with. What says
        // is what reads the value before anything writes over it.
        //
        // Every component the load writes, so that one filling a register with an
        // address and a colour at once is left alone rather than reinterpreted
        // whole.
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
    /// Whether the first thing to read a register component reads it as a float,
    /// before anything writes over it. Read in program order, the way the shadow
    /// walk above counts: a component holds what it holds until something else
    /// writes it, and what the readers make of it is what the value is.
    ///
    /// False where nothing reads it, which leaves a load whose value goes nowhere
    /// as the plain load it looks like.
    /// </summary>
    private bool IsReadAsFloat(D3D10Instruction after, RegisterKey register, int component)
    {
        return _programOrder.FirstReaderOf(after, register, component)
                is (D3D10Instruction reader, int operand)
            && GetConsumedKind(reader, operand) == ValueKind.Float;
    }

    // Whether the destination is an output register the signature does not type as
    // an integer - so a float, whose bits an integer instruction writes.
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
    /// Set while one instruction is written as more than one statement, to the
    /// components this statement is for. One instruction can write two things that
    /// have to be said separately - `mov o1.xyz, r0.xyw` over a TEXCOORD at o1.xy
    /// and a TEXCOORD1 at o1.z is two assignments - and naming it once wrote the
    /// whole of it into the first field and left the second unwritten.
    /// </summary>
    private int? _destinationMaskOverride;

    private static int FirstComponent(int mask)
    {
        return Enumerable.Range(0, 4).First(c => (mask & (1 << c)) != 0);
    }

    /// <summary>
    /// The components an instruction writes, grouped by the output declaration each
    /// belongs to, or null where they are all one declaration's - which is every
    /// instruction but the few that write a packed output across both.
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
    /// A structured load or store that takes a double and another member of the
    /// element at once - `ld_structured r1.xyz, i, l(8), t0.xyzx` over a struct whose
    /// double is followed by a uint - split into the masks each member wants. One
    /// statement cannot be both: the register is the shadow variable for the double's
    /// two components and itself for the rest, and a run of each is one statement.
    /// Null where the element holds no double or the whole mask is one kind.
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
            // The upper half of a double belongs with the value it is half of, not
            // with whatever follows: a run breaks where the kind changes, and the
            // top half of a number is the same kind as its bottom half.
            if (isDouble != last)
            {
                masks.Add(0);
                last = isDouble;
            }
            masks[^1] |= 1 << component;
        }
        return masks.Count > 1 ? [.. masks] : null;
    }

    private void WriteInstruction(D3D10Instruction instruction)
    {
        int[] split = SplitPackedOutputMasks(instruction)
            ?? SplitDoubleElementMasks(instruction);
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
                WriteResult(instruction, "{0} = {1} + {2};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.IShl:
                WriteResult(instruction, "{0} = {1} << {2};", GetOperandName(instruction, 0), ShiftOperand(instruction, 1), ShiftOperand(instruction, 2));
                break;
            // Shifts and masks, since HLSL has no intrinsic for a bit field. An
            // unsigned extract has to shift an unsigned value or it drags the sign
            // bit down; a signed one puts the field at the top of a signed value
            // and shifts it back.
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
                    // A temp register is declared signed, and >> on a signed value
                    // shifts in the sign bit rather than zeroes.
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
                // discard_nz tests the bits, like if_nz; clip tests the sign of a
                // float, and a comparison mask is NaN as a float, which is never
                // negative - `clip(mask)` never discarded anything.
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
            // Control flow was skipped entirely, so a DXBC loop with a guarded break
            // printed as `while (true)` with nothing to end it.
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
            // The float comparisons, beside ieq and ine further down: both pairs
            // write the same mask and are told apart by what they read it from.
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
            // A buffer that keeps a counter without being an append or a consume one
            // hands the slot back for the shader to subscript with, which is a value
            // and an ordinary statement.
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
                    // A consume buffer has one method and it both takes the slot and
                    // reads the element, so the call goes here and the loads that
                    // follow read the variable it filled - the same shape
                    // GetDimensions takes, and for the same reason.
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
                    // A component of the element that call read, picked by the byte
                    // offset the load asks for - and by the member it sits in, where
                    // the element is a struct: `consumed0.x` off an Item is X3018.
                    int component = instruction.GetParamInt(2, 0) / 4;
                    string member = _registers.NameStructuredMembers(
                        instruction.GetParamRegisterKey(3), consumedElement, 0, [component]);
                    WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
                        member ?? $"{consumedElement}.{"xyzw"[component]}");
                    break;
                }
            case D3D10Opcode.LdStructured:
                {
                    // The byte offset picks a row where the element is a matrix. A
                    // struct element would pick a member, which is not read yet.
                    string element = $"{GetOperandName(instruction, 3)}[{GetOperandName(instruction, 1)}]";
                    RegisterKey buffer = instruction.GetParamRegisterKey(3);
                    int offset = instruction.GetParamInt(2, 0);
                    // Which components of the element are read, in the order the
                    // destination mask writes them.
                    byte[] elementSwizzle = instruction.GetSourceSwizzleComponents(3);
                    int writeMask = DestinationMask(instruction);
                    // One entry per value the load reads, which for a double is one
                    // per pair of components: counted per component, a struct of two
                    // doubles named four members and built a float4 of them. Asked of
                    // the member this statement reads rather than of the element, so
                    // that a float beside a double in one is still one value to one
                    // component.
                    int loadPerElement = IsDoubleStructuredComponent(instruction, 3) ? 2 : 1;
                    List<int> loaded = [.. Enumerable.Range(0, 4)
                        .Where(c => (writeMask & (1 << c)) != 0)];
                    List<int> read = [];
                    for (int value = 0; value < loaded.Count; value += loadPerElement)
                    {
                        read.Add(elementSwizzle[loaded[value]]);
                    }
                    // Groupshared memory wider than one register is a struct of them,
                    // and the byte offset picks which. Named as the element itself, a
                    // read of one member was a whole struct assigned to a register and
                    // a swizzle taken off a struct besides.
                    if (_registers.ThreadGroupSharedMember(buffer, offset)
                        is var (sharedName, sharedComponents, sharedBase))
                    {
                        string sharedSwizzle = sharedComponents == 1
                            ? ""
                            : Rebased(
                                instruction.GetSourceSwizzleNameForMask(3,
                                    instruction.GetDestinationWriteMask()),
                                sharedBase, sharedComponents);
                        // Rebased only drops a swizzle that names the whole of what it
                        // is on when it has rebasing to do; a member read whole from
                        // its first component keeps it otherwise, and `.m0.xyzw` off a
                        // float4 says nothing.
                        if (sharedSwizzle == "." + "xyzw"[..sharedComponents])
                        {
                            sharedSwizzle = "";
                        }
                        WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
                            $"{element}.{sharedName}{sharedSwizzle}");
                        break;
                    }
                    string readElement = _registers.NameStructuredMembers(buffer, element, offset, read)
                        ?? _registers.ApplyStructuredElementRow(buffer, element, offset);
                    // Where the element is neither a matrix (whose offset picks a row)
                    // nor a struct (whose offset picks a member), it is a scalar or a
                    // vector and the byte offset selects a component of it: reading the
                    // .w of a uint4 is the load at offset twelve, whole components past
                    // the operand's own. A double takes two of those components for
                    // each value it is, so a pair of them names one - the load at
                    // offset eight of a double2 is its `.y`, not its `.zw`.
                    if (readElement == element)
                    {
                        var names = new List<char>();
                        // One name per entry, the list already holding one entry per
                        // value: the component it names is divided down to the value
                        // it is half of.
                        foreach (int component in read)
                        {
                            names.Add("xyzw"[(component + offset / 4) % 4 / loadPerElement]);
                        }
                        // Left off where it names the element's values in order, which
                        // is the element itself. Written only for the offset before
                        // this, a load fxc reordered - `t0.zwxy` over a double2, whose
                        // first value is the element's second - came out as the whole
                        // element with its values the wrong way round.
                        string picked = new string([.. names]);
                        int width = _registers.GetRegisterMaskedLength(buffer) / loadPerElement;
                        if (picked != "xyzw"[..Math.Min(width, 4)])
                        {
                            readElement += "." + picked;
                        }
                    }
                    // Two components a double instruction goes on to read as one
                    // number are its two words, and the asdouble that joins them
                    // belongs here, where they enter: the destination names the
                    // shadow, which holds the double itself, and the arithmetic over
                    // it reads that. Written as a load of two floats instead, the
                    // components were converted to the numbers their bits spell and
                    // the arithmetic read a shadow nothing had assigned to.
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
                    // Groupshared memory is declared as an array, so a raw read of it
                    // is a subscript rather than a Load, and each dword it names is
                    // an element of its own.
                    int readMask = instruction.GetWriteMask(0);
                    if (BitOperations.PopCount((uint)readMask) != 1)
                    {
                        throw new NotImplementedException(
                            "a raw groupshared load of more than one dword");
                    }
                    byte[] sharedSwizzle = instruction.GetSourceSwizzleComponents(2);
                    int dword = sharedSwizzle[BitOperations.TrailingZeroCount((uint)readMask)];
                    WriteResult(instruction, "{0} = {1}[{2}];", GetOperandName(instruction, 0),
                        GetOperandName(instruction, 2), GroupSharedElement(instruction, 1, dword));
                    break;
                }
            case D3D10Opcode.LdRaw:
                {
                    // As many dwords as the highest component asked for, at the byte
                    // offset: Load, Load2, Load3 or Load4, then the resource's swizzle.
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
            // msad4 takes the two words of an eight byte source; the instruction takes
            // the four windows fxc slid along them, and HLSL has no intrinsic for that
            // shape. Both words are in the windows: the first window is the low one
            // outright, and the fourth is that word's top byte with three bytes of the
            // high one above it, so shifting the fourth down by a byte gives the high
            // word's bottom three. Its fourth byte lies past the end of the last window
            // and is no part of the answer, which is what makes this the same call.
            case D3D10Opcode.MSAD:
                {
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
                // A mov that is a double entering the shader as its two words: the
                // destination names the shadow the number is kept in, and the two
                // 32-bit values the source names are joined into it. Written as a
                // plain move the shadow was assigned nothing at all, and fxc said so
                // - X4000, used without having been completely initialized.
                if (_joinedDoubleMovs.Contains(instruction)
                    && SplitTwoComponentName(GetOperandName(instruction, 1))
                        is (string lowWord, string highWord))
                {
                    WriteResult(instruction, "{0} = asdouble({1}, {2});",
                        GetOperandName(instruction, 0), lowWord, highWord);
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
            // The double precision arithmetic. Each one is the operator or the
            // intrinsic it is named after, over the shadow variables the doubles
            // are kept in; only the width of a conversion is its own question.
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
                // The instruction HLSL has for this, rather than the division and
                // the root it is made of: fxc folds neither back, so `1 / sqrt(x)`
                // is a div and a sqrt where the shader had one rsq.
                WriteResult(instruction, "{0} = rsqrt({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.Sample:
                WriteResult(instruction, "{0} = {2}.Sample({3}, {1}{4}){5};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3), GetSampleOffset(instruction), GetResourceSwizzle(instruction));
                break;
            // The resource swizzle chooses the level rather than naming a channel,
            // and the level is a float, so it is read here and not written after.
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
                    // The swizzle on the sampler picks the channel gathered. Red is what
                    // Gather returns; the other three have methods of their own.
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
                    // The offset is an operand rather than part of the mnemonic, so
                    // the texture and the sampler are one further along and the
                    // offset goes where an immediate one would - last, after the
                    // compared value where there is one.
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
                    // The same four texels as a gather, each compared against the
                    // value after the coordinate. The channel is the sampler's
                    // swizzle again, and only red has a method without a suffix.
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
                // A texture buffer has no Load: each element of it is a variable of
                // the block, and the address says which.
                if (TextureBufferVariable(instruction) is string bufferVariable)
                {
                    WriteResult(instruction, "{0} = {1}{2};", GetOperandName(instruction, 0),
                        bufferVariable, GetResourceSwizzle(instruction));
                    break;
                }
                WriteResult(instruction, "{0} = {2}.Load({1}{3}){4};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetSampleOffset(instruction), GetResourceSwizzle(instruction));
                break;
            case D3D10Opcode.LDMS:
                // One sample of a texel: Texture2DMS.Load(int2, sampleIndex).
                WriteResult(instruction, "{0} = {2}.Load({1}, {3}{4}){5};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3), GetSampleOffset(instruction), GetResourceSwizzle(instruction));
                break;
            case D3D10Opcode.ResInfo:
                WriteResourceInfo(instruction);
                break;
            case D3D10Opcode.BufInfo:
                WriteBufferInfo(instruction);
                break;
            // Where the attribute is evaluated is an integer: a sample index, or an
            // offset in sixteenths of a pixel.
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
            // The centroid of the covered part of the pixel, which takes no operand
            // to say so.
            case D3D10Opcode.EvalCentroid:
                WriteResult(instruction, "{0} = EvaluateAttributeCentroid({1});",
                    GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.SampleInfo:
                WriteSampleInfo(instruction);
                break;
            // The resource names itself and the sample index is an integer; the
            // render target has no name, and a function of its own instead.
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
                    // Quotient and remainder, either of which may be null. Unsigned,
                    // and it has to say so: `asint(x) % 5` is a signed modulus, which
                    // fxc lowers with a sign test of its own, and where the shader had
                    // already taken the sign off - the abs and the mask of a signed
                    // modulus lowered once - fxc folded the two together and lost it.
                    string dividend = AsUint(instruction, 2);
                    string divisor = AsUint(instruction, 3);
                    bool wantsQuotient = instruction.GetOperandType(0) != OperandType.Null;
                    bool wantsRemainder = instruction.GetOperandType(1) != OperandType.Null;
                    // One instruction writing two results is two statements here, and
                    // the quotient is assigned before the remainder reads its
                    // operands. Where the quotient lands in a register an operand is
                    // read from - a signed division lowers to
                    // `udiv r1.xyzw, r2.xyzw, r1.xyzw, r2.xyzw`, the dividend and the
                    // quotient being the one register - the remainder divided the
                    // quotient by the divisor. Naming an operand that the quotient is
                    // about to overwrite keeps the value the instruction read.
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
                        // fxc folds `asfloat(u % 5)` - an unsigned remainder by an
                        // immediate, reinterpreted - to zero, a bug of its own; the
                        // remainder spelled out as `u - u / 5 * 5` it compiles.
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
                    // The sine is assigned before the cosine reads the angle, and fxc
                    // gives the sine the register the angle is in.
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
                    // The same subscript a store uses, read rather than written - and
                    // the components of the texel it asks for, which the operand on the
                    // view says and the destination mask counts. A uint4 buffer read
                    // into one register component is `tiles[i].x`; without the swizzle
                    // the whole texel was assigned to the one component, which is the
                    // implicit truncation fxc warns about with X3206.
                    string texel = instruction.GetSourceSwizzleNameForMask(
                        2, instruction.GetDestinationWriteMask());
                    // A texel read whole keeps no swizzle: `.xyzw` off a float4 says
                    // nothing, the way the structured load says it below.
                    WriteResult(instruction, "{0} = {1}[{2}]{3};", GetOperandName(instruction, 0),
                        GetOperandName(instruction, 2), GetOperandName(instruction, 1),
                        texel == ".xyzw" ? "" : texel);
                    break;
                }
            case D3D10Opcode.StoreUAVTyped:
                // A texel, addressed by as many coordinates as the resource has
                // dimensions - which is what GetSourceLength answers for it.
                WriteLine("{0}[{1}] = {2};", GetOperandName(instruction, 0),
                    GetOperandName(instruction, 1),
                    StoredBits(instruction, 2) ?? GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.StoreStructured:
                {
                    // A struct element is written a member at a time: one store of
                    // sixteen bytes over a struct of a float3 and a float is both
                    // of them, and writing it as one assignment kept only the last.
                    RegisterKey buffer = instruction.GetParamRegisterKey(0);
                    string element = $"{GetOperandName(instruction, 0)}[{GetOperandName(instruction, 1)}]";
                    int writeMask = DestinationMask(instruction);
                    // One entry per value the store writes, the way the load reads
                    // them: a double takes two components and is one member.
                    int storePerElement = IsDoubleStructuredComponent(instruction, 0) ? 2 : 1;
                    List<int> masked = [.. Enumerable.Range(0, 4).Where(c => (writeMask & (1 << c)) != 0)];
                    List<int> written = [];
                    for (int value = 0; value < masked.Count; value += storePerElement)
                    {
                        written.Add(masked[value]);
                    }
                    IList<(string Name, int[] Values)> runs = _registers.FindStructuredMemberRuns(
                        buffer, element, instruction.GetParamInt(2, 0), written);
                    if (runs == null)
                    {
                        // And the same for a store: written as the element, a store of
                        // one member assigned the whole of it, and the second member's
                        // store overwrote the first's.
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
                            StoredBits(instruction, 3) ?? GetOperandName(instruction, 3));
                        break;
                    }
                    // One member, or the double's two words would be written once
                    // per run. A double is one member wherever it is split: the
                    // uint2 it goes into is one, and a pair of uints beside each
                    // other in a struct is not something fxc writes with one store.
                    if (runs.Count == 1 && WriteDoubleBitsStore(instruction, runs[0].Name))
                    {
                        break;
                    }
                    byte[] valueSwizzle = instruction.GetSourceSwizzleComponents(3);
                    foreach ((string name, int[] values) in runs)
                    {
                        // Which part of the value operand goes here, counted in the
                        // values it holds: the second double of a shadow is its .y,
                        // where the register's components make it the .z. A shadow
                        // holding one double has no halves to pick from, and `d0.x`
                        // off a double is not HLSL.
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
                    // A byte address buffer takes the interlocked operations as its
                    // own methods over a byte offset; everything else takes them as
                    // free functions over the element. The imm_ forms keep what the
                    // resource held, in a register that goes in front of the rest -
                    // so every operand is one further along - and HLSL takes it as
                    // the out parameter after the value.
                    string method = instruction.Opcode.AtomicMethodName();
                    bool keepsOriginal = instruction.Opcode.IsImmediateAtomic();
                    int first = keepsOriginal ? 1 : 0;
                    string resource = GetOperandName(instruction, first);
                    string address = GetOperandName(instruction, first + 1);
                    bool hasCompare = instruction.Opcode
                        is D3D10Opcode.AtomicCmpStore or D3D10Opcode.ImmAtomicCmpExch;
                    int valueIndex = first + (hasCompare ? 3 : 2);
                    // A float whose bits the memory holds is reinterpreted rather
                    // than cast: a depth written in as asuint is minimised as the
                    // integer those bits make, and a cast made it the whole number
                    // nearest the depth, which for every tile was nought or one.
                    string bits = StoredBits(instruction, valueIndex);
                    string value = bits ?? GetOperandName(instruction, valueIndex);
                    // InterlockedMax and InterlockedMin come signed and unsigned,
                    // chosen by the type of the value, and a register here is an int
                    // or a float: the unsigned one has to say so at the value or the
                    // signed one is what is written back. An asuint has said it
                    // already.
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
                    string target = $"{resource}[{address}]";
                    // An element that is a struct takes the operation on one of its
                    // members, and which one is the byte offset beside the element
                    // index in the address. Read off it where that address is an
                    // immediate; where fxc works it into a register the register is
                    // only as knowable as its writes, and one write of an immediate
                    // is knowable enough. Named by neither, the call goes out with
                    // no member at all and fxc answers X3013.
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
                    // And the same for a store: one element, one assignment.
                    int storeMask = instruction.GetWriteMask(0);
                    if (BitOperations.PopCount((uint)storeMask) != 1)
                    {
                        throw new NotImplementedException(
                            "a raw groupshared store of more than one dword");
                    }
                    WriteLine("{0}[{1}] = {2};", GetOperandName(instruction, 0),
                        GroupSharedElement(instruction, 1,
                            BitOperations.TrailingZeroCount((uint)storeMask)),
                        GetOperandName(instruction, 2));
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
            // The run it names is written as one array parameter, above.
            case D3D10Opcode.DclIndexRange:
            // How many times the phase runs. The phase is unrolled into a copy of
            // its body per run before it reaches here, so the count is already
            // spent and each copy writes the factor its own run wrote.
            case D3D10Opcode.DclHSForkPhaseInstanceCount:
            case D3D10Opcode.DclHSJoinPhaseInstanceCount:
            case D3D10Opcode.DclGSInputPrimitive:
            case D3D10Opcode.DclGSMaxOutputVertexCount:
            // The [instance] attribute, written with the signature.
            case D3D10Opcode.DclGSInstanceCount:
            case D3D10Opcode.DclInput:
            case D3D10Opcode.DclInputPS:
            case D3D10Opcode.DclInputPSSgv:
            case D3D10Opcode.DclInputPSSiv:
            case D3D10Opcode.DclInputSgv:
            case D3D10Opcode.DclOutputSgv:
            case D3D10Opcode.DclInputSiv:
            case D3D10Opcode.DclOutput:
            // Written as the [domain] attribute and the patch's size, with the
            // signature rather than in the body.
            case D3D10Opcode.DclInputControlPointCount:
            case D3D10Opcode.DclOutputControlPointCount:
            case D3D10Opcode.DclTessDomain:
            // And so are the rest of a hull shader's tessellation declarations,
            // beside the phase markers it is split along, the bound on a factor
            // among them.
            case D3D10Opcode.DclTessPartitioning:
            case D3D10Opcode.DclTessOutputPrimitive:
            case D3D10Opcode.DclHSMaxTessFactor:
            case D3D10Opcode.HsDecls:
            case D3D10Opcode.HsControlPointPhase:
            case D3D10Opcode.HsForkPhase:
            case D3D10Opcode.HsJoinPhase:
            case D3D10Opcode.DclGSOutputPrimitiveTopology:
            // The stream the emits go to, which is written as the parameter the
            // shader already declares.
            case D3D10Opcode.DclStream:
            case D3D10Opcode.DclOutputSiv:
            case D3D10Opcode.DclResource:
            case D3D10Opcode.DclUnorderedAccessViewTyped:
            case D3D10Opcode.DclResourceStructured:
            case D3D10Opcode.DclResourceRaw:
            case D3D10Opcode.DclUnorderedAccessViewRaw:
            // The rows are written out with the other declarations.
            case D3D10Opcode.CustomData:
            case D3D10Opcode.DclSampler:
            case D3D10Opcode.DclTemps:
            // Declared with the temps, above.
            case D3D10Opcode.DclIndexableTemp:
            case D3D10Opcode.DclThreadGroup:
            case D3D10Opcode.DclUnorderedAccessViewStructured:
            // Declared at file scope, before main.
            case D3D10Opcode.DclThreadGroupSharedMemoryRaw:
            case D3D10Opcode.DclThreadGroupSharedMemoryStructured:
            // Declared at file scope, before main, in their own words.
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
                // The last ret is the method returning, which is written after the
                // body. Anywhere else it is an early return, and dropping it lost the
                // branch that took it. With dynamic linkage main ends at the first
                // label - the rets beyond it close the bodies, and each body writes
                // its own return.
                if (!ReferenceEquals(instruction, LastMainInstruction()))
                {
                    WriteLine(_registers.MethodOutputRegisters.Count != 0 && _shader.Type != ShaderType.Geometry
                        ? $"return {_registers.OutputVariableName};"
                        : "return;");
                }
                break;
            default:
                // Anything not listed above writes nothing, which silently drops
                // the instruction - sample_l left an empty method body.
                throw new NotImplementedException(instruction.Opcode.ToString());
        }
    }

    // sincos writes the cosine to x and the sine to y, and a shader wanting only
    // one of them masks the other off. HLSL names them the other way round -
    // sincos(value, sine, cosine) - and writing the one destination as both
    // arguments put whichever came second in both components.
    private void WriteSinCos(D3D9Instruction instruction)
    {
        const int CosineComponent = 1;
        const int SineComponent = 2;
        int writeMask = instruction.GetDestinationWriteMask();
        string register = GetDestinationRegisterName(instruction);
        // One component: sincos replicates a scalar, and naming the source as
        // wide as the destination asked HLSL for float2 outputs.
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

    // The register alone, for the one caller that names its own components.
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
        // Addr and Texture are the same register type number, told apart by the
        // kind of shader. Not by the opcode: shader model 1 has no mova and writes
        // the address register with a plain mov, which was being named as an input.
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

    // rowOffset reads a register after the one the operand names, which is
    // how a matrix product names its rows: the operand is the first of them.
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
                // A relatively addressed def is the base of an array, not the value
                // being read - substituting its literal drops the subscript.
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

                // A struct member has a name of its own, and the register it owns
                // swizzles the member rather than the struct. A scalar is named whole:
                // HLSL broadcasts it where a wider value is wanted. A register indexed
                // at run time names its member below, off the declaration rather than
                // off a register number the index is not part of.
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

                // An array of structs picked at run time: the address register counts
                // registers across the array, so the element is that over the registers
                // one takes, and the constant beside it says which register of the
                // element - which member - is read. Indexed as though each register
                // were an element, `g_Lights[a0 + 2].x` reads a struct where a float4
                // was wanted, two elements past the one the shader asked for.
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

                // An array of matrices takes two subscripts, and its register offset
                // counts registers across the whole array: the element is that offset
                // over the registers an element takes - its columns, stored by column
                // - and the register within the element is what is left.
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

                // The registers are the matrix's rows where it was packed row major
                // and its columns otherwise, and the constant table says which.
                if (decl.TypeInfo.ParameterClass is ParameterClass.MatrixRows
                    or ParameterClass.MatrixColumns)
                {
                    sourceRegisterName = RegisterState.MatrixRegisterName(
                        decl.TypeInfo, decl.Name, registerKey.Number - decl.RegisterIndex);
                }
                else if (decl.TypeInfo.NumElements > 1)
                {
                    // Each element of `float4 m[4]` has a register of its own, and every
                    // one of them reads as m without the subscript. When the subscript
                    // is the address register the offset folds into it instead.
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

    // x0[3], x0[r0.x], x0[r0.x + 1]: the array, then the element it names.
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

    /// <param name="member">The struct member named, when the operand reads an
    /// element of an array of structs picked at run time; the swizzle then wants
    /// rebasing onto it, and a scalar wants none.</param>
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
            // One index rather than a buffer and an element, and no declaration to be
            // named from. The immediate beside the index is the row the read starts
            // at, which names which of the arrays in the buffer it reads; dropped, the
            // read was of the wrong rows.
            return $"{_registers.ImmediateConstantBufferName((int)operandIndices[0].Immediate)}"
                + $"[{index}]";
        }
        // A run of output registers declared as one array by dcl_indexrange, written
        // through the index. The only thing that indexes one is an instanced hull
        // phase, writing the factor of the run it is - so the register is the run's
        // number past the first of the range, and the phase was unrolled into a copy
        // per run, which is what that number is. Named the ordinary way from there,
        // so the factor reads as the field it is rather than as a subscripted one.
        if (operandType == OperandType.Output && instruction.ForkInstance is int instance)
        {
            var writtenKey = new D3D10RegisterKey(
                OperandType.Output, (int)operandIndices[0].Immediate + instance);
            int outputMask = instruction.GetWriteMask(operandIndex);
            return _registers.GetRegisterName(
                new RegisterComponentKey(writtenKey, FirstComponent(outputMask)));
        }
        // A control point of a patch is read the way a vertex of a primitive is -
        // which control point, then which register of it - so it is named the same
        // way. Left out, it fell through to the constant buffer case at the end and
        // was named off whatever declaration sat at that register: a hull shader
        // looping over its control points asked for `dot(f[r0.y].xyz, f[r0.y].xyz)`,
        // where f is the cbuffer float beside it, which does not compile.
        if (operandType is OperandType.Input or OperandType.InputControlPoint
            or OperandType.OutputControlPoint)
        {
            // A run of input registers declared as one array by dcl_indexrange has
            // one index, and the immediate beside it is the first register of the
            // run; a geometry shader has two, and the second names the register
            // while the first is the vertex.
            if (operandIndices.Length == 1)
            {
                var arrayKey = new D3D10RegisterKey(
                    OperandType.Input, (int)operandIndices[0].Immediate);
                return $"{_registers.GetRegisterName(new RegisterComponentKey(arrayKey, 0))}[{index}]";
            }
            // The vertex is the dynamic part; the second index names the register.
            // Which member of the vertex is a question about the component read, not
            // about the register: two semantics can share one, and naming the
            // register's own declaration read the first of them whichever was meant.
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
            // An array of structs: the element is the index over the registers one
            // takes, and the constant part of the offset picks the member.
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
            // An array of matrices takes two subscripts. The index counts registers,
            // which is rows across the whole array, so the element is that over the row
            // count - `dot(position, instances[r0.x])` asks for a dot with a matrix.
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

    // aL counts the enclosing loop; a0 is the address register, one index per
    // component - `c0[a0.y]` is not `c0[a0.x]`, and reading the one for the other
    // indexes the array in the wrong place.
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

            // The subscripted register need not be the first of the array, whether the
            // array is a run of defs or a declared one: `floats[i + 2]` reads c2[a0.x]
            // when floats starts at c0.
            var registerKey = instruction.GetParamRegisterKey(srcIndex);
            int elementOffset = 0;
            if (_registers.FindConstantArray(registerKey) is ConstantArray literals)
            {
                elementOffset = registerKey.Number - literals.BaseRegisterIndex;
            }
            // A matrix array is not this case: its register offset counts rows, and
            // the row has already been taken off it by the time we get here.
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

        // Which entries of the swizzle are read depends on which components are
        // written, not on how many: `mad oC0.zw, v0.z, c0.xyxy, c0.xyyx` reads
        // entries 2 and 3, so the second addend is (y, x) and not (x, y). Taking
        // the first two made the mad add 1 to z and 0 to w, both wrong. This is
        // how GetSourceSwizzleName has always selected them. A caller naming a
        // length instead means the low components, as it does there.
        // A dot product reads as many components as it is wide, whatever it
        // writes: `dp3 r0.w, v0, c0` against a def'd c0 read c0's w alone, the one
        // component the destination names, and came out `dot(color.xyz, 0)`. This
        // is the width GetSourceSwizzleName gives the same operands.
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
                // Only defb gives a bool register a literal value, and nothing in the
                // codebase emits one. Otherwise the register names a uniform, which
                // the caller resolves through the constant table.
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

                    // Negate, Abs and AbsAndNegate over an integer constant would be
                    // `-c`, `abs(c)` and `-abs(c)` component by component. They were
                    // written as loops around a throw, which is three unreachable
                    // statements and the same behaviour; no shader in the corpus has
                    // reached here with one.
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
    /// The element a raw groupshared access names, from the byte address it carries
    /// and which dword of that access this is. Folded where the address is the
    /// literal it almost always is - a component of a groupshared variable sits at a
    /// constant offset.
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

        // Which run of an instanced phase this is, which the unrolling settled: each
        // copy of the body reads it as the number of its own run, and there is
        // nothing in HLSL to name it by.
        if (registerKey.OperandType == OperandType.InputForkInstanceID)
        {
            return (instruction.ForkInstance ?? 0).ToString(_culture);
        }

        if (registerKey.OperandType == OperandType.Immediate64)
        {
            // The operand carries two doubles, and the instruction reads one per
            // value it computes: the pair of slots its swizzle names for each of
            // them. One value reading one double is a scalar; two reading different
            // ones are a vector, the same as the register beside it they multiply.
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
            // The 32 bits are typed by the instruction using them. Reading an integer
            // as a float printed its bit pattern - l(4) came out as 0.000000. A mov
            // uses them for nothing itself, so its immediate is typed by whatever
            // reads the register afterwards: -1.0f moved into a register that is a
            // loop counter elsewhere is still -1.0f.
            bool isInteger = IsIntegerImmediate(instruction);
            // A float immediate moved into an int register is stored as its bits,
            // which is how that register holds floats - but the asint goes on in
            // Moved or WriteResult, so nothing is needed here.
            bool asBits = false;
            if (registerKey.ImmediateSingle.Length == 1)
            {
                string scalar = isInteger
                    ? instruction.GetParamInt(operandIndex, 0).ToString(_culture)
                    : ConstantFormatter.Format(registerKey.ImmediateSingle[0]);
                return asBits ? AsInt(scalar) : scalar;
            }
            byte[] swizzle = instruction.GetSourceSwizzleComponents(operandIndex);
            // Which entries of the swizzle are read depends on which components are
            // written, as it does for a defined constant: `mov o0.zw, l(0, 0, 1, 2)`
            // writes 1 and 2, not the first two.
            int[] components = GetSourceComponents(instruction, operandIndex, swizzle);
            // Typed the same way as the single component above: a vector immediate
            // feeding an integer instruction is a vector of integers, and
            // `& float2(0.000000, 0.000000)` is the mask 255 read as float bits.
            string[] constant = [.. components
                .Select(s => isInteger
                    ? instruction.GetParamInt(operandIndex, s).ToString(_culture)
                    : ConstantFormatter.Format(registerKey.ImmediateSingle[s]))];
            // Unsigned where the instruction reads it so. A bare literal takes the
            // type of what it is compared against, which is why one of those is left
            // alone, but a vector immediate is written out as a constructor and an
            // int4 of it really is signed: `(uint)i >= int4(1, 2, 4, 8)` is the
            // mismatch fxc resolves by assuming unsigned and warns about.
            string immediateType = isInteger
                ? (ReadsUnsignedImmediates(instruction) ? "uint" : "int")
                : "float";
            // One component is a scalar, not a one wide vector. `int1(0)` is legal
            // HLSL almost everywhere and not as a subscript, where fxc wants a
            // scalar: `bounds[int1(0)]` is an invalid index.
            string vector = components.Length == 1
                ? constant[0]
                : $"{immediateType}{components.Length}(" + string.Join(", ", constant) + ")";
            return asBits ? AsInt(vector) : vector;
        }

        D3D10OperandModifier modifier = instruction.GetOperandModifier(operandIndex);
        // A double in a temp register is held in the shadow variable for it rather
        // than in the two float components it really occupies.
        if (GetDoubleRegisterOperandName(instruction, operandIndex) is string doubleRegister)
        {
            return ApplyModifier(modifier, doubleRegister);
        }
        if (GetDoubleConstantOperandName(instruction, operandIndex) is string doubleConstant)
        {
            return ApplyModifier(modifier, doubleConstant);
        }
        // A relatively addressed operand decodes to a meaningless register number,
        // so its element has to be named from the index register instead.
        D3D10OperandTokenCollection.OperandIndex[] operandIndices =
            instruction.OperandTokens.GetOperandIndices(operandIndex);
        string registerName;
        bool isPackedScalar = false;
        StructMemberAccess dynamicMember = null;
        if (registerKey.OperandType == OperandType.IndexableTemp)
        {
            registerName = GetIndexableTempOperandName(instruction, operandIndex, operandIndices);
        }
        else if (operandIndices.Any(i => i.IsRelative))
        {
            registerName = GetDynamicOperandName(instruction, operandIndex, operandIndices, out dynamicMember);
            isPackedScalar = dynamicMember != null && dynamicMember.Width == 1;
            // A vertex of a patch or a primitive, read at an index: the member it
            // names is as wide as its own declaration, and a member one component
            // wide has no swizzle to pick from.
            if (registerKey.OperandType is OperandType.Input or OperandType.InputControlPoint
                    or OperandType.OutputControlPoint
                && operandIndices.Length > 1
                && !operandIndices[1].IsRelative)
            {
                // Keyed by the register within the vertex, which is the immediate
                // beside the index. The number decoded from a relatively addressed
                // operand is meaningless - the parser says so where it models the
                // element rather than reading it - so a vertex read at a computed
                // index, `v[r1.z][0]`, found no declaration for the key as it stood
                // and the width fell through to a throw. A geometry shader that
                // loops over its vertices rather than being unrolled is that shape,
                // and it stopped the instruction writer outright.
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
            // Several variables can share one register, so which is being read
            // depends on the component: cb0[0].y is `n`, not `mode.y`.
            byte component = instruction.GetSourceSwizzleComponents(operandIndex)[0];
            registerName = _registers.GetRegisterName(new RegisterComponentKey(registerKey, component));
            // Width of what was actually named, which for a struct is the member and
            // not the struct: `o.a.s` is a float, however wide struct2 is.
            isPackedScalar = _registers.GetRegisterMaskedLength(
                new RegisterComponentKey(registerKey, component)) == 1;
            // One operand can read across two variables packed into one register.
            // `imax r0.yz, cb0[0].xy, -cb0[0].xy` reads a and b, and naming it from the
            // first component alone read a twice.
            if (_destinationMaskOverride == null
                && TryNamePackedComponents(instruction, operandIndex, registerKey, out string packed))
            {
                return ApplyModifier(modifier, packed);
            }
        }
        // A control point read back from the control point phase is packed the
        // way that phase's outputs were.
        else if (registerKey.OperandType is OperandType.Input or OperandType.OutputControlPoint)
        {
            // fxc can pack two differently named inputs into one register - TEXCOORD0
            // at v2.xy and TEXCOORD1 at v2.z - so which is meant depends on which
            // component is actually read, the same as a packed constant.
            byte component = instruction.GetSourceSwizzleComponents(operandIndex)[0];
            var inputComponentKey = new RegisterComponentKey(registerKey, component);
            registerName = _registers.GetRegisterName(inputComponentKey);
            // As wide as the declaration covering the component, packed or not: a
            // lone `float thickness` at v0.w is one component too, and reading it
            // as `thickness.w` off a float is not HLSL.
            isPackedScalar = _registers.GetRegisterMaskedLength(inputComponentKey) == 1;
            // One read can cross from one packed variable into another: a coordinate
            // built as `float3(uv2, uv1.x)` reads v0.zw and v0.x. Named from the first
            // component and rebased onto it, the component that belongs to the
            // earlier-packed variable runs below its base. A constructor over each
            // variable, each swizzled as its own, says what is read.
            if (_destinationMaskOverride == null
                && TryNamePackedComponents(instruction, operandIndex, registerKey, out string packedInputs))
            {
                return ApplyModifier(modifier, packedInputs);
            }
        }
        else if (registerKey.IsOutput && instruction.IsDestinationOperand(operandIndex))
        {
            // fxc packs two outputs into one register as readily as two inputs, so
            // which field is written depends on the component. Named by the register
            // alone, both writes went to the first of them.
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
        // A buffer is named, never masked or swizzled: what it reads or writes is
        // said by the method - Load2, Store - and the register's own mask.
        if ((instruction.Opcode == D3D10Opcode.LdStructured && operandIndex == 3)
            || (instruction.Opcode == D3D10Opcode.LdRaw && operandIndex == 2)
            || (instruction.Opcode == D3D10Opcode.StoreRaw && operandIndex == 0)
            // A structured store carries a mask saying which members of the element
            // it writes, which the members named after the subscript already say:
            // `store_structured u0.xy, ...` came out as `dst.xy[i].c`, a subscript
            // the buffer has not got.
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
            // The mask within the field written, not within the register holding it.
            // A patch constant packed above a tessellation factor - CENTRE at o0.yzw -
            // was named by the register's own components: the write of its z said
            // `o.centre.z`, which is its y, and the write of its w said `o.centre`,
            // assigning a float to a float3. Shifted down by where the field starts,
            // a write of the whole of it says nothing, as it should.
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
            // A texture or a sampler is named, never swizzled. The swizzle a sampling
            // instruction carries on its resource operand selects components of the
            // result, and on the sampler of a gather it selects the channel; neither
            // belongs on the object itself. A writable view carries one the same way
            // - `resinfo r0.xy, l(0), u0.xyzw` - and `tex.xy.GetDimensions(...)` is
            // not a subscript the object has.
            if (registerKey.OperandType == OperandType.Resource
                || registerKey.OperandType == OperandType.Sampler
                || registerKey.OperandType == OperandType.UnorderedAccessView)
            {
                return ApplyModifier(modifier, registerName);
            }

            // One statement of a split instruction reads only the components it
            // writes, so the source swizzle follows that statement's mask.
            if (_destinationMaskOverride != null && GetSourceLength(instruction, operandIndex) == null)
            {
                return ApplyModifier(modifier, registerName
                    + instruction.GetSourceSwizzleNameForMask(operandIndex, _destinationMaskOverride.Value));
            }

            int? maskedLength = GetSourceLength(instruction, operandIndex);
            // A scalar variable sharing a register has no component of its own to
            // name once the variable itself is named.
            if (dynamicMember != null)
            {
                // A vector member of a run-time element: as wide as the member, and
                // rebased onto it.
                maskedLength = dynamicMember.Width;
            }
            writeMaskName = isPackedScalar
                ? ""
                : Rebased(
                    instruction.GetSourceSwizzleName(operandIndex, maskedLength),
                    dynamicMember?.ComponentBase ?? GetConstantComponentBase(instruction, operandIndex),
                    maskedLength);
            // An integer instruction whose result is reinterpreted has to compute in
            // integers. A register holding the number 3 as a float, added to 4 as a
            // float, gives the float 7 - and the bits of 7.0f are not 7, so the
            // asfloat that stores the sum and the asint that reads it back give
            // something else entirely. `(i + 1) & 63` over a loop counter is the
            // ordinary way to meet this.
            if (GetConsumedKind(instruction, operandIndex) == ValueKind.Integer
                && GetSourceStorage(instruction, operandIndex) == ComponentStorage.Numeric
                // A constant the declaration types as an integer is one already.
                && !IsIntegerConstant(instruction, operandIndex)
                && instruction.HasDestination
                // Only where the result is an integer's, which is what makes the
                // float the operand holds the wrong thing to compute with. A load's
                // address is read as an integer too and has nothing to do with what
                // the texel it fetches is kept as.
                && GetProducedKind(instruction) == ValueKind.Integer
                && GetDestinationStorage(instruction, instruction.GetDestinationParamIndex() ?? 0)
                    == ComponentStorage.Integer)
            {
                // As wide as the operand where it has a width of its own - a load's
                // address is three wide under a one wide load - and as the
                // destination otherwise. A scalar cast over a vector takes the first
                // component and spreads it.
                int length = maskedLength ?? instruction.GetDestinationMaskLength();
                string size = length == 1 ? "" : length.ToString();
                return ApplyModifier(modifier,
                    $"(int{size}){string.Format("{0}{1}", registerName, writeMaskName)}");
            }
            // And an index HLSL takes unsigned, out of a float register holding a
            // signed integer. The subscript of a buffer or of a typed view takes a
            // uint, so the implicit conversion clamps a negative to zero where the
            // bytecode's ftoi kept it negative, and a read past the edge of a
            // texture became a read of texel zero. Cast to int it wraps instead,
            // which is what the instruction did with the dwords it was handed.
            if (GetConsumedKind(instruction, operandIndex) == ValueKind.Integer
                && GetSourceStorage(instruction, operandIndex) == ComponentStorage.Numeric
                && !IsIntegerConstant(instruction, operandIndex)
                && IsUnsignedIndexOperand(instruction, operandIndex)
                && HoldsSignedInteger(instruction, operandIndex))
            {
                // As wide as the index: a typed view's coordinate counts by the
                // dimensions of the resource, and an element index is one.
                int indexLength = maskedLength ?? 1;
                string indexSize = indexLength == 1 ? "" : indexLength.ToString();
                return ApplyModifier(modifier,
                    $"(int{indexSize}){string.Format("{0}{1}", registerName, writeMaskName)}");
            }
            // An int register holding bits: what a float instruction reading it
            // wants is the float those bits are, and not the number they make. A
            // register of loop counters is declared int too and is the other way
            // about, which is why the two are told apart component by component.
            // And a uniform or an attribute declared an integer: fxc writes a utof or
            // an itof wherever the source meant the number, so a float instruction
            // reading one as it stands is reading a float's bits.
            if (GetConsumedKind(instruction, operandIndex) == ValueKind.Float
                && (HoldsFloatBits(instruction, operandIndex)
                    || IsIntegerUniformOrInput(instruction, operandIndex)))
            {
                return ApplyModifier(modifier,
                    $"asfloat({string.Format("{0}{1}", registerName, writeMaskName)})");
            }
            // A negation or an absolute on bits a mov carries is a float operation:
            // `movc r2, r3, r2, -r2` over a fraction negates the number, and
            // negating the bits of one gives a different number entirely. Read as a
            // float, negated, and put back as bits. An integer instruction's
            // negation is the integer's, whatever the component otherwise holds.
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

    // An operand a shift reads. A register that holds an integer as a float holds
    // the number wanted and not its bits, so it is converted rather than
    // reinterpreted - and it has to be, since HLSL will not shift a float at all
    // (X3082). Bits storage is read with asint already, an int register and a
    // constant declared int need nothing, and the cast is as wide as what is
    // written, since a bare (int) over two components is X3014.
    private string ShiftOperand(D3D10Instruction instruction, int operandIndex)
    {
        string name = GetOperandName(instruction, operandIndex);
        if (instruction.GetOperandType(operandIndex) == OperandType.Immediate32
            || HoldsInteger(instruction, operandIndex))
        {
            return name;
        }
        // A temp holding an integer as a float holds the number wanted, and is
        // converted; a register the declaration says is a float holds a float, and
        // what a shift wants of one is its bits.
        if (!instruction.GetParamRegisterKey(operandIndex).IsTempRegister)
        {
            return AsInt(name);
        }
        int length = instruction.GetDestinationMaskLength();
        string size = length == 1 ? "" : length.ToString();
        return $"(int{size}){name}";
    }

    // An operand read as an unsigned integer: bits reinterpreted as such, an int
    // register cast - as wide as the destination, since a bare (uint) over two
    // components is X3014 - and an immediate as it is.
    // How many operands have been named to keep them from a result written over them.
    private int _keptOperandCount;

    /// <summary>
    /// Whether an operand reads the register given - the one a result is about to be
    /// written into. An immediate reads no register and can never be overwritten.
    /// </summary>
    private static bool IsSameRegister(D3D10Instruction instruction, int operandIndex, RegisterKey registerKey)
    {
        return instruction.GetOperandType(operandIndex) != OperandType.Immediate32
            && instruction.GetParamRegisterKey(operandIndex).Equals(registerKey);
    }

    /// <summary>
    /// Names an operand that a result is about to be written over, so that the
    /// statements after it read the value the instruction read. One instruction
    /// answering two results is two statements here, and the first can land in a
    /// register the second still reads: `udiv r1.xyzw, r2.xyzw, r1.xyzw, r2.xyzw`
    /// and `sincos r0.x, r1.x, r0.x` are both written that way by fxc. Left alone,
    /// the remainder divided the quotient and the cosine answered cos(sin(x)).
    /// Returns the expression unchanged where there is nothing to keep.
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
    /// Whether a loop has to be marked [loop] for fxc to compile it at all: a
    /// sample with implicit derivatives inside a loop fxc cannot count is an error
    /// unless the loop is marked, so the source it came from was marked. Every
    /// other loop is left to fxc. A loop fxc can count is one whose first break
    /// tests a comparison against an immediate - `ige r, r, l(4)` then breakc.
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

    // if_nz branches when the register is non-zero, if_z when it is zero.
    // A test against zero is of the bits, not the number: if_nz and movc take
    // -0.0f and a comparison mask as set, and `x != 0` as a float takes neither.
    // Only a register declared int is tested as it is.
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

    // What the component an operand reads first is declared to hold.
    private DeclaredType DeclaredTypeOf(D3D10Instruction instruction, int operandIndex)
    {
        return _registers.GetDeclaredType(new RegisterComponentKey(
            instruction.GetParamRegisterKey(operandIndex),
            instruction.GetSourceSwizzleComponents(operandIndex)[0]));
    }

    // A constant declared bool, int or uint holds its integer as one.
    private bool IsIntegerConstant(D3D10Instruction instruction, int operandIndex)
    {
        return instruction.GetOperandType(operandIndex) == OperandType.ConstantBuffer
            && DeclaredTypeOf(instruction, operandIndex) is DeclaredType.Bool or DeclaredType.Int or DeclaredType.Uint;
    }

    // Not a bool, which fxc reads into float arithmetic through a select of 1.0.
    // And only in arithmetic: a gather's offset and GetSamplePosition's index count
    // as float operands to GetConsumedKind, and are the integers they are declared.
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
    /// Whether an operand holds an integer rather than a float. A temp by its
    /// register's declaration, which the register rule decides; anything else by
    /// the declaration that names it - a constant buffer variable by its own type,
    /// an input by the component type its signature gives. The register rule is no
    /// use for those: a float input read by nothing but a shift looks like an
    /// integer to it, and the shift is reading its bits.
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

    // The resource operand carries a swizzle saying which channel of the texture
    // each component of the result comes from: `sample r0, v0.xyxx, t2.yzxw, s0`
    // puts green in x. Leaving it out reads the wrong channels and compiles.
    // A comparison sample returns one value, so there is nothing to permute.
    private int _resourceInfoCount;

    // GetDimensions fills out parameters rather than returning a value, so the
    // result goes through a variable of its own and is then moved to the register,
    // which is what carries the resource operand's swizzle and the write mask. The
    // two-argument overload is width and height at mip 0; anything more takes the
    // full form, which for a 2D texture is the mip in and width, height and mip
    // count out - z, the depth or array size, is what a 2D texture has not got.
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
        // A multisampled texture has one overload and it is the three argument
        // one: no mip level, and the sample count where the mip count would be.
        // Asking it for two came out as X3013, no matching intrinsic.
        if (IsMultisampled(instruction, 2))
        {
            WriteMultisampledDimensions(instruction, 2, type, dimensions);
        }
        else
        {
            // The mip form is the one with the level in and the mip count out; the
            // resinfo's own mip operand says whether the shader asked at a level,
            // and reading the count says it asked for the mip form whichever way it
            // was asked at. What else is out, and in which components, is the
            // resource's shape: a 1D reports width alone and the count in w; its
            // array the element count in y; an array of 2D, a cube array and a 3D
            // the element count or the depth in z. Writing them all as the 2D
            // overload asked for intrinsics those shapes do not have.
            ResourceDimension? dimension = ResourceDimensionOf(instruction, 2);
            bool hasDepth = dimension is ResourceDimension.Texture2DArray
                or ResourceDimension.TextureCubeArray or ResourceDimension.Texture3D;
            bool is1D = dimension == ResourceDimension.Texture1D;
            // Anything read past the components the shape's no-mip form reports -
            // the mip count always, the depth of a shape that has none - has no
            // no-mip spelling either, so that too is left in the mip form, where a
            // 4-wide variable has the component the swizzle reads either way.
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
    /// bufinfo, which reports how many elements a buffer holds - or how many bytes
    /// a byte address one does. HLSL asks through GetDimensions like the rest, in
    /// the overload the buffer takes: a structured one reports its stride beside
    /// the count, which fxc knows as a constant and does not ask for, so the second
    /// out parameter is written into and never read.
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
    /// sampleinfo, which reports how many samples a resource has. HLSL asks for it
    /// through GetDimensions like the size, so this writes a call of its own and
    /// leaves the width and height it also returns unread - one instruction more
    /// than the shader had, which is what writing instruction by instruction costs
    /// wherever one HLSL call becomes two.
    /// </summary>
    private void WriteSampleInfo(D3D10Instruction instruction)
    {
        // How many samples the render target has, which is asked for on its own and
        // not read out of a GetDimensions: there is no resource to call one on.
        if (instruction.GetOperandType(1) == OperandType.Rasterizer)
        {
            WriteResult(instruction, "{0} = GetRenderTargetSampleCount();",
                GetOperandName(instruction, 0));
            return;
        }
        string type = instruction.ResInfoReturnType == D3D10ResInfoReturnType.Uint ? "uint" : "float";
        string dimensions = $"dimensions{_resourceInfoCount++}";
        WriteMultisampledDimensions(instruction, 1, type, dimensions);
        // The count is the last out parameter, which an array pushes to w by
        // reporting how many elements it has in front of it.
        string component = IsMultisampledArray(instruction, 1) ? "w" : "z";
        WriteResult(instruction, "{0} = {1}.{2};", GetOperandName(instruction, 0), dimensions, component);
    }

    /// <summary>
    /// The GetDimensions a multisampled texture takes: width, height and the sample
    /// count, with the element count between them for an array, and no mip level in
    /// either - there are no mips to ask about.
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

    /// <summary>Whether the resource an operand names is a multisampled texture,
    /// which changes which GetDimensions overloads there are.</summary>
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

    private static string GetResourceSwizzle(D3D10Instruction instruction)
    {
        if (instruction.Opcode is D3D10Opcode.SampleC or D3D10Opcode.SampleCLZ
            or D3D10Opcode.Gather4PoC)
        {
            return "";
        }
        // Of the operand that names the texture, which a register offset moves.
        return instruction.GetSourceSwizzleName(TextureOperandIndex(instruction));
    }

    // An offset shifts the sample by whole texels. Leaving it out compiles and
    // reads the wrong ones, so it belongs in the call.
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
    /// The variable an ld reads, where the resource it reads is a texture buffer
    /// and the address is a constant: each 16 byte element of the block is one of
    /// its variables. Null for anything else.
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
            // Read at an index the shader works out rather than a constant one, so
            // no one variable of the block is what is being read: the array it is an
            // element of is named instead, subscripted by the address. Falling
            // through to a Load on the buffer's own name does not compile - a
            // tbuffer is a block like a cbuffer, and the block is not an identifier,
            // only what it declares.
            D3D10ConstantDeclaration array = declarations
                .FirstOrDefault(d => d.TypeInfo.NumElements > 1);
            if (array == null)
            {
                return null;
            }
            // The address counts registers within the buffer, so an array whose
            // element takes more than one register is indexed by however many.
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

    // A texture buffer is declared with a dcl_resource_buffer and read with an ld
    // like any other buffer, so it is addressed the same way and belongs in both
    // questions above; only what the load is written as differs.
    private ResourceDefinition LoadedResource(D3D10Instruction instruction)
    {
        return _registers.ResourceDefinitions
            .Where(d => d.ShaderInputType is D3DShaderInputType.Texture
                or D3DShaderInputType.TBuffer)
            .FirstOrDefault(d => d.BindPoint
                == instruction.GetParamRegisterNumber(TextureOperandIndex(instruction)));
    }

    /// <summary>
    /// Which operand names the texture. The third for everything that samples or
    /// loads, and the fourth for a gather that takes its offset from a register -
    /// that offset is an operand of its own, and pushes the rest along.
    /// </summary>
    private static int TextureOperandIndex(D3D10Instruction instruction)
    {
        return instruction.Opcode is D3D10Opcode.Gather4Po or D3D10Opcode.Gather4PoC ? 3 : 2;
    }

    // Which declared variable a component of a packed register belongs to, told apart
    // by where that variable starts: two packed constants, or two packed inputs, never
    // share a starting component.
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
    /// A constructor over the variables one operand reads, when fxc packed several
    /// into one register - two scalars, or TEXCOORD0 at v0.xy and TEXCOORD1 at v0.zw -
    /// and the read crosses from one into another. Naming the operand from its first
    /// component and rebasing the whole swizzle onto that variable runs a component
    /// belonging to a differently-packed variable off its end: below the base when it
    /// sits in the earlier-packed variable, or past it when a later one is read
    /// through an earlier variable's name. Each run of consecutive components sharing a
    /// variable is named and swizzled as that variable.
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

        // Maximal runs of consecutive components that share one variable.
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
            // All one variable, which a single rebased swizzle already names.
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
            // The whole variable read in order needs no swizzle on it.
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
    /// A constant buffer packs several variables into one register, so a float3
    /// declared after a float sits at .yzw. The swizzle naming it is rebased onto the
    /// variable, whose own components start at x - eyePos.yzw asks for components it
    /// has not got.
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

    // Which entries of a source swizzle an instruction reads: those the destination
    // mask selects, unless the operand has a width of its own - a texture coordinate
    // is as wide as the texture whatever the destination mask says.
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

    // The coordinate is as wide as the texture, not as wide as whatever the sample
    // is being written into: a comparison sample writes one component and still
    // reads a two component coordinate. ld addresses a texel of a particular mip,
    // so its address is one wider still - Load on a Texture2D takes an int3.
    private int? GetSourceLength(D3D10Instruction instruction, int operandIndex)
    {
        // A dot product reads both its operands as wide as the product and not as
        // wide as the one component it writes. Sized by the destination, the
        // immediate of `dp3 r0.w, r0.xyzx, l(0.2125, 0.7154, 0.0721, 0)` came out
        // as the one component the w mask selects: float1(0), a luminance of zero.
        // An interlocked operation reads one component from each of its operands. The
        // address is a whole operand rather than the two a store splits it into, so
        // its second component is the byte offset within the element and is not part
        // of the subscript - unless the resource is a typed texture, which is
        // addressed by a coordinate as wide as it has dimensions, the same as the
        // store into it.
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
        // An operand of a double instruction that is not itself doubles - the
        // condition of a dmovc, the float a ftod converts - is as wide as the values
        // the instruction computes. As wide as the destination mask it was read two
        // components for each of them: the condition of a dmovc over one double came
        // out `r0.xx`, which is the same bool twice.
        if (instruction.HasDoubleOperands && !instruction.IsDoubleOperand(operandIndex))
        {
            return instruction.ValueCount;
        }
        // The gradients a sample_d reads are as wide as the texture, the same as the
        // coordinate before them: a 2D gradient is a float2, and written as wide as
        // the destination `SampleGrad(s, c, r0.xyxx, r0.zwzz)` hands it two float4s
        // and truncates both.
        if (instruction.Opcode == D3D10Opcode.SampleD && operandIndex is 4 or 5)
        {
            return GetTextureDimension(instruction);
        }
        // The level a sample_l reads, the bias of a sample_b and the value a
        // comparison sample tests are one number each, whatever the destination mask
        // is: `SampleLevel(s, c, r0.zzz)` is a float3 truncated to the one float it
        // takes, which is not what the shader asked for.
        if (operandIndex == 4 && instruction.Opcode is D3D10Opcode.SampleL
            or D3D10Opcode.SampleB or D3D10Opcode.SampleC or D3D10Opcode.SampleCLZ
            or D3D10Opcode.Gather4C)
        {
            return 1;
        }
        // The offset a gather4_po reads from a register is as wide as the texture,
        // the same as the coordinate before it.
        if (operandIndex == 2
            && instruction.Opcode is D3D10Opcode.Gather4Po or D3D10Opcode.Gather4PoC)
        {
            return GetTextureDimension(instruction);
        }
        // Where an attribute is evaluated is as wide as the question: one sample
        // index, or an offset in x and y. The operand is as wide as the destination
        // mask otherwise, and `EvaluateAttributeSnapped(v, int4(...))` is not an
        // overload of anything.
        if (operandIndex == 2)
        {
            if (instruction.Opcode == D3D10Opcode.EvalSnapped)
            {
                return 2;
            }
            // And which sample GetSamplePosition is asked about is one index,
            // however wide the float2 it answers with.
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

        // A texel of the view being written, addressed by as many coordinates as it
        // has dimensions - and no mip, there being only the one to write to.
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
            // One byte offset.
            return 1;
        }
        // One element and one byte offset within it, however many components the
        // element has. Sized by the destination, `ld_structured r0.xy,
        // vThreadID.x, l(0), t0` subscripted the buffer with `i.xx`, which fxc
        // refuses: an index is a scalar.
        if (instruction.Opcode is D3D10Opcode.LdStructured or D3D10Opcode.StoreStructured
            && operandIndex is 1 or 2)
        {
            return 1;
        }
        return null;
    }

    // Those that take a texture coordinate at operand 1 and the resource at 2. ld is
    // not one: its address carries a mip level rather than a coordinate.
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