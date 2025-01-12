using System;

namespace HlslDecompiler.DirectXShaderModel
{
    public class D3D10Instruction : Instruction
    {
        public D3D10Opcode Opcode { get; }
        public D3D10OperandTokenCollection OperandTokens { get; }

        public D3D10Instruction(D3D10Opcode opcode, uint[] paramTokens)
        {
            Opcode = opcode;
            OperandTokens = new D3D10OperandTokenCollection(paramTokens);
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
                D3D10ComponentSelectionMode selectionMode = GetOperandComponentSelectionMode(destinationParamIndex);
                if (selectionMode == D3D10ComponentSelectionMode.Mask)
                {
                    Span<uint> span = OperandTokens.GetSpan(destinationParamIndex);
                    int mask = (int)((span[0] >> 4) & 0xF);
                    return mask;
                }
                else if (selectionMode == D3D10ComponentSelectionMode.Swizzle)
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
                else if (selectionMode == D3D10ComponentSelectionMode.Select1)
                {
                    throw new NotImplementedException();
                }
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

        private D3D10ComponentSelectionMode GetOperandComponentSelectionMode(int index)
        {
            Span<uint> span = OperandTokens.GetSpan(index);
            return (D3D10ComponentSelectionMode)((span[0] >> 2) & 3);
        }

        private int GetOperandComponentSwizzle(int index, int component)
        {
            D3D10OperandNumComponents componentSelection = GetOperandComponentSelection(index);
            if (componentSelection == D3D10OperandNumComponents.Operand4Component)
            {
                D3D10ComponentSelectionMode selectionMode = GetOperandComponentSelectionMode(index);
                if (selectionMode == D3D10ComponentSelectionMode.Mask)
                {
                    Span<uint> span = OperandTokens.GetSpan(index);
                    int mask = (int)((span[0] >> 4) & 0xF);
                    throw new NotImplementedException();
                    //return component;
                }
                else if (selectionMode == D3D10ComponentSelectionMode.Swizzle)
                {
                    Span<uint> span = OperandTokens.GetSpan(index);
                    int swizzle = (int)((span[0] >> 2) & 3);
                    return (swizzle >> (2 * component)) & 3;
                }
                else if (selectionMode == D3D10ComponentSelectionMode.Select1)
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
                D3D10ComponentSelectionMode selectionMode = GetOperandComponentSelectionMode(srcIndex);
                if (selectionMode == D3D10ComponentSelectionMode.Mask)
                {
                    return (0 << 0) | (1 << 2) | (2 << 4) | (3 << 6);
                }
                else if (selectionMode == D3D10ComponentSelectionMode.Swizzle)
                {
                    Span<uint> span = OperandTokens.GetSpan(srcIndex);
                    return (int)((span[0] >> 4) & 0xFF);
                }
                else if (selectionMode == D3D10ComponentSelectionMode.Select1)
                {
                    Span<uint> span = OperandTokens.GetSpan(srcIndex);
                    int component = (int)((span[0] >> 4) & 3);
                    return component * 0x55;
                }
            }
            throw new NotImplementedException();
        }

        public override string GetSourceSwizzleName(int srcIndex, int? destinationLength = null)
        {
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
            switch (GetOperandType(0))
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

        private byte[] GetOperandValueBytes(int index, int componentIndex)
        {
            Span<uint> span = OperandTokens.GetSpan(index);
            uint value;
            if (Opcode == D3D10Opcode.DclTemps)
            {
                value = span[0];
            }
            else if (Opcode == D3D10Opcode.DclConstantBuffer)
            {
                value = span[2];
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

        public override float GetParamSingle(int index)
        {
            return BitConverter.ToSingle(GetOperandValueBytes(index, 0), 0);
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
            if ((span[0] & 0x80000000f) == 0)
            {
                throw new InvalidOperationException();
            }
            return (D3D10OperandModifier) ((span[1] >> 6) & 0xFF);
        }

        public override RegisterKey GetParamRegisterKey(int index)
        {
            return new D3D10RegisterKey(
                GetOperandType(index),
                GetParamRegisterNumber(index));
        }

        public OperandType GetOperandType(int index)
        {
            Span<uint> span = OperandTokens.GetSpan(index);
            return (OperandType)((span[0] >> 12) & 0xFF);
        }

        public override string GetParamRegisterName(int index)
        {
            var operandType = GetOperandType(index);
            int registerNumber = GetParamRegisterNumber(index);

            string registerTypeName;
            switch (operandType)
            {
                case OperandType.Input:
                    registerTypeName = "v";
                    break;
                case OperandType.Output:
                    registerTypeName = "o";
                    break;
                case OperandType.Temp:
                    registerTypeName = "r";
                    break;
                case OperandType.ConstantBuffer:
                    registerTypeName = "cb";
                    break;
                default:
                    throw new NotImplementedException();
            }

            return $"{registerTypeName}{registerNumber}";
        }

        public override int GetParamRegisterNumber(int index)
        {
            Span<uint> span = OperandTokens.GetSpan(index);
            bool isExtended = (span[0] & 0x80000000) != 0;
            if (isExtended)
            {
                return (int)span[2];
            }
            if (GetOperandType(index) == OperandType.ConstantBuffer)
            {
                return (int)span[2];
            }
            return (int) span[1];
        }

        public override string ToString()
        {
            return Opcode.ToString();
        }
    }
}
