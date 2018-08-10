namespace HlslDecompiler.Hlsl
{
    public class NegateOperation : Operation
    {
        public NegateOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public HlslTreeNode Value => Children[0];

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
