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
            // A product with a literal is a weighted sum written out, and is left
            // that way - except where the shader dotted a vector against weights of
            // its own: a dp3 of a colour against a def'd (0.3, 0.59, 0.11) came back
            // as three products added up, a mul and two mads recompiled where the
            // shader had one dp3. Weights all alike are a common factor, not a dot.
            // The weights first, as a constant register goes first: NodeFinalizer
            // puts it there, and the three and four component templates extend the
            // dot side by side as it stands.
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
            // Not a mirror of the allowance above, and deliberately not. Letting
            // this side be arbitrary because the a, b side is components of one
            // register reads as the obvious symmetry and costs instructions:
            // measured over the corpus it took gbuffer_write from 55 to 60 and
            // tangent_lighting from 45 to 51, and what it did to the second says
            // why. `t4.x * t9 + cross(t6, t9) * t4.y + t4.z * t6` became three dot
            // products of t4.xy against a constructor holding one component of the
            // cross each, and the normalize above it came apart into a length and a
            // divide. The dot had eaten the components the cross product grouper and
            // the normalize grouper were going to claim, and those run at compile
            // time, after every template - so a template cannot ask what they would
            // have taken. A dp3 of an input against a vector built a component at a
            // time therefore stays three products added up, which is the price of
            // the richer idioms keeping theirs.
            return null;
        }

        return new DotProductContext(new GroupNode(a, b), new GroupNode(x, y));
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
    // Two literals that differ, weighting two components of one value.
    private bool IsLiteralWeights(HlslTreeNode weight1, HlslTreeNode weight2,
        HlslTreeNode value1, HlslTreeNode value2)
    {
        return weight1 is ConstantNode c1 && weight2 is ConstantNode c2
            && c1.Value != c2.Value
            && value1 is not ConstantNode && value2 is not ConstantNode
            && value1 is IHasComponentIndex && value2 is IHasComponentIndex
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
