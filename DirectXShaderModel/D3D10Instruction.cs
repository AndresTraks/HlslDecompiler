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
    public D3D10OperandTokenCollection OperandTokens { get; }

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
                case D3D10Opcode.DclIndexableTemp:
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
                case D3D10Opcode.Dp2:
                case D3D10Opcode.Dp3:
                case D3D10Opcode.Dp4:
                case D3D10Opcode.GE:
                case D3D10Opcode.IAdd:
                case D3D10Opcode.IToF:
                case D3D10Opcode.LdStructured:
                case D3D10Opcode.Mad:
                case D3D10Opcode.Mov:
                case D3D10Opcode.MovC:
                case D3D10Opcode.Mul:
                case D3D10Opcode.Rsq:
                case D3D10Opcode.Sample:
                case D3D10Opcode.SampleC:
                case D3D10Opcode.SampleCLZ:
                case D3D10Opcode.SampleL:
                case D3D10Opcode.SampleD:
                case D3D10Opcode.SampleB:
                case D3D10Opcode.SinCos:
                case D3D10Opcode.Sqrt:
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

    public override int GetDestinationParamIndex()
    {
        return 0;
    }

    public override int GetDestinationWriteMask()
    {
        return GetOperandWriteMask(GetDestinationParamIndex());
    }

    public int GetOperandWriteMask(int operandIndex)
    {
        D3D10OperandNumComponents componentSelection = GetOperandComponentSelection(operandIndex);
        if (componentSelection == D3D10OperandNumComponents.Operand1Component)
        {
            throw new NotImplementedException();
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
                int name = (int)((span[0] >> 4) & 0x2);
                return 1 << name;
            }
        }
        else if (componentSelection == D3D10OperandNumComponents.Operand0Component)
        {
            return 0;
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
                return "linearCentroid";
            case D3D10InterpolationMode.LinearNoPerspective:
                return "linearNoPerspective";
            case D3D10InterpolationMode.LinearNoPerspectiveCentroid:
                return "linearNoPerspectiveCentroid";
            case D3D10InterpolationMode.LinearSample:
                return "linearSample";
            case D3D10InterpolationMode.LinearNoPerspectiveSample:
                return "linearNoPerspectiveSample";
            default:
                throw new NotImplementedException();
        }
    }

    private D3D10InterpolationMode GetInterpolationMode()
    {
        Span<uint> span = OperandTokens.GetSpan(0);
        return (D3D10InterpolationMode)((span[0] >> 11) & 0xF);
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
        if (GetOperandComponentSelection(srcIndex) == D3D10OperandNumComponents.Operand0Component)
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
                else
                {
                    destinationMask = GetDestinationWriteMask();
                    destinationLength = GetDestinationMaskLength();
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
        int destIndex = GetDestinationParamIndex();
        OperandType operandType = GetOperandType(destIndex);
        string name = operandType switch
        {
            OperandType.Input => "SV_Position",
            OperandType.Output => "SV_Target",
            OperandType.InputThreadID => "SV_DispatchThreadID",
            _ => throw new NotImplementedException()
        };
        if (operandType != OperandType.InputThreadID)
        {
            int numberIndex = (_isGeometryShader && operandType == OperandType.Input) ? 2 : 1;
            int declIndex = (int) GetParamIndexImmediate32(destIndex, numberIndex);
            name += declIndex;
        }
        return name;
    }

    private byte[] GetOperandValueBytes(int index, int componentIndex)
    {
        Span<uint> span = OperandTokens.GetSpan(index);
        uint value;
        if (Opcode == D3D10Opcode.DclTemps || Opcode == D3D10Opcode.DclGSMaxOutputVertexCount)
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

    public override float GetParamInt(int index)
    {
        return BitConverter.ToInt32(GetOperandValueBytes(index, 0), 0);
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
            if (Opcode == D3D10Opcode.Discard)
            {
                return new D3D10RegisterKey([ GetParamInt(index) ]);
            }
            return new D3D10RegisterKey(GetParamSingle(index));
        }
        else if (operandType == OperandType.InputThreadID)
        {
            return new D3D10RegisterKey(operandType, 0);
        }
        if (_isGeometryShader && operandType == OperandType.Input)
        {
            return D3D10RegisterKey.CreateGSInput(
                (int)GetParamIndexImmediate32(index, 2),
                (int)GetParamIndexImmediate32(index, 1));
        }
        return new D3D10RegisterKey(
            operandType,
            GetParamRegisterNumber(index));
    }

    public OperandType GetOperandType(int index)
    {
        Span<uint> span = OperandTokens.GetSpan(index);
        return (OperandType)((span[0] >> 12) & 0xFF);
    }

    public override int GetParamRegisterNumber(int index)
    {
        return (int) GetParamIndexImmediate32(index, 1);
    }

    public int GetParamConstantBufferOffset(int index)
    {
        return (int) GetParamIndexImmediate32(index, 2);
    }

    public int GetResourceReturnTypeToken()
    {
        return (int) GetParamIndexImmediate32(0, 2);
    }

    public uint GetResourceStructuredBufferStride()
    {
        return GetParamIndexImmediate32(1, 0);
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
