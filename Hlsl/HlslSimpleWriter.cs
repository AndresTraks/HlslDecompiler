using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using HlslDecompiler.Hlsl.FlowControl;
using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HlslDecompiler;

public class HlslSimpleWriter : HlslWriter
{
    private int _loopVariableIndex = -1;
    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;
    private readonly IntegerOperandAnalysis _integerOperandAnalysis;

    public HlslSimpleWriter(ShaderModel shader)
        : base(shader)
    {
        _integerOperandAnalysis = new IntegerOperandAnalysis(shader);
    }

    protected override void WriteMethodBody()
    {
        if (_registers.MethodOutputRegisters.Count != 0)
        {
            if (_shader.Type == ShaderType.Geometry)
            {
                WriteLine("GS_OUT {0};", _registers.OutputVariableName);
            }
            else
            {
                WriteLine("{0} {1};", GetMethodReturnType(), _registers.OutputVariableName);
            }
            WriteLine();
        }

        WriteTemporaryVariableDeclarations();
        WriteIndexableTempDeclarations(_integerOperandAnalysis);
        for (int index = 0; index < _shader.Instructions.Count; index++)
        {
            Instruction instruction = _shader.Instructions[index];
            if (instruction is D3D9Instruction d3d9Instruction)
            {
                WriteInstruction(d3d9Instruction);
            }
            else if (instruction is D3D10Instruction d9d10Instruction)
            {
                // The one place a statement is two instructions rather than one.
                // An append buffer has no spelling for half of it: no subscript to
                // store through, and no counter to keep the slot in - so
                // imm_atomic_alloc and the store after it are written as the Append
                // they came from, or neither of them can be written at all.
                if (d9d10Instruction.Opcode == D3D10Opcode.ImmAtomicAlloc
                    && index + 1 < _shader.Instructions.Count
                    && _shader.Instructions[index + 1] is D3D10Instruction store
                    && IsAppendPair(d9d10Instruction, store))
                {
                    WriteLine("{0}.Append({1});", GetOperandName(d9d10Instruction, 1),
                        GetOperandName(store, 3));
                    index++;
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

    // The variable each consume call filled, by the register it left its slot in.
    private readonly Dictionary<RegisterKey, string> _consumedSlots = [];
    private int _consumeCount;

    private bool IsConsumeResource(RegisterKey registerKey)
    {
        return _registers.ResourceDefinitions.Any(d => d.BindPoint == registerKey.Number
            && d.ShaderInputType == D3DShaderInputType.UavConsumeStructured);
    }

    private void WriteTemporaryVariableDeclarations()
    {
        Dictionary<RegisterKey, int> registerWriteMasks = FindTemporaryRegisterAssignments(_shader.Instructions);
        foreach (var register in registerWriteMasks)
        {
            int writeMask = register.Value;
            // An array subscript has to be an integer, and the address register is
            // only ever used as one.
            bool isAddressRegister = register.Key is D3D9RegisterKey addressKey
                && addressKey.Type == RegisterType.Addr;
            // A register holding only integers has to be declared as one: a shift or a
            // bitwise operator will not take a float, however the bits got there.
            string scalarType = isAddressRegister || IsIntegerTempRegister(register.Key, writeMask)
                ? "int"
                : "float";
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
            WriteLine("{0} {1};", writeMaskName, GetTempRegisterName(register.Key));
        }
    }

    // Only when every written component is an integer no float instruction ever
    // touches. fxc reuses a register freely, and one it uses for a loop counter and
    // later for an angle is a float register holding an integer for a while, not
    // an int register holding a float - the float would truncate. What such a
    // component holds, and how the integer instructions get at it, is the
    // register's storage: see IntegerOperandAnalysis.GetStorage.
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
            byte component = instruction.GetSourceSwizzleComponents(operandIndex)[0];
            ConstantDeclaration constant = _registers.FindConstant(
                (D3D10RegisterKey)instruction.GetParamRegisterKey(operandIndex), component);
            return constant?.TypeInfo.ParameterType is ParameterType.Int or ParameterType.Uint or ParameterType.Bool
                ? ComponentStorage.Integer
                : ComponentStorage.Numeric;
        }
        if (instruction.GetOperandType(operandIndex) is not (OperandType.Temp or OperandType.Input
            or OperandType.InputThreadID or OperandType.InputThreadGroupID
            or OperandType.InputThreadIDInGroup or OperandType.InputThreadIDInGroupFlattened
            or OperandType.InputPrimitiveID))
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
        if (instruction.GetOperandType(operandIndex)
            is not (OperandType.Temp or OperandType.OutputCoverageMask))
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
            storage = GetStorage(instruction, operandIndex, component);
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
        // attribute itself is made of.
        if (instruction.Opcode is D3D10Opcode.EvalSampleIndex or D3D10Opcode.EvalSnapped)
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

    private Dictionary<RegisterKey, int> FindTemporaryRegisterAssignments(IList<Instruction> instructions)
    {
        var tempRegisters = new Dictionary<RegisterKey, int>();
        foreach (Instruction instruction in instructions)
        {
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

    private static string GetModifier(D3D9Instruction instruction)
    {
        string source = "{1}";
        ResultModifier resultModifier = instruction.GetDestinationResultModifier();
        if (resultModifier.HasFlag(ResultModifier.Saturate))
        {
            source = $"saturate({source})";
        }
        if (resultModifier.HasFlag(ResultModifier.PartialPrecision))
        {
            string size = instruction.GetDestinationMaskLength().ToString();
            size = size == "1" ? "" : size;
            source = $"half{size}({source})";
        }
        return "{0} = " + source + ";";
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
                WriteLine("if ({0} {2} {1}) break;", GetSourceName(instruction, 0), GetSourceName(instruction, 1), instruction.Comparison.ToHlslString());
                break;
            case Opcode.Cmp:
                // TODO: should be per-component
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"({GetSourceName(instruction, 1)} >= 0) ? {GetSourceName(instruction, 2)} : {GetSourceName(instruction, 3)}");
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
                WriteLine("if ({0}) {{", GetSourceName(instruction, 0));
                indent += "\t";
                break;
            case Opcode.IfC:
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
                if (intRegister == null && LoopSamplesWithGradients(instruction))
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
                if (loopRegister == null && LoopSamplesWithGradients(instruction))
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
        if (unsigned
            && !IsUnsignedOperand(instruction, 1)
            && !IsUnsignedOperand(instruction, 2))
        {
            int length = instruction.GetDestinationMaskLength();
            string size = length == 1 ? "" : length.ToString();
            if (instruction.GetOperandType(1) != OperandType.Immediate32)
            {
                left = $"(uint{size}){left}";
            }
            else
            {
                right = $"(uint{size}){right}";
            }
        }
        WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
            $"({left} {op} {right}) ? -1 : 0");
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
        RegisterKey registerKey = instruction.GetParamRegisterKey(operandIndex);
        ConstantDeclaration constant = _registers.FindConstant(registerKey);
        if (constant != null)
        {
            return constant.TypeInfo.ParameterType == ParameterType.Uint;
        }
        return _registers.RegisterDeclarations.TryGetValue(registerKey, out RegisterDeclaration declaration)
            && declaration.TypeName.Contains("uint");
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
        const string assignment = "{0} = ";
        if (instruction.Saturate)
        {
            string expression = format[assignment.Length..^1];
            format = $"{assignment}saturate({expression});";
        }
        // An output register the signature types as a float holds an integer result
        // as its bits: a shader packing two half floats ends with `iadd o0.x, ...`,
        // and that is a float's bit pattern and not the number the bits add up to.
        if (GetProducedKind(instruction) == ValueKind.Integer
            && IsFloatOutput(instruction, destinationIndex))
        {
            string expression = format[assignment.Length..^1];
            format = $"{assignment}asfloat({expression});";
        }
        // A float result into an int register keeps its bits the same way, which is
        // how that register holds a float at all. Around the saturate above and not
        // inside it: what is clamped to [0, 1] is the number, not its bits.
        if (IsReinterpretedResult(instruction, destinationIndex))
        {
            string expression = format[assignment.Length..^1];
            format = $"{assignment}asint({expression});";
        }
        WriteLine(format, args);
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

    private void WriteInstruction(D3D10Instruction instruction)
    {
        int[] split = SplitPackedOutputMasks(instruction);
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
                WriteLine("stream.RestartStrip();");
                break;
            case D3D10Opcode.EmitThenCut:
            case D3D10Opcode.EmitThenCutStream:
                WriteLine("stream.Append(o);");
                WriteLine("stream.RestartStrip();");
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
                WriteLine("stream.Append(o);");
                break;
            // Control flow was skipped entirely, so a DXBC loop with a guarded break
            // printed as `while (true)` with nothing to end it.
            case D3D10Opcode.If:
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
                    // offset the load asks for.
                    int component = instruction.GetParamInt(2, 0) / 4;
                    WriteResult(instruction, "{0} = {1}.{2};", GetOperandName(instruction, 0),
                        consumedElement, "xyzw"[component]);
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
                    int writeMask = instruction.GetDestinationWriteMask();
                    List<int> read = [.. Enumerable.Range(0, 4)
                        .Where(c => (writeMask & (1 << c)) != 0)
                        .Select(c => (int)elementSwizzle[c])];
                    WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
                        _registers.NameStructuredMembers(buffer, element, offset, read)
                            ?? _registers.ApplyStructuredElementRow(buffer, element, offset));
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
                if (LoopSamplesWithGradients(instruction))
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
            case D3D10Opcode.Mov:
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
            // The resource names itself and the sample index is an integer.
            case D3D10Opcode.SamplePos:
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
                    if (instruction.GetOperandType(0) != OperandType.Null)
                    {
                        WriteResult(instruction, "{0} = {1} / {2};", GetOperandName(instruction, 0), dividend, divisor);
                    }
                    if (instruction.GetOperandType(1) != OperandType.Null)
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
                if (instruction.GetOperandType(0) != OperandType.Null)
                {
                    WriteResult(instruction, "{0} = sin({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 2));
                }
                if (instruction.GetOperandType(1) != OperandType.Null)
                {
                    WriteResult(instruction, "{0} = cos({1});", GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                }
                break;
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
                // The same subscript a store uses, read rather than written.
                WriteResult(instruction, "{0} = {1}[{2}];", GetOperandName(instruction, 0),
                    GetOperandName(instruction, 2), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.StoreUAVTyped:
                // A texel, addressed by as many coordinates as the resource has
                // dimensions - which is what GetSourceLength answers for it.
                WriteLine("{0}[{1}] = {2};", GetOperandName(instruction, 0),
                    GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.StoreStructured:
                {
                    // A struct element is written a member at a time: one store of
                    // sixteen bytes over a struct of a float3 and a float is both
                    // of them, and writing it as one assignment kept only the last.
                    RegisterKey buffer = instruction.GetParamRegisterKey(0);
                    string element = $"{GetOperandName(instruction, 0)}[{GetOperandName(instruction, 1)}]";
                    int writeMask = instruction.GetWriteMask(0);
                    List<int> written = [.. Enumerable.Range(0, 4).Where(c => (writeMask & (1 << c)) != 0)];
                    IList<(string Name, int[] Values)> runs = _registers.FindStructuredMemberRuns(
                        buffer, instruction.GetParamInt(2, 0), written);
                    if (runs == null)
                    {
                        WriteLine("{0} = {1};", element, GetOperandName(instruction, 3));
                        break;
                    }
                    byte[] valueSwizzle = instruction.GetSourceSwizzleComponents(3);
                    foreach ((string name, int[] values) in runs)
                    {
                        string picked = "." + string.Concat(
                            values.Select(v => "xyzw"[valueSwizzle[written[v]]]));
                        WriteLine("{0}.{1} = {2}{3};", element, name,
                            GetOperandName(instruction, 3).Split('.')[0], picked);
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
                    string value = GetOperandName(instruction, first + (hasCompare ? 3 : 2));
                    // InterlockedMax and InterlockedMin come signed and unsigned,
                    // chosen by the type of the value, and a register here is an int
                    // or a float: the unsigned one has to say so at the value or the
                    // signed one is what is written back.
                    if (instruction.Opcode is D3D10Opcode.AtomicUMax or D3D10Opcode.AtomicUMin
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
                    WriteLine("{0}({1}[{2}], {3});", method, resource, address, arguments);
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
            case D3D10Opcode.DclThreadGroupSharedMemoryStructured:
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
                // branch that took it.
                if (!ReferenceEquals(instruction, _shader.Instructions[_shader.Instructions.Count - 1]))
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

    private string GetSourceName(D3D9Instruction instruction, int srcIndex, int? destinationLength = null)
    {
        string sourceRegisterName;

        var registerKey = instruction.GetParamRegisterKey(srcIndex);
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
                    string constantValue = GetSourceConstantValue(instruction, srcIndex, destinationLength);
                    if (constantValue != null)
                    {
                        return constantValue;
                    }
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
                    string matrix = $"transpose({decl.Name}[{element}])";
                    return ApplyModifier(instruction.GetSourceModifier(srcIndex),
                        $"{matrix}[{offset % rows}]"
                            + instruction.GetSourceSwizzleName(srcIndex, destinationLength));
                }

                // The registers are the matrix's rows where it was packed row major
                // and its columns otherwise, and the constant table says which. HLSL
                // subscripts by row whatever the packing, so a column has to be got
                // at through a transpose.
                if (decl.TypeInfo.ParameterClass == ParameterClass.MatrixRows)
                {
                    int row = registerKey.Number - decl.RegisterIndex;
                    sourceRegisterName = $"{decl.Name}[{row}]";
                }
                else if (decl.TypeInfo.ParameterClass == ParameterClass.MatrixColumns)
                {
                    int column = registerKey.Number - decl.RegisterIndex;
                    sourceRegisterName = $"transpose({decl.Name})[{column}]";
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
            // named from.
            return $"icb[{index}]";
        }
        if (operandType == OperandType.Input)
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
            var vertexKey = D3D10RegisterKey.CreateGSInput((int)operandIndices[1].Immediate, 0);
            return $"i[{index}].{_registers.RegisterDeclarations[vertexKey].Name}";
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
            string matrix = $"transpose({declaration.Name}[{index} / {declaration.RegistersPerElement}])";
            return $"{matrix}[{elementOffset}]";
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
        _addressWriteMasks ??= FindTemporaryRegisterAssignments(_shader.Instructions);
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

    private string GetSourceConstantValue(D3D9Instruction instruction, int srcIndex, int? destinationLength = null)
    {
        var registerType = instruction.GetParamRegisterType(srcIndex);
        int registerNumber = instruction.GetParamRegisterNumber(srcIndex);
        byte[] swizzle = instruction.GetSourceSwizzleComponents(srcIndex);

        // Which entries of the swizzle are read depends on which components are
        // written, not on how many: `mad oC0.zw, v0.z, c0.xyxy, c0.xyyx` reads
        // entries 2 and 3, so the second addend is (y, x) and not (x, y). Taking
        // the first two made the mad add 1 to z and 0 to w, both wrong. This is
        // how GetSourceSwizzleName has always selected them. A caller naming a
        // length instead means the low components, as it does there.
        int[] components;
        if (destinationLength != null)
        {
            components = [.. swizzle.Take(destinationLength.Value).Select(c => (int)c)];
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

    private string GetOperandName(D3D10Instruction instruction, int operandIndex)
    {
        D3D10RegisterKey registerKey = instruction.GetParamRegisterKey(operandIndex);

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
            string immediateType = isInteger ? "int" : "float";
            // One component is a scalar, not a one wide vector. `int1(0)` is legal
            // HLSL almost everywhere and not as a subscript, where fxc wants a
            // scalar: `bounds[int1(0)]` is an invalid index.
            string vector = components.Length == 1
                ? constant[0]
                : $"{immediateType}{components.Length}(" + string.Join(", ", constant) + ")";
            return asBits ? AsInt(vector) : vector;
        }

        D3D10OperandModifier modifier = instruction.GetOperandModifier(operandIndex);
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
            // One operand can read two of those scalars at once. `imax r0.yz,
            // cb0[0].xy, -cb0[0].xy` reads a and b, and naming it from the first
            // component alone read a twice.
            if (isPackedScalar
                && TryNamePackedScalars(instruction, operandIndex, registerKey, out string packed))
            {
                return ApplyModifier(modifier, packed);
            }
        }
        else if (registerKey.OperandType == OperandType.Input)
        {
            // fxc can pack two differently named inputs into one register - TEXCOORD0
            // at v2.xy and TEXCOORD1 at v2.z - so which is meant depends on which
            // component is actually read, the same as a packed constant.
            byte component = instruction.GetSourceSwizzleComponents(operandIndex)[0];
            var inputComponentKey = new RegisterComponentKey(registerKey, component);
            registerName = _registers.GetRegisterName(inputComponentKey);
            isPackedScalar = _registers.IsPackedInputComponent(inputComponentKey)
                && _registers.GetRegisterMaskedLength(inputComponentKey) == 1;
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
            writeMaskName = _destinationMaskOverride == null
                ? instruction.GetWriteMaskName(
                    operandIndex, _registers.GetRegisterMaskedLength(registerKey))
                : instruction.GetWriteMaskName(
                    operandIndex,
                    _registers.GetRegisterMaskedLength(new RegisterComponentKey(
                        registerKey, FirstComponent(_destinationMaskOverride.Value))),
                    _destinationMaskOverride.Value);
        }
        else
        {
            // A texture or a sampler is named, never swizzled. The swizzle a sampling
            // instruction carries on its resource operand selects components of the
            // result, and on the sampler of a gather it selects the channel; neither
            // belongs on the object itself.
            if (registerKey.OperandType == OperandType.Resource
                || registerKey.OperandType == OperandType.Sampler)
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
            // An int register holding bits: what a float instruction reading it
            // wants is the float those bits are, and not the number they make. A
            // register of loop counters is declared int too and is the other way
            // about, which is why the two are told apart component by component.
            if (GetConsumedKind(instruction, operandIndex) == ValueKind.Float
                && HoldsFloatBits(instruction, operandIndex))
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
        for (int i = _shader.Instructions.IndexOf(loop); i < _shader.Instructions.Count; i++)
        {
            Instruction instruction = _shader.Instructions[i];
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

    // A constant declared bool, int or uint holds its integer as one.
    private bool IsIntegerConstant(D3D10Instruction instruction, int operandIndex)
    {
        if (instruction.GetOperandType(operandIndex) != OperandType.ConstantBuffer)
        {
            return false;
        }
        ConstantDeclaration constant = _registers.FindConstant(instruction.GetParamRegisterKey(operandIndex));
        return constant?.TypeInfo.ParameterType is ParameterType.Bool or ParameterType.Int or ParameterType.Uint;
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
        if (IsIntegerConstant(instruction, operandIndex))
        {
            return true;
        }
        return _registers.RegisterDeclarations.TryGetValue(key, out RegisterDeclaration declaration)
            && declaration.TypeName.Contains("int");
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
        else if (!read[2] && !read[3] && mipLevel == "0")
        {
            WriteLine($"{type}2 {dimensions};");
            WriteLine($"{resource}.GetDimensions({dimensions}.x, {dimensions}.y);");
        }
        else
        {
            WriteLine($"{type}4 {dimensions} = 0;");
            WriteLine($"{resource}.GetDimensions({mipLevel}, {dimensions}.x, {dimensions}.y, {dimensions}.w);");
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
        if (_registers.IsRawResource(instruction.GetParamRegisterKey(1)))
        {
            WriteLine($"uint {dimensions};");
            WriteLine($"{resource}.GetDimensions({dimensions});");
        }
        else
        {
            WriteLine($"uint2 {dimensions};");
            WriteLine($"{resource}.GetDimensions({dimensions}.x, {dimensions}.y);");
        }
        WriteResult(instruction, "{0} = {1};", GetOperandName(instruction, 0),
            _registers.IsRawResource(instruction.GetParamRegisterKey(1))
                ? dimensions
                : $"{dimensions}.x");
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
        int bindPoint = instruction.GetParamRegisterNumber(operandIndex);
        return _registers.ResourceDefinitions
            .Where(d => d.ShaderInputType == D3DShaderInputType.Texture)
            .FirstOrDefault(d => d.BindPoint == bindPoint)?.Dimension;
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
        if (buffer?.ShaderInputType != D3DShaderInputType.TBuffer
            || instruction.GetOperandType(1) != OperandType.Immediate32)
        {
            return null;
        }
        int register = instruction.GetParamInt(1, 0);
        return _registers.ConstantDeclarations
            .OfType<D3D10ConstantDeclaration>()
            .Where(d => d.IsTextureBuffer && d.BufferName == buffer.Name)
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

    // A constructor over the variables an operand reads, when it reads more than
    // one and they are not all the same. Each is a scalar of its own, so there is
    // no name that covers them.
    private bool TryNamePackedScalars(
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

        string[] names = [.. components.Select(c =>
            _registers.GetRegisterName(new RegisterComponentKey(registerKey, c)))];
        if (names.Distinct().Count() < 2)
        {
            // All one variable, which the name alone already says.
            return false;
        }

        string type = _integerOperandAnalysis.IsIntegerOperand(instruction) ? "int" : "float";
        name = $"{type}{components.Length}({string.Join(", ", names)})";
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
            OperandType.Input or OperandType.InputPatchConstant =>
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
        // of the subscript.
        if (instruction.Opcode.IsAtomic() && operandIndex != 0)
        {
            return 1;
        }
        if (instruction.Opcode.IsImmediateAtomic() && operandIndex > 1)
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