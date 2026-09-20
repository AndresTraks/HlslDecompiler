namespace HlslDecompiler.DirectXShaderModel;

// https://docs.microsoft.com/en-us/windows-hardware/drivers/display/source-parameter-token?redirectedfrom=MSDN
//
// No fixture reaches most of these and none can. Bias through DivideByW are
// ps_1_x modifiers, and d3dcompiler_47 answers `error X3539: ps_1_x is no
// longer supported`; Not is the predicate modifier, and the profiles that used
// predication - ps_2_x and vs_2_x - are gone from it too, `error X3506:
// unrecognized compiler target`. Negate, Abs and AbsAndNegate are the ones a
// shader compiled today can carry, and those the fixtures do exercise.
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
