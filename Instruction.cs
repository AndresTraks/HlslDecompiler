using System;

namespace HlslDecompiler
{
    // D3DSIO_X
    // https://msdn.microsoft.com/en-us/library/windows/hardware/ff569706
    public enum Opcode
    {
        Nop,
        Mov,
        Add,
        Sub,
        Mad,
        Mul,
        Rcp,
        Rsq,
        Dp3,
        Dp4,
        Min,
        Max,
        Slt,
        Sge,
        Exp,
        Log,
        Lit,
        Dst,
        Lrp,
        Frc,
        M4x4,
        M4x3,
        M3x4,
        M3x3,
        M3x2,
        Call,
        CallNZ,
        Loop,
        Ret,
        EndLoop,
        Label,
        Dcl,
        Pow,
        Crs,
        Sgn,
        Abs,
        Nrm,
        SinCos,
        Rep,
        EndRep,
        If,
        IfC,
        Else,
        Endif,
        Break,
        BreakC,
        MovA,
        DefB,
        DefI,
        TexCoord = 64,
        TexKill,
        Tex,
        TexBem,
        TexBeml,
        TexReg2AR,
        TexReg2GB,
        TeXM3x2Pad,
        TexM3x2Tex,
        TeXM3x3Pad,
        TexM3x3Tex,
        TexM3x3Diff,
        TexM3x3Spec,
        TexM3x3VSpec,
        ExpP,
        LogP,
        Cnd,
        Def,
        TexReg2RGB,
        TexDP3Tex,
        TexM3x2Depth,
        TexDP3,
        TexM3x3,
        TexDepth,
        Cmp,
        Bem,
        DP2Add,
        DSX,
        DSY,
        TexLDD,
        SetP,
        TexLDL,
        Breakp,
        Phase = 0xFFFD,
        Comment = 0xFFFE,
        End = 0xFFFF
    }

    // https://msdn.microsoft.com/en-us/library/windows/hardware/ff569707%28v=vs.85%29.aspx
    public enum RegisterType
    {
        Temp,
        Input,
        Const,
        Texture,
        Addr = Texture,
        RastOut,
        AttrOut,
        Output,
        ConstInt,
        ColorOut,
        DepthOut,
        Sampler,
        Const2,
        Const3,
        Const4,
        ConstBool,
        Loop,
        TempFloat16,
        MiscType,
        Label,
        Predicate
    }

    // https://msdn.microsoft.com/en-us/library/windows/hardware/ff552738%28v=vs.85%29.aspx
    public enum ResultModifier
    {
        None,
        Saturate,
        PartialPrecision,
        Centroid
    }

    // https://msdn.microsoft.com/en-us/library/windows/hardware/ff569716%28v=vs.85%29.aspx
    public enum SourceModifier
    {
        None,
        Negate,
        Bias,
        BiasAndNegate,
        Sign,
        SignAndNegate,
        Complement,
        X2,
        X2AndNegate,
        DivideByZ,
        DivideByW,
        Abs,
        AbsAndNegate,
        Not
    }

    enum IfComparison
    {
        None,
        GT,
        EQ,
        GE,
        LT,
        NE,
        LE
    }

    public class Instruction
    {
        public Opcode Opcode { get; private set; }
        public uint[] Params { get; private set; }

        public int Modifier { get; set; }
        public bool Predicated { get; set; }

        public bool HasDestination
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

        public bool IsTextureOperation
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

        public Instruction(Opcode opcode, int numParams)
        {
            Opcode = opcode;
            Params = new uint[numParams];
        }

        byte[] GetParamBytes(int index)
        {
            return BitConverter.GetBytes(Params[index]);
        }

        public float GetParamSingle(int index)
        {
            return BitConverter.ToSingle(GetParamBytes(index), 0);
        }

        public RegisterKey GetParamRegisterKey(int index)
        {
            return new RegisterKey(
                GetParamRegisterType(index),
                GetParamRegisterNumber(index));
        }

        public RegisterType GetParamRegisterType(int index)
        {
            uint p = Params[index];
            return (RegisterType)(((p >> 28) & 0x7) | ((p >> 8) & 0x18));
        }

