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
    private bool _isGeometryShader;

    public D3D10Opcode Opcode { get; }

    // Destination saturate modifier: the result is clamped to [0, 1]. Encoded in
    // the opcode token rather than in an operand, so the reader sets it.
    public bool Saturate { get; set; }

    // Whether if, breakc and the rest take the branch when the tested register is
    // non-zero, which is the _nz form, or when it is zero, which is _z. In the
    // opcode token rather than in an operand, so the reader sets it.
    public bool TestNonZero { get; set; } = true;

    // The texel offsets of sample_aoffimmi, or null when the sample has none.
    public int[] SampleOffsets { get; set; }

    // What shader model 5 says about the resource at the instruction that reads it,
    // in extended opcode tokens of its own: its dimension, the stride of a
    // structured buffer, and the four return types. The declaration says the same,
    // so nothing here needs them to decompile - but fxc writes them into the
    // mnemonic, `ld_indexable(texture2d)(float,float,float,float)`, and the
    // listing follows it. Null where the instruction carries none.
    public ResourceDimension? IndexableResourceDimension { get; set; }
    public int IndexableResourceStride { get; set; }
    public int? IndexableResourceReturnTypeToken { get; set; }

    // How resinfo reports what it measures: as floats, as their reciprocals, or as
    // the integers GetDimensions' uint overloads take. In the opcode token, so the
    // reader sets it.
    public D3D10ResInfoReturnType ResInfoReturnType { get; set; }

    // What a sync waits on: the threads of the group, group shared memory, or UAV
    // memory. In the opcode token, so the reader sets it.
    public D3D10SyncFlags SyncFlags { get; set; }
    public D3D10OperandTokenCollection OperandTokens { get; }

    // dcl_indexableTemp carries no operand tokens: three plain dwords name the x#
    // register, how many elements it has, and how many components each holds.
    public int IndexableTempRegister => (int)OperandTokens.Tokens[0];
    public int IndexableTempElementCount => (int)OperandTokens.Tokens[1];
    public int IndexableTempComponentCount => (int)OperandTokens.Tokens[2];

    // dcl_indexrange carries one operand - the first register of the run, with the
    // mask the run is indexed over - and then a plain dword for how many registers
    // the run covers. It sits after the operand's tokens, however many those are.
    public int IndexRangeCount => (int)OperandTokens.Tokens[OperandTokens.GetSpan(0).Length];

    /// <summary>
    /// The payload of a customdata instruction, which is not operand tokens - an
    /// immediate constant buffer is four floats per row.
    /// </summary>
    public uint[] CustomData { get; private set; }

    public static D3D10Instruction CreateCustomData(uint[] customData, bool isGeometryShader)
    {
        return new D3D10Instruction(D3D10Opcode.CustomData, [], isGeometryShader)
        {
            CustomData = customData,
        };
    }

    public D3D10Instruction(D3D10Opcode opcode, uint[] paramTokens, bool isGeometryShader)
    {
        Opcode = opcode;
        OperandTokens = new D3D10OperandTokenCollection(paramTokens, opcode);
        _isGeometryShader = isGeometryShader;
    }

    public D3D10Instruction(D3D10Opcode opcode, uint[] paramTokens, ResourceDimension resourceDimension, bool isGeometryShader)
        : this(opcode, paramTokens, isGeometryShader)
    {
        _resourceDimension = resourceDimension;
    }

    public D3D10Instruction(D3D10Opcode opcode, D3D10GlobalFlags globalFlags, bool isGeometryShader)
        : this(opcode, [], isGeometryShader)
    {
        _globalFlags = globalFlags;
    }

    public D3D10Instruction(D3D10Opcode opcode, D3D10Primitive primitive, bool isGeometryShader)
        : this(opcode, [], isGeometryShader)
    {
        _primitive = primitive;
    }

    public D3D10Instruction(D3D10Opcode opcode, D3D10PrimitiveTopology primitiveTopology, bool isGeometryShader)
    : this(opcode, [], isGeometryShader)
    {
        _primitiveTopology = primitiveTopology;
    }

    public ResourceDimension GetResourceDimension()
    {
        return _resourceDimension;
    }

    /// <summary>The sample count a dcl_resource_texture2dms declares, or 0.</summary>
    public int ResourceSampleCount { get; set; }

    /// <summary>
    /// Whether a declared constant buffer is read at an index a register holds
    /// rather than at an immediate one. It is one bit of the opcode token, and it
    /// says what the shader does with the buffer.
    /// </summary>
    public bool IsDynamicallyIndexed { get; set; }

    /// <summary>What a tessellator subdivides: a triangle, a quad or a line.</summary>
    public D3D10TessellatorDomain TessellatorDomain { get; set; }

    /// <summary>How many control points a patch has, in or out.</summary>
    public int ControlPointCount { get; set; }

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
                // Two destinations is still destinations. Saying they had none
                // left both operands looking like sources, so they took source
                // swizzles and the sources were read four components wide - a udiv
                // by l(3, 3, 0, 0) then divides by the zeroes as well.
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
                case D3D10Opcode.IMad:
                case D3D10Opcode.IMax:
                // The unsigned arithmetic writes a destination like the signed.
                case D3D10Opcode.UMax:
                case D3D10Opcode.UMin:
                case D3D10Opcode.Umad:
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
        // udiv, imul and sincos write two destinations and a shader wanting only
        // one of the two leaves the other null. A null carries no write mask, so
        // taking it as the destination said the instruction writes nothing - and
        // `udiv null, r0.zw, r0.zzzw, l(0, 0, 7, 7)` then read the divisor from the
        // first two components, which are the 0 of a division by zero.
        if (GetOperandType(0) == OperandType.Null && HasSecondDestination())
        {
            return 1;
        }
        return 0;
    }

    /// <summary>
    /// Whether this operand is written rather than read. udiv, imul and sincos
    /// write two, and treating the second as a source gave it a source swizzle
    /// where it wanted its write mask.
    /// </summary>
    public bool IsDestinationOperand(int operandIndex)
    {
        if (operandIndex == GetDestinationParamIndex())
        {
            return true;
        }
        return operandIndex == 1 && HasSecondDestination();
    }

    private bool HasSecondDestination()
    {
        switch (Opcode)
        {
            case D3D10Opcode.Udiv:
            case D3D10Opcode.IMul:
            case D3D10Opcode.UMul:
            case D3D10Opcode.SinCos:
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
    /// The same over part of what the instruction writes. One instruction can write
    /// two things that have to be said separately - two output semantics packed
    /// into one register - and each statement names only its own components.
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

    public int GetWriteMask(int operandIndex)
    {
        D3D10OperandNumComponents componentSelection = GetOperandComponentSelection(operandIndex);
        if (componentSelection == D3D10OperandNumComponents.Operand1Component)
        {
            // A one-component operand - oDepth, say - has the one component and no
            // mask to spell it out with.
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
            // dcl_input vThreadIDInGroupFlattened declares SV_GroupIndex with no
            // components at all - it is a scalar, and every read of it is .x. So is
            // oMask, which fxc writes bare for the same reason; an empty mask left
            // the instruction writer with no component to ask the type of, and it
            // cast a uint coverage mask to float.
            OperandType operandType = GetOperandType(operandIndex);
            return IsThreadRegister(operandType)
                || operandType == OperandType.OutputCoverageMask
                || operandType == OperandType.InputCoverageMask
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

    // The mode lives in the opcode token, not the operand token this used to read,
    // so every input declared as linear whatever it really was. The reader keeps it,
    // the way it keeps the saturate bit.
    public D3D10InterpolationMode InterpolationMode { get; set; }

    // How dcl_sampler declares the sampler. In the opcode token rather than in an
    // operand, so the reader sets it.
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
        if (componentSelection == D3D10OperandNumComponents.Operand1Component)
        {
            return (0 << 0) | (1 << 2) | (2 << 4) | (3 << 6);
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
        // A one component operand - vPrim - is a scalar, and fxc writes it bare;
        // the identity swizzle it decodes to would read .xyz off a scalar.
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
                // lod writes one component and reads a coordinate as wide as the
                // resource, so its destination says nothing about how wide its
                // sources are. Narrowed by it, a 2D lookup read `v0.x` where the
                // coordinate is `v0.xyxx`.
                else if (Opcode == D3D10Opcode.Lod)
                {
                    destinationMask = 15;
                    destinationLength = 4;
                }
                // The address of an interlocked operation is a whole operand too.
                // fxc writes it as it is stored - the coordinate of a texel, or the
                // element and the byte offset within one - whatever the width of the
                // single value the operation keeps.
                else if ((Opcode.IsAtomic() && srcIndex == 1)
                    || (Opcode.IsImmediateAtomic() && srcIndex == 2))
                {
                    destinationMask = 15;
                    destinationLength = 4;
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
            // The coverage the rasterizer handed this pixel, read where an order-
            // independent pass wants to weight a fragment by it. Same name as the
            // mask written out, and - like it - it names no register to index.
            OperandType.InputCoverageMask => "SV_Coverage",
            _ => throw new NotImplementedException(operandType.ToString())
        };
        // These name no register, so there is no index to append.
        // A domain location names one register and no number, the way the thread
        // ids do - but it is a float where they are uints, so it is not one of
        // them.
        if (!IsThreadRegister(operandType)
            && operandType != OperandType.InputDomainPoint
            && operandType != OperandType.OutputDepth
            && operandType != OperandType.OutputDepthGreaterEqual
            && operandType != OperandType.OutputDepthLessEqual
            && operandType != OperandType.OutputCoverageMask
            && operandType != OperandType.InputCoverageMask)
        {
            int numberIndex = (_isGeometryShader && operandType == OperandType.Input)
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
        // A bare count rather than an operand: the dword after the opcode token is
        // the number itself, with no operand token in front of it to describe it.
        if (Opcode == D3D10Opcode.DclTemps || Opcode == D3D10Opcode.DclGSMaxOutputVertexCount
            || Opcode == D3D10Opcode.DclGSInstanceCount)
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
                // As wide as the operand, the same as the float case below: taking
                // only the first component read l(1, 2, 0, 0) - a texel address - as 1.
                return GetOperandComponentSelection(index) == D3D10OperandNumComponents.Operand1Component
                    ? new D3D10RegisterKey([ GetParamInt(index) ])
                    : new D3D10RegisterKey([
                        GetParamInt(index, 0),
                        GetParamInt(index, 1),
                        GetParamInt(index, 2),
                        GetParamInt(index, 3)
                        ]);
            }
            return new D3D10RegisterKey(GetParamSingle(index));
        }
        else if (IsThreadRegister(operandType))
        {
            return new D3D10RegisterKey(operandType, 0);
        }
        else if (operandType == OperandType.IndexableTemp)
        {
            // x0[3] and x0[r0.x + 1] are one register: the first index names it and
            // the second, immediate or not, picks the element. The key is the whole
            // array, and the element is modelled by whoever reads the operand.
            return new D3D10RegisterKey(
                operandType,
                (int)OperandTokens.GetOperandIndices(index)[0].Immediate);
        }
        if ((_isGeometryShader && operandType == OperandType.Input)
            // vicp[2][0] is the same shape: which control point, then which
            // register of it. The patch is an array of vertices the way a
            // geometry shader's input is.
            || operandType == OperandType.InputControlPoint)
        {
            return D3D10RegisterKey.CreateGSInput(
                (int)GetParamIndexImmediate32(index, 2),
                (int)GetParamIndexImmediate32(index, 1));
        }
        return new D3D10RegisterKey(
            operandType,
            GetParamRegisterNumber(index));
    }

    /// <summary>
    /// The registers a shader is given rather than declared: a compute shader's
    /// thread and group indices, and a geometry shader's primitive id. None of
    /// them names a register number of its own.
    /// </summary>
    public static bool IsThreadRegister(OperandType operandType)
    {
        return operandType is OperandType.InputThreadID
            or OperandType.InputThreadGroupID
            or OperandType.InputThreadIDInGroup
            or OperandType.InputThreadIDInGroupFlattened
            or OperandType.InputPrimitiveID
            // A geometry shader instance is numbered the way a primitive is: one
            // register, no number, and an unsigned integer.
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

    // The element index of a constant buffer operand. Zero when the operand is
    // addressed relatively - cb0[r0.x] carries no constant part - so callers that
    // care must ask GetIndexRepresentation as well.
    public int GetParamConstantBufferOffset(int index)
    {
        return IsRelativelyAddressed(index, ConstantBufferElementIndex)
            ? 0
            : (int)GetParamIndexImmediate32(index, 2);
    }

    // How an operand encodes one of its indices. A relative index is a nested
    // operand - cb0[r0.x] - rather than a literal number.
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
        // The dword after the operand, either way. A texture's operand token says it
        // carries two indices and the return type is read as the second of them; a
        // typed UAV's says one, so the dword falls outside the operand and is
        // counted as another of its own.
        return Opcode == D3D10Opcode.DclUnorderedAccessViewTyped
            ? (int)GetParamIndexImmediate32(1, 0)
            : (int)GetParamIndexImmediate32(0, 2);
    }

    public uint GetResourceStructuredBufferStride()
    {
        return GetParamIndexImmediate32(1, 0);
    }

    // dcl_tgsm_structured g0, stride, count: the g# operand is followed by two plain
    // dwords, the element stride in bytes and the element count.
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
