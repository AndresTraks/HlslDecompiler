using System;

namespace HlslDecompiler.DirectXShaderModel;

// Instruction
// 80000000 extended opcode token
// 7F000000 instruction length
// 00FFF800 opcode specific control
// 00780000 precise value
// 00040000 boolean test
// 00002000 saturate mask
// 00001800 return type
// 000007FF shader instruction opcode

// Operand
// 80000000 extended operand token
// 7FC00000 index representation
// 00300000 index dimension
// 000FF000 operand type
// 00000FFC component selection
// 00000003 number of components

// Extended operand
// 80000000 extended operand token
// 7FFC0000 ignored
// 00020000 non-uniform
// 0001C000 min precision
// 00003FC0 operand modifier
// 0000003F extended operand type

// Resource return type token
// FFFF0000 reserved
// 0000F000 component W
// 00000F00 component Z
// 000000F0 component Y
// 0000000F component X

public class D3D10Instruction : Instruction
{
    private ResourceDimension _resourceDimension;
    private D3D10GlobalFlags _globalFlags;
    private D3D10Primitive _primitive;
    private D3D10PrimitiveTopology _primitiveTopology;
    // GS and HS inputs are indexed by vertex as well as by register.
    private bool _inputsAreVertexArrays;

    public D3D10Opcode Opcode { get; }

    // Opcode-token fields below are set by the reader.
    public bool Saturate { get; set; }

    // _nz (true) or _z.
    public bool TestNonZero { get; set; } = true;

    // _opc: the structured UAV has an order-preserving counter.
    public bool HasOrderPreservingCounter { get; set; }

    // [precise(...)]: the destination lanes the precise keyword covers.
    public int PreciseMask { get; set; }

    // _glc: the UAV is globallycoherent.
    public bool IsGloballyCoherent { get; set; }

    // The texel offsets of sample_aoffimmi, or null.
    public int[] SampleOffsets { get; set; }

    // SM5 extended opcode tokens repeating the declaration, kept only for the
    // listing: `ld_indexable(texture2d)(float,float,float,float)`.
    public ResourceDimension? IndexableResourceDimension { get; set; }
    public int IndexableResourceStride { get; set; }
    public int? IndexableResourceReturnTypeToken { get; set; }

    public D3D10ResInfoReturnType ResInfoReturnType { get; set; }

    public D3D10SyncFlags SyncFlags { get; set; }
    public D3D10OperandTokenCollection OperandTokens { get; }

    // dcl_indexableTemp has no operand tokens: three dwords (register, element
    // count, component count).
    public int IndexableTempRegister => (int)OperandTokens.Tokens[0];
    public int IndexableTempElementCount => (int)OperandTokens.Tokens[1];
    public int IndexableTempComponentCount => (int)OperandTokens.Tokens[2];

    // dcl_indexrange: one operand, then a plain dword with the register count.
    public int IndexRangeCount => (int)OperandTokens.Tokens[OperandTokens.GetSpan(0).Length];

    /// <summary>The raw customdata payload, e.g. an immediate constant buffer.</summary>
    public uint[] CustomData { get; private set; }

    public static D3D10Instruction CreateCustomData(uint[] customData, bool inputsAreVertexArrays)
    {
        return new D3D10Instruction(D3D10Opcode.CustomData, [], inputsAreVertexArrays)
        {
            CustomData = customData,
        };
    }

    public D3D10Instruction(D3D10Opcode opcode, uint[] paramTokens, bool inputsAreVertexArrays)
    {
        Opcode = opcode;
        OperandTokens = new D3D10OperandTokenCollection(paramTokens, opcode);
        _inputsAreVertexArrays = inputsAreVertexArrays;
    }

    public D3D10Instruction(D3D10Opcode opcode, uint[] paramTokens, ResourceDimension resourceDimension, bool inputsAreVertexArrays)
        : this(opcode, paramTokens, inputsAreVertexArrays)
    {
        _resourceDimension = resourceDimension;
    }

    public D3D10Instruction(D3D10Opcode opcode, D3D10GlobalFlags globalFlags, bool inputsAreVertexArrays)
        : this(opcode, [], inputsAreVertexArrays)
    {
        _globalFlags = globalFlags;
    }

    public D3D10Instruction(D3D10Opcode opcode, D3D10Primitive primitive, bool inputsAreVertexArrays)
        : this(opcode, [], inputsAreVertexArrays)
    {
        _primitive = primitive;
    }

    public D3D10Instruction(D3D10Opcode opcode, D3D10PrimitiveTopology primitiveTopology, bool inputsAreVertexArrays)
    : this(opcode, [], inputsAreVertexArrays)
    {
        _primitiveTopology = primitiveTopology;
    }

    public ResourceDimension GetResourceDimension()
    {
        return _resourceDimension;
    }

