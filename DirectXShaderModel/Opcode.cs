namespace HlslDecompiler.DirectXShaderModel
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

    public static class Extensions
    {
        public static bool HasDestination(this Opcode opcode)
        {
            switch (opcode)
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
        public static bool IsTextureOperation(this Opcode opcode)
        {
            switch (opcode)
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
}
