namespace HlslDecompiler.Hlsl
{
    public class CastOperation : ConsumerOperation
    {
        private readonly string _mnemonic;

        public CastOperation(HlslTreeNode value, string mnemonic)
        {
            AddInput(value);
            _mnemonic = mnemonic;
        }

        public override string Mnemonic => _mnemonic;
    }
}
