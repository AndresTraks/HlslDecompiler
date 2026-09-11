namespace HlslDecompiler.Hlsl.TemplateMatch;

// 4 by 4 dot product has a pattern of:
// #1  dot3(abc, xyz) + dw
// #2  dw + dot3(abc, xyz)
public class DotProduct4Template : IGroupTemplate
{
    private TemplateMatcher _templateMatcher;
    private bool allowMatrixColumn = true;

    public DotProduct4Template(TemplateMatcher templateMatcher)
    {
        _templateMatcher = templateMatcher;
    }

    public IGroupContext Match(HlslTreeNode node)
    {
        return MatchDotProduct4(node);
    }

    private DotProductContext MatchDotProduct4(HlslTreeNode node)
    {
        if (!(node is AddOperation addition))
        {
            return null;
        }

        HlslTreeNode fourth;
        if (addition.Addend1 is DotProductOperation dot && dot.X.Length == 3)
        {
            fourth = addition.Addend2;
        }
        else
        {
            dot = addition.Addend2 as DotProductOperation;
            if (dot == null || dot.X.Length != 3)
            {
                return null;
            }

            fourth = addition.Addend1;
        }

        // `d * 1` folds to `d` long before this runs, and a dp4 whose w is one is
        // how a point is transformed - float4(xyz, 1) times a matrix. Reading a bare
        // addend back as `d * 1` recovers it, and is only allowed where the other
        // side really is a matrix, so an unrelated addend is not taken for one.
        bool wasFolded = fourth is not MultiplyOperation;
        HlslTreeNode dFactor = fourth is MultiplyOperation multiply ? multiply.Factor1 : fourth;
        HlslTreeNode wFactor = fourth is MultiplyOperation product ? product.Factor2 : new ConstantNode(1);

        if (wasFolded)
        {
            // The matrix sits on whichever side the bare addend belongs to, and the
            // other side takes the 1 - which is the float4(xyz, 1) of a point being
            // transformed. Neither side is fixed: the vector can be either operand.
            HlslTreeNode xThird = dot.X.Inputs[2];
            HlslTreeNode yThird = dot.Y.Inputs[2];
            if (_templateMatcher.CanGroupComponents(xThird, dFactor, allowMatrixColumn))
            {
                return new DotProductContext(
                    new GroupNode(dot.X.Inputs[0], dot.X.Inputs[1], xThird, dFactor),
                    new GroupNode(dot.Y.Inputs[0], dot.Y.Inputs[1], yThird, wFactor));
            }
            if (_templateMatcher.CanGroupComponents(yThird, dFactor, allowMatrixColumn))
            {
                return new DotProductContext(
                    new GroupNode(dot.X.Inputs[0], dot.X.Inputs[1], xThird, wFactor),
                    new GroupNode(dot.Y.Inputs[0], dot.Y.Inputs[1], yThird, dFactor));
            }
            return null;
        }

        HlslTreeNode c = dot.X.Inputs[2];
        HlslTreeNode d = dFactor;
        if (_templateMatcher.CanGroupComponents(c, d, allowMatrixColumn))
        {
            HlslTreeNode a = dot.X.Inputs[0];
            HlslTreeNode b = dot.X.Inputs[1];
            HlslTreeNode x = dot.Y.Inputs[0];
            HlslTreeNode y = dot.Y.Inputs[1];
            HlslTreeNode z = dot.Y.Inputs[2];
            HlslTreeNode w = wFactor;
            if (allowMatrixColumn && _templateMatcher.SharesMatrixColumnOrRow(c, d))
            {
                // If one of the arguments is a matrix, allow the other argument to be arbitrary.
                return new DotProductContext(new GroupNode(a, b, c, d), new GroupNode(x, y, z, w));
            }
            // A folded w costs nothing to assume: a*x + b*y + c*z + d is the same
            // number as the dot of (a, b, c, d) with (x, y, z, 1), and writing it
            // that way is what lets four of them become a matrix multiply.
            if (_templateMatcher.CanGroupComponents(z, w, allowMatrixColumn))
            {
                return new DotProductContext(new GroupNode(a, b, c, d), new GroupNode(x, y, z, w));
            }
        }
        else if (allowMatrixColumn
            && _templateMatcher.SharesMatrixColumnOrRow(dot.Y.Inputs[2], wFactor))
        {
            // As in DotProduct3Template: the matrix is on the other side, so this one
            // may be arbitrary.
            return new DotProductContext(
                new GroupNode(dot.X.Inputs[0], dot.X.Inputs[1], dot.X.Inputs[2], dFactor),
                new GroupNode(dot.Y.Inputs[0], dot.Y.Inputs[1], dot.Y.Inputs[2], wFactor));
        }

        return null;
    }

    public HlslTreeNode Reduce(HlslTreeNode node, IGroupContext groupContext)
    {
        var dotProductContext = groupContext as DotProductContext;
        return new DotProductOperation(dotProductContext.Value1, dotProductContext.Value2);
    }
}