    /// <summary>The sample count a dcl_resource_texture2dms declares, or 0.</summary>
    public int ResourceSampleCount { get; set; }

    /// <summary>A constant buffer declared as indexed by a register.</summary>
    public bool IsDynamicallyIndexed { get; set; }

    public D3D10TessellatorDomain TessellatorDomain { get; set; }

    public int ControlPointCount { get; set; }

    /// <summary>
    /// The geometry shader output stream of a declaration or write; null with one
    /// stream.
    /// </summary>
    public int? Stream { get; set; }

    public D3D10TessellatorPartitioning TessellatorPartitioning { get; set; }

    public D3D10TessellatorOutputPrimitive TessellatorOutputPrimitive { get; set; }

    public D3D10GlobalFlags GetGlobalFlags()
    {
        return _globalFlags;
    }

    public D3D10Primitive GetPrimitive()
    {
        return _primitive;
    }

    public D3D10PrimitiveTopology GetPrimitiveTopology()
    {
        return _primitiveTopology;
    }

    public override bool HasDestination
    {
        get
        {
            switch (Opcode)
            {
                case D3D10Opcode.Add:
                case D3D10Opcode.DclInputPSSgv:
                case D3D10Opcode.DclInputPSSiv:
                case D3D10Opcode.DclInputPS:
                case D3D10Opcode.DclInputSiv:
                case D3D10Opcode.DclInputSgv:
                case D3D10Opcode.DclInput:
                case D3D10Opcode.DclOutputSgv:
                case D3D10Opcode.DclOutputSiv:
                case D3D10Opcode.DclOutput:
                case D3D10Opcode.DerivRtx:
                case D3D10Opcode.DerivRty:
                case D3D10Opcode.DerivRtxCoarse:
                case D3D10Opcode.DerivRtxFine:
                case D3D10Opcode.DerivRtyCoarse:
                case D3D10Opcode.DerivRtyFine:
                case D3D10Opcode.Rcp:
                case D3D10Opcode.DAdd:
                case D3D10Opcode.DMul:
                case D3D10Opcode.DDiv:
                case D3D10Opcode.DMax:
                case D3D10Opcode.DMin:
                case D3D10Opcode.DEq:
                case D3D10Opcode.DGe:
                case D3D10Opcode.DLt:
                case D3D10Opcode.DNe:
                case D3D10Opcode.DMov:
                case D3D10Opcode.DMovC:
                case D3D10Opcode.DFMA:
                case D3D10Opcode.DRCP:
                case D3D10Opcode.DToF:
                case D3D10Opcode.FToD:
                case D3D10Opcode.DToI:
                case D3D10Opcode.DToU:
                case D3D10Opcode.IToD:
                case D3D10Opcode.UToD:
                // Two destinations each.
                case D3D10Opcode.Udiv:
                case D3D10Opcode.IMul:
                case D3D10Opcode.UMul:
                case D3D10Opcode.Dp2:
                case D3D10Opcode.Dp3:
                case D3D10Opcode.Dp4:
                case D3D10Opcode.And:
                case D3D10Opcode.Xor:
                case D3D10Opcode.Not:
                case D3D10Opcode.Div:
                case D3D10Opcode.Or:
                case D3D10Opcode.Eq:
                case D3D10Opcode.GE:
                case D3D10Opcode.LT:
                case D3D10Opcode.Ne:
                case D3D10Opcode.Exp:
                case D3D10Opcode.Frc:
                case D3D10Opcode.Ftoi:
                case D3D10Opcode.Ftou:
                case D3D10Opcode.Log:
                case D3D10Opcode.Max:
                case D3D10Opcode.Min:
                case D3D10Opcode.IAdd:
                case D3D10Opcode.IShl:
                case D3D10Opcode.IShr:
                case D3D10Opcode.UShr:
                case D3D10Opcode.MSAD:
                case D3D10Opcode.IMad:
                case D3D10Opcode.IMax:
                case D3D10Opcode.UMax:
                case D3D10Opcode.UMin:
                case D3D10Opcode.Umad:
                // Two destinations each: the result and the carry or borrow (which
                // may be null) or the value's swap partner (which may not).
                case D3D10Opcode.UAddC:
                case D3D10Opcode.USubB:
                case D3D10Opcode.SwapC:
                case D3D10Opcode.IMin:
                case D3D10Opcode.INeg:
                case D3D10Opcode.Ine:
                case D3D10Opcode.RoundNe:
                case D3D10Opcode.RoundNi:
                case D3D10Opcode.RoundPi:
                case D3D10Opcode.RoundZ:
                case D3D10Opcode.Ieq:
                case D3D10Opcode.Ige:
                case D3D10Opcode.UGE:
                case D3D10Opcode.ULT:
                case D3D10Opcode.Ilt:
                case D3D10Opcode.IToF:
                case D3D10Opcode.UTof:
                case D3D10Opcode.LD:
                case D3D10Opcode.LDMS:
                case D3D10Opcode.LdStructured:
                case D3D10Opcode.LdRaw:
                case D3D10Opcode.StoreRaw:
                case D3D10Opcode.ResInfo:
                case D3D10Opcode.Mad:
                case D3D10Opcode.Mov:
                case D3D10Opcode.MovC:
                case D3D10Opcode.Mul:
                case D3D10Opcode.Rsq:
                case D3D10Opcode.Gather4:
                case D3D10Opcode.Lod:
                case D3D10Opcode.SamplePos:
                case D3D10Opcode.SampleInfo:
                case D3D10Opcode.Sample:
                case D3D10Opcode.Gather4Po:
                case D3D10Opcode.Gather4PoC:
                case D3D10Opcode.Gather4C:
                case D3D10Opcode.SampleC:
                case D3D10Opcode.SampleCLZ:
                case D3D10Opcode.SampleL:
                case D3D10Opcode.SampleD:
                case D3D10Opcode.SampleB:
                case D3D10Opcode.SinCos:
                case D3D10Opcode.Sqrt:
                case D3D10Opcode.StoreStructured:
                case D3D10Opcode.EvalSampleIndex:
                case D3D10Opcode.EvalSnapped:
                case D3D10Opcode.EvalCentroid:
                case D3D10Opcode.BufInfo:
                case D3D10Opcode.StoreUAVTyped:
                case D3D10Opcode.LdUAVTyped:
                case D3D10Opcode.ImmAtomicAlloc:
                case D3D10Opcode.ImmAtomicConsume:
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
                case D3D10Opcode.CountBits:
                case D3D10Opcode.FirstBitLo:
                case D3D10Opcode.FirstBitHi:
                case D3D10Opcode.FirstBitSHi:
                case D3D10Opcode.BFRev:
                case D3D10Opcode.UBFE:
                case D3D10Opcode.IBFE:
                case D3D10Opcode.BFI:
                case D3D10Opcode.F32ToF16:
                case D3D10Opcode.F16ToF32:
                    return true;
                default:
                    return false;
            }
        }
    }

