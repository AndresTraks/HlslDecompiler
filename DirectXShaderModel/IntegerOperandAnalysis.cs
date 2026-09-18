using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.DirectXShaderModel;

public sealed class IntegerOperandAnalysis
{
    private HashSet<RegisterComponentKey> _integerRegisters;
    private HashSet<RegisterComponentKey> _floatRegisters;
    private HashSet<RegisterComponentKey> _bitsRegisters;
    private HashSet<RegisterComponentKey> _maskRegisters;
    private HashSet<RegisterKey> _integerDeclaredRegisters;
    private HashSet<RegisterComponentKey> _integerOutputSignatures;
    private HashSet<RegisterKey> _bitsDeclaredRegisters;
    private HashSet<RegisterComponentKey> _convertedToFloat;

    /// <summary>How many moves a value is followed through before giving up. Real
    /// chains are one or two; the bound is there so a shader full of them cannot
    /// make the search exponential.</summary>
    private const int MaximumCarryDepth = 8;
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
    /// How the instruction writer keeps a temp register component. Registers are
    /// declared once each, and fxc reuses them freely, so one component can carry
    /// a loop counter, a comparison mask and a float in turn. A register is
    /// declared int only when nothing float ever touches any component it writes;
    /// otherwise it is a float, and a component in it holds either its values -
    /// an integer as the float of the same value, converted on the way in and out
    /// - or, once a comparison or a bitwise operator has had it, its bits, which
    /// the integer instructions then read with asint and write with asfloat.
    /// Reinterpreting rather than converting is what keeps a mask a mask.
    /// </summary>
    public ComponentStorage GetStorage(RegisterComponentKey registerComponent)
    {
        if (!registerComponent.RegisterKey.IsTempRegister)
        {
            return IsIntegerRegister(registerComponent) ? ComponentStorage.Integer : ComponentStorage.Numeric;
        }
        if (IsIntegerDeclaredRegister(registerComponent.RegisterKey))
        {
            return ComponentStorage.Integer;
        }
        // A comparison mask that only tests, branches and integer arithmetic read
        // is -1 or 0 as a number just as well, and stays a number - so long as
        // the component is otherwise an integer's. One nothing integer ever touches
        // is kept as bits, as it always was, since -1.0f anded with the bits of 8.0f
        // is not 8.
        if (IsBitsTouched(registerComponent)
            || (IsMask(registerComponent) && !IsIntegerRegister(registerComponent)))
        {
            return ComponentStorage.Bits;
        }
        return ComponentStorage.Numeric;
    }

    /// <summary>Whether a comparison writes the component.</summary>
    private bool IsMask(RegisterComponentKey registerComponent)
    {
        _maskRegisters ??= FindMaskRegisters();
        return _maskRegisters.Contains(registerComponent);
    }

    private HashSet<RegisterComponentKey> FindMaskRegisters()
    {
        var masks = new HashSet<RegisterComponentKey>();
        foreach (D3D10Instruction instruction in _shader.Instructions.OfType<D3D10Instruction>())
        {
            if (instruction.Opcode.ProducedKind() == ValueKind.Bits
                && instruction.GetOperandType(0) == OperandType.Temp)
            {
                AddWrittenComponents(instruction, 0, masks);
            }
        }
        return masks;
    }

    /// <summary>Whether a temp register is declared int: every component any
    /// instruction writes is an integer that no float instruction touches.</summary>
    public bool IsIntegerDeclaredRegister(RegisterKey registerKey)
    {
        _integerDeclaredRegisters ??= FindIntegerDeclaredRegisters();
        return _integerDeclaredRegisters.Contains(registerKey);
    }

    /// <summary>Whether a float instruction reads or writes the component, directly
    /// or through a move.</summary>
    public bool IsFloatTouched(RegisterComponentKey registerComponent)
    {
        _floatRegisters ??= FindTouchedRegisters(ValueKind.Float);
        return _floatRegisters.Contains(registerComponent);
    }

    /// <summary>Whether a bitwise operator reads or writes the component, or a
    /// move brings in a value from one that does - the one kind of reader for
    /// which a value's bits, and not its number, are what matters.</summary>
    public bool IsBitsTouched(RegisterComponentKey registerComponent)
    {
        _bitsRegisters ??= FindTouchedRegisters(ValueKind.Bits);
        return _bitsRegisters.Contains(registerComponent);
    }

