namespace HlslDecompiler.DirectXShaderModel;

// https://docs.microsoft.com/en-us/windows-hardware/drivers/display/source-parameter-token?redirectedfrom=MSDN
// Bias through DivideByW are ps_1_x only and Not is the predicate modifier;
// d3dcompiler_47 emits only Negate, Abs and AbsAndNegate.
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
