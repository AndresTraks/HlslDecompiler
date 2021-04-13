namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class AddZeroTemplate : NodeTemplate<AddOperation>
    {
        private TemplateMatcher _templateMatcher;

        public AddZeroTemplate(TemplateMatcher templateMatcher)
        {
            _templateMatcher = templateMatcher;
        }

        public override bool Match(HlslTreeNode node)
        {
            return node is AddOperation add &&
                (_templateMatcher.IsZero(add.Addend1) || _templateMatcher.IsZero(add.Addend2));
        }

        public override HlslTreeNode Reduce(AddOperation node)
        {
            return _templateMatcher.IsZero(node.Addend1)
                ? node.Addend2
                : node.Addend1;
        }
    }
}
