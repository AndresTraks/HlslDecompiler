namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class AddConstantsTemplate : NodeTemplate<AddOperation>
    {
        private TemplateMatcher _templateMatcher;

        public AddConstantsTemplate(TemplateMatcher templateMatcher)
        {
            _templateMatcher = templateMatcher;
        }

        public override bool Match(HlslTreeNode node)
        {
            return node is AddOperation add
                && _templateMatcher.IsConstant(add.Addend1)
                && _templateMatcher.IsConstant(add.Addend2);
        }

        public override HlslTreeNode Reduce(AddOperation node)
        {
            var value = (node.Addend1 as ConstantNode).Value + (node.Addend2 as ConstantNode).Value;
            return new ConstantNode(value);
        }
    }
}
