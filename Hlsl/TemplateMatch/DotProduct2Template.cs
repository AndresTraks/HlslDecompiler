namespace HlslDecompiler.Hlsl.TemplateMatch;

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
                if (_templateMatcher.CanGroupComponents(x, y, allowMatrixColumn)
                    && a is not DotProductOperation
                    && b is not DotProductOperation)
                {
                    // The other side is components of one register, so this side may be
                    // arbitrary - a vector with a different expression per component is
                    // still the vector the dot was taken over. Dots are left out of it:
                    // they group as rows of one matrix multiply, and taking them here
                    // costs a mul that the multiplication grouper would have kept.
                    return new DotProductContext(new GroupNode(a, b), new GroupNode(x, y));
                }
                // `a*a + b*b` is a length squared, and the vector it is taken over
                // needs no grouping to be a vector: both factors of each product are
                // the same value, so whatever the components are the shader squared
                // them and added them up. grass_wave takes the length of a position
                // whose x and z are a mad and whose y is read straight from the
                // input, and those do not group with one another.
                if (NodeGrouper.AreNodesEquivalent(a, x) && NodeGrouper.AreNodesEquivalent(b, y))
                {
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
