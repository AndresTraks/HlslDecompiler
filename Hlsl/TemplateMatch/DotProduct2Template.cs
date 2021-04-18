using HlslDecompiler.Operations;

namespace HlslDecompiler.Hlsl.TemplateMatch
{
    // 2 by 2 dot product has a pattern of:
    // a*x + b*y
    public class DotProduct2Template : IGroupTemplate
    {
        private TemplateMatcher _templateMatcher;
        private bool allowMatrixColumn = true;

        public DotProduct2Template(TemplateMatcher templateMatcher)
        {
            _templateMatcher = templateMatcher;
        }

        public IGroupContext Match(HlslTreeNode node)
        {
            return MatchDotProduct2(node);
        }

        private DotProductContext MatchDotProduct2(HlslTreeNode node)
        {
            if (!(node is AddOperation add) ||
                !(add.Addend1 is MultiplyOperation ax) ||
                !(add.Addend2 is MultiplyOperation by))
            {
                return null;
            }

            HlslTreeNode a = ax.Factor1;
            HlslTreeNode b = by.Factor1;
            HlslTreeNode x = ax.Factor2;
            HlslTreeNode y = by.Factor2;

            if (a is ConstantNode || b is ConstantNode || x is ConstantNode || y is ConstantNode)
            {
                return null;
            }

            if (_templateMatcher.CanGroupComponents(a, b, allowMatrixColumn) == false)
            {
                if (_templateMatcher.CanGroupComponents(a, y, allowMatrixColumn) == false)
                {
                    if (allowMatrixColumn && _templateMatcher.SharesMatrixColumnOrRow(x, y))
                    {
                        // If one of the arguments is a matrix, allow the other argument to be arbitrary.
                        return new DotProductContext(new GroupNode(a, b), new GroupNode(x, y));
                    }
                    return null;
                }
                Swap(ref b, ref y);
            }

            if (allowMatrixColumn && _templateMatcher.SharesMatrixColumnOrRow(a, b))
            {
                // If one of the arguments is a matrix, allow the other argument to be arbitrary.
            }
            else if (_templateMatcher.CanGroupComponents(x, y, allowMatrixColumn) == false)
            {
                return null;
            }

            return new DotProductContext(new GroupNode(a, b), new GroupNode(x, y));
        }

        public HlslTreeNode Reduce(HlslTreeNode node, IGroupContext groupContext)
        {
            var dotProductContext = groupContext as DotProductContext;
            return new DotProductOperation(dotProductContext.Value1, dotProductContext.Value2);
        }

        private static void Swap(ref HlslTreeNode a, ref HlslTreeNode b)
        {
            HlslTreeNode temp = a;
            a = b;
            b = temp;
        }
    }
}
