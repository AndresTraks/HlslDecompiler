namespace HlslDecompiler.Hlsl
{
    public class NegateOperation : Operation
    {
        public NegateOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public override HlslTreeNode Reduce()
        {
            var value = Children[0];
            if (value is NegateOperation)
            {
                var newValue = value.Children[0];
                Replace(newValue);
                return newValue;
            }
            return base.Reduce();
        }

        public override string ToString()
        {
            return $"-({Children[0]})";
        }
    }
}
