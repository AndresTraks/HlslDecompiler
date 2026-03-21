namespace HlslDecompiler.DirectXShaderModel;

// https://docs.microsoft.com/en-us/windows-hardware/drivers/display/source-parameter-token?redirectedfrom=MSDN
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