    public override bool IsTextureOperation
    {
        get
        {
            switch (Opcode)
            {
                default:
                    return false;
            }
        }
    }

    public override int? GetDestinationParamIndex()
    {
        if (!HasDestination)
        {
            return null;
        }
        // With two destinations, a null first one has no mask; use the second.
        if (GetOperandType(0) == OperandType.Null && HasSecondDestination())
        {
            return 1;
        }
        return 0;
    }

    /// <summary>Whether this operand is written; udiv, imul and sincos write two.</summary>
    public bool IsDestinationOperand(int operandIndex)
    {
        if (operandIndex == GetDestinationParamIndex())
        {
            return true;
        }
        return operandIndex == 1 && HasSecondDestination();
    }

    public bool HasSecondDestination()
    {
        switch (Opcode)
        {
            case D3D10Opcode.Udiv:
            case D3D10Opcode.IMul:
            case D3D10Opcode.UMul:
            case D3D10Opcode.SinCos:
            case D3D10Opcode.UAddC:
            case D3D10Opcode.USubB:
            case D3D10Opcode.SwapC:
                return true;
            default:
                return false;
        }
    }

    // A constant buffer operand is indexed twice: by buffer, then by element.
    private const int ConstantBufferElementIndex = 1;

    public override int GetDestinationWriteMask()
    {
        return GetWriteMask(GetDestinationParamIndex().Value);
    }

    public override string GetWriteMaskName(int operandIndex, int destinationLength)
    {
        return FormatWriteMask(GetWriteMask(operandIndex), destinationLength);
    }

    /// <summary>
    /// The write mask for part of the destination, e.g. one of two output
    /// semantics packed into one register.
    /// </summary>
    public string GetWriteMaskName(int operandIndex, int destinationLength, int mask)
    {
        return FormatWriteMask(mask, destinationLength);
    }

    /// <summary>The source swizzle for a given set of destination components,
    /// rather than for all of them.</summary>
    public string GetSourceSwizzleNameForMask(int srcIndex, int destinationMask)
    {
        if (GetOperandComponentSelection(srcIndex) is D3D10OperandNumComponents.Operand0Component
            or D3D10OperandNumComponents.Operand1Component)
        {
            return "";
        }
        byte[] swizzle = GetSourceSwizzleComponents(srcIndex);
        string swizzleName = "";
        for (int i = 0; i < 4; i++)
        {
            if ((destinationMask & (1 << i)) != 0)
            {
                swizzleName += "xyzw"[swizzle[i]];
            }
        }
        return swizzleName.Length == 0 ? "" : "." + swizzleName;
    }

