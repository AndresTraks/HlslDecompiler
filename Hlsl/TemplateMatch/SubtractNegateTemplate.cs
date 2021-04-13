namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class SubtractNegateTemplate : NodeTemplate<SubtractOperation>
    {
        public override bool Match(HlslTreeNode node)
        {
            return node is SubtractOperation subtract && subtract.Subtrahend is NegateOperation;
        }

        public override HlslTreeNode Reduce(SubtractOperation node)
        {
            return new AddOperation(node.Minuend, (node.Subtrahend as NegateOperation).Value);
        }
    }
}
