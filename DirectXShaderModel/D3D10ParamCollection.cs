namespace HlslDecompiler.DirectXShaderModel
{
    public class D3D10ParamCollection
    {
        public uint[] Tokens { get; }
        public virtual int Count => Tokens.Length;

        public D3D10ParamCollection(uint[] paramTokens)
        {
            Tokens = paramTokens;
        }
    }
}