        public int GetParamRegisterNumber(int index)
        {
            return (int)(Params[index] & 0x7FF);
        }

        public string GetParamRegisterName(int index)
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
                    registerTypeName = "oDepth";
                    break;
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
                default:
                    throw new NotImplementedException();
            }

            return $"{registerTypeName}{registerNumber}";
        }

        public int GetDestinationParamIndex()
        {
            if (Opcode == Opcode.Dcl) return 1;

            return 0;
        }

        public ResultModifier GetDestinationResultModifier()
        {
            int destIndex = GetDestinationParamIndex();
            return (ResultModifier)((Params[destIndex] >> 20) & 0xF);
        }

        public int GetDestinationWriteMask()
        {
            int destIndex = GetDestinationParamIndex();
            return (int)((Params[destIndex] >> 16) & 0xF);
        }

        public string GetDestinationWriteMaskName(int destinationLength, bool hlsl)
        {
            int writeMask = GetDestinationWriteMask();
            int writeMaskLength = GetDestinationMaskLength();

            if (!hlsl)
            {
                destinationLength = 4; // explicit mask in assembly
            }

            // Check if mask is the same length and of the form .xyzw
            if (writeMaskLength == destinationLength && writeMask == ((1 << writeMaskLength) - 1))
            {
                return "";
            }

            string writeMaskName =
                string.Format(".{0}{1}{2}{3}",
                ((writeMask & 1) != 0) ? "x" : "",
                ((writeMask & 2) != 0) ? "y" : "",
                ((writeMask & 4) != 0) ? "z" : "",
                ((writeMask & 8) != 0) ? "w" : "");
            return writeMaskName;
        }

        // Length of ".yw" = 4 (xyzw)
        public int GetDestinationMaskedLength()
        {
            int writeMask = GetDestinationWriteMask();
            for (int i = 3; i != 0; i--)
            {
                if ((writeMask & (1 << i)) != 0)
                {
                    return i + 1;
                }
            }
            return 0;
        }

        // Length of ".yw" = 2
        public int GetDestinationMaskLength()
        {
            int writeMask = GetDestinationWriteMask();
            int length = 0;
            for (int i = 0; i < 4; i++)
            {
                if ((writeMask & (1 << i)) != 0)
                {
                    length++;
                }
            }
            return length;
        }

        public int GetSourceSwizzle(int srcIndex)
        {
            return (int)((Params[srcIndex] >> 16) & 0xFF);
        }

        public byte[] GetSourceSwizzleComponents(int srcIndex)
        {
            int swizzle = GetSourceSwizzle(srcIndex);
            byte[] swizzleArray = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                swizzleArray[i] = (byte)((swizzle >> (i * 2)) & 0x3);
            }
            return swizzleArray;
        }

        public string GetSourceSwizzleName(int srcIndex)
        {
            int swizzleLength;
            if (Opcode == Opcode.Dp4)
            {
                swizzleLength = 4;
            }
            else if (Opcode == Opcode.Dp3)
            {
                swizzleLength = 3;
            }
            else if (HasDestination)
            {
                swizzleLength = GetDestinationMaskLength();
            }
            else
            {
                swizzleLength = 4;
            }

            string swizzleName = "";
            byte[] swizzle = GetSourceSwizzleComponents(srcIndex);
            for (int i = 0; i < swizzleLength; i++)
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

        public SourceModifier GetSourceModifier(int srcIndex)
        {
            return (SourceModifier)((Params[srcIndex] >> 24) & 0xF);
        }

        // For output, input and texture declarations
        public DeclUsage GetDeclUsage()
        {
            return (DeclUsage)(Params[0] & 0x1F);
        }

        // For output, input and texture declarations
        public int GetDeclIndex()
        {
            return (int)(Params[0] >> 16) & 0x0F;
        }

        // For sampler declarations
        public SamplerTextureType GetDeclSamplerTextureType()
        {
            return (SamplerTextureType)((Params[0] >> 27) & 0xF);
        }

        public string GetDeclSemantic()
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

        public override string ToString()
        {
            return Opcode.ToString();
        }
    }
}
