namespace HlslDecompiler.Hlsl.TemplateMatch
{
    // 3 by 3 dot product has a pattern of:
    // #1  dot(ab, xy) + c*z
    // #2  c*z + dot(ab, xy)
    public class DotProduct3Template : IGroupTemplate
    {
        private TemplateMatcher _templateMatcher;
        private bool allowMatrixColumn = true;

        public DotProduct3Template(TemplateMatcher templateMatcher)
        {
            _templateMatcher = templateMatcher;
        }

        public IGroupContext Match(HlslTreeNode node)
        {
            return MatchDotProduct3(node);
        }

        private DotProductContext MatchDotProduct3(HlslTreeNode node)
        {
            if (!(node is AddOperation addition))
            {
                return null;
            }

            MultiplyOperation cz;
            if (addition.Addend1 is DotProductOperation dot && dot.X.Length == 2)
            {
                cz = addition.Addend2 as MultiplyOperation;
                if (cz == null)
                {
                    return null;
                }
            }
            else
            {
                dot = addition.Addend2 as DotProductOperation;
                if (dot == null || dot.X.Length != 2)
                {
                    return null;
                }

                cz = addition.Addend1 as MultiplyOperation;
                if (cz == null)
                {
                    return null;
                }
            }

            HlslTreeNode b = dot.X.Inputs[1];
            HlslTreeNode c = cz.Factor1;
            if (_templateMatcher.CanGroupComponents(b, c, allowMatrixColumn))
            {
                HlslTreeNode a = dot.X.Inputs[0];
                HlslTreeNode x = dot.Y.Inputs[0];
                HlslTreeNode y = dot.Y.Inputs[1];
                HlslTreeNode z = cz.Factor2;
                if (allowMatrixColumn && _templateMatcher.SharesMatrixColumnOrRow(a, b))
                {
                    // If one of the arguments is a matrix, allow the other argument to be arbitrary.
                    return new DotProductContext(new GroupNode(a, b, c), new GroupNode(x, y, z));
                }
                if (_templateMatcher.CanGroupComponents(y, z, allowMatrixColumn))
                {
                    return new DotProductContext(new GroupNode(a, b, c), new GroupNode(x, y, z));
                }
            }

            return null;
        }

        public HlslTreeNode Reduce(HlslTreeNode node, IGroupContext groupContext)
        {
            var dotProductContext = groupContext as DotProductContext;
            return new DotProductOperation(dotProductContext.Value1, dotProductContext.Value2);
        }
    }
}
