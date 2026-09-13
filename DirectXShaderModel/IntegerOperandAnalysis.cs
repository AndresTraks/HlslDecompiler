using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.DirectXShaderModel;

public sealed class IntegerOperandAnalysis
{
    private HashSet<RegisterComponentKey> _integerRegisters;
    private readonly ShaderModel _shader;

    public IntegerOperandAnalysis(ShaderModel shader)
    {
        _shader = shader;
    }

    public bool IsIntegerOperand(D3D10Instruction instruction)
    {
        if (instruction.Opcode.IsInteger())
        {
            return true;
        }

        _integerRegisters ??= FindIntegerRegisters(_shader);
        return IsIntegerMove(instruction);
    }

    // Whether a register component only ever holds an integer, so that a temp
    // standing for it can be declared as one.
    public bool IsIntegerRegister(RegisterComponentKey registerComponent)
    {
        _integerRegisters ??= FindIntegerRegisters(_shader);
        return _integerRegisters.Contains(registerComponent);
    }

    /// <summary>
    /// Whether an indexable temp holds integers: every store into it is an integer
    /// instruction or a move of a register component known to be one. Its elements
    /// share one key, so the register rule cannot answer for it - one element
    /// written from an index would make every element an integer.
    /// </summary>
    public bool IsIntegerIndexableTemp(int register)
    {
        _integerRegisters ??= FindIntegerRegisters(_shader);
        return IsIntegerIndexableTemp(register, _integerRegisters);
    }

    /// <summary>
    /// Whether a groupshared array holds integers. The register rule cannot answer:
    /// the value a store writes usually sits in a register that also carried the
    /// element index a moment before, and that alone would make every array an
    /// integer. So each store is traced back to the instruction that last wrote the
    /// value it stores - an integer operation, a float one, or a load whose buffer
    /// the reflection data types. Integer only when some store says so and none
    /// says float.
    /// </summary>
    public bool IsIntegerThreadGroupSharedMemory(int register)
    {
        const int ValueIndex = 3;
        var instructions = _shader.Instructions.OfType<D3D10Instruction>().ToList();
        bool anyInteger = false;
        for (int i = 0; i < instructions.Count; i++)
        {
            D3D10Instruction store = instructions[i];
            if (store.Opcode != D3D10Opcode.StoreStructured
                || store.GetOperandType(0) != OperandType.ThreadGroupSharedMemory
                || store.GetParamRegisterNumber(0) != register)
            {
                continue;
            }
            switch (StoredValueType(instructions, i, ValueIndex))
            {
                case StoredType.Float:
                    return false;
                case StoredType.Integer:
                    anyInteger = true;
                    break;
            }
        }
        return anyInteger;
    }

    private enum StoredType { Unknown, Integer, Float }

    // What the operand of the instruction at index holds, by the instruction that
    // last wrote it. Straight-line order only; a value written in one branch and
    // read after the join reads as whichever write comes last in the listing, which
    // is as much as the bytecode says without a flow graph.
    private StoredType StoredValueType(List<D3D10Instruction> instructions, int index, int operandIndex)
    {
        D3D10Instruction reader = instructions[index];
        OperandType type = reader.GetOperandType(operandIndex);
        if (type == OperandType.Immediate32)
        {
            return StoredType.Unknown;
        }
        if (type != OperandType.Temp)
        {
            // An input or a constant, typed by its own declaration; not worth
            // chasing for this.
            return _integerRegisters != null && IsIntegerRegister(
                new RegisterComponentKey(reader.GetParamRegisterKey(operandIndex), reader.GetSourceSwizzleComponents(operandIndex)[0]))
                ? StoredType.Integer
                : StoredType.Unknown;
        }
        RegisterKey source = reader.GetParamRegisterKey(operandIndex);
        int component = reader.GetSourceSwizzleComponents(operandIndex)[0];
        for (int i = index - 1; i >= 0; i--)
        {
            D3D10Instruction writer = instructions[i];
            if (writer.Opcode.IsDeclaration() || !writer.HasDestination
                || writer.Opcode == D3D10Opcode.StoreStructured)
            {
                continue;
            }
            int destination = writer.GetDestinationParamIndex().Value;
            if (!writer.GetParamRegisterKey(destination).Equals(source)
                || (writer.GetWriteMask(destination) & (1 << component)) == 0)
            {
                continue;
            }
            if (writer.Opcode.IsInteger() || writer.Opcode == D3D10Opcode.Ftoi || writer.Opcode == D3D10Opcode.Ftou)
            {
                return StoredType.Integer;
            }
            switch (writer.Opcode)
            {
                case D3D10Opcode.Mov:
                    return StoredValueType(instructions, i, 1);
                case D3D10Opcode.MovC:
                    {
                        StoredType first = StoredValueType(instructions, i, 2);
                        return first != StoredType.Unknown ? first : StoredValueType(instructions, i, 3);
                    }
                case D3D10Opcode.LdStructured:
                    return LoadedElementType(writer);
                default:
                    return StoredType.Float;
            }
        }
        return StoredType.Unknown;
    }

