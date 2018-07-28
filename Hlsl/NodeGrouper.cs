using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public static class NodeGrouper
    {
        public static IList<IList<HlslTreeNode>> GroupComponents(List<HlslTreeNode> nodes)
        {
            switch (nodes.Count)
            {
                case 0:
                case 1:
                    return new List<IList<HlslTreeNode>> { nodes };
            }

            var groups = new List<IList<HlslTreeNode>>();
            int n, groupStart = 0;
            for (n = 1; n < nodes.Count; n++)
            {
                if (!NodeGrouper.CanGroupComponents(nodes[groupStart], nodes[n]))
                {
                    groups.Add(nodes.GetRange(groupStart, n - groupStart));
                    groupStart = n;
                }
            }
            groups.Add(nodes.GetRange(groupStart, n - groupStart));
            return groups;
        }

        public static bool IsMatrixMultiplication(HlslTreeNode node1, HlslTreeNode node2)
        {
            // 2x2 matrix multiplication has a pattern of:
            // node1 = A*X + B*Y
            // node2 = C*X + D*Y
            if (!(node1 is AddOperation add1))
            {
                return false;
            }
            if (!(node2 is AddOperation add2))
            {
                return false;
            }

            if (!(add1.Addend1 is MultiplyOperation ax))
            {
                return false;
            }
            if (!(add1.Addend2 is MultiplyOperation by))
            {
                return false;
            }
            if (!(add2.Addend1 is MultiplyOperation cx))
            {
                return false;
            }
            if (!(add2.Addend2 is MultiplyOperation dy))
            {
                return false;
            }

            HlslTreeNode x = GetCommonFactor(ax, cx);
            if (x == null)
            {
                x = GetCommonFactor(ax, dy);
                if (x == null)
                {
                    return false;
                }
                Swap(ref cx, ref dy);
            }
            HlslTreeNode a = GetOther(ax, x);
            HlslTreeNode c = GetOther(cx, x);

            HlslTreeNode y = GetCommonFactor(by, dy);
            if (y == null)
            {
                return false;
            }
            HlslTreeNode b = GetOther(by, y);
            HlslTreeNode d = GetOther(dy, y);

            if (CanGroupComponents(x, y) == false)
            {
                return false;
            }

            return true;
        }

        // Returns true if children differ at most by component index, meaning they can be combined, for example:
        // n1 = a.x + b.x
        // n2 = a.y + b.y
        // =>
        // n.xy = a.xy + b.xy
        // n = a + b
        public static bool CanGroupComponents(HlslTreeNode node1, HlslTreeNode node2)
        {
            if (IsMatrixMultiplication(node1, node2))
            {
                return true;
            }

            if (node1 is ConstantNode constant1 &&
                node2 is ConstantNode constant2)
            {
                return true;
            }

            if (node1 is RegisterInputNode input1 &&
                node2 is RegisterInputNode input2)
            {
                return input1.RegisterComponentKey.Type == input2.RegisterComponentKey.Type &&
                       input1.RegisterComponentKey.Number == input2.RegisterComponentKey.Number;
            }

            if (node1 is Operation operation1 &&
                node2 is Operation operation2)
            {
                if (operation1 is AddOperation add1 &&
                    operation2 is AddOperation add2)
                {
                    return add1.Children.Any(c1 => add2.Children.Any(c2 => CanGroupComponents(c1, c2)));
                }
                else if (
                    (operation1 is AbsoluteOperation && operation2 is AbsoluteOperation)
                    || (operation1 is CosineOperation && operation2 is CosineOperation)
                    || (operation1 is FractionalOperation && operation2 is FractionalOperation)
                    || (operation1 is NegateOperation && operation2 is NegateOperation)
                    || (operation1 is ReciprocalOperation && operation2 is ReciprocalOperation)
                    || (operation1 is ReciprocalSquareRootOperation && operation2 is ReciprocalSquareRootOperation))
                {
                    return CanGroupComponents(operation1.Children[0], operation2.Children[0]);
                }
                else if (
                    (operation1 is MinimumOperation && operation2 is MinimumOperation) ||
                    (operation1 is SignGreaterOrEqualOperation && operation2 is SignGreaterOrEqualOperation) ||
                    (operation1 is SignLessOperation && operation2 is SignLessOperation))
                {
                    return CanGroupComponents(operation1.Children[0], operation2.Children[0])
                        && CanGroupComponents(operation1.Children[1], operation2.Children[1]);
                }
                else if (operation1 is MultiplyOperation multiply1 &&
                         operation2 is MultiplyOperation multiply2)
                {
                    return multiply1.Children.Any(c1 => multiply2.Children.Any(c2 => CanGroupComponents(c1, c2)));
                }
                else if (operation1 is SubtractOperation subtract1 &&
                         operation2 is SubtractOperation subtract2)
                {
                    return CanGroupComponents(subtract1.Subtrahend, subtract2.Subtrahend);
                }
            }

            if (node1 is IHasComponentIndex &&
                node2 is IHasComponentIndex)
            {
                if (node1.Children.Count == node2.Children.Count)
                {
                    for (int i = 0; i < node1.Children.Count; i++)
                    {
                        if (node1.Children[i].Equals(node2.Children[i]) == false)
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }

            return false;
        }

        public static bool AreNodesEquivalent(HlslTreeNode node1, HlslTreeNode node2)
        {
            if (node1 is ConstantNode constant1 &&
                node2 is ConstantNode constant2)
            {
                return constant1.Value == constant2.Value;
            }

            if (node1 is RegisterInputNode input1 &&
                node2 is RegisterInputNode input2)
            {
                return input1.RegisterComponentKey.Equals(input2.RegisterComponentKey);
            }

            if (node1 is Operation operation1 &&
                node2 is Operation operation2)
            {
                if (operation1 is AddOperation add1 &&
                    operation2 is AddOperation add2)
                {
                    return add1.Children.Any(c1 => add2.Children.Any(c2 => AreNodesEquivalent(c1, c2)));
                }
                else if (
                    (operation1 is AbsoluteOperation && operation2 is AbsoluteOperation)
                    || (operation1 is CosineOperation && operation2 is CosineOperation)
                    || (operation1 is FractionalOperation && operation2 is FractionalOperation)
                    || (operation1 is NegateOperation && operation2 is NegateOperation)
                    || (operation1 is ReciprocalOperation && operation2 is ReciprocalOperation)
                    || (operation1 is ReciprocalSquareRootOperation && operation2 is ReciprocalSquareRootOperation))
                {
                    return AreNodesEquivalent(operation1.Children[0], operation2.Children[0]);
                }
                else if (
                    (operation1 is MinimumOperation && operation2 is MinimumOperation) ||
                    (operation1 is SignGreaterOrEqualOperation && operation2 is SignGreaterOrEqualOperation) ||
                    (operation1 is SignLessOperation && operation2 is SignLessOperation))
                {
                    return AreNodesEquivalent(operation1.Children[0], operation2.Children[0])
                        && AreNodesEquivalent(operation1.Children[1], operation2.Children[1]);
                }
                else if (operation1 is MultiplyOperation multiply1 &&
                         operation2 is MultiplyOperation multiply2)
                {
                    return multiply1.Children.Any(c1 => multiply2.Children.Any(c2 => AreNodesEquivalent(c1, c2)));
                }
                else if (operation1 is SubtractOperation subtract1 &&
                         operation2 is SubtractOperation subtract2)
                {
                    return subtract1.Subtrahend.Equals(subtract2.Subtrahend);
                }
            }

            if (node1 is IHasComponentIndex &&
                node2 is IHasComponentIndex)
            {
                if (node1.Children.Count == node2.Children.Count)
                {
                    for (int i = 0; i < node1.Children.Count; i++)
                    {
                        if (node1.Children[i].Equals(node2.Children[i]) == false)
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }

            return false;
        }

        private static HlslTreeNode GetCommonFactor(MultiplyOperation ax, MultiplyOperation cx)
        {
            if (AreNodesEquivalent(ax.Factor1, cx.Factor1) ||
                AreNodesEquivalent(ax.Factor1, cx.Factor2))
            {
                return ax.Factor1;
            }

            if (AreNodesEquivalent(ax.Factor2, cx.Factor1) ||
                AreNodesEquivalent(ax.Factor2, cx.Factor2))
            {
                return ax.Factor2;
            }

            return null;
        }

        private static HlslTreeNode GetOther(MultiplyOperation ax, HlslTreeNode x)
        {
            return AreNodesEquivalent(ax.Factor1, x)
                ? ax.Factor2
                : ax.Factor1;
        }

        private static void Swap(ref MultiplyOperation cx, ref MultiplyOperation dy)
        {
            MultiplyOperation temp = cx;
            cx = dy;
            dy = temp;
        }
    }
}
