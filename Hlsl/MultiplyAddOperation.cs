namespace HlslDecompiler.Hlsl
{
    public class MultiplyAddOperation : Operation
    {
        public MultiplyAddOperation(HlslTreeNode factor1, HlslTreeNode factor2, HlslTreeNode addend)
             : base(OperationType.MultiplyAdd)
        {
            AddChild(factor1);
            AddChild(factor2);
            AddChild(addend);
        }

        public HlslTreeNode Factor1 => Children[0];
        public HlslTreeNode Factor2 => Children[1];
        public HlslTreeNode Addend => Children[2];

        public override HlslTreeNode Reduce()
        {
            var multiplication = new MultiplyOperation(Factor1, Factor2);
            Factor1.Parents.Remove(this);
            Factor2.Parents.Remove(this);

            var addition = new AddOperation(multiplication, Addend);
            Replace(addition);

            return addition.Reduce();
        }
    }
}
