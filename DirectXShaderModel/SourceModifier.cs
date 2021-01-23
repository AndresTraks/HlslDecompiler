namespace HlslDecompiler.DirectXShaderModel
{
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
}