    /// <summary>
    /// Whether the destination holds doubles, a component pair each (.xy is one).
    /// False for double comparisons and conversions to float/int.
    /// </summary>
    public bool WritesDoubles => Opcode is
        D3D10Opcode.DAdd or D3D10Opcode.DMul or D3D10Opcode.DDiv or
        D3D10Opcode.DMax or D3D10Opcode.DMin or D3D10Opcode.DMov or
        D3D10Opcode.DMovC or D3D10Opcode.DFMA or D3D10Opcode.DRCP or
        D3D10Opcode.FToD or D3D10Opcode.IToD or D3D10Opcode.UToD;

    /// <summary>Whether a source operand holds doubles (not a dmovc condition).</summary>
    public bool IsDoubleOperand(int operandIndex) => Opcode switch
    {
        D3D10Opcode.DAdd or D3D10Opcode.DMul or D3D10Opcode.DDiv or
        D3D10Opcode.DMax or D3D10Opcode.DMin or D3D10Opcode.DEq or
        D3D10Opcode.DGe or D3D10Opcode.DLt or D3D10Opcode.DNe =>
            operandIndex is 1 or 2,
        D3D10Opcode.DFMA => operandIndex is 1 or 2 or 3,
        D3D10Opcode.DMov or D3D10Opcode.DRCP or D3D10Opcode.DToF or
        D3D10Opcode.DToI or D3D10Opcode.DToU => operandIndex == 1,
        D3D10Opcode.DMovC => operandIndex is 2 or 3,
        _ => false,
    };

    /// <summary>
    /// Whether doubles appear on either side. Operands are then read by value
    /// ordinal rather than by component.
    /// </summary>
    public bool HasDoubleOperands => WritesDoubles || IsDoubleOperand(1);

    /// <summary>
    /// How many values the instruction computes: one per double where it writes
    /// doubles, else one per component. Operand widths derive from this.
    /// </summary>
    public int ValueCount
    {
        get
        {
            int mask = GetDestinationWriteMask();
            if (WritesDoubles)
            {
                mask &= 0b0101;
            }
            int count = 0;
            for (int component = 0; component < 4; component++)
            {
                if ((mask & (1 << component)) != 0)
                {
                    count++;
                }
            }
            return count;
        }
    }

    /// <summary>
    /// Which operand pair the nth value reads. For double writes it is the
    /// destination's pair (.zw is pair 1 even as the first value).
    /// </summary>
    public int GetValuePair(int valueOrdinal)
    {
        if (!WritesDoubles)
        {
            return valueOrdinal;
        }
        int mask = GetDestinationWriteMask() & 0b0101;
        int seen = 0;
        for (int component = 0; component < 4; component += 2)
        {
            if ((mask & (1 << component)) == 0)
            {
                continue;
            }
            if (seen++ == valueOrdinal)
            {
                return component / 2;
            }
        }
        return valueOrdinal;
    }

    /// <summary>
    /// A destination component's position among the written values. fxc repeats a
    /// double operand's pair across the swizzle (.xyxy), so the nth double is at slot 2n.
    /// </summary>
    public int GetValueOrdinal(int destinationComponent)
    {
        int mask = GetDestinationWriteMask();
        if (WritesDoubles)
        {
            mask &= 0b0101;
        }
        int ordinal = 0;
        for (int component = 0; component < destinationComponent; component++)
        {
            if ((mask & (1 << component)) != 0)
            {
                ordinal++;
            }
        }
        return ordinal;
    }

    /// <summary>
    /// The vForkInstanceID value of this copy of an unrolled instanced hull phase,
    /// or null where the phase runs once.
    /// </summary>
    public int? ForkInstance { get; private set; }

    // Memberwise, so opcode-token state (dimension, interpolation, stream) is kept.
    public D3D10Instruction ForInstance(int instance)
    {
        var copy = (D3D10Instruction)MemberwiseClone();
        copy.ForkInstance = instance;
        return copy;
    }

    public int GetWriteMask(int operandIndex)
    {
        D3D10OperandNumComponents componentSelection = GetOperandComponentSelection(operandIndex);
        if (componentSelection == D3D10OperandNumComponents.Operand1Component)
        {
            return 1;
        }
        else if (componentSelection == D3D10OperandNumComponents.Operand4Component)
        {
            Span<uint> span = OperandTokens.GetSpan(operandIndex);

            ComponentSelectionMode selectionMode = GetOperandComponentSelectionMode(operandIndex);
            if (selectionMode == ComponentSelectionMode.Mask)
            {
                int mask = (int)((span[0] >> 4) & 0xF);
                return mask;
            }
            else if (selectionMode == ComponentSelectionMode.Swizzle)
            {
                int swizzle = (int)((span[0] >> 4) & 0xff);
                int mask = 0;
                for (int i = 0; i < 4; i++)
                {
                    int componentSwizzle = (swizzle >> (2 * i)) & 3;
                    if (componentSwizzle != i)
                    {
                        mask |= 1 << componentSwizzle;
                    }
                }
                return mask;
            }
            else if (selectionMode == ComponentSelectionMode.Select1)
            {
                int component = (int)((span[0] >> 4) & 0x3);
                return 1 << component;
            }
        }
        else if (componentSelection == D3D10OperandNumComponents.Operand0Component)
        {
            // Zero-component scalars (vThreadIDInGroupFlattened, oMask, ...) still
            // write .x; an empty mask leaves no component to type.
            OperandType operandType = GetOperandType(operandIndex);
            return IsThreadRegister(operandType)
                || operandType == OperandType.OutputCoverageMask
                || operandType == OperandType.InputCoverageMask
                || operandType == OperandType.OutputStencilRef
                || operandType == OperandType.InputInnerCoverage
                || operandType == OperandType.OutputControlPointID
                ? 1
                : 0;
        }
        throw new NotImplementedException();
    }

