namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class AddConstantsTemplate : NodeTemplate<AddOperation>
    {
        public override bool Match(HlslTreeNode node)
        {
            return node is AddOperation add
                && ConstantMatcher.IsConstant(add.Addend1)
                && ConstantMatcher.IsConstant(add.Addend2);
        }

        public override HlslTreeNode Reduce(AddOperation node)
        {
            var value = (node.Addend1 as ConstantNode).Value + (node.Addend2 as ConstantNode).Value;
            return new ConstantNode(value);
        }
    }
}
