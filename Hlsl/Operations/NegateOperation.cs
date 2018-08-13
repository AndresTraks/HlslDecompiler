namespace HlslDecompiler.Hlsl
{
    public class NegateOperation : UnaryOperation
    {
        public NegateOperation(HlslTreeNode value)
        {
            AddInput(value);
        }

        public override string Mnemonic => "-";

        public override HlslTreeNode Reduce()
        {
            if (Value is NegateOperation negate)
            {
                HlslTreeNode newValue = negate.Value;
                Replace(newValue);
                return newValue;
            }
            if (Value is ConstantNode constant)
            {
                var newValue = new ConstantNode(-constant.Value);
                Replace(newValue);
                return newValue;
            }
            return base.Reduce();
        }
    }
}
