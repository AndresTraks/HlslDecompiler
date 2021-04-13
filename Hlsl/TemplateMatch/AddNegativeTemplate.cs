namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class AddNegativeTemplate : NodeTemplate<AddOperation>
    {
        private TemplateMatcher _templateMatcher;

        public AddNegativeTemplate(TemplateMatcher templateMatcher)
        {
            _templateMatcher = templateMatcher;
        }

        public override bool Match(HlslTreeNode node)
        {
            return node is AddOperation add &&
                ((_templateMatcher.IsNegative(add.Addend1) && !_templateMatcher.IsNegative(add.Addend2)) ||
                (!_templateMatcher.IsNegative(add.Addend1) && _templateMatcher.IsNegative(add.Addend2)));
        }

        public override HlslTreeNode Reduce(AddOperation node)
        {
            if (_templateMatcher.IsNegative(node.Addend1))
            {
                return new SubtractOperation(node.Addend2, new ConstantNode(-(node.Addend1 as ConstantNode).Value));
            }
            return new SubtractOperation(node.Addend1, new ConstantNode(-(node.Addend2 as ConstantNode).Value));
        }
    }
}