    public string GetInterpolationModeName()
    {
        switch (GetInterpolationMode())
        {
            case D3D10InterpolationMode.Undefined:
                return "";
            case D3D10InterpolationMode.Constant:
                return "constant";
            case D3D10InterpolationMode.Linear:
                return "linear";
            case D3D10InterpolationMode.LinearCentroid:
                return "linear centroid";
            case D3D10InterpolationMode.LinearNoPerspective:
                return "linear noperspective";
            case D3D10InterpolationMode.LinearNoPerspectiveCentroid:
                return "linear noperspective centroid";
            case D3D10InterpolationMode.LinearSample:
                return "linear sample";
            case D3D10InterpolationMode.LinearNoPerspectiveSample:
                return "linear noperspective sample";
            default:
                throw new NotImplementedException();
        }
    }

    // In the opcode token, not the operand token; set by the reader.
    public D3D10InterpolationMode InterpolationMode { get; set; }

    public D3D10SamplerMode SamplerMode { get; set; }

    public D3D10InterpolationMode GetInterpolationMode()
    {
        return InterpolationMode;
    }

    public D3D10OperandIndexRepresentation[] GetOperandIndexRepresentation(int index)
    {
        int dimension = GetOperandIndexDimension(index);
        var representation = new D3D10OperandIndexRepresentation[dimension];
        Span<uint> span = OperandTokens.GetSpan(index);
        for (int d = 0; d < dimension; d++)
        {
            representation[d] = (D3D10OperandIndexRepresentation)((span[0] >> (22 + d * 3)) & 7);
        }
        return representation;
    }

    private int GetOperandIndexDimension(int index)
    {
        Span<uint> span = OperandTokens.GetSpan(index);
        return (int)((span[0] >> 20) & 3);
    }

    public D3D10OperandNumComponents GetOperandComponentSelection(int index)
    {
        Span<uint> span = OperandTokens.GetSpan(index);
        return (D3D10OperandNumComponents)(span[0] & 3);
    }

    private ComponentSelectionMode GetOperandComponentSelectionMode(int index)
    {
        Span<uint> span = OperandTokens.GetSpan(index);
        return (ComponentSelectionMode)((span[0] >> 2) & 3);
    }

    public override int GetSourceSwizzle(int srcIndex)
    {
        D3D10OperandNumComponents componentSelection = GetOperandComponentSelection(srcIndex);
        // A one-component operand (vOutputControlPointID) gives its value to every lane.
        if (componentSelection == D3D10OperandNumComponents.Operand1Component)
        {
            return 0;
        }
        else if (componentSelection == D3D10OperandNumComponents.Operand4Component)
        {
            ComponentSelectionMode selectionMode = GetOperandComponentSelectionMode(srcIndex);
            if (selectionMode == ComponentSelectionMode.Mask)
            {
                return (0 << 0) | (1 << 2) | (2 << 4) | (3 << 6);
            }
            else if (selectionMode == ComponentSelectionMode.Swizzle)
            {
                Span<uint> span = OperandTokens.GetSpan(srcIndex);
                return (int)((span[0] >> 4) & 0xFF);
            }
            else if (selectionMode == ComponentSelectionMode.Select1)
            {
                Span<uint> span = OperandTokens.GetSpan(srcIndex);
                int component = (int)((span[0] >> 4) & 3);
                return component * 0x55;
            }
        }
        else if (componentSelection == D3D10OperandNumComponents.Operand0Component)
        {
            return 0;
        }
        throw new NotImplementedException();
    }

