using System.Collections.Generic;
using System.Linq;

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
        return DotProductContext.UnlessFactored(MatchDotProduct2(node), _templateMatcher);
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
            // A product with a literal stays a weighted sum, unless the literals are
            // distinct weights over a groupable vector (a luminance dot). Weights go
            // first, where NodeFinalizer puts constants and the dp3/dp4 templates
            // extend the dot.
            if (IsLiteralWeights(a, b, x, y))
            {
                return new DotProductContext(new GroupNode(a, b), new GroupNode(x, y));
            }
            if (IsLiteralWeights(x, y, a, b))
            {
                return new DotProductContext(new GroupNode(x, y), new GroupNode(a, b));
            }
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
                    && b is not DotProductOperation
                    && !IsTransposedGather(a, b))
                {
                    // The other side is one register, so this side may be arbitrary.
                    // Not dots: they group as rows of a matrix multiply instead.
                    return new DotProductContext(new GroupNode(a, b), new GroupNode(x, y));
                }
                // `a*a + b*b` is a length squared over any components.
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
            // Deliberately not a mirror of the allowance above: an arbitrary side
            // here would eat components the cross and normalize groupers claim
            // later, at compile time. Plain reads are allowed, since no other
            // grouper uses them and fxc's operand order decides which side they're on.
            if (!IsPlainRead(x) || !IsPlainRead(y) || IsTransposedGather(x, y))
            {
                return null;
            }
        }

        return new DotProductContext(new GroupNode(a, b), new GroupNode(x, y));
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
    // A component read, not computed: nothing the cross/normalize groupers claim.
    private static bool IsPlainRead(HlslTreeNode node) =>
        node is IHasComponentIndex && node is not Operation && node is not DotProductOperation;

    // Two different literals weighting two components that group.
    private bool IsLiteralWeights(HlslTreeNode weight1, HlslTreeNode weight2,
        HlslTreeNode value1, HlslTreeNode value2)
    {
        return weight1 is ConstantNode c1 && weight2 is ConstantNode c2
            && c1.Value != c2.Value
            && value1 is not ConstantNode && value2 is not ConstantNode
            && _templateMatcher.CanGroupComponents(value1, value2, allowMatrixColumn);
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
