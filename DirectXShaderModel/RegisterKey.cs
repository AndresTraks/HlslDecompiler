namespace HlslDecompiler.DirectXShaderModel
{
    public interface RegisterKey
    {
        public int Number { get; }

        public bool TypeEquals(RegisterKey registerKey);

        public bool IsTempRegister { get; }
        public bool IsOutput { get; }
    }
}
