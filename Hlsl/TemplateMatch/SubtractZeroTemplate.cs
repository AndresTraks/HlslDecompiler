namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class SubtractZeroTemplate : NodeTemplate<SubtractOperation>
    {
        private TemplateMatcher _templateMatcher;

        public SubtractZeroTemplate(TemplateMatcher templateMatcher)
        {
            _templateMatcher = templateMatcher;
        }

        public override bool Match(HlslTreeNode node)
        {
            return node is SubtractOperation subtract &&
                ((_templateMatcher.IsZero(subtract.Minuend) && !_templateMatcher.IsZero(subtract.Subtrahend))
                || (!_templateMatcher.IsZero(subtract.Minuend) && _templateMatcher.IsZero(subtract.Subtrahend)));
        }

        public override HlslTreeNode Reduce(SubtractOperation node)
        {
            if (_templateMatcher.IsZero(node.Minuend))
            {
                return new NegateOperation(node.Subtrahend);
            }
            return node.Minuend;
        }
    }
}
