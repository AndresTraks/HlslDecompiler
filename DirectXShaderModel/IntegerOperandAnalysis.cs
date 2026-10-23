using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.DirectXShaderModel;

public sealed class IntegerOperandAnalysis
{
    private HashSet<RegisterComponentKey> _integerRegisters;
    private HashSet<RegisterComponentKey> _doubleRegisters;
    private HashSet<RegisterComponentKey> _floatRegisters;
    private HashSet<RegisterComponentKey> _bitsRegisters;
    private HashSet<RegisterComponentKey> _maskRegisters;
    private HashSet<RegisterKey> _integerDeclaredRegisters;
    private HashSet<RegisterComponentKey> _integerOutputSignatures;
    private HashSet<RegisterComponentKey> _convertedToFloat;
    private HashSet<RegisterComponentKey> _floatReadRegisters;
    private HashSet<RegisterComponentKey> _floatMovedRegisters;
    private readonly HashSet<int> _sharedMemoryBeingTyped = [];

    /// <summary>How many moves a value is followed through; bounds the search.</summary>
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

        // A raw store writes bits; every dword is a uint whatever it was before.
        if (instruction.Opcode == D3D10Opcode.StoreRaw)
        {
            return true;
        }

        // A coverage mask or stencil reference is a uint whatever writes it;
        // nothing reads oMask or oStencilRef afterwards to settle a move into it.
        int? coverageDestination = instruction.GetDestinationParamIndex();
        if (coverageDestination != null
            && instruction.GetOperandType(coverageDestination.Value)
                is OperandType.OutputCoverageMask or OperandType.OutputStencilRef)
        {
            return true;
        }