    /// <summary>
    /// What ld_structured reads or store_structured writes: the element type the
    /// reflection data gives the buffer, or nothing for groupshared memory.
    /// </summary>
    public ValueKind GetStructuredElementKind(D3D10Instruction instruction)
    {
        return LoadedElementType(instruction) switch
        {
            StoredType.Integer => ValueKind.Integer,
            StoredType.Float => ValueKind.Float,
            _ => ValueKind.Unknown,
        };
    }

    /// <summary>
    /// Whether the output signature types a register component as an integer.
    /// IsIntegerRegister answers the same question of the instructions as well,
    /// and an integer instruction writing a float output is exactly the case that
    /// has to be told apart: what it writes there is a float's bits.
    /// </summary>
    public bool IsIntegerOutputSignature(RegisterComponentKey registerComponent)
    {
        _integerOutputSignatures ??= FindIntegerOutputSignatures();
        return _integerOutputSignatures.Contains(registerComponent);
    }

    private HashSet<RegisterComponentKey> FindIntegerOutputSignatures()
    {
        const int UInt32ComponentType = 1;
        const int SInt32ComponentType = 2;
        var integers = new HashSet<RegisterComponentKey>();
        foreach (RegisterSignature signature in _shader.OutputSignatures)
        {
            if (signature.ComponentType != UInt32ComponentType && signature.ComponentType != SInt32ComponentType)
            {
                continue;
            }
            for (int component = 0; component < 4; component++)
            {
                if ((signature.Mask & (1 << component)) != 0)
                {
                    integers.Add(new RegisterComponentKey(signature.RegisterKey, component));
                }
            }
        }
        return integers;
    }

    /// <summary>
    /// What ld and ldms read: a texel of the type the resource was declared with,
    /// which for a Texture2D&lt;uint4&gt; is an integer and not the float a texel
    /// usually is. A G-buffer packs bits into such a texture, and reading them as
    /// floats loses them.
    /// </summary>
    public ValueKind GetTypedLoadKind(D3D10Instruction load)
    {
        // The resource is the operand after the address.
        const int ResourceIndex = 2;
        ResourceDefinition definition = _shader.ResourceDefinitions?
            .FirstOrDefault(d => d.ShaderInputType == D3DShaderInputType.Texture
                && d.BindPoint == load.GetParamRegisterNumber(ResourceIndex));
        return definition?.IsIntegerReturnType == true ? ValueKind.Integer : ValueKind.Float;
    }

    /// <summary>
    /// What the immediate a mov or movc writes is, by the instructions that go on
    /// to read the register components it writes - the mov itself says nothing.
    /// Unknown where the components' readers disagree, or there are none.
    /// </summary>
    public ValueKind GetImmediateKindByReaders(D3D10Instruction instruction)
    {
        ValueKind kind = ValueKind.Unknown;
        foreach (ValueKind component in GetImmediateKindsByReaders(instruction))
        {
            if (component == ValueKind.Unknown)
            {
                continue;
            }
            if (kind != ValueKind.Unknown && kind != component)
            {
                return ValueKind.Unknown;
            }
            kind = component;
        }
        return kind;
    }

    /// <summary>
    /// The same, one answer per component of the destination register - `mov
    /// r0.xy, l(0, 0)` can start a float accumulator and an integer counter at
    /// once. A forward scan, stopping for a component once it is written again at
    /// the same or a shallower nesting level; a write deeper inside an if or a
    /// loop leaves a read after it possible, so the scan looks past it.
    /// </summary>
    public ValueKind[] GetImmediateKindsByReaders(D3D10Instruction instruction)
    {
        return GetImmediateKindsByReaders(instruction, []);
    }