    // What ld_structured reads: the element type the reflection data gives the
    // buffer, or nothing for groupshared memory, which has none.
    private StoredType LoadedElementType(D3D10Instruction load)
    {
        const int ResourceIndex = 3;
        OperandType type = load.GetOperandType(ResourceIndex);
        D3DShaderInputType inputType = type switch
        {
            OperandType.Resource => D3DShaderInputType.Structured,
            OperandType.UnorderedAccessView => D3DShaderInputType.UavRWStructured,
            _ => (D3DShaderInputType)(-1),
        };
        ResourceDefinition definition = _shader.ResourceDefinitions
            .FirstOrDefault(d => d.ShaderInputType == inputType
                && d.BindPoint == load.GetParamRegisterNumber(ResourceIndex));
        if (definition?.ElementType == null)
        {
            return StoredType.Unknown;
        }
        return definition.ElementType.ParameterType switch
        {
            ParameterType.Int or ParameterType.Uint or ParameterType.Bool => StoredType.Integer,
            ParameterType.Float => StoredType.Float,
            _ => StoredType.Unknown,
        };
    }

    private bool IsIntegerIndexableTemp(int register, HashSet<RegisterComponentKey> integerRegisters)
    {
        bool anyStore = false;
        foreach (D3D10Instruction instruction in _shader.Instructions.OfType<D3D10Instruction>())
        {
            if (instruction.Opcode.IsDeclaration() || !instruction.HasDestination)
            {
                continue;
            }
            int destination = instruction.GetDestinationParamIndex().Value;
            if (instruction.GetOperandType(destination) != OperandType.IndexableTemp
                || instruction.OperandTokens.GetOperandIndices(destination)[0].Immediate != register)
            {
                continue;
            }
            anyStore = true;
            if (instruction.Opcode.IsInteger())
            {
                continue;
            }
            if (instruction.Opcode != D3D10Opcode.Mov
                || instruction.GetOperandType(1) == OperandType.Immediate32
                || instruction.GetOperandType(1) == OperandType.IndexableTemp)
            {
                // A float instruction, or a move whose source says nothing.
                return false;
            }
            RegisterKey source = instruction.GetParamRegisterKey(1);
            int writeMask = instruction.GetDestinationWriteMask();
            byte[] swizzle = instruction.GetSourceSwizzleComponents(1);
            for (int component = 0; component < 4; component++)
            {
                if ((writeMask & (1 << component)) != 0
                    && !integerRegisters.Contains(new RegisterComponentKey(source, swizzle[component])))
                {
                    return false;
                }
            }
        }
        return anyStore;
    }