    public override string GetSourceSwizzleName(int srcIndex, int? destinationLength = null)
    {
        // Scalar operands (vPrim) are written bare.
        if (GetOperandComponentSelection(srcIndex) is D3D10OperandNumComponents.Operand0Component
            or D3D10OperandNumComponents.Operand1Component)
        {
            return "";
        }

        int destinationMask;
        switch (destinationLength)
        {
            case 1:
                destinationMask = 1;
                break;
            case 2:
                destinationMask = 3;
                break;
            case 3:
                destinationMask = 7;
                break;
            case 4:
                destinationMask = 15;
                break;
            default:
                if (Opcode == D3D10Opcode.Dp2)
                {
                    destinationMask = 3;
                    destinationLength = 2;
                }
                else if (Opcode == D3D10Opcode.Dp3)
                {
                    destinationMask = 7;
                    destinationLength = 3;
                }
                else if (Opcode == D3D10Opcode.Dp4)
                {
                    destinationMask = 15;
                    destinationLength = 4;
                }
                // lod's destination width says nothing about its coordinate's.
                else if (Opcode == D3D10Opcode.Lod)
                {
                    destinationMask = 15;
                    destinationLength = 4;
                }
                // Double operands are as wide as the value count (doubled for a
                // double operand); the destination mask does not say so, e.g.
                // `dlt r0.z, r0.xyxy, r1.xyxy`.
                else if (HasDoubleOperands)
                {
                    // Show up to the highest pair written: .zw reads r0.zwzw.
                    destinationLength = IsDoubleOperand(srcIndex)
                        ? WritesDoubles
                            ? (GetValuePair(ValueCount - 1) + 1) * 2
                            : ValueCount * 2
                        : ValueCount;
                    destinationMask = (1 << destinationLength.Value) - 1;
                }
                // An atomic's address is printed full width, as stored.
                else if ((Opcode.IsAtomic() && srcIndex == 1)
                    || (Opcode.IsImmediateAtomic() && srcIndex == 2))
                {
                    destinationMask = 15;
                    destinationLength = 4;
                }
                // Each destination reads the sources at its own components, so a
                // swapc writing r1.xy and r2.zw reads all four.
                else if (HasSecondDestination())
                {
                    destinationMask = 0;
                    for (int index = 0; index < 2; index++)
                    {
                        if (GetOperandType(index) != OperandType.Null)
                        {
                            destinationMask |= GetWriteMask(index);
                        }
                    }
                }
                else if (HasDestination)
                {
                    destinationMask = GetDestinationWriteMask();
                    destinationLength = GetDestinationMaskLength();
                }
                else
                {
                    destinationMask = 15;
                    destinationLength = 4;
                }
                break;
        }

        byte[] swizzle = GetSourceSwizzleComponents(srcIndex);

        string swizzleName = "";
        for (int i = 0; i < 4; i++)
        {
            if ((destinationMask & (1 << i)) != 0)
            {
                swizzleName += swizzle[i] switch
                {
                    0 => "x",
                    1 => "y",
                    2 => "z",
                    3 => "w",
                    _ => ""
                };
            }
        }
        return swizzleName switch
        {
            "xyzw" => "",
            "xxxx" => ".x",
            "yyyy" => ".y",
            "zzzz" => ".z",
            "wwww" => ".w",
            _ => "." + swizzleName
        };
    }

    /// <summary>
    /// Whether an operand is a buffer address (element index or byte offset),
    /// which is an integer whatever the data type.
    /// </summary>
    public bool IsAddressOperand(int index)
    {
        return Opcode switch
        {
            D3D10Opcode.LdStructured or D3D10Opcode.StoreStructured => index is 1 or 2,
            D3D10Opcode.LdRaw or D3D10Opcode.StoreRaw
                or D3D10Opcode.LdUAVTyped or D3D10Opcode.StoreUAVTyped => index == 1,
            _ => false,
        };
    }

    public override string GetDeclSemantic()
    {
        int destIndex = GetDestinationParamIndex().Value;
        OperandType operandType = GetOperandType(destIndex);

        if (Opcode == D3D10Opcode.DclOutputSiv
            || Opcode == D3D10Opcode.DclInputSiv
            || Opcode == D3D10Opcode.DclInputPSSiv)
        {
            var systemValueName = (D3D10Name)GetParamIndexImmediate32(1, 0);
            if (systemValueName == D3D10Name.Position)
            {
                return "SV_Position";
            }
        }

        string name = operandType switch
        {
            OperandType.Input => "SV_Position",
            OperandType.Output => "SV_Target",
            OperandType.InputThreadID => "SV_DispatchThreadID",
            OperandType.InputThreadGroupID => "SV_GroupID",
            OperandType.InputThreadIDInGroup => "SV_GroupThreadID",
            OperandType.InputThreadIDInGroupFlattened => "SV_GroupIndex",
            OperandType.InputPrimitiveID => "SV_PrimitiveID",
            OperandType.InputGSInstanceID => "SV_GSInstanceID",
            OperandType.InputDomainPoint => "SV_DomainLocation",
            OperandType.OutputControlPointID => "SV_OutputControlPointID",
            OperandType.OutputDepth => "SV_Depth",
            OperandType.OutputDepthGreaterEqual => "SV_DepthGreaterEqual",
            OperandType.OutputDepthLessEqual => "SV_DepthLessEqual",
            OperandType.OutputCoverageMask => "SV_Coverage",
            OperandType.InputCoverageMask => "SV_Coverage",
            OperandType.OutputStencilRef => "SV_StencilRef",
            OperandType.InputInnerCoverage => "SV_InnerCoverage",
            _ => throw new NotImplementedException(operandType.ToString())
        };
        // These name no register, so there is no index to append.
        if (!IsThreadRegister(operandType)
            && operandType != OperandType.InputDomainPoint
            && operandType != OperandType.OutputControlPointID
            && operandType != OperandType.OutputDepth
            && operandType != OperandType.OutputDepthGreaterEqual
            && operandType != OperandType.OutputDepthLessEqual
            && operandType != OperandType.OutputCoverageMask
            && operandType != OperandType.InputCoverageMask
            && operandType != OperandType.OutputStencilRef
            && operandType != OperandType.InputInnerCoverage)
        {
            int numberIndex = (_inputsAreVertexArrays && operandType == OperandType.Input)
                || operandType == OperandType.InputControlPoint ? 2 : 1;
            int declIndex = (int) GetParamIndexImmediate32(destIndex, numberIndex);
            name += declIndex;
        }
        return name;
    }