    private ValueKind[] GetImmediateKindsByReaders(
        D3D10Instruction instruction, HashSet<D3D10Instruction> visited)
    {
        var kinds = new ValueKind[4];
        var instructions = _shader.Instructions.OfType<D3D10Instruction>().ToList();
        int start = instructions.IndexOf(instruction);
        int? destinationIndex = instruction.GetDestinationParamIndex();
        if (start < 0 || destinationIndex == null
            || instruction.GetOperandType(destinationIndex.Value) != OperandType.Temp)
        {
            return kinds;
        }
        RegisterKey destination = instruction.GetParamRegisterKey(destinationIndex.Value);
        int live = instruction.GetDestinationWriteMask();
        var disagree = new bool[4];
        int depth = 0;
        for (int i = start + 1; i < instructions.Count && live != 0; i++)
        {
            D3D10Instruction reader = instructions[i];
            switch (reader.Opcode)
            {
                case D3D10Opcode.If:
                case D3D10Opcode.Loop:
                case D3D10Opcode.Swtich:
                    depth++;
                    break;
                case D3D10Opcode.EndIf:
                case D3D10Opcode.EndLoop:
                case D3D10Opcode.EndSwitch:
                    depth--;
                    break;
            }
            if (reader.Opcode.IsDeclaration() || reader.Opcode == D3D10Opcode.CustomData)
            {
                continue;
            }
            // An instruction reads its sources before it writes its destination, so
            // what it overwrites is taken away only once its own reads are counted:
            // `movc r1.xy, c, r1.wz, r1.xy` reads r1.y and writes it, and killing
            // the component first lost the read.
            int overwritten = 0;
            for (int operand = 0; operand < reader.OperandTokens.OperandCount; operand++)
            {
                if (reader.GetOperandType(operand) != OperandType.Temp
                    || !reader.GetParamRegisterKey(operand).Equals(destination))
                {
                    continue;
                }
                if (reader.IsDestinationOperand(operand))
                {
                    if (depth <= 0)
                    {
                        overwritten |= reader.GetWriteMask(operand);
                    }
                    continue;
                }
                ValueKind consumed = reader.Opcode.ConsumedKind();
                if (reader.Opcode == D3D10Opcode.StoreStructured && operand == 3)
                {
                    consumed = GetStructuredElementKind(reader);
                }
                if (consumed != ValueKind.Integer && consumed != ValueKind.Float)
                {
                    // A mov or a movc reads nothing of its own; it carries the
                    // value on, and what the value is comes from whatever reads
                    // the register it lands in. Stopping here left the constants
                    // of `movc r2.xy, c, l(8, 12), l(0, 4)` typed as floats, where
                    // the iadd that reads them through a second movc says they are
                    // integers - and 8 as a float is a denormal that prints as 0.
                    // A movc's first source is the condition it tests, not a value
                    // it carries.
                    if (reader.Opcode is D3D10Opcode.Mov or D3D10Opcode.MovC
                        && !(reader.Opcode == D3D10Opcode.MovC && operand == 1))
                    {
                        CarryKindsOnward(reader, operand, live, kinds, disagree, visited);
                    }
                    continue;
                }
                foreach (byte component in reader.GetSourceSwizzleComponents(operand).Distinct())
                {
                    if ((live & (1 << component)) == 0 || disagree[component])
                    {
                        continue;
                    }
                    if (kinds[component] != ValueKind.Unknown && kinds[component] != consumed)
                    {
                        kinds[component] = ValueKind.Unknown;
                        disagree[component] = true;
                        continue;
                    }
                    kinds[component] = consumed;
                }
            }
            live &= ~overwritten;
        }
        return kinds;
    }

    /// <summary>
    /// Takes what reads the destination of a carrying move and applies it to the
    /// components that move reads, so an immediate two moves away from the
    /// instruction that gives it a type still gets one.
    /// </summary>
    private void CarryKindsOnward(
        D3D10Instruction move,
        int operand,
        int live,
        ValueKind[] kinds,
        bool[] disagree,
        HashSet<D3D10Instruction> visited)
    {
        int? destinationIndex = move.GetDestinationParamIndex();
        if (destinationIndex == null
            || move.GetOperandType(destinationIndex.Value) != OperandType.Temp
            || visited.Count >= MaximumCarryDepth
            || !visited.Add(move))
        {
            return;
        }
        // The set is the path and not everything seen: a move carries each of its
        // sources on, and leaving it in would let the first of them stop the rest.
        ValueKind[] onward;
        try
        {
            onward = GetImmediateKindsByReaders(move, visited);
        }
        finally
        {
            visited.Remove(move);
        }
        byte[] swizzle = move.GetSourceSwizzleComponents(operand);
        int writeMask = move.GetWriteMask(destinationIndex.Value);
        for (int destination = 0; destination < 4; destination++)
        {
            if ((writeMask & (1 << destination)) == 0
                || onward[destination] == ValueKind.Unknown)
            {
                continue;
            }
            int component = swizzle[destination];
            if ((live & (1 << component)) == 0 || disagree[component])
            {
                continue;
            }
            if (kinds[component] != ValueKind.Unknown && kinds[component] != onward[destination])
            {
                kinds[component] = ValueKind.Unknown;
                disagree[component] = true;
                continue;
            }
            kinds[component] = onward[destination];
        }
    }

