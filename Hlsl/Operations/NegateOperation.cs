namespace HlslDecompiler.Hlsl
{
    public class NegateOperation : UnaryOperation
    {
        public NegateOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public override string Mnemonic => "-";

        public override HlslTreeNode Reduce()
        {
            if (Value is NegateOperation negate)
            {
                var newValue = negate.Value;
                Replace(newValue);
                return newValue;
            }
            return base.Reduce();
        }
    }
}
