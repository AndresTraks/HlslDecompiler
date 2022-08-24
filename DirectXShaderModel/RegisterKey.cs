namespace HlslDecompiler.DirectXShaderModel
{
    public abstract class RegisterKey
    {
        public int Number { get; protected set; }

        abstract public bool TypeEquals(RegisterKey registerKey);
    }
}