    private byte[] GetOperandValueBytes(int index, int componentIndex)
    {
        Span<uint> span = OperandTokens.GetSpan(index);
        uint value;
        // A bare dword count with no operand token.
        if (Opcode == D3D10Opcode.DclTemps || Opcode == D3D10Opcode.DclGSMaxOutputVertexCount
            || Opcode == D3D10Opcode.DclGSInstanceCount
            || Opcode == D3D10Opcode.DclHSMaxTessFactor
            || Opcode == D3D10Opcode.DclHSForkPhaseInstanceCount
            || Opcode == D3D10Opcode.DclHSJoinPhaseInstanceCount)
        {
            value = span[0];
        }
        else
        {
            var componentSelection = GetOperandComponentSelection(index);
            if (componentSelection == D3D10OperandNumComponents.Operand1Component)
            {
                value = span[1];
            }
            else
            {
                value = span[1 + componentIndex];
            }
        }
        return BitConverter.GetBytes(value);
    }

    public override float[] GetParamSingle(int index)
    {
        D3D10OperandNumComponents selection = GetOperandComponentSelection(index);
        if (selection == D3D10OperandNumComponents.Operand1Component)
        {
            return [BitConverter.ToSingle(GetOperandValueBytes(index, 0), 0)];
        }
        else if (selection == D3D10OperandNumComponents.Operand4Component)
        {
            return [
                BitConverter.ToSingle(GetOperandValueBytes(index, 0), 0),
                BitConverter.ToSingle(GetOperandValueBytes(index, 1), 0),
                BitConverter.ToSingle(GetOperandValueBytes(index, 2), 0),
                BitConverter.ToSingle(GetOperandValueBytes(index, 3), 0)
                ];
        }
        throw new NotImplementedException();
    }

    public float GetParamSingle(int index, int componentIndex)
    {
        return BitConverter.ToSingle(GetOperandValueBytes(index, componentIndex), 0);
    }

    public override int GetParamInt(int index)
    {
        return BitConverter.ToInt32(GetOperandValueBytes(index, 0), 0);
    }

    public int GetParamInt(int index, int componentIndex)
    {
        return BitConverter.ToInt32(GetOperandValueBytes(index, componentIndex), 0);
    }

    // The nth double of a d() operand: two dwords after the operand token, low first.
    public double GetParamDouble(int index, int doubleIndex)
    {
        Span<uint> span = OperandTokens.GetSpan(index);
        uint low = span[1 + doubleIndex * 2];
        uint high = span[2 + doubleIndex * 2];
        return BitConverter.Int64BitsToDouble((long)high << 32 | low);
    }

    public D3D10OperandModifier GetOperandModifier(int index)
    {
        Span<uint> span = OperandTokens.GetSpan(index);
        bool isExtended = (span[0] & 0x80000000) != 0;
        if (isExtended)
        {
            return (D3D10OperandModifier)((span[1] >> 6) & 0xFF);
        }
        return D3D10OperandModifier.None;
    }