    private HashSet<RegisterKey> FindIntegerDeclaredRegisters()
    {
        return [.. FindWrittenTempComponents()
            .Where(r => r.Value.All(c => IsIntegerRegister(c) && !IsFloatTouched(c))
                || r.Value.All(IsBitsOnly))
            .Select(r => r.Key)];
    }

    /// <summary>Every temp register any instruction writes, with the components
    /// written.</summary>
    private Dictionary<RegisterKey, HashSet<RegisterComponentKey>> FindWrittenTempComponents()
    {
        var written = new Dictionary<RegisterKey, HashSet<RegisterComponentKey>>();
        foreach (D3D10Instruction instruction in _shader.Instructions.OfType<D3D10Instruction>())
        {
            if (instruction.Opcode.IsDeclaration() || instruction.Opcode == D3D10Opcode.CustomData)
            {
                continue;
            }
            for (int operand = 0; operand < instruction.OperandTokens.OperandCount; operand++)
            {
                if (!instruction.IsDestinationOperand(operand)
                    || instruction.GetOperandType(operand) != OperandType.Temp)
                {
                    continue;
                }
                RegisterKey key = instruction.GetParamRegisterKey(operand);
                if (!written.TryGetValue(key, out HashSet<RegisterComponentKey> components))
                {
                    components = [];
                    written[key] = components;
                }
                int writeMask = instruction.GetWriteMask(operand);
                for (int component = 0; component < 4; component++)
                {
                    if ((writeMask & (1 << component)) != 0)
                    {
                        components.Add(new RegisterComponentKey(key, component));
                    }
                }
            }
        }
        return written;
    }

    /// <summary>
    /// Whether a temp register holds bits and nothing else, so that it is declared
    /// int and what it holds is read back with asfloat rather than converted. A
    /// register of loop counters is declared int too and is not this: the number
    /// in it is what a float instruction reading it wants.
    /// </summary>
    public bool IsBitsRegister(RegisterKey registerKey)
    {
        _bitsDeclaredRegisters ??= FindBitsDeclaredRegisters();
        return _bitsDeclaredRegisters.Contains(registerKey);
    }

    private HashSet<RegisterKey> FindBitsDeclaredRegisters()
    {
        return [.. FindWrittenTempComponents()
            .Where(r => r.Value.All(IsBitsOnly))
            .Select(r => r.Key)];
    }

    /// <summary>
    /// Whether a component holds bits and nothing else. A register all of whose
    /// components are such is declared int rather than float, which is not a
    /// nicety: kept in a float and reinterpreted at every use, the bits do not
    /// survive recompilation at all. fxc reads asfloat as producing a float and
    /// flushes a denormal to zero, so a mask of the low bits of anything - every
    /// step of its own f32tof16, a packed G-buffer, an index anded with 1 - comes
    /// back as zero. In an int variable there is no asfloat to flush.
    ///
    /// Only where no float instruction writes the component. One that carries a
    /// float for part of the shader and bits for the rest has to go on being a
    /// float register with the bits reinterpreted, since the float would not
    /// survive the other way round.
    /// </summary>
    private bool IsBitsOnly(RegisterComponentKey registerComponent)
    {
        return (IsBitsTouched(registerComponent)
                || (IsMask(registerComponent) && !IsIntegerRegister(registerComponent)))
            && !IsFloatTouched(registerComponent)
            && !IsConvertedToFloat(registerComponent);
    }

    /// <summary>
    /// Whether itof or utof writes the component. Those are left out of the float
    /// instructions on purpose - an integer register holds the whole number they
    /// write without loss - but what they write is a number and not bits, so a
    /// component they write is not one of these.
    /// </summary>
    private bool IsConvertedToFloat(RegisterComponentKey registerComponent)
    {
        _convertedToFloat ??= FindConvertedToFloat();
        return _convertedToFloat.Contains(registerComponent);
    }

    private HashSet<RegisterComponentKey> FindConvertedToFloat()
    {
        var converted = new HashSet<RegisterComponentKey>();
        foreach (D3D10Instruction instruction in _shader.Instructions.OfType<D3D10Instruction>())
        {
            if (instruction.Opcode is D3D10Opcode.IToF or D3D10Opcode.UTof
                && instruction.GetOperandType(0) == OperandType.Temp)
            {
                AddWrittenComponents(instruction, 0, converted);
            }
        }
        return converted;
    }