    private HashSet<RegisterComponentKey> FindIntegerRegisters(ShaderModel shader)
    {
        var integerRegisters = new HashSet<RegisterComponentKey>();

        // An input the signature types as an integer holds one before any
        // instruction touches it. Geometry shader inputs are keyed by vertex as
        // well, so they are left to the instructions that read them.
        const int UInt32ComponentType = 1;
        const int SInt32ComponentType = 2;
        if (shader.Type != ShaderType.Geometry)
        {
            foreach (RegisterSignature signature in shader.InputSignatures)
            {
                if (signature.ComponentType != UInt32ComponentType && signature.ComponentType != SInt32ComponentType)
                {
                    continue;
                }
                for (int component = 0; component < 4; component++)
                {
                    if ((signature.Mask & (1 << component)) != 0)
                    {
                        integerRegisters.Add(new RegisterComponentKey(signature.RegisterKey, component));
                    }
                }
            }
        }

        foreach (D3D10Instruction instruction in shader.Instructions.OfType<D3D10Instruction>())
        {
            if (instruction.Opcode != D3D10Opcode.IToF
                && instruction.Opcode != D3D10Opcode.UTof
                && GetSourceCount(instruction.Opcode) != 0)
            {
                AddDestinationComponents(instruction, integerRegisters);
            }
            // resinfo_uint writes the dimensions as integers; its mip level source
            // is one too, but an immediate or a register the shader already treats
            // as one, so only the destination is worth recording.
            if (instruction.Opcode == D3D10Opcode.ResInfo
                && instruction.ResInfoReturnType == D3D10ResInfoReturnType.Uint)
            {
                AddDestinationComponents(instruction, integerRegisters);
            }
            // ftoi and ftou read floats and write integers, so the destination is
            // an integer component but the source is not.
            if (instruction.Opcode != D3D10Opcode.Ftoi
                && instruction.Opcode != D3D10Opcode.Ftou
                && GetSourceCount(instruction.Opcode) != 0)
            {
                AddSourceComponents(instruction, integerRegisters);
            }
            // Whatever indexes a local array is an integer.
            AddIndexableTempIndexComponents(instruction, integerRegisters);
        }

        // mov, and, or and xor take their type from what they touch, so a register
        // they write is only known to be integer once one they touch is. `xor` feeding
        // an `or` whose result reaches `utof` needs two rounds to settle.
        bool changed;
        do
        {
            changed = false;
            foreach (D3D10Instruction instruction in shader.Instructions.OfType<D3D10Instruction>())
            {
                int sourceCount = GetPolymorphicSourceCount(instruction.Opcode);
                if (sourceCount == 0)
                {
                    continue;
                }
                // Per component, not per instruction: `mov r0.xy, l(0,0,0,0)` can
                // write a float accumulator and an integer counter at once, and one
                // says nothing about the other.
                foreach (HashSet<RegisterComponentKey> group in GetComponentGroups(instruction, sourceCount))
                {
                    if (!group.Overlaps(integerRegisters))
                    {
                        continue;
                    }
                    foreach (RegisterComponentKey component in group)
                    {
                        changed |= integerRegisters.Add(component);
                    }
                }
            }
        }
        while (changed);

        // A register read out of an integer array is an integer. The array's type
        // is decided from the registers written into it, so it comes after them.
        var integerArrays = new HashSet<int>();
        foreach (D3D10Instruction instruction in shader.Instructions.OfType<D3D10Instruction>())
        {
            if (instruction.Opcode == D3D10Opcode.DclIndexableTemp
                && IsIntegerIndexableTemp(instruction.IndexableTempRegister, integerRegisters))
            {
                integerArrays.Add(instruction.IndexableTempRegister);
            }
        }
        foreach (D3D10Instruction instruction in shader.Instructions.OfType<D3D10Instruction>())
        {
            if (instruction.Opcode == D3D10Opcode.Mov
                && instruction.GetOperandType(1) == OperandType.IndexableTemp
                && integerArrays.Contains((int)instruction.OperandTokens.GetOperandIndices(1)[0].Immediate))
            {
                AddDestinationComponents(instruction, integerRegisters);
            }
        }

        return integerRegisters;
    }

    // Each written component together with the source components feeding it.
    private static IEnumerable<HashSet<RegisterComponentKey>> GetComponentGroups(
        D3D10Instruction instruction, int sourceCount)
    {
        int? destinationIndex = instruction.GetDestinationParamIndex();
        if (destinationIndex == null)
        {
            yield break;
        }

        // An indexable temp is memory whose elements all share one key, so a mov
        // through it would tie every element to whatever register touched any of
        // them - an integer index moved into x0[3] would make x0[0] an integer too,
        // and then whatever reads x0[0]. Only an integer instruction writing it
        // says anything about it.
        if (instruction.GetOperandType(destinationIndex.Value) == OperandType.IndexableTemp)
        {
            yield break;
        }
        RegisterKey destinationKey = instruction.GetParamRegisterKey(destinationIndex.Value);
        int writeMask = instruction.GetDestinationWriteMask();
        for (int component = 0; component < 4; component++)
        {
            if ((writeMask & (1 << component)) == 0)
            {
                continue;
            }
            var group = new HashSet<RegisterComponentKey>
            {
                new RegisterComponentKey(destinationKey, component),
            };
            for (int source = 1; source <= sourceCount; source++)
            {
                if (instruction.GetOperandType(source) == OperandType.Immediate32
                    || instruction.GetOperandType(source) == OperandType.IndexableTemp)
                {
                    continue;
                }
                RegisterKey sourceKey = instruction.GetParamRegisterKey(source);
                group.Add(new RegisterComponentKey(
                    sourceKey, instruction.GetSourceSwizzleComponents(source)[component]));
            }
            yield return group;
        }
    }

    // Opcodes whose operand type is decided by their neighbours rather than by the
    // opcode itself, with how many sources each takes.
    private static int GetPolymorphicSourceCount(D3D10Opcode opcode)
    {
        return opcode switch
        {
            D3D10Opcode.Mov => 1,
            D3D10Opcode.And or D3D10Opcode.Or or D3D10Opcode.Xor => 2,
            _ => 0,
        };
    }

