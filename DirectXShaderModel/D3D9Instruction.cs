using System;

namespace HlslDecompiler.DirectXShaderModel
{
    // Instruction
    // 80000000 set to 0
    // 40000000 co-issue
    // 20000000 reserved, set to 0
    // 10000000 predicated
    // 0F000000 instruction length (except vs_1_1, ps_1_x)
    // 07FF0000 instruction length for comment
    // 00FF0000 opcode specific control
    // 00070000 comparison
    // 00030000 texld controls
    // 0000FFFF shader instruction opcode

    // Destination parameter
    // 80000000 set to 1
    // 70001800 register type
    // 0F000000 reserved, set to 0
    // 00F00000 result modifier
    // 000F0000 write mask
    // 0000C000 reserved, set to 0
    // 00002000 relative addressing
    // 000007FF register number

    // Source parameter
    // 80000000 set to 1
    // 70001800 register type
    // 0F000000 source modifier
    // 00FF0000 swizzle
    // 0000C000 reserved, set to 0
    // 00002000 relative addressing
    // 000007FF register number

    // DCL sampler instruction
    // 80000000 set to 1
    // 78000000 texture type
    // 07FFFFFF reserved, set to 0

    public class D3D9Instruction : Instruction
    {
        public D3D9Instruction(uint instructionToken, uint[] paramTokens) 
        {
            InstructionToken = instructionToken;
            switch (Opcode)
            {
                case Opcode.Comment:
                case Opcode.Def:
                case Opcode.DefB:
                case Opcode.DefI:
                    Params = new D3D9ParamCollection(paramTokens);
                    break;
                default:
                    Params = new ParamRelativeCollection(paramTokens);
                    break;
            }
        }

        public uint InstructionToken { get; }
        public D3D9ParamCollection Params { get; }

        public Opcode Opcode => (Opcode)(InstructionToken & 0xffff);
        public IfComparison Comparison => (IfComparison)((InstructionToken >> 16) & 7);
        public TexldControls TexldControls => (TexldControls)((InstructionToken >> 16) & 3);
        public bool Predicated => (InstructionToken & 0x10000000) != 0;

        public override bool HasDestination => Opcode.HasDestination();
        public override bool IsTextureOperation => Opcode.IsTextureOperation();

        public override string GetDeclSemantic()
        {
            switch (GetParamRegisterType(1))
            {
                case RegisterType.Input:
                case RegisterType.Output:
                    string name;
                    switch (GetDeclUsage())
                    {
                        case DeclUsage.Binormal:
                        case DeclUsage.BlendIndices:
                        case DeclUsage.BlendWeight:
                        case DeclUsage.Color:
                        case DeclUsage.Depth:
                        case DeclUsage.Fog:
                        case DeclUsage.Normal:
                        case DeclUsage.Position:
                        case DeclUsage.PositionT:
                        case DeclUsage.PSize:
                        case DeclUsage.Sample:
                        case DeclUsage.Tangent:
                        case DeclUsage.TessFactor:
                        case DeclUsage.TexCoord:
                            name = GetDeclUsage().ToString().ToUpper();
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                    if (GetDeclIndex() != 0)
                    {
                        name += GetDeclIndex().ToString();
                    }
                    return name;
                case RegisterType.Sampler:
                    switch (GetDeclSamplerTextureType())
                    {
                        case SamplerTextureType.TwoD:
                            return "2d";
                        case SamplerTextureType.Cube:
                            return "cube";
                        case SamplerTextureType.Volume:
                            return "volume";
                        default:
                            throw new NotImplementedException();
                    }
                case RegisterType.MiscType:
                    switch (GetParamRegisterNumber(1))
                    {
                        case 0:
                            return "VPOS";
                        case 1:
                            return "VFACE";
                        default:
                            throw new NotImplementedException();
                    }
                default:
                    throw new NotImplementedException();
            }
        }

        // For output, input and texture declarations
        private DeclUsage GetDeclUsage()
        {
            return (DeclUsage)(Params[0] & 0x1F);
        }

        // For output, input and texture declarations
        private int GetDeclIndex()
        {
            return (int)(Params[0] >> 16) & 0x0F;
        }

        // For sampler declarations
        public SamplerTextureType GetDeclSamplerTextureType()
        {
            return (SamplerTextureType)((Params[0] >> 27) & 0xF);
        }

        public override int GetDestinationSemanticSize()
        {
            RegisterType registerType = GetParamRegisterType(GetDestinationParamIndex());
            if (registerType == RegisterType.DepthOut)
            {
                return 1;
            }
            return 4;
        }

        public override int GetDestinationParamIndex()
        {
            if (Opcode == Opcode.Dcl) return 1;

            return 0;
        }

        public override int GetDestinationWriteMask()
        {
            int destIndex = GetDestinationParamIndex();
            return (int)((Params[destIndex] >> 16) & 0xF);
        }

        public SourceModifier GetSourceModifier(int srcIndex)
        {
            return (SourceModifier)((Params[srcIndex] >> 24) & 0xF);
        }

        public override int GetSourceSwizzle(int srcIndex)
        {
            return (int)((Params[srcIndex] >> 16) & 0xFF);
        }

        public override string GetSourceSwizzleName(int srcIndex, int? destinationLength = null)
        {
            if (GetParamRegisterType(srcIndex) == RegisterType.MiscType && GetParamRegisterNumber(srcIndex) == 1) // VFACE
            {
                return "";
            }

            if (Opcode == Opcode.Loop || Opcode == Opcode.Rep)
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
                    if (Opcode == Opcode.DP2Add)
                    {
                        destinationMask = 3;
                    }
                    else if (Opcode == Opcode.Dp3)
                    {
                        destinationMask = 7;
                    }
                    else if (Opcode == Opcode.Dp4 || Opcode == Opcode.IfC || Opcode == Opcode.BreakC)
                    {
                        destinationMask = 15;
                    }
                    else
                    {
                        destinationMask = GetDestinationWriteMask();
                    }
                    break;
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

        public RegisterType GetParamRegisterType(int index)
        {
            uint p = Params[index];
            return (RegisterType)(((p >> 28) & 0x7) | ((p >> 8) & 0x18));
        }

        public override float GetParamSingle(int index)
        {
            return BitConverter.ToSingle(GetParamBytes(index), 0);
        }

        public override float GetParamInt(int index)
        {
            return BitConverter.ToInt32(GetParamBytes(index), 0);
        }

        private byte[] GetParamBytes(int index)
        {
            return BitConverter.GetBytes(Params[index]);
        }

        public override RegisterKey GetParamRegisterKey(int index)
        {
            return new D3D9RegisterKey(
                GetParamRegisterType(index),
                GetParamRegisterNumber(index));
        }

        public RegisterType GetRelativeParamRegisterType(int index)
        {
            uint p = Params.GetRelativeToken(index);
            return (RegisterType)(((p >> 28) & 0x7) | ((p >> 8) & 0x18));
        }

        public override int GetParamRegisterNumber(int index)
        {
            return (int)(Params[index] & 0x7FF);
        }

        public ResultModifier GetDestinationResultModifier()
        {
            int destIndex = GetDestinationParamIndex();
            return (ResultModifier)((Params[destIndex] >> 20) & 0xF);
        }

        public override string ToString()
        {
            return Opcode.ToString();
        }
    }

    public enum IfComparison
    {
        None,
        GT,
        EQ,
        GE,
        LT,
        NE,
        LE,
        Reserved
    }

    [Flags]
    public enum TexldControls
    {
        None = 0,
        Project = 1,
        Bias = 2
    }
}
