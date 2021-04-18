using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class NodeGrouper
    {
        private readonly RegisterState _registers;

        public NodeGrouper(RegisterState registers)
        {
            MatrixMultiplicationGrouper = new MatrixMultiplicationGrouper(registers);
            NormalizeGrouper = new NormalizeGrouper();
            _registers = registers;
        }

        public MatrixMultiplicationGrouper MatrixMultiplicationGrouper { get; }
        public NormalizeGrouper NormalizeGrouper { get; }

        public IList<IList<HlslTreeNode>> GroupComponents(List<HlslTreeNode> nodes)
        {
            switch (nodes.Count)
            {
                case 0:
                case 1:
                    return new IList<HlslTreeNode>[] { nodes };
            }

            List<IList<HlslTreeNode>> groups;

            var multiplicationGroup = MatrixMultiplicationGrouper.TryGetMultiplicationGroup(nodes);
            if (multiplicationGroup != null)
            {
                int dimension = multiplicationGroup.MatrixRowCount;
                groups = new List<IList<HlslTreeNode>>(new[]
                    { nodes.Take(dimension).ToList()
                });
                if (dimension < nodes.Count)
                {
                    List<HlslTreeNode> rest = nodes.Skip(dimension).ToList();
                    groups.AddRange(GroupComponents(rest));
                }
                return groups;
            }

            var normalizeGroup = NormalizeGrouper.TryGetContext(nodes);
            if (normalizeGroup != null)
            {
                int dimension = normalizeGroup.Length;
                groups = new List<IList<HlslTreeNode>>(new[]
                    { nodes.Take(dimension).ToList()
                });
                if (dimension < nodes.Count)
                {
                    List<HlslTreeNode> rest = nodes.Skip(dimension).ToList();
                    groups.AddRange(GroupComponents(rest));
                }
                return groups;
            }

            groups = new List<IList<HlslTreeNode>>();

            int groupStart = 0;
            int nodeIndex;
            for (nodeIndex = 1; nodeIndex < nodes.Count; nodeIndex++)
            {
                HlslTreeNode node1 = nodes[groupStart];
                HlslTreeNode node2 = nodes[nodeIndex];
                if (CanGroupComponents(node1, node2) == false)
                {
                    groups.Add(nodes.GetRange(groupStart, nodeIndex - groupStart));
                    groupStart = nodeIndex;
                }
            }
            groups.Add(nodes.GetRange(groupStart, nodeIndex - groupStart));
            return groups;
        }

        // Returns true if children differ at most by component index, meaning they can be combined, for example:
        // n1 = a.x + b.x
        // n2 = a.y + b.y
        // =>
        // n.xy = a.xy + b.xy
        // n = a + b
        public bool CanGroupComponents(HlslTreeNode node1, HlslTreeNode node2, bool allowMatrixColumn = false)
        {
            if (node1.GetType() != node2.GetType())
            {
                return false;
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
                    if (input1.RegisterComponentKey.Number == input2.RegisterComponentKey.Number)
                    {
                        return true;
                    }

                    if (allowMatrixColumn)
                    {
                        return SharesMatrixColumnOrRow(input1, input2);
                    }
                }
                return false;
            }

            if (node1 is Operation operation1 &&
                node2 is Operation operation2)
            {
                if (operation1 is AddOperation add1 &&
                    operation2 is AddOperation add2)
                {
                    return add1.Inputs.Any(c1 => add2.Inputs.Any(c2 => CanGroupComponents(c1, c2)));
                }
                else if (
                    (operation1 is AbsoluteOperation && operation2 is AbsoluteOperation)
                    || (operation1 is CosineOperation && operation2 is CosineOperation)
                    || (operation1 is FractionalOperation && operation2 is FractionalOperation)
                    || (operation1 is NegateOperation && operation2 is NegateOperation)
                    || (operation1 is ReciprocalOperation && operation2 is ReciprocalOperation)
                    || (operation1 is ReciprocalSquareRootOperation && operation2 is ReciprocalSquareRootOperation))
                {
                    return CanGroupComponents(operation1.Inputs[0], operation2.Inputs[0]);
                }
                else if (
                    (operation1 is MinimumOperation && operation2 is MinimumOperation) ||
                    (operation1 is SignGreaterOrEqualOperation && operation2 is SignGreaterOrEqualOperation) ||
                    (operation1 is SignLessOperation && operation2 is SignLessOperation))
                {
                    return CanGroupComponents(operation1.Inputs[0], operation2.Inputs[0])
                        && CanGroupComponents(operation1.Inputs[1], operation2.Inputs[1]);
                }
                else if (operation1 is MultiplyOperation multiply1 &&
                         operation2 is MultiplyOperation multiply2)
                {
                    return multiply1.Inputs.Any(c1 => multiply2.Inputs.Any(c2 => CanGroupComponents(c1, c2)));
                }
                else if (operation1 is DivisionOperation divison1 &&
                         operation2 is DivisionOperation divison2)
                {
                    return AreNodesEquivalent(divison1.Dividend, divison2.Dividend)
                        && CanGroupComponents(divison1.Divisor, divison1.Divisor);
                }
                else if (operation1 is SubtractOperation subtract1 &&
                         operation2 is SubtractOperation subtract2)
                {
                    return CanGroupComponents(subtract1.Subtrahend, subtract2.Subtrahend);
                }
                else if (operation1 is CompareOperation compare1 &&
                         operation2 is CompareOperation compare2)
                {
                    return AreNodesEquivalent(compare1.Value, compare2.Value) &&
                        CanGroupComponents(compare1.LessValue, compare2.LessValue) &&
                        CanGroupComponents(compare1.GreaterEqualValue, compare2.GreaterEqualValue);
                }
                else if (operation1 is ClipOperation clip1 &&
                         operation2 is ClipOperation clip2)
                {
                    return CanGroupComponents(clip1.Value, clip2.Value);
                }
                else if (operation1 is LengthOperation length1 &&
                         operation2 is LengthOperation length2)
                {
                    return CanGroupComponents(length1.X, length2.X);
                }
            }

            if ((node1 is IHasComponentIndex && node2 is IHasComponentIndex) ||
                (node1 is GroupNode && node2 is GroupNode))
            {
                if (node1.Inputs.Count == node2.Inputs.Count)
                {
                    for (int i = 0; i < node1.Inputs.Count; i++)
                    {
                        if (node1.Inputs[i].Equals(node2.Inputs[i]) == false)
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }

            return false;
        }

        public bool SharesMatrixColumnOrRow(RegisterInputNode input1, RegisterInputNode input2)
        {
            return SharesMatrixColumn(input1, input2)
                || SharesMatrixRow(input1, input2);
        }

        public bool SharesMatrixColumn(RegisterInputNode input1, RegisterInputNode input2)
        {
            if (input1.RegisterComponentKey.ComponentIndex !=
                input2.RegisterComponentKey.ComponentIndex)
            {
                return false;
            }

            int constIndex1 = input1.RegisterComponentKey.Number;
            int constIndex2 = input2.RegisterComponentKey.Number;
            var constantRegister = _registers.FindConstant(ParameterType.Float, constIndex1);
            return constantRegister != null
                && constantRegister.ContainsIndex(constIndex2)
                && IsMatrixConstantRegister(constantRegister);
        }

        public bool SharesMatrixRow(RegisterInputNode input1, RegisterInputNode input2)
        {
            if (input1.RegisterComponentKey.RegisterKey !=
                input2.RegisterComponentKey.RegisterKey)
            {
                return false;
            }

            int constIndex = input1.RegisterComponentKey.Number;
            var constantRegister = _registers.FindConstant(ParameterType.Float, constIndex);
            return constantRegister != null
                && IsMatrixConstantRegister(constantRegister);
        }

        private static bool IsMatrixConstantRegister(ConstantDeclaration constantRegister)
        {
            return constantRegister.ParameterClass == ParameterClass.MatrixColumns
                || constantRegister.ParameterClass == ParameterClass.MatrixRows;
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
                    return (AreNodesEquivalent(add1.Addend1, add2.Addend1) && AreNodesEquivalent(add1.Addend2, add2.Addend2))
                        || (AreNodesEquivalent(add1.Addend1, add2.Addend2) && AreNodesEquivalent(add1.Addend2, add2.Addend1));
                }
                else if (
                    (operation1 is ConsumerOperation unaryOperation1 &&
                    operation2 is ConsumerOperation unaryOperation2 &&
                    unaryOperation1.GetType() == unaryOperation2.GetType()))
                {
                    return AreNodesEquivalent(unaryOperation1.Value, unaryOperation2.Value);
                }
                else if (
                    (operation1 is MinimumOperation && operation2 is MinimumOperation) ||
                    (operation1 is SignGreaterOrEqualOperation && operation2 is SignGreaterOrEqualOperation) ||
                    (operation1 is SignLessOperation && operation2 is SignLessOperation))
                {
                    return AreNodesEquivalent(operation1.Inputs[0], operation2.Inputs[0])
                        && AreNodesEquivalent(operation1.Inputs[1], operation2.Inputs[1]);
                }
                else if (operation1 is MultiplyOperation multiply1 &&
                         operation2 is MultiplyOperation multiply2)
                {
                    return (AreNodesEquivalent(multiply1.Factor1, multiply2.Factor1) && AreNodesEquivalent(multiply1.Factor2, multiply2.Factor2))
                        || (AreNodesEquivalent(multiply1.Factor1, multiply2.Factor2) && AreNodesEquivalent(multiply1.Factor2, multiply2.Factor1));
                }
                else if (operation1 is SubtractOperation subtract1 &&
                         operation2 is SubtractOperation subtract2)
                {
                    return subtract1.Subtrahend.Equals(subtract2.Subtrahend);
                }
                else if (operation1 is LengthOperation length1 &&
                         operation2 is LengthOperation length2)
                {
                    return AreNodesEquivalent(length1.X, length2.X);
                }
            }

            if ((node1 is IHasComponentIndex && node2 is IHasComponentIndex) ||
                (node1 is GroupNode && node2 is GroupNode))
            {
                if (node1.Inputs.Count == node2.Inputs.Count)
                {
                    for (int i = 0; i < node1.Inputs.Count; i++)
                    {
                        if (!AreNodesEquivalent(node1.Inputs[i], node2.Inputs[i]))
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
