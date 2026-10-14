using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.TemplateMatch;

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
        return DotProductContext.UnlessFactored(MatchDotProduct3(node), _templateMatcher);
    }

    private DotProductContext MatchDotProduct3(HlslTreeNode node)
    {
        if (!(node is AddOperation addition))
        {
            return null;
        }

        HlslTreeNode third;
        if (addition.Addend1 is DotProductOperation dot && dot.X.Length == 2)
        {
            third = addition.Addend2;
        }
        else
        {
            dot = addition.Addend2 as DotProductOperation;
            if (dot == null || dot.X.Length != 2)
            {
                return null;
            }
            third = addition.Addend1;
        }

        // A bare matrix component is a product with a folded 1, as in
        // float4(xy, 1, 1) against a matrix.
        if (third is not MultiplyOperation cz)
        {
            HlslTreeNode xThird = dot.X.Inputs[1];
            HlslTreeNode yThird = dot.Y.Inputs[1];
            if (allowMatrixColumn && _templateMatcher.SharesMatrixColumnOrRow(xThird, third))
            {
                return new DotProductContext(
                    new GroupNode(dot.X.Inputs[0], xThird, third),
                    new GroupNode(dot.Y.Inputs[0], yThird, new ConstantNode(1)));
            }
            if (allowMatrixColumn && _templateMatcher.SharesMatrixColumnOrRow(yThird, third))
            {
                return new DotProductContext(
                    new GroupNode(dot.X.Inputs[0], xThird, new ConstantNode(1)),
                    new GroupNode(dot.Y.Inputs[0], yThird, third));
            }
            return null;
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
        else if ((allowMatrixColumn
            && _templateMatcher.SharesMatrixColumnOrRow(dot.Y.Inputs[1], cz.Factor2))
            || (_templateMatcher.CanGroupComponents(dot.Y.Inputs[1], cz.Factor2, allowMatrixColumn)
                && dot.X.Inputs[0] is not DotProductOperation
                && b is not DotProductOperation
                && c is not DotProductOperation
                && !IsTransposedGather(dot.X.Inputs[0], b, c)))
        {
            // The other side is a matrix or one register, so this side may be
            // arbitrary, except dots: those are matrix multiply rows.
            return new DotProductContext(
                new GroupNode(dot.X.Inputs[0], b, c),
                new GroupNode(dot.Y.Inputs[0], dot.Y.Inputs[1], cz.Factor2));
        }
        else if (NodeGrouper.AreNodesEquivalent(dot.X, dot.Y)
            && NodeGrouper.AreNodesEquivalent(cz.Factor1, cz.Factor2))
        {
            // A length squared over any components.
            return new DotProductContext(
                new GroupNode(dot.X.Inputs[0], b, c),
                new GroupNode(dot.Y.Inputs[0], dot.Y.Inputs[1], cz.Factor2));
        }

        return null;
    }


    /// <summary>
    /// Whether these are the same component of several values: a transpose, which
    /// fxc must gather an instruction apiece, so a dot over it never pays. A matrix
    /// row differs in component index and is not caught.
    /// </summary>
    private static bool IsTransposedGather(params HlslTreeNode[] nodes)
    {
        return nodes.All(node => node is IHasComponentIndex)
            && nodes.Select(node => ((IHasComponentIndex)node).ComponentIndex)
                .Distinct().Count() == 1
            && nodes.Distinct(ReferenceEqualityComparer.Instance).Count() == nodes.Length
            // Same node kind: a load's .x beside a variable holding its .yz is
            // one split value, not a transpose.
            && nodes.Select(node => node.GetType()).Distinct().Count() == 1;
    }
    public HlslTreeNode Reduce(HlslTreeNode node, IGroupContext groupContext)
    {
        var dotProductContext = groupContext as DotProductContext;
        return new DotProductOperation(dotProductContext.Value1, dotProductContext.Value2);
    }
}