        _integerRegisters ??= FindIntegerRegisters(_shader);
        return IsIntegerMove(instruction);
    }

    // Whether a register component only ever holds an integer.
    public bool IsIntegerRegister(RegisterComponentKey registerComponent)
    {
        _integerRegisters ??= FindIntegerRegisters(_shader);
        return _integerRegisters.Contains(registerComponent);
    }

    /// <summary>
    /// Whether a register component holds a double, keyed on the lower of its two
    /// components. Only double instructions writing or reading it say so.
    /// </summary>
    public bool IsDoubleRegister(RegisterComponentKey registerComponent)
    {
        _doubleRegisters ??= FindDoubleRegisters(_shader);
        return _doubleRegisters.Contains(registerComponent);
    }

    private static HashSet<RegisterComponentKey> FindDoubleRegisters(ShaderModel shader)
    {
        var doubleRegisters = new HashSet<RegisterComponentKey>();
        foreach (Instruction instruction in shader.Instructions)
        {
            if (instruction is not D3D10Instruction d3d10 || !d3d10.HasDoubleOperands)
            {
                continue;
            }
            if (d3d10.WritesDoubles && d3d10.GetDestinationParamIndex() is int destination)
            {
                AddDoublePairs(doubleRegisters, d3d10, destination,
                    d3d10.GetDestinationWriteMask());
            }
            // Sources too: a double a load left in a register is never written by a
            // double instruction.
            for (int operand = 1; operand <= 3; operand++)
            {
                if (!d3d10.IsDoubleOperand(operand))
                {
                    continue;
                }
                byte[] swizzle = d3d10.GetSourceSwizzleComponents(operand);
                AddDoublePairs(doubleRegisters, d3d10, operand, 1 << swizzle[0]);
            }
        }
        return doubleRegisters;
    }

    private static void AddDoublePairs(
        HashSet<RegisterComponentKey> doubleRegisters,
        D3D10Instruction instruction,
        int operandIndex,
        int mask)
    {
        RegisterKey registerKey = instruction.GetParamRegisterKey(operandIndex);
        if (registerKey == null)
        {
            return;
        }
        for (int component = 0; component < 4; component += 2)
        {
            if ((mask & (1 << component)) != 0)
            {
                doubleRegisters.Add(new RegisterComponentKey(registerKey, component));
            }
        }
    }

    /// <summary>
    /// How the instruction writer keeps a temp register component. A register any
    /// of whose components holds bits is declared int, its floats held as bits
    /// (asfloat/asint). Not the other way about: fxc flushes a denormal through
    /// asfloat to zero, losing a small integer's bits.
    /// </summary>
    public ComponentStorage GetStorage(RegisterComponentKey registerComponent)
    {
        if (!registerComponent.RegisterKey.IsTempRegister)
        {
            return IsIntegerRegister(registerComponent) ? ComponentStorage.Integer : ComponentStorage.Numeric;
        }
        return IsIntegerDeclaredRegister(registerComponent.RegisterKey)
            ? ComponentStorage.Integer
            : ComponentStorage.Numeric;
    }

    /// <summary>
    /// Whether the component carries bits rather than a number: a bitwise operator
    /// touches it, or it is a comparison mask not otherwise an integer.
    /// </summary>
    private bool HoldsBits(RegisterComponentKey registerComponent)
    {
        return IsBitsTouched(registerComponent)
            || (IsMask(registerComponent) && !IsIntegerRegister(registerComponent));
    }

    /// <summary>
    /// Whether an int register's component holds a float as its bits: a float
    /// instruction writes or reads it, an itof writes it, or a mov carries a float
    /// in. A float reader then wants asfloat, not a conversion.
    /// </summary>
    public bool HoldsFloatBits(RegisterComponentKey registerComponent)
    {
        return registerComponent.RegisterKey.IsTempRegister
            && IsIntegerDeclaredRegister(registerComponent.RegisterKey)
            && (IsFloatTouched(registerComponent)
                || IsConvertedToFloat(registerComponent)
                || IsFloatRead(registerComponent)
                || IsFloatMoved(registerComponent));
    }

    /// <summary>
    /// Whether a mov carries a float into the component, judged by its source.
    /// </summary>
    private bool IsFloatMoved(RegisterComponentKey registerComponent)
    {
        _floatMovedRegisters ??= FindFloatMovedRegisters();
        return _floatMovedRegisters.Contains(registerComponent);
    }

    private HashSet<RegisterComponentKey> FindFloatMovedRegisters()
    {
        var moved = new HashSet<RegisterComponentKey>();
        bool changed;
        do
        {
            changed = false;
            foreach (D3D10Instruction instruction in _shader.Instructions.OfType<D3D10Instruction>())
            {
                if (instruction.Opcode is not (D3D10Opcode.Mov or D3D10Opcode.MovC)
                    || instruction.GetOperandType(0) != OperandType.Temp)
                {
                    continue;
                }
                int writeMask = instruction.GetDestinationWriteMask();
                int firstValue = instruction.Opcode == D3D10Opcode.Mov ? 1 : 2;
                for (int component = 0; component < 4; component++)
                {
                    if ((writeMask & (1 << component)) == 0)
                    {
                        continue;
                    }
                    var destination = new RegisterComponentKey(
                        instruction.GetParamRegisterKey(0), component);
                    for (int operand = firstValue; operand < instruction.OperandTokens.OperandCount; operand++)
                    {
                        if (instruction.GetOperandType(operand) == OperandType.Immediate32)
                        {
                            // An immediate is typed by its readers.
                            if (GetImmediateKindByReaders(instruction) == ValueKind.Float)
                            {
                                changed |= moved.Add(destination);
                            }
                            continue;
                        }
                        var source = new RegisterComponentKey(
                            instruction.GetParamRegisterKey(operand),
                            instruction.GetSourceSwizzleComponents(operand)[component]);
                        bool sourceIsFloat = instruction.GetOperandType(operand) switch
                        {
                            OperandType.Temp => IsFloatTouched(source)
                                || IsConvertedToFloat(source)
                                || moved.Contains(source)
                                || !IsIntegerDeclaredRegister(source.RegisterKey),
                            OperandType.IndexableTemp =>
                                !IsIntegerIndexableTemp(source.RegisterKey.Number),
                            _ => !IsIntegerRegister(source),
                        };
                        if (sourceIsFloat)
                        {
                            changed |= moved.Add(destination);
                        }
                    }
                }
            }
        }
        while (changed);
        return moved;
    }

    private bool IsFloatRead(RegisterComponentKey registerComponent)
    {
        _floatReadRegisters ??= FindFloatReadRegisters();
        return _floatReadRegisters.Contains(registerComponent);
    }

    private HashSet<RegisterComponentKey> FindFloatReadRegisters()
    {
        var read = new HashSet<RegisterComponentKey>();
        foreach (D3D10Instruction instruction in _shader.Instructions.OfType<D3D10Instruction>())
        {
            if (instruction.Opcode.IsDeclaration() || instruction.Opcode.ConsumedKind() != ValueKind.Float)
            {
                continue;
            }
            for (int operand = 0; operand < instruction.OperandTokens.OperandCount; operand++)
            {
                if (instruction.GetOperandType(operand) == OperandType.Temp
                    && !instruction.IsDestinationOperand(operand))
                {
                    AddReadComponents(instruction, operand, read);
                }
            }
        }
        return read;
    }

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

    /// <summary>Whether a temp register is declared int: all its written components
    /// are float-free integers, or any holds bits.</summary>
    public bool IsIntegerDeclaredRegister(RegisterKey registerKey)
    {
        _integerDeclaredRegisters ??= FindIntegerDeclaredRegisters();
        return _integerDeclaredRegisters.Contains(registerKey);
    }

    /// <summary>Whether a float instruction writes the component, directly or
    /// through a move.</summary>
    public bool IsFloatTouched(RegisterComponentKey registerComponent)
    {
        _floatRegisters ??= FindTouchedRegisters(ValueKind.Float);
        return _floatRegisters.Contains(registerComponent);
    }

    /// <summary>Whether a bitwise operator reads or writes the component, directly
    /// or through a move.</summary>
    public bool IsBitsTouched(RegisterComponentKey registerComponent)
    {
        _bitsRegisters ??= FindTouchedRegisters(ValueKind.Bits);
        return _bitsRegisters.Contains(registerComponent);
    }

    /// <summary>What ld_structured reads or store_structured writes, by the
    /// buffer's reflected element type.</summary>
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
    /// Whether the output signature alone types a component as an integer. Unlike
    /// IsIntegerRegister, an integer instruction writing a float output does not
    /// count: it writes a float's bits.
    /// </summary>
    public bool IsIntegerOutputSignature(RegisterComponentKey registerComponent)
    {
        _integerOutputSignatures ??= FindIntegerOutputSignatures();
        if (_integerOutputSignatures.Contains(registerComponent))
        {
            return true;
        }
        // A multi-stream geometry shader's output operand names the stream it is
        // emitted to (o1 on stream 2), which the signature key does not: match the
        // register and that stream's own entry, or o1 reads as stream 0's float.
        if (registerComponent.RegisterKey is not D3D10RegisterKey { Stream: int stream } key)
        {
            return false;
        }
        const int UInt32ComponentType = 1;
        const int SInt32ComponentType = 2;
        return _shader.OutputSignatures.Any(s =>
            s.ComponentType is UInt32ComponentType or SInt32ComponentType
            && s.RegisterKey.OperandType == key.OperandType
            && s.RegisterKey.Number == key.Number
            && (s.Mask & (1 << registerComponent.ComponentIndex)) != 0
            && s.Stream == stream);
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
    /// What ld and ldms read: a texel of the resource's declared type, an integer
    /// for a Texture2D&lt;uint4&gt;.
    /// </summary>
    public ValueKind GetTypedLoadKind(D3D10Instruction load)
    {
        return LoadsIntegerTexels(_shader, load) ? ValueKind.Integer : ValueKind.Float;
    }

    /// <summary>
    /// What a typed store writes: a texel of the view's declared type. HLSL
    /// converts a float stored into an integer texel by value, so this must not
    /// default to float.
    /// </summary>
    public ValueKind GetTypedStoreKind(D3D10Instruction store)
    {
        return LoadedElementType(store) == StoredType.Integer
            ? ValueKind.Integer
            : ValueKind.Float;
    }

    /// <summary>
    /// The kind of the immediate a mov or movc writes, by the readers of its
    /// destination. Unknown where they disagree or there are none.
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
    /// The same, per destination component. Scans forward until a component is
    /// rewritten at the same or a shallower nesting level; a deeper write may be
    /// skipped at run time, so the scan looks past it.
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
            // Sources are read before the destination is written, so kill
            // overwritten components only after counting this instruction's reads.
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
                // An immediate a branch tests is a 0/-1 flag: an integer.
                if (reader.Opcode.HasBooleanTest())
                {
                    consumed = ValueKind.Integer;
                }
                // A bitwise operator reads bits, which an int variable holds.
                if (reader.Opcode is D3D10Opcode.And or D3D10Opcode.Or
                    or D3D10Opcode.Xor or D3D10Opcode.Not)
                {
                    consumed = ValueKind.Integer;
                }
                if (consumed != ValueKind.Integer && consumed != ValueKind.Float)
                {
                    // A mov or movc carries the value on; its own readers type it.
                    // A movc's first source is its condition, not a carried value.
                    if (reader.Opcode is D3D10Opcode.Mov or D3D10Opcode.MovC
                        && !(reader.Opcode == D3D10Opcode.MovC && operand == 1))
                    {
                        int? readerDestination = reader.GetDestinationParamIndex();
                        if (readerDestination != null
                            && reader.GetOperandType(readerDestination.Value) != OperandType.Temp)
                        {
                            // A move into an output is typed by the output signature.
                            RegisterKey outputRegister =
                                reader.GetParamRegisterKey(readerDestination.Value);
                            foreach (byte component in reader.GetSourceSwizzleComponents(operand).Distinct())
                            {
                                if ((live & (1 << component)) == 0 || disagree[component])
                                {
                                    continue;
                                }
                                ValueKind outputKind = IsIntegerOutputSignature(
                                    new RegisterComponentKey(outputRegister, component))
                                    ? ValueKind.Integer
                                    : ValueKind.Float;
                                if (kinds[component] != ValueKind.Unknown
                                    && kinds[component] != outputKind)
                                {
                                    kinds[component] = ValueKind.Unknown;
                                    disagree[component] = true;
                                    continue;
                                }
                                kinds[component] = outputKind;
                            }
                            continue;
                        }
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
    /// Types the components a carrying move reads by the readers of its destination.
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
        // The set is the current path, not everything seen, so each source of a
        // move is carried on.
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
        HashSet<RegisterComponentKey> integerWritten = FindIntegerWrittenComponents();
        return [.. FindWrittenTempComponents()
            .Where(r => r.Value.Any(IsIntegerRegister)
                || r.Value.Any(HoldsBits)
                || r.Value.Any(integerWritten.Contains))
            .Select(r => r.Key)];
    }

    /// <summary>
    /// Temp components some write leaves integer bits in, though the component is
    /// not an integer register throughout: a structured load of an integer element
    /// (its words may go on to be a double's halves), or a mov/movc that selects an
    /// integer beside a float. Declaring their register float would carry those
    /// bits through asfloat, where fxc flushes a small integer's denormal to zero.
    /// Only the declaration reads this, so the AST writer's typing is untouched.
    /// </summary>
    private HashSet<RegisterComponentKey> FindIntegerWrittenComponents()
    {
        var written = new HashSet<RegisterComponentKey>();
        foreach (D3D10Instruction instruction in _shader.Instructions.OfType<D3D10Instruction>())
        {
            if (!instruction.HasDestination
                || instruction.GetOperandType(0) != OperandType.Temp)
            {
                continue;
            }
            bool integer = instruction.Opcode switch
            {
                D3D10Opcode.LdStructured => GetStructuredElementKind(instruction) == ValueKind.Integer,
                D3D10Opcode.Mov or D3D10Opcode.MovC => SelectsInteger(instruction),
                _ => false,
            };
            if (integer)
            {
                AddDestinationComponents(instruction, written);
            }
        }
        return written;
    }

    // Whether a mov/movc value operand reads an integer register component.
    private bool SelectsInteger(D3D10Instruction instruction)
    {
        int firstValue = instruction.Opcode == D3D10Opcode.Mov ? 1 : 2;
        for (int operand = firstValue; operand < instruction.OperandTokens.OperandCount; operand++)
        {
            if (instruction.GetOperandType(operand) is OperandType.Immediate32 or OperandType.Immediate64)
            {
                continue;
            }
            RegisterKey key = instruction.GetParamRegisterKey(operand);
            if (key != null && instruction.GetSourceSwizzleComponents(operand).Distinct()
                .Any(c => IsIntegerRegister(new RegisterComponentKey(key, c))))
            {
                return true;
            }
        }
        return false;
    }

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
    /// Whether a component holds bits and nothing else. Bits kept in a float
    /// register do not survive fxc, which flushes denormals through asfloat.
    /// </summary>
    private bool IsBitsOnly(RegisterComponentKey registerComponent)
    {
        return (IsBitsTouched(registerComponent)
                || (IsMask(registerComponent) && !IsIntegerRegister(registerComponent)))
            && !IsFloatTouched(registerComponent)
            && !IsConvertedToFloat(registerComponent);
    }

    /// <summary>
    /// Whether itof or utof writes the component. They are not float-touching (an
    /// int register holds their whole number), but they write a number, not bits.
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
    // writes, followed forward through moves. Float writes, not reads: a float
    // read of an int register converts correctly, a float write truncates. itof
    // and utof are excluded; a moved immediate counts as whatever reads it.
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
            else if (instruction.Opcode is D3D10Opcode.LD or D3D10Opcode.LDMS or D3D10Opcode.LdUAVTyped)
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
    /// Whether an indexable temp holds integers. Its elements share one key, so
    /// the register rule cannot answer for it.
    /// </summary>
    public bool IsIntegerIndexableTemp(int register)
    {
        _integerRegisters ??= FindIntegerRegisters(_shader);
        return IsIntegerIndexableTemp(register, _integerRegisters);
    }

    /// <summary>
    /// Whether the integers a groupshared array holds are unsigned. An atomic
    /// min/max decides it (HLSL picks umin or imin from the declared type);
    /// otherwise uint where some store is unsigned and none signed.
    /// </summary>
    public bool IsUnsignedThreadGroupSharedMemory(int register)
    {
        var instructions = _shader.Instructions.OfType<D3D10Instruction>().ToList();
        if (instructions.Any(instruction =>
            instruction.Opcode is D3D10Opcode.AtomicUMin or D3D10Opcode.AtomicUMax
                or D3D10Opcode.ImmAtomicUMin or D3D10Opcode.ImmAtomicUMax
            && IsThreadGroupSharedAtomicOn(instruction, register)))
        {
            return true;
        }
        if (instructions.Any(instruction =>
            instruction.Opcode is D3D10Opcode.AtomicIMin or D3D10Opcode.AtomicIMax
                or D3D10Opcode.ImmAtomicIMin or D3D10Opcode.ImmAtomicIMax
            && IsThreadGroupSharedAtomicOn(instruction, register)))
        {
            return false;
        }
        const int ValueIndex = 3;
        Signedness stored = Signedness.Unknown;
        for (int i = 0; i < instructions.Count; i++)
        {
            D3D10Instruction store = instructions[i];
            if (store.Opcode == D3D10Opcode.StoreStructured
                && store.GetOperandType(0) == OperandType.ThreadGroupSharedMemory
                && store.GetParamRegisterNumber(0) == register)
            {
                stored |= StoredSignedness(instructions, i, ValueIndex, depth: 0);
            }
        }
        return stored == Signedness.Unsigned;
    }

    [System.Flags]
    private enum Signedness { Unknown = 0, Unsigned = 1, Signed = 2 }

    private const int SignednessDepth = 8;

    // The operand's sign, by the instructions that made it. Sign-neutral ones (a
    // move, an add, a bitwise operator) pass the question to their operands.
    // Straight-line order, as StoredValueType.
    private Signedness StoredSignedness(List<D3D10Instruction> instructions, int index,
        int operandIndex, int depth)
    {
        D3D10Instruction reader = instructions[index];
        OperandType type = reader.GetOperandType(operandIndex);
        if (type == OperandType.Immediate32 || depth > SignednessDepth)
        {
            return Signedness.Unknown;
        }
        if (type != OperandType.Temp)
        {
            return DeclaredSignedness(reader, operandIndex);
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
            // Either of a udiv's or an imul's two destinations may be the maker.
            int[] destinations = writer.HasSecondDestination() ? [0, 1] : [writer.GetDestinationParamIndex().Value];
            if (!destinations.Any(destination => writer.GetOperandType(destination) != OperandType.Null
                && writer.GetParamRegisterKey(destination).Equals(source)
                && (writer.GetWriteMask(destination) & (1 << component)) != 0))
            {
                continue;
            }
            // The sources follow every destination.
            int firstSource = destinations[^1] + 1;
            switch (writer.Opcode)
            {
                case D3D10Opcode.UShr or D3D10Opcode.UMin or D3D10Opcode.UMax
                    or D3D10Opcode.Udiv or D3D10Opcode.Ftou or D3D10Opcode.UBFE
                    or D3D10Opcode.ImmAtomicAlloc or D3D10Opcode.ImmAtomicConsume:
                    return Signedness.Unsigned;
                case D3D10Opcode.IShr or D3D10Opcode.IMin or D3D10Opcode.IMax
                    or D3D10Opcode.Ftoi or D3D10Opcode.IBFE:
                    return Signedness.Signed;
                case D3D10Opcode.LdStructured:
                    return LoadedElementSignedness(writer);
                case D3D10Opcode.Mov:
                    return StoredSignedness(instructions, i, 1, depth + 1);
                case D3D10Opcode.MovC:
                    return StoredSignedness(instructions, i, 2, depth + 1)
                        | StoredSignedness(instructions, i, 3, depth + 1);
                case D3D10Opcode.And or D3D10Opcode.Or or D3D10Opcode.Xor
                    or D3D10Opcode.IAdd or D3D10Opcode.IMul or D3D10Opcode.IShl:
                    return StoredSignedness(instructions, i, firstSource, depth + 1)
                        | StoredSignedness(instructions, i, firstSource + 1, depth + 1);
                case D3D10Opcode.Not:
                    return StoredSignedness(instructions, i, firstSource, depth + 1);
                default:
                    return Signedness.Unknown;
            }
        }
        return Signedness.Unknown;
    }

    // An input or a constant's sign, by its declaration.
    private Signedness DeclaredSignedness(D3D10Instruction reader, int operandIndex)
    {
        OperandType type = reader.GetOperandType(operandIndex);
        if (D3D10Instruction.IsThreadRegister(type))
        {
            return Signedness.Unsigned;
        }
        RegisterKey key = reader.GetParamRegisterKey(operandIndex);
        int component = reader.GetSourceSwizzleComponents(operandIndex)[0];
        if (type == OperandType.Input)
        {
            RegisterSignature signature = _shader.InputSignatures.FirstOrDefault(s =>
                s.RegisterKey.Equals(key) && (s.Mask & (1 << component)) != 0);
            return signature?.ComponentType switch
            {
                1 => Signedness.Unsigned,
                2 => Signedness.Signed,
                _ => Signedness.Unknown,
            };
        }
        if (type == OperandType.ConstantBuffer && key is D3D10RegisterKey constantKey)
        {
            int offset = constantKey.ConstantBufferOffset.GetValueOrDefault() * 16 + component * 4;
            D3D10ConstantDeclaration constant = _shader.ConstantDeclarations
                .OfType<D3D10ConstantDeclaration>()
                .FirstOrDefault(c => c.RegisterIndex == constantKey.Number
                    && offset >= c.VariableOffset && offset < c.VariableOffset + c.VariableSize);
            return constant?.TypeInfo.ParameterType switch
            {
                ParameterType.Uint => Signedness.Unsigned,
                ParameterType.Int => Signedness.Signed,
                _ => Signedness.Unknown,
            };
        }
        return Signedness.Unknown;
    }

    // Nothing for groupshared memory, which is the question being asked.
    private Signedness LoadedElementSignedness(D3D10Instruction load)
    {
        const int ResourceIndex = 3;
        OperandType type = load.GetOperandType(ResourceIndex);
        D3DShaderInputType[] inputTypes = type switch
        {
            OperandType.Resource => [D3DShaderInputType.Structured],
            OperandType.UnorderedAccessView =>
            [
                D3DShaderInputType.UavRWStructured,
                D3DShaderInputType.UavRWStucturedWithCounter,
                D3DShaderInputType.UavAppendStructured,
                D3DShaderInputType.UavConsumeStructured,
            ],
            _ => [],
        };
        ResourceDefinition definition = _shader.ResourceDefinitions?
            .FirstOrDefault(d => inputTypes.Contains(d.ShaderInputType)
                && d.BindPoint == load.GetParamRegisterNumber(ResourceIndex));
        return definition?.ElementType?.ParameterType switch
        {
            ParameterType.Uint => Signedness.Unsigned,
            ParameterType.Int => Signedness.Signed,
            _ => Signedness.Unknown,
        };
    }

    // The imm_ forms put the returned old value first, so the memory is operand 1.
    private static bool IsThreadGroupSharedAtomicOn(D3D10Instruction instruction, int register)
    {
        int destination = instruction.Opcode.IsAtomic() ? 0 : 1;
        return instruction.GetOperandType(destination) == OperandType.ThreadGroupSharedMemory
            && instruction.GetParamRegisterNumber(destination) == register;
    }

    public bool IsIntegerThreadGroupSharedMemory(int register)
    {
        const int ValueIndex = 3;
        var instructions = _shader.Instructions.OfType<D3D10Instruction>().ToList();
        // Raw shared memory holds bits: declared uint, a float read asks for asfloat.
        if (instructions.Any(instruction =>
            instruction.Opcode == D3D10Opcode.DclThreadGroupSharedMemoryRaw
            && instruction.GetParamRegisterNumber(0) == register))
        {
            return true;
        }
        // Any atomic over the array settles it: there is no float atomic.
        if (instructions.Any(instruction =>
            (instruction.Opcode.IsAtomic() || instruction.Opcode.IsImmediateAtomic())
            && instruction.GetOperandType(instruction.Opcode.IsAtomic() ? 0 : 1)
                == OperandType.ThreadGroupSharedMemory
            && instruction.GetParamRegisterNumber(instruction.Opcode.IsAtomic() ? 0 : 1) == register))
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

    /// <summary>Operand types that are a uint by definition: thread ids and
    /// coverage masks.</summary>
    private static bool IsIntegerSystemValue(OperandType type)
    {
        return type is OperandType.InputThreadID or OperandType.InputThreadGroupID
            or OperandType.InputThreadIDInGroup
            or OperandType.InputThreadIDInGroupFlattened
            or OperandType.InputCoverageMask or OperandType.InputInnerCoverage
            or OperandType.InputGSInstanceID;
    }

    private enum StoredType { Unknown, Integer, Float }

    // A denormal, infinity or NaN: bits no shader writes as a float literal.
    public static bool IsDenormalOrNaN(uint bits)
    {
        uint exponent = (bits >> 23) & 0xFF;
        return (exponent == 0 && (bits & 0x7FFFFFFF) != 0) || exponent == 0xFF;
    }

    // What the operand holds, by the instruction that last wrote it in listing
    // order (no flow graph: after a join, the last write in the listing wins).
    private StoredType StoredValueType(List<D3D10Instruction> instructions, int index, int operandIndex)
    {
        D3D10Instruction reader = instructions[index];
        OperandType type = reader.GetOperandType(operandIndex);
        if (type == OperandType.Immediate32)
        {
            // Denormal/NaN bits cannot come from a float (fxc flushes even
            // asfloat(3) to 0), so they mark an integer store. Others say nothing.
            return IsDenormalOrNaN((uint)reader.GetParamInt(operandIndex, 0))
                ? StoredType.Integer
                : StoredType.Unknown;
        }
        if (type != OperandType.Temp)
        {
            // Thread ids and coverage masks are uints, and nothing else says so.
            if (IsIntegerSystemValue(type))
            {
                return StoredType.Integer;
            }
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
            // A typed load is typed by the view's return type. Must precede the
            // IsInteger test, which counts ld/ldms as integer by their address.
            if (writer.Opcode is D3D10Opcode.LD or D3D10Opcode.LDMS
                or D3D10Opcode.LdUAVTyped)
            {
                return LoadedElementType(writer);
            }
            if (writer.Opcode.IsInteger() || writer.Opcode == D3D10Opcode.Ftoi || writer.Opcode == D3D10Opcode.Ftou)
            {
                return StoredType.Integer;
            }
            // A bitwise operator writes bits, which an int variable holds.
            if (writer.Opcode is D3D10Opcode.And or D3D10Opcode.Or or D3D10Opcode.Xor
                or D3D10Opcode.Not)
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

    // The element type a load reads or a store writes, by reflection data.
    private StoredType LoadedElementType(D3D10Instruction load)
    {
        // A typed view or texture is typed by its return type.
        if (load.Opcode is D3D10Opcode.StoreUAVTyped or D3D10Opcode.LdUAVTyped
            or D3D10Opcode.LD or D3D10Opcode.LDMS)
        {
            bool isTexture = load.Opcode is D3D10Opcode.LD or D3D10Opcode.LDMS;
            int typedIndex = load.Opcode == D3D10Opcode.StoreUAVTyped ? 0 : 2;
            ResourceDefinition view = _shader.ResourceDefinitions?
                .FirstOrDefault(d => d.ShaderInputType == (isTexture
                        ? D3DShaderInputType.Texture
                        : D3DShaderInputType.UavRWTyped)
                    && d.BindPoint == load.GetParamRegisterNumber(typedIndex));
            return view?.ResourceReturnType switch
            {
                D3DResourceReturnType.UInt or D3DResourceReturnType.SInt => StoredType.Integer,
                D3DResourceReturnType.Float or D3DResourceReturnType.UNorm
                    or D3DResourceReturnType.SNorm => StoredType.Float,
                _ => StoredType.Unknown,
            };
        }
        // The resource is the last operand of a load and the first of a store.
        int ResourceIndex = load.Opcode == D3D10Opcode.StoreStructured ? 0 : 3;
        OperandType type = load.GetOperandType(ResourceIndex);
        // Groupshared memory has no reflection data; the declaration rule answers.
        if (type == OperandType.ThreadGroupSharedMemory)
        {
            // A store of what the array itself holds (a sort's swap) says nothing;
            // the array's other stores decide. Asking again would not end.
            int register = load.GetParamRegisterNumber(ResourceIndex);
            if (!_sharedMemoryBeingTyped.Add(register))
            {
                return StoredType.Unknown;
            }
            try
            {
                return IsIntegerThreadGroupSharedMemory(register)
                    ? StoredType.Integer
                    : StoredType.Float;
            }
            finally
            {
                _sharedMemoryBeingTyped.Remove(register);
            }
        }
        D3DShaderInputType[] inputTypes = type switch
        {
            OperandType.Resource => [D3DShaderInputType.Structured],
            // Append and consume buffers bind as their own kinds.
            OperandType.UnorderedAccessView =>
            [
                D3DShaderInputType.UavRWStructured,
                D3DShaderInputType.UavRWStucturedWithCounter,
                D3DShaderInputType.UavAppendStructured,
                D3DShaderInputType.UavConsumeStructured,
            ],
            _ => [],
        };
        ResourceDefinition definition = _shader.ResourceDefinitions?
            .FirstOrDefault(d => inputTypes.Contains(d.ShaderInputType)
                && d.BindPoint == load.GetParamRegisterNumber(ResourceIndex));
        if (definition?.ElementType == null)
        {
            return StoredType.Unknown;
        }
        // A struct is typed by the members the instruction reaches.
        if (definition.ElementType.MemberInfo is { Count: > 0 } members)
        {
            return ReachedMembersType(load, members);
        }
        return definition.ElementType.ParameterType switch
        {
            ParameterType.Int or ParameterType.Uint or ParameterType.Bool => StoredType.Integer,
            ParameterType.Float => StoredType.Float,
            _ => StoredType.Unknown,
        };
    }

    // The type of the struct members a structured load or store reaches. Unknown
    // where they disagree or a member is not scalar. A computed offset reaches an
    // array member: typed where every array member is one type.
    private static StoredType ReachedMembersType(D3D10Instruction instruction, IList<ShaderStructMemberInfo> members)
    {
        const int OffsetIndex = 2;
        if (instruction.GetOperandType(OffsetIndex) != OperandType.Immediate32)
        {
            StoredType[] arrayTypes = [.. members
                .Where(m => m.TypeInfo.NumElements > 1 && m.TypeInfo.MemberInfo is not { Count: > 0 })
                .Select(m => m.TypeInfo.ParameterType switch
                {
                    ParameterType.Int or ParameterType.Uint or ParameterType.Bool => StoredType.Integer,
                    ParameterType.Float => StoredType.Float,
                    _ => StoredType.Unknown,
                })
                .Distinct()];
            return arrayTypes.Length == 1 ? arrayTypes[0] : StoredType.Unknown;
        }
        int byteOffset = instruction.GetParamInt(OffsetIndex, 0);
        IEnumerable<int> dwords;
        if (instruction.Opcode == D3D10Opcode.StoreStructured)
        {
            int mask = instruction.GetWriteMask(0);
            dwords = Enumerable.Range(0, 4).Where(c => (mask & (1 << c)) != 0);
        }
        else
        {
            int mask = instruction.GetWriteMask(0);
            byte[] swizzle = instruction.GetSourceSwizzleComponents(3);
            dwords = Enumerable.Range(0, 4).Where(c => (mask & (1 << c)) != 0).Select(c => (int)swizzle[c]);
        }
        StoredType type = StoredType.Unknown;
        foreach (int dword in dwords)
        {
            int at = byteOffset + dword * 4;
            // The leaf, inside an array of structs too (`l[3].r`).
            ShaderStructMemberInfo member = Hlsl.RegisterState.FindStructuredMember(members, at, 0)?.Member
                ?? members
                    .Where(m => m.ByteOffset <= at)
                    .OrderByDescending(m => m.ByteOffset)
                    .FirstOrDefault();
            StoredType memberType = member?.TypeInfo.ParameterType switch
            {
                ParameterType.Int or ParameterType.Uint or ParameterType.Bool => StoredType.Integer,
                ParameterType.Float => StoredType.Float,
                _ => StoredType.Unknown,
            };
            if (memberType == StoredType.Unknown || (type != StoredType.Unknown && type != memberType))
            {
                return StoredType.Unknown;
            }
            type = memberType;
        }
        return type;
    }

    private static bool LoadsIntegerTexels(ShaderModel shader, D3D10Instruction load)
    {
        const int ResourceIndex = 2;
        int resource = load.GetParamRegisterNumber(ResourceIndex);
        D3DShaderInputType type = load.Opcode == D3D10Opcode.LdUAVTyped
            ? D3DShaderInputType.UavRWTyped
            : D3DShaderInputType.Texture;
        return shader.ResourceDefinitions?
            .FirstOrDefault(d => d.ShaderInputType == type && d.BindPoint == resource)
            ?.IsIntegerReturnType == true;
    }

    // Integer where some store is an integer and none is float arithmetic: a mov
    // of a float (bits copied, `b[j] = asuint(a[k])`) or an immediate says nothing.
    private bool IsIntegerIndexableTemp(int register, HashSet<RegisterComponentKey> integerRegisters)
    {
        bool anyInteger = false;
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
            if (instruction.Opcode.IsInteger())
            {
                anyInteger = true;
                continue;
            }
            if (instruction.Opcode != D3D10Opcode.Mov)
            {
                return false;
            }
            if (instruction.GetOperandType(1) is OperandType.Immediate32 or OperandType.IndexableTemp)
            {
                continue;
            }
            RegisterKey source = instruction.GetParamRegisterKey(1);
            int writeMask = instruction.GetDestinationWriteMask();
            byte[] swizzle = instruction.GetSourceSwizzleComponents(1);
            for (int component = 0; component < 4; component++)
            {
                if ((writeMask & (1 << component)) == 0)
                {
                    continue;
                }
                var sourceComponent = new RegisterComponentKey(source, swizzle[component]);
                // Integer and not float as well: integer registers are shader-wide,
                // and fxc reuses a register for an address and a float.
                if (integerRegisters.Contains(sourceComponent) && !IsFloatTouched(sourceComponent))
                {
                    anyInteger = true;
                }
            }
        }
        return anyInteger;
    }

    private HashSet<RegisterComponentKey> FindIntegerRegisters(ShaderModel shader)
    {
        var integerRegisters = new HashSet<RegisterComponentKey>();

        // Signature-typed integer inputs and outputs. Geometry shader inputs are
        // keyed by vertex too, so they are left to their readers.
        const int UInt32ComponentType = 1;
        const int SInt32ComponentType = 2;
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

        // Constant buffer variables declared int, uint or bool.
        foreach (D3D10ConstantDeclaration constant in shader.ConstantDeclarations.OfType<D3D10ConstantDeclaration>())
        {
            if (constant.TypeInfo.ParameterType is not (ParameterType.Int or ParameterType.Uint or ParameterType.Bool))
            {
                continue;
            }
            for (int offset = constant.VariableOffset; offset < constant.VariableOffset + constant.VariableSize; offset += 4)
            {
                var key = new D3D10RegisterKey(OperandType.ConstantBuffer, constant.RegisterIndex, offset / 16);
                integerRegisters.Add(new RegisterComponentKey(key, offset % 16 / 4));
            }
        }

        // Thread and group ids and the primitive id are uints by definition. So is
        // a coverage mask, whose signature entry is keyed to register -1 and
        // matches no operand.
        foreach (D3D10Instruction instruction in shader.Instructions.OfType<D3D10Instruction>())
        {
            for (int operand = 0; operand < instruction.OperandTokens.Count; operand++)
            {
                OperandType operandType = instruction.GetOperandType(operand);
                if (D3D10Instruction.IsThreadRegister(operandType)
                    || operandType == OperandType.OutputCoverageMask
                    || operandType == OperandType.OutputStencilRef
                    || operandType == OperandType.InputCoverageMask)
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
                // f16tof32 writes a float, like itof.
                && instruction.Opcode != D3D10Opcode.F16ToF32
                && GetSourceCount(instruction.Opcode) != 0)
            {
                AddDestinationComponents(instruction, integerRegisters);
            }
            // resinfo_uint writes integer dimensions.
            if (instruction.Opcode == D3D10Opcode.ResInfo
                && instruction.ResInfoReturnType == D3D10ResInfoReturnType.Uint)
            {
                AddDestinationComponents(instruction, integerRegisters);
            }
            // A texel of a Texture2D<uint>, typed only by reflection data.
            if (instruction.Opcode is D3D10Opcode.LD or D3D10Opcode.LDMS or D3D10Opcode.LdUAVTyped
                && LoadsIntegerTexels(shader, instruction))
            {
                AddDestinationComponents(instruction, integerRegisters);
            }
            // A raw load returns uint dwords.
            if (instruction.Opcode == D3D10Opcode.LdRaw)
            {
                AddDestinationComponents(instruction, integerRegisters);
            }
            // The mirror of a structured load: a temp stored into an integer element
            // of a UAV holds an integer, whatever else (a movc, an immediate) wrote it.
            // UAVs only: their element types come from reflection, groupshared
            // memory's from this analysis.
            if (instruction.Opcode == D3D10Opcode.StoreStructured
                && instruction.GetOperandType(0) == OperandType.UnorderedAccessView
                && instruction.GetOperandType(3) == OperandType.Temp
                && GetStructuredElementKind(instruction) == ValueKind.Integer)
            {
                RegisterKey stored = instruction.GetParamRegisterKey(3);
                foreach (byte component in instruction.GetSourceSwizzleComponents(3)
                    .Take(System.Numerics.BitOperations.PopCount((uint)instruction.GetDestinationWriteMask())))
                {
                    integerRegisters.Add(new RegisterComponentKey(stored, component));
                }
            }
            // ftoi, ftou and f32tof16 read floats.
            if (instruction.Opcode != D3D10Opcode.Ftoi
                && instruction.Opcode != D3D10Opcode.Ftou
                && instruction.Opcode != D3D10Opcode.F32ToF16
                && GetSourceCount(instruction.Opcode) != 0)
            {
                AddSourceComponents(instruction, integerRegisters);
            }
            // Whatever indexes a local array is an integer.
            AddIndexableTempIndexComponents(instruction, integerRegisters);
        }

        // mov, and, or and xor take their type from what they touch; iterate to a
        // fixed point.
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
                // Per component: one mov can start a float and an integer at once.
                foreach (HashSet<RegisterComponentKey> group in GetComponentGroups(instruction, sourceCount))
                {
                    if (!group.Overlaps(integerRegisters))
                    {
                        continue;
                    }
                    foreach (RegisterComponentKey component in group)
                    {
                        // Inputs, constants and outputs are typed by their declarations
                        // only; a float input moved into an int register stays float,
                        // and so does an output fed from a reused register.
                        if (component.RegisterKey is D3D10RegisterKey
                            { OperandType: OperandType.Input or OperandType.ConstantBuffer or OperandType.Output })
                        {
                            continue;
                        }
                        changed |= integerRegisters.Add(component);
                    }
                }
            }
        }
        while (changed);

        // A register read out of an integer array is an integer. Must come after
        // the fixed point, which types the array.
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

        // An indexable temp's elements share one key; a mov through it would tie
        // every element to any register touching one.
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

    // Opcodes typed by their neighbours, with their source counts.
    private static int GetPolymorphicSourceCount(D3D10Opcode opcode)
    {
        return opcode switch
        {
            D3D10Opcode.Mov or D3D10Opcode.Not => 1,
            D3D10Opcode.And or D3D10Opcode.Or or D3D10Opcode.Xor => 2,
            _ => 0,
        };
    }

    // mov carries anything, and and/or/xor can select a float (`and r1, r1,
    // l(1.0)`), so these are judged by the registers they touch.
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
        // imul's high and low halves, uaddc's sum and carry: both are integers,
        // and either may be null.
        if (instruction.HasSecondDestination())
        {
            for (int index = 0; index < 2; index++)
            {
                if (instruction.GetOperandType(index) != OperandType.Null)
                {
                    AddDestinationComponents(instruction, index, instruction.GetWriteMask(index), components);
                }
            }
            return;
        }

        int? destinationIndex = instruction.GetDestinationParamIndex();
        if (destinationIndex == null)
        {
            return;
        }
        AddDestinationComponents(instruction, destinationIndex.Value, instruction.GetDestinationWriteMask(), components);
    }

    private static void AddDestinationComponents(D3D10Instruction instruction, int destinationIndex, int writeMask,
        HashSet<RegisterComponentKey> components)
    {
        RegisterKey registerKey = instruction.GetParamRegisterKey(destinationIndex);
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
        // The sources follow every destination: two of them for imul and uaddc.
        int firstSource = instruction.HasSecondDestination() ? 2 : 1;
        for (int source = firstSource; source < firstSource + sourceCount; source++)
        {
            if (instruction.GetOperandType(source) is OperandType.Immediate32
                or OperandType.UnorderedAccessView or OperandType.ThreadGroupSharedMemory)
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
            // f32tof16/f16tof32 count for their integer side only; the callers
            // exclude the float side, as for ftou.
            case D3D10Opcode.CountBits:
            case D3D10Opcode.FirstBitLo:
            case D3D10Opcode.FirstBitHi:
            case D3D10Opcode.FirstBitSHi:
            case D3D10Opcode.BFRev:
            case D3D10Opcode.F32ToF16:
            case D3D10Opcode.F16ToF32:
                return 1;
            case D3D10Opcode.BufInfo:
                return 0;
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
            // The unsigned relatives read the same two integer sources.
            case D3D10Opcode.UMul:
            case D3D10Opcode.UMax:
            case D3D10Opcode.UMin:
            case D3D10Opcode.UAddC:
            case D3D10Opcode.USubB:
            // The dividend and divisor, after the quotient and remainder.
            case D3D10Opcode.Udiv:
            // An atomic's address and value are integers.
            case D3D10Opcode.AtomicIAdd:
            case D3D10Opcode.AtomicAnd:
            case D3D10Opcode.AtomicOr:
            case D3D10Opcode.AtomicXor:
            case D3D10Opcode.AtomicIMax:
            case D3D10Opcode.AtomicIMin:
            case D3D10Opcode.AtomicUMax:
            case D3D10Opcode.AtomicUMin:
                return 2;
            // The imm_ forms have the old value first, shifting every operand by one.
            case D3D10Opcode.ImmAtomicAnd:
            case D3D10Opcode.ImmAtomicOr:
            case D3D10Opcode.ImmAtomicXor:
            case D3D10Opcode.ImmAtomicIMax:
            case D3D10Opcode.ImmAtomicIMin:
            case D3D10Opcode.ImmAtomicUMax:
            case D3D10Opcode.ImmAtomicUMin:
            case D3D10Opcode.ImmAtomicExch:
            case D3D10Opcode.ImmAtomicIAdd:
                return 3;
            case D3D10Opcode.ImmAtomicCmpExch:
                return 4;
            case D3D10Opcode.IMad:
            case D3D10Opcode.Umad:
            case D3D10Opcode.AtomicCmpStore:
            // The width, the offset and the value.
            case D3D10Opcode.UBFE:
            case D3D10Opcode.IBFE:
            // The reference, the windows and the accumulator.
            case D3D10Opcode.MSAD:
                return 3;
            // And the bits going in.
            case D3D10Opcode.BFI:
                return 4;
            default:
                return 0;
        }
    }
}

/// <summary>See <see cref="IntegerOperandAnalysis.GetStorage"/>.</summary>
public enum ComponentStorage
{
    /// <summary>In an int register; floats held as their bits.</summary>
    Integer,
    /// <summary>In a float register, by value.</summary>
    Numeric,
}
