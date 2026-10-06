using System;
using System.Linq;

namespace HlslDecompiler.Hlsl.TemplateMatch;

public class DotProductContext : IGroupContext
{
    public DotProductContext(GroupNode value1, GroupNode value2)
    {
        if (value1.Length != value2.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(value1));
        }

        Value1 = value1;
        Value2 = value2;

        OrderByComponent();
    }

    public GroupNode Value1 { get; private set; }
    public GroupNode Value2 { get; private set; }

    public int Dimension => Value1.Length;

    /// <summary>
    /// The match, unless one side of it is one value over and over and the other is
    /// not a vector at all. `a*x + a*y` is a dot product of (a, a) with (x, y) as far
    /// as the arithmetic goes, and a common factor as far as anyone writing it goes:
    /// `tint * t1 + tint * k` came back as four `dot(tint.xx, float2(t1.x, k))`, one
    /// per component, where the products and their sum say what it is.
    ///
    /// Against a vector it is a dot product however it is written - `dot(weight.xxx,
    /// normal)` is a dp3 in the shader and attribute_snapped's source says so - and
    /// so are two sides of one value each, `dot(v.ww, v.xx)`, which is what
    /// dot_product2_add_scalar's source says.
    /// </summary>
    public static DotProductContext UnlessFactored(DotProductContext context, TemplateMatcher templateMatcher)
    {
        if (context == null)
        {
            return null;
        }
        bool firstIsOneValue = IsOneValue(context.Value1);
        bool secondIsOneValue = IsOneValue(context.Value2);
        if (firstIsOneValue == secondIsOneValue)
        {
            return context;
        }
        GroupNode other = firstIsOneValue ? context.Value2 : context.Value1;
        bool otherIsVector = Enumerable.Range(1, other.Length - 1)
            .All(i => templateMatcher.CanGroupComponents(other[0], other[i], true));
        return otherIsVector ? context : null;
    }

    private static bool IsOneValue(GroupNode group)
    {
        return Enumerable.Range(1, group.Length - 1).All(i => IsSameValue(group[0], group[i]));
    }

    // The same value, and not merely the same expression of other components - which
    // is what NodeGrouper.AreNodesEquivalent asks, for grouping: the x and y of one
    // matrix product are equivalent to it and are not one value.
    private static bool IsSameValue(HlslTreeNode a, HlslTreeNode b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }
        return (a, b) switch
        {
            (RegisterInputNode x, RegisterInputNode y) => x.RegisterComponentKey.Equals(y.RegisterComponentKey),
            (ConstantNode x, ConstantNode y) => x.Value == y.Value,
            (Operation x, Operation y) => x.GetType() == y.GetType()
                && x.Inputs.Count == y.Inputs.Count
                && x.Inputs.Zip(y.Inputs).All(pair => IsSameValue(pair.First, pair.Second)),
            _ => false,
        };
    }

    private void OrderByComponent()
    {
        int[] components = GetComponentsIfUniqueAndUnordered();
        if (components == null)
        {
            return;
        }

        int dimension = components.Length;
        var newValue1 = new HlslTreeNode[dimension];
        var newValue2 = new HlslTreeNode[dimension];
        for (int i = 0; i < dimension; i++)
        {
            int min = int.MaxValue;
            int minIndex = 0;
            for (int j = 0; j < dimension; j++)
            {
                if (components[j] < min)
                {
                    min = components[j];
                    minIndex = j;
                }
            }
            newValue1[i] = Value1[minIndex];
            newValue2[i] = Value2[minIndex];
            components[minIndex] = int.MaxValue;
        }
        Value1 = new GroupNode(newValue1);
        Value2 = new GroupNode(newValue2);
    }

    private int[] GetComponentsIfUniqueAndUnordered()
    {
        if (ValuesHaveSameComponents() == false)
        {
            return null;
        }

        bool isUnordered = false;
        var components = new int[Dimension];
        for (int i = 0; i < Dimension; i++)
        {
            // TODO: get child component index

            var componentIndex = (Value1[i] as IHasComponentIndex).ComponentIndex;
            if (Array.IndexOf(components, componentIndex, 0, i) != -1)
            {
                return null;
            }

            components[i] = componentIndex;

            if (componentIndex != i)
            {
                isUnordered = true;
            }
        }

        return isUnordered ? components : null;
    }

    private bool ValuesHaveSameComponents()
    {
        for (int i = 0; i < Dimension; i++)
        {
            // TODO: get child component index

            if (!(Value1[i] is IHasComponentIndex value1))
            {
                return false;
            }

            if (!(Value2[i] is IHasComponentIndex value2))
            {
                return false;
            }

            if (value1.ComponentIndex != value2.ComponentIndex)
            {
                return false;
            }
        }

        return true;
    }
}