    public override D3D10RegisterKey GetParamRegisterKey(int index)
    {
        OperandType operandType = GetOperandType(index);
        if (operandType == OperandType.ConstantBuffer)
        {
            return new D3D10RegisterKey(
                operandType,
                GetParamRegisterNumber(index),
                GetParamConstantBufferOffset(index));
        }
        else if (operandType == OperandType.Immediate32)
        {
            if (Opcode.IsInteger())
            {
                // Explicit float[]: a collection expression of ints is ambiguous
                // between the float[] and double[] constructors.
                return GetOperandComponentSelection(index) == D3D10OperandNumComponents.Operand1Component
                    ? new D3D10RegisterKey(new float[] { GetParamInt(index) })
                    : new D3D10RegisterKey(new float[]
                        {
                            GetParamInt(index, 0),
                            GetParamInt(index, 1),
                            GetParamInt(index, 2),
                            GetParamInt(index, 3)
                        });
            }
            return new D3D10RegisterKey(GetParamSingle(index));
        }
        else if (operandType == OperandType.Immediate64)
        {
            return new D3D10RegisterKey([
                GetParamDouble(index, 0),
                GetParamDouble(index, 1)
                ]);
        }
        else if (IsThreadRegister(operandType))
        {
            return new D3D10RegisterKey(operandType, 0);
        }
        else if (operandType == OperandType.IndexableTemp)
        {
            // Keyed by the whole x# array; the element index is left to the reader.
            return new D3D10RegisterKey(
                operandType,
                (int)OperandTokens.GetOperandIndices(index)[0].Immediate);
        }
        if ((_inputsAreVertexArrays && operandType == OperandType.Input)
            // vicp[point][register], shaped like a GS input.
            || operandType == OperandType.InputControlPoint)
        {
            return D3D10RegisterKey.CreateGSInput(
                (int)GetParamIndexImmediate32(index, 2),
                (int)GetParamIndexImmediate32(index, 1));
        }
        if (operandType == OperandType.OutputControlPoint)
        {
            return D3D10RegisterKey.CreateControlPointOutput(
                (int)GetParamIndexImmediate32(index, 2),
                (int)GetParamIndexImmediate32(index, 1));
        }
        return new D3D10RegisterKey(
            operandType,
            GetParamRegisterNumber(index),
            Stream,
            operandType == OperandType.Output);
    }

    /// <summary>
    /// System-provided uint registers with no register number: thread/group ids,
    /// primitive id and GS instance id.
    /// </summary>
    public static bool IsThreadRegister(OperandType operandType)
    {
        return operandType is OperandType.InputThreadID
            or OperandType.InputThreadGroupID
            or OperandType.InputThreadIDInGroup
            or OperandType.InputThreadIDInGroupFlattened
            or OperandType.InputPrimitiveID
            or OperandType.InputGSInstanceID;
    }

    public OperandType GetOperandType(int index)
    {
        Span<uint> span = OperandTokens.GetSpan(index);
        return (OperandType)((span[0] >> 12) & 0xFF);
    }

    public override int GetParamRegisterNumber(int index)
    {
        // oDepth and null name no register and carry no index to read.
        if (OperandTokens.GetOperandIndices(index).Length == 0)
        {
            return 0;
        }
        return (int) GetParamIndexImmediate32(index, 1);
    }

    // Zero for a relative index (cb0[r0.x]); check GetIndexRepresentation.
    public int GetParamConstantBufferOffset(int index)
    {
        return IsRelativelyAddressed(index, ConstantBufferElementIndex)
            ? 0
            : (int)GetParamIndexImmediate32(index, 2);
    }

    public D3D10OperandIndexRepresentation GetIndexRepresentation(int operandIndex, int index)
    {
        Span<uint> span = OperandTokens.GetSpan(operandIndex);
        return (D3D10OperandIndexRepresentation)((span[0] >> (22 + index * 3)) & 7);
    }

    public bool IsRelativelyAddressed(int operandIndex, int index)
    {
        D3D10OperandIndexRepresentation representation = GetIndexRepresentation(operandIndex, index);
        return representation == D3D10OperandIndexRepresentation.Relative
            || representation == D3D10OperandIndexRepresentation.Immediate32PlusRelative
            || representation == D3D10OperandIndexRepresentation.Immediate64PlusRelative;
    }

    public int GetResourceReturnTypeToken()
    {
        // The dword after the operand: a texture operand has two indices so it
        // reads as index 2; a typed UAV's has one, so it parses as operand 1.
        return Opcode == D3D10Opcode.DclUnorderedAccessViewTyped
            ? (int)GetParamIndexImmediate32(1, 0)
            : (int)GetParamIndexImmediate32(0, 2);
    }

    public uint GetResourceStructuredBufferStride()
    {
        return GetParamIndexImmediate32(1, 0);
    }

    // dcl_tgsm_structured g0, stride, count: two plain dwords after the operand.
    public uint GetThreadGroupSharedMemoryStride()
    {
        return GetParamIndexImmediate32(1, 0);
    }

    public uint GetThreadGroupSharedMemoryCount()
    {
        return GetParamIndexImmediate32(2, 0);
    }

    public uint GetParamIndexImmediate32(int operandIndex, int index)
    {
        Span<uint> span = OperandTokens.GetSpan(operandIndex);
        bool isExtended = (span[0] & 0x80000000) != 0;
        int offset = 0;
        if (isExtended)
        {
            offset += 1;
        }
        return span[offset + index];
    }

    public override string ToString()
    {
        return Opcode.ToString();
    }
}
