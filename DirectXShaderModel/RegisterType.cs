namespace HlslDecompiler.DirectXShaderModel
{
    // https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/d3d9types/ne-d3d9types-_d3dshader_param_register_type
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
