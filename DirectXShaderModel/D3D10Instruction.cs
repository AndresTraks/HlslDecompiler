using System;

namespace HlslDecompiler.DirectXShaderModel
{
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

        public D3D10Opcode Opcode { get; }
        public D3D10OperandTokenCollection OperandTokens { get; }

        public D3D10Instruction(D3D10Opcode opcode, uint[] paramTokens)
        {
            Opcode = opcode;
            OperandTokens = new D3D10OperandTokenCollection(paramTokens, opcode == D3D10Opcode.DclResource);
        }

        public D3D10Instruction(D3D10Opcode opcode, uint[] paramTokens, ResourceDimension resourceDimension)
            : this(opcode, paramTokens)
        {
            _resourceDimension = resourceDimension;
        }

        public ResourceDimension GetResourceDimension()
        {
            return _resourceDimension;
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
            int destinationParamIndex = GetDestinationParamIndex();

            D3D10OperandNumComponents componentSelection = GetOperandComponentSelection(destinationParamIndex);
            if (componentSelection == D3D10OperandNumComponents.Operand1Component ||
                componentSelection == D3D10OperandNumComponents.Operand4Component)
            {
                ComponentSelectionMode selectionMode = GetOperandComponentSelectionMode(destinationParamIndex);
                if (selectionMode == ComponentSelectionMode.Mask)
                {
                    Span<uint> span = OperandTokens.GetSpan(destinationParamIndex);
                    int mask = (int)((span[0] >> 4) & 0xF);
                    return mask;
                }
                else if (selectionMode == ComponentSelectionMode.Swizzle)
                {
                    int mask = 0;
                    int dimension = GetOperandIndexDimension(destinationParamIndex);
                    for (int i = 0; i < dimension; i++)
                    {
                        int swizzle = GetOperandComponentSwizzle(destinationParamIndex, i);
                        if (swizzle != i)
                        {
                            mask |= 1 << swizzle;
                        }
                    }
                    return mask;
                }
                else if (selectionMode == ComponentSelectionMode.Select1)
                {
                    throw new NotImplementedException();
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

        private int GetOperandComponentSwizzle(int index, int component)
        {
            D3D10OperandNumComponents componentSelection = GetOperandComponentSelection(index);
            if (componentSelection == D3D10OperandNumComponents.Operand4Component)
            {
                ComponentSelectionMode selectionMode = GetOperandComponentSelectionMode(index);
                if (selectionMode == ComponentSelectionMode.Mask)
                {
                    Span<uint> span = OperandTokens.GetSpan(index);
                    int mask = (int)((span[0] >> 4) & 0xF);
                    throw new NotImplementedException();
                    //return component;
                }
                else if (selectionMode == ComponentSelectionMode.Swizzle)
                {
                    Span<uint> span = OperandTokens.GetSpan(index);
                    int swizzle = (int)((span[0] >> 2) & 3);
                    return (swizzle >> (2 * component)) & 3;
                }
                else if (selectionMode == ComponentSelectionMode.Select1)
                {
                    throw new NotImplementedException();
                }
            }
            throw new NotImplementedException();
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
            switch (Opcode)
            {
                case D3D10Opcode.Dp2:
                    destinationMask = 3;
                    break;
                case D3D10Opcode.Dp3:
                    destinationMask = 7;
                    break;
                case D3D10Opcode.Dp4:
                    destinationMask = 15;
                    break;
                default:
                    destinationMask = GetDestinationWriteMask();
                    break;
            }

            byte[] swizzle = GetSourceSwizzleComponents(srcIndex);

            string swizzleName = "";
            for (int i = 0; i < 4; i++)
            {
                if ((destinationMask & (1 << i)) != 0)
                {
                    switch (swizzle[i])
                    {
                        case 0:
                            swizzleName += "x";
                            break;
                        case 1:
                            swizzleName += "y";
                            break;
                        case 2:
                            swizzleName += "z";
                            break;
                        case 3:
                            swizzleName += "w";
                            break;
                    }
                }
            }
            switch (swizzleName)
            {
                case "xyzw":
                    return "";
                case "xxxx":
                    return ".x";
                case "yyyy":
                    return ".y";
                case "zzzz":
                    return ".z";
                case "wwww":
                    return ".w";
                default:
                    return "." + swizzleName;
            }
        }

        public override string GetDeclSemantic()
        {
            string name;
            switch (GetOperandType(GetDestinationParamIndex()))
            {
                case OperandType.Input:
                    name = "SV_Position";
                    break;
                case OperandType.Output:
                    name = "SV_Target";
                    break;
                default:
                    throw new NotImplementedException();
            }
            int declIndex = (int) OperandTokens.GetSpan(0)[1];
            if (declIndex != 0)
            {
                name += declIndex;
            }
            return name;
        }

        public override int GetDestinationSemanticSize()
        {
            if (GetOperandType(GetDestinationParamIndex()) == OperandType.OutputDepth)
            {
                return 1;
            }
            return 4;
        }

        private byte[] GetOperandValueBytes(int index, int componentIndex)
        {
            Span<uint> span = OperandTokens.GetSpan(index);
            uint value;
            if (Opcode == D3D10Opcode.DclTemps)
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

        public override RegisterKey GetParamRegisterKey(int index)
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
            Span<uint> span = OperandTokens.GetSpan(index);
            bool isExtended = (span[0] & 0x80000000) != 0;
            if (isExtended)
            {
                return (int)span[2];
            }
            return (int) span[1];
        }

        public int GetParamConstantBufferOffset(int index)
        {
            Span<uint> span = OperandTokens.GetSpan(index);
            bool isExtended = (span[0] & 0x80000000) != 0;
            if (isExtended)
            {
                return (int)span[3];
            }
            return (int)span[2];
        }

        public int GetResourceReturnTypeToken()
        {
            Span<uint> span = OperandTokens.GetSpan(0);
            bool isExtended = (span[0] & 0x80000000) != 0;
            if (isExtended)
            {
                return (int)span[3];
            }
            return (int)span[2];
        }

        public override string ToString()
        {
            return Opcode.ToString();
        }
    }
}
