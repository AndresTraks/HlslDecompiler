namespace HlslDecompiler.DirectXShaderModel
{
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
}