    // The components a float instruction writes, or a bitwise operator reads or
    // writes, followed forward through the moves that carry them on. Writes rather than reads: an int register read by a float
    // instruction converts its value on the way, which is right, where one
    // written by a float instruction truncates it. itof and utof write a float
    // that is a whole number, which an int register holds without loss, and
    // counting them would turn every integer register that is ever converted into
    // a float one. A moved immediate counts as whatever reads it afterwards.
    private HashSet<RegisterComponentKey> FindTouchedRegisters(ValueKind kind)
    {
        var touched = new HashSet<RegisterComponentKey>();
        var instructions = _shader.Instructions.OfType<D3D10Instruction>().ToList();
        foreach (D3D10Instruction instruction in instructions)
        {
            if (instruction.Opcode.IsDeclaration() || instruction.Opcode == D3D10Opcode.CustomData)
            {
                continue;
            }
            ValueKind produced = instruction.Opcode.ProducedKind();
            if (instruction.Opcode == D3D10Opcode.ResInfo)
            {
                produced = instruction.ResInfoReturnType == D3D10ResInfoReturnType.Uint
                    ? ValueKind.Integer
                    : ValueKind.Float;
            }
            else if (instruction.Opcode == D3D10Opcode.LdStructured)
            {
                produced = GetStructuredElementKind(instruction);
            }
            else if (instruction.Opcode is D3D10Opcode.LD or D3D10Opcode.LDMS)
            {
                produced = GetTypedLoadKind(instruction);
            }
            else if (instruction.Opcode is D3D10Opcode.IToF or D3D10Opcode.UTof)
            {
                produced = ValueKind.Unknown;
            }
            else if (instruction.Opcode is D3D10Opcode.Mov or D3D10Opcode.MovC
                && Enumerable.Range(1, instruction.OperandTokens.OperandCount - 1)
                    .Any(operand => instruction.GetOperandType(operand) == OperandType.Immediate32))
            {
                produced = GetImmediateKindByReaders(instruction);
            }
            bool bitwise = instruction.Opcode is D3D10Opcode.And or D3D10Opcode.Or
                or D3D10Opcode.Xor or D3D10Opcode.Not;
            for (int operand = 0; operand < instruction.OperandTokens.OperandCount; operand++)
            {
                if (instruction.GetOperandType(operand) != OperandType.Temp)
                {
                    continue;
                }
                if (instruction.IsDestinationOperand(operand))
                {
                    if ((kind == ValueKind.Float && produced == kind) || (kind == ValueKind.Bits && bitwise))
                    {
                        AddWrittenComponents(instruction, operand, touched);
                    }
                }
                else if (kind == ValueKind.Bits && bitwise)
                {
                    AddReadComponents(instruction, operand, touched);
                }
            }
        }

        bool changed;
        do
        {
            changed = false;
            foreach (D3D10Instruction instruction in instructions)
            {
                if (instruction.Opcode != D3D10Opcode.Mov && instruction.Opcode != D3D10Opcode.MovC)
                {
                    continue;
                }
                if (instruction.GetOperandType(0) != OperandType.Temp)
                {
                    continue;
                }
                RegisterKey destination = instruction.GetParamRegisterKey(0);
                int writeMask = instruction.GetDestinationWriteMask();
                int firstValue = instruction.Opcode == D3D10Opcode.Mov ? 1 : 2;
                for (int component = 0; component < 4; component++)
                {
                    if ((writeMask & (1 << component)) == 0)
                    {
                        continue;
                    }
                    var destinationComponent = new RegisterComponentKey(destination, component);
                    var sources = new List<RegisterComponentKey>();
                    for (int operand = firstValue; operand < instruction.OperandTokens.OperandCount; operand++)
                    {
                        if (instruction.GetOperandType(operand) == OperandType.Temp)
                        {
                            sources.Add(new RegisterComponentKey(
                                instruction.GetParamRegisterKey(operand),
                                instruction.GetSourceSwizzleComponents(operand)[component]));
                        }
                    }
                    if (sources.Any(touched.Contains))
                    {
                        changed |= touched.Add(destinationComponent);
                    }
                }
            }
        }
        while (changed);

        return touched;
    }

