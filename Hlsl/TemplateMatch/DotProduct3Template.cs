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

        // The same folded 1 DotProduct4Template reads back: a point in the plane
        // transformed by a matrix is float4(xy, 1, 1) against it, and both ones
        // fold away, so the sum stops at two products and two bare matrix
        // components. Only where the bare addend is a matrix component, so an
        // unrelated addend is not taken for one.
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
            // The other side is a matrix, or components of one register: either way
            // this side may be arbitrary - one component built by an expression is
            // still a component. DotProduct2Template allows the same thing, and
            // without it here a dot product over such a vector stops after two
            // components. A component that is itself a dot stays out, so that rows
            // of a matrix multiply reach the multiplication grouper whole.
            return new DotProductContext(
                new GroupNode(dot.X.Inputs[0], b, c),
                new GroupNode(dot.Y.Inputs[0], dot.Y.Inputs[1], cz.Factor2));
        }
        else if (NodeGrouper.AreNodesEquivalent(dot.X, dot.Y)
            && NodeGrouper.AreNodesEquivalent(cz.Factor1, cz.Factor2))
        {
            // The same as the two component case: a dot of a vector with itself is
            // a length squared whatever its components are, so they need not group.
            return new DotProductContext(
                new GroupNode(dot.X.Inputs[0], b, c),
                new GroupNode(dot.Y.Inputs[0], dot.Y.Inputs[1], cz.Factor2));
        }

        return null;
    }


    /// <summary>
    /// Whether these are one component taken from each of several values, which is a
    /// transpose and not a vector. fxc has to move each into place before it can dot
    /// them, an instruction apiece, so a dot over one never costs less than writing
    /// the products out - and taking it can cost a great deal more, because the dots
    /// it leaves behind do not group with one another. A terrain splat weighting
    /// three layers by one splat sample came back as a dot per output component, the
    /// lerp above them could not group over three unrelated dots, and the whole
    /// colour was written a component at a time: eighteen instructions out, thirty
    /// back.
    ///
    /// A matrix is read the other way round and is not caught by this: a row has one
    /// byte offset and a component apiece, so its components differ in index where
    /// these agree.
    /// </summary>
    private static bool IsTransposedGather(params HlslTreeNode[] nodes)
    {
        return nodes.All(node => node is IHasComponentIndex)
            && nodes.Select(node => ((IHasComponentIndex)node).ComponentIndex)
                .Distinct().Count() == 1
            && nodes.Distinct(ReferenceEqualityComparer.Instance).Count() == nodes.Length
            // Reads of the same kind, which is what makes them parallel rather than
            // one value the writer happened to split. A luminance dot over a texel
            // reaches here as the load's .x beside a float2 variable holding its
            // .yz, and those share a component index without being a transpose at
            // all - the components of one read, named halfway through.
            && nodes.Select(node => node.GetType()).Distinct().Count() == 1;
    }
    public HlslTreeNode Reduce(HlslTreeNode node, IGroupContext groupContext)
    {
        var dotProductContext = groupContext as DotProductContext;
        return new DotProductOperation(dotProductContext.Value1, dotProductContext.Value2);
    }
}