    // mov carries whatever it is given, and and/or/xor are bitwise on integers but
    // logical on comparison results - `and r1, r1, l(1.0)` selects a float. None of
    // them can be judged by opcode alone, so they are judged by the registers they
    // touch, which the unambiguous integer opcodes establish.
    private bool IsIntegerMove(D3D10Instruction instruction)
    {
        if (_integerRegisters.Count == 0)
        {
            return false;
        }
        int sourceCount = GetPolymorphicSourceCount(instruction.Opcode);
        if (sourceCount == 0)
        {
            return false;
        }

        int? destination = instruction.GetDestinationParamIndex();
        if (destination != null && instruction.GetOperandType(destination.Value) == OperandType.IndexableTemp)
        {
            return IsIntegerIndexableTemp(
                (int)instruction.OperandTokens.GetOperandIndices(destination.Value)[0].Immediate, _integerRegisters);
        }

        var components = new HashSet<RegisterComponentKey>();
        AddDestinationComponents(instruction, components);
        if (instruction.Opcode != D3D10Opcode.Mov)
        {
            AddSourceComponents(instruction, components, sourceCount);
        }
        return components.Overlaps(_integerRegisters);
    }

    private static void AddIndexableTempIndexComponents(
        D3D10Instruction instruction, HashSet<RegisterComponentKey> components)
    {
        if (instruction.Opcode.IsDeclaration() || instruction.Opcode == D3D10Opcode.CustomData)
        {
            return;
        }
        for (int operand = 0; operand < instruction.OperandTokens.OperandCount; operand++)
        {
            if (instruction.GetOperandType(operand) != OperandType.IndexableTemp)
            {
                continue;
            }
            const int ElementIndex = 1;
            if (!instruction.IsRelativelyAddressed(operand, ElementIndex))
            {
                continue;
            }
            (OperandType type, int number, byte component) =
                instruction.OperandTokens.GetRelativeIndexOperand(operand, ElementIndex);
            components.Add(new RegisterComponentKey(new D3D10RegisterKey(type, number), component));
        }
    }

    private static void AddDestinationComponents(D3D10Instruction instruction, HashSet<RegisterComponentKey> components)
    {
        int? destinationIndex = instruction.GetDestinationParamIndex();
        if (destinationIndex == null)
        {
            return;
        }

        RegisterKey registerKey = instruction.GetParamRegisterKey(destinationIndex.Value);
        int writeMask = instruction.GetDestinationWriteMask();
        for (int component = 0; component < 4; component++)
        {
            if ((writeMask & (1 << component)) != 0)
            {
                components.Add(new RegisterComponentKey(registerKey, component));
            }
        }
    }

    private static void AddSourceComponents(D3D10Instruction instruction, HashSet<RegisterComponentKey> components)
    {
        AddSourceComponents(instruction, components, GetSourceCount(instruction.Opcode));
    }

    private static void AddSourceComponents(
        D3D10Instruction instruction, HashSet<RegisterComponentKey> components, int sourceCount)
    {
        for (int source = 1; source <= sourceCount; source++)
        {
            if (instruction.GetOperandType(source) == OperandType.Immediate32)
            {
                continue;
            }

            RegisterKey registerKey = instruction.GetParamRegisterKey(source);
            foreach (byte component in instruction.GetSourceSwizzleComponents(source))
            {
                components.Add(new RegisterComponentKey(registerKey, component));
            }
        }
    }

    private static int GetSourceCount(D3D10Opcode opcode)
    {
        switch (opcode)
        {
            case D3D10Opcode.Ftoi:
            case D3D10Opcode.Ftou:
            case D3D10Opcode.IToF:
            case D3D10Opcode.UTof:
            case D3D10Opcode.INeg:
                return 1;
            case D3D10Opcode.IAdd:
            case D3D10Opcode.IShl:
            case D3D10Opcode.IShr:
            case D3D10Opcode.UShr:
            case D3D10Opcode.Ieq:
            case D3D10Opcode.Ige:
            case D3D10Opcode.UGE:
            case D3D10Opcode.ULT:
            case D3D10Opcode.Ilt:
            case D3D10Opcode.IMin:
            case D3D10Opcode.IMax:
            case D3D10Opcode.IMul:
            case D3D10Opcode.Ine:
                return 2;
            case D3D10Opcode.IMad:
                return 3;
            default:
                return 0;
        }
    }
}
