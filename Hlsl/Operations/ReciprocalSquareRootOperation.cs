namespace HlslDecompiler.Hlsl
{
    public class ReciprocalSquareRootOperation : UnaryOperation
    {
        public ReciprocalSquareRootOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public override string Mnemonic => "rsqrt";

        public override HlslTreeNode Reduce()
        {
            var squareRoot = new SquareRootOperation(Value);
            var division = new DivisionOperation(new ConstantNode(1), squareRoot);
            Replace(division);
            return division;
        }
    }
}