    private static void AddWrittenComponents(
        D3D10Instruction instruction, int operand, HashSet<RegisterComponentKey> components)
    {
        RegisterKey key = instruction.GetParamRegisterKey(operand);
        int writeMask = instruction.GetWriteMask(operand);
        for (int component = 0; component < 4; component++)
        {
            if ((writeMask & (1 << component)) != 0)
            {
                components.Add(new RegisterComponentKey(key, component));
            }
        }
    }

    private static void AddReadComponents(
        D3D10Instruction instruction, int operand, HashSet<RegisterComponentKey> components)
    {
        RegisterKey key = instruction.GetParamRegisterKey(operand);
        foreach (byte component in instruction.GetSourceSwizzleComponents(operand).Distinct())
        {
            components.Add(new RegisterComponentKey(key, component));
        }
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
        // An interlocked operation over the array settles it on its own: there is no
        // atomic over a float, so an array one reaches holds integers whatever the
        // stores into it look like. A histogram's bins are cleared with a store of
        // l(0), whose bits say nothing either way.
        if (instructions.Any(instruction => instruction.Opcode.IsAtomic()
            && instruction.GetOperandType(0) == OperandType.ThreadGroupSharedMemory
            && instruction.GetParamRegisterNumber(0) == register))
        {
            return true;
        }
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
        // The resource is the last operand of a load and the first of a store.
        int ResourceIndex = load.Opcode == D3D10Opcode.StoreStructured ? 0 : 3;
        OperandType type = load.GetOperandType(ResourceIndex);
        D3DShaderInputType inputType = type switch
        {
            OperandType.Resource => D3DShaderInputType.Structured,
            OperandType.UnorderedAccessView => D3DShaderInputType.UavRWStructured,
            _ => (D3DShaderInputType)(-1),
        };
        ResourceDefinition definition = _shader.ResourceDefinitions?
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
        // An output the signature types as an integer - a viewport or render
        // target index - holds one whatever is moved into it: `mov o1.x, l(1)`
        // is the integer 1, and as a float it is a denormal that prints as 0.
        IEnumerable<RegisterSignature> typedSignatures = shader.Type != ShaderType.Geometry
            ? shader.InputSignatures.Concat(shader.OutputSignatures)
            : shader.OutputSignatures;
        {
            foreach (RegisterSignature signature in typedSignatures)
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

        // The registers a shader is given rather than declared - thread and group
        // indices, the primitive id - are unsigned integers by definition, whatever
        // reads them: `and r0.xyz, vPrim, l(3, 1, 2, 0)` says nothing about its
        // operands' type, and would have left the id a float.
        foreach (D3D10Instruction instruction in shader.Instructions.OfType<D3D10Instruction>())
        {
            for (int operand = 0; operand < instruction.OperandTokens.Count; operand++)
            {
                if (D3D10Instruction.IsThreadRegister(instruction.GetOperandType(operand)))
                {
                    RegisterKey key = instruction.GetParamRegisterKey(operand);
                    for (int component = 0; component < 4; component++)
                    {
                        integerRegisters.Add(new RegisterComponentKey(key, component));
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
            D3D10Opcode.Mov or D3D10Opcode.Not => 1,
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
            // An interlocked operation reads an address and a value, both integers.
            // Without these the uint a structured buffer load put in a register was
            // not known to be one, and the register was declared float: the value
            // went into the atomic through a conversion each way and came out as the
            // float its bits spell.
            case D3D10Opcode.AtomicIAdd:
            case D3D10Opcode.AtomicAnd:
            case D3D10Opcode.AtomicOr:
            case D3D10Opcode.AtomicXor:
            case D3D10Opcode.AtomicIMax:
            case D3D10Opcode.AtomicIMin:
            case D3D10Opcode.AtomicUMax:
            case D3D10Opcode.AtomicUMin:
                return 2;
            case D3D10Opcode.IMad:
            case D3D10Opcode.AtomicCmpStore:
                return 3;
            default:
                return 0;
        }
    }
}

/// <summary>How the instruction writer keeps a register component. See
/// <see cref="IntegerOperandAnalysis.GetStorage"/>.</summary>
public enum ComponentStorage
{
    /// <summary>In a register declared int.</summary>
    Integer,
    /// <summary>In a float register, as its value: an integer as the float equal to it.</summary>
    Numeric,
    /// <summary>In a float register, as its bits: an integer with asint and asfloat around it.</summary>
    Bits,
}
