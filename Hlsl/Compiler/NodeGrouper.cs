using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class NodeGrouper
    {
        private readonly RegisterState _registers;

        public NodeGrouper(RegisterState registers)
        {
            MatrixMultiplicationGrouper = new MatrixMultiplicationGrouper(this, registers);
            _registers = registers;
        }

        public MatrixMultiplicationGrouper MatrixMultiplicationGrouper { get; }

        public IList<IList<HlslTreeNode>> GroupComponents(List<HlslTreeNode> nodes)
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
                if (!CanGroupComponents(nodes[groupStart], nodes[n]))
                {
                    groups.Add(nodes.GetRange(groupStart, n - groupStart));
                    groupStart = n;
                }
            }
            groups.Add(nodes.GetRange(groupStart, n - groupStart));
            return groups;
        }

        // Returns true if children differ at most by component index, meaning they can be combined, for example:
        // n1 = a.x + b.x
        // n2 = a.y + b.y
        // =>
        // n.xy = a.xy + b.xy
        // n = a + b
        public bool CanGroupComponents(HlslTreeNode node1, HlslTreeNode node2)
        {
            if (MatrixMultiplicationGrouper.CanGroup(node1, node2))
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
                if (input1.RegisterComponentKey.Type == input2.RegisterComponentKey.Type)
                {
                    int constIndex1 = input1.RegisterComponentKey.Number;
                    int constIndex2 = input2.RegisterComponentKey.Number;
                    if (constIndex1 == constIndex2)
                    {
                        return true;
                    }

                    var constantRegister1 = _registers.FindConstant(ParameterType.Float, constIndex1);
                    return constantRegister1 != null && constantRegister1.ContainsIndex(constIndex2);
                }
                return false;
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

        public bool AreNodesEquivalent(HlslTreeNode node1, HlslTreeNode node2)
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
    }
}
