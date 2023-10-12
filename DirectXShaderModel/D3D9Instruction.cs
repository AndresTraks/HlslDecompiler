using System;

namespace HlslDecompiler.DirectXShaderModel
{
    public class D3D9Instruction : Instruction
    {
        public Opcode Opcode { get; }
        public D3D9ParamCollection Params { get; }

        public D3D9Instruction(Opcode opcode, uint[] paramTokens) 
        {
            Opcode = opcode;
            switch (opcode)
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

        public override bool HasDestination
        {
            get
            {
                switch (Opcode)
                {
                    case Opcode.Abs:
                    case Opcode.Add:
                    case Opcode.Bem:
                    case Opcode.Cmp:
                    case Opcode.Cnd:
                    case Opcode.Crs:
                    case Opcode.Dcl:
                    case Opcode.Def:
                    case Opcode.DefB:
                    case Opcode.DefI:
                    case Opcode.DP2Add:
                    case Opcode.Dp3:
                    case Opcode.Dp4:
                    case Opcode.Dst:
                    case Opcode.DSX:
                    case Opcode.DSY:
                    case Opcode.Exp:
                    case Opcode.ExpP:
                    case Opcode.Frc:
                    case Opcode.Lit:
                    case Opcode.Log:
                    case Opcode.LogP:
                    case Opcode.Lrp:
                    case Opcode.M3x2:
                    case Opcode.M3x3:
                    case Opcode.M3x4:
                    case Opcode.M4x3:
                    case Opcode.M4x4:
                    case Opcode.Mad:
                    case Opcode.Max:
                    case Opcode.Min:
                    case Opcode.Mov:
                    case Opcode.MovA:
                    case Opcode.Mul:
                    case Opcode.Nrm:
                    case Opcode.Pow:
                    case Opcode.Rcp:
                    case Opcode.Rsq:
                    case Opcode.SetP:
                    case Opcode.Sge:
                    case Opcode.Sgn:
                    case Opcode.SinCos:
                    case Opcode.Slt:
                    case Opcode.Sub:
                    case Opcode.Tex:
                    case Opcode.TexBem:
                    case Opcode.TexBeml:
                    case Opcode.TexCoord:
                    case Opcode.TexDepth:
                    case Opcode.TexDP3:
                    case Opcode.TexDP3Tex:
                    case Opcode.TexKill:
                    case Opcode.TexLDD:
                    case Opcode.TexLDL:
                    case Opcode.TexM3x2Depth:
                    case Opcode.TeXM3x2Pad:
                    case Opcode.TexM3x2Tex:
                    case Opcode.TexM3x3:
                    case Opcode.TexM3x3Diff:
                    case Opcode.TeXM3x3Pad:
                    case Opcode.TexM3x3Spec:
                    case Opcode.TexM3x3Tex:
                    case Opcode.TexM3x3VSpec:
                    case Opcode.TexReg2AR:
                    case Opcode.TexReg2GB:
                    case Opcode.TexReg2RGB:
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
                    case Opcode.Tex:
                    case Opcode.TexBem:
                    case Opcode.TexBeml:
                    case Opcode.TexCoord:
                    case Opcode.TexDepth:
                    case Opcode.TexDP3:
                    case Opcode.TexDP3Tex:
                    case Opcode.TexKill:
                    case Opcode.TexLDD:
                    case Opcode.TexLDL:
                    case Opcode.TexM3x2Depth:
                    case Opcode.TeXM3x2Pad:
                    case Opcode.TexM3x2Tex:
                    case Opcode.TexM3x3:
                    case Opcode.TexM3x3Diff:
                    case Opcode.TeXM3x3Pad:
                    case Opcode.TexM3x3Spec:
                    case Opcode.TexM3x3Tex:
                    case Opcode.TexM3x3VSpec:
                    case Opcode.TexReg2AR:
                    case Opcode.TexReg2GB:
                    case Opcode.TexReg2RGB:
                        return true;
                    default:
                        return false;
                }
            }
        }

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
                    if (GetParamRegisterNumber(1) == 0)
                    {
                        return "vFace";
                    }
                    if (GetParamRegisterNumber(1) == 1)
                    {
                        return "vPos";
                    }
                    throw new NotImplementedException();
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
        private SamplerTextureType GetDeclSamplerTextureType()
        {
            return (SamplerTextureType)((Params[0] >> 27) & 0xF);
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

        public override string GetSourceSwizzleName(int srcIndex)
        {
            int destinationMask;
            switch (Opcode)
            {
                case Opcode.Dp3:
                    destinationMask = 7;
                    break;
                case Opcode.Dp4:
                case Opcode.IfC:
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

        public override string GetParamRegisterName(int index)
        {
            var registerType = GetParamRegisterType(index);
            int registerNumber = GetParamRegisterNumber(index);

            string registerTypeName;
            switch (registerType)
            {
                case RegisterType.Addr:
                    registerTypeName = "a";
                    break;
                case RegisterType.Const:
                    registerTypeName = "c";
                    break;
                case RegisterType.Const2:
                    registerTypeName = "c";
                    registerNumber += 2048;
                    break;
                case RegisterType.Const3:
                    registerTypeName = "c";
                    registerNumber += 4096;
                    break;
                case RegisterType.Const4:
                    registerTypeName = "c";
                    registerNumber += 6144;
                    break;
                case RegisterType.ConstBool:
                    registerTypeName = "b";
                    break;
                case RegisterType.ConstInt:
                    registerTypeName = "i";
                    break;
                case RegisterType.Input:
                    registerTypeName = "v";
                    break;
                case RegisterType.Output:
                    registerTypeName = "o";
                    break;
                case RegisterType.RastOut:
                    registerTypeName = "rast";
                    break;
                case RegisterType.Temp:
                    registerTypeName = "r";
                    break;
                case RegisterType.Sampler:
                    registerTypeName = "s";
                    break;
                case RegisterType.ColorOut:
                    registerTypeName = "oC";
                    break;
                case RegisterType.DepthOut:
                    return "oDepth";
                case RegisterType.MiscType:
                    if (registerNumber == 0)
                    {
                        return "vFace";
                    }
                    else if (registerNumber == 1)
                    {
                        return "vPos";
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                case RegisterType.Loop:
                    return "aL";
                default:
                    throw new NotImplementedException();
            }

            string relativeAddressing = string.Empty;
            if (Params.HasRelativeAddressing(index))
            {
                RegisterType relativeType = GetRelativeParamRegisterType(index);
                switch (relativeType)
                {
                    case RegisterType.Loop:
                        relativeAddressing = "[aL]";
                        break;
                    case RegisterType.Addr:
                        relativeAddressing = "";
                        break;
                    default:
                        throw new NotSupportedException(relativeType.ToString());
                }
            }

            return $"{registerTypeName}{registerNumber}{relativeAddressing}";
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
}
