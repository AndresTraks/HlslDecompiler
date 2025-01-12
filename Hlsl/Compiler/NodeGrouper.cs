using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Operations;
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

            if (node1 is ConstantNode)
            {
                return true;
            }

            if (node1 is RegisterInputNode input1 &&
                node2 is RegisterInputNode input2)
            {
                if (input1.RegisterComponentKey.RegisterKey.TypeEquals(input2.RegisterComponentKey.RegisterKey))
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

            if (node1 is DotProductOperation)
            {
                // FIXME: prevent grouping unrelated matrix rows
                return false;
            }

            if (node1 is TempAssignmentNode assignment1 && node2 is TempAssignmentNode assignment2)
            {
                return CanGroupComponents(assignment1.TempVariable, assignment2.TempVariable);
            }

            if (node1 is TempVariableNode tempVariable1 && node2 is TempVariableNode tempVariable2)
            {
                return tempVariable1.RegisterComponentKey.RegisterKey.Equals(
                    tempVariable2.RegisterComponentKey.RegisterKey) &&
                    tempVariable1.DeclarationIndex == tempVariable2.DeclarationIndex;
            }

            if (node1 is ComparisonNode comparison1 && node2 is ComparisonNode comparison2)
            {
                return CanGroupComponents(comparison1.Left, comparison2.Left)
                    && CanGroupComponents(comparison1.Right, comparison2.Right)
                    && comparison1.Comparison == comparison2.Comparison;
            }

            if (node1 is TextureLoadOutputNode textureload1 && node2 is TextureLoadOutputNode textureload2)
            {
                if (textureload1.Controls != textureload2.Controls)
                {
                    return false;
                }
            }

            if (node1 is IHasComponentIndex ||
                node1 is GroupNode ||
                node1 is Operation)
            {
                if (node1.Inputs.Count == node2.Inputs.Count)
                {
                    for (int i = 0; i < node1.Inputs.Count; i++)
                    {
                        if (!CanGroupComponents(node1.Inputs[i], node2.Inputs[i]))
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
            if (node1.GetType() != node2.GetType())
            {
                return false;
            }

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
                else if (operation1 is MultiplyOperation multiply1 &&
                         operation2 is MultiplyOperation multiply2)
                {
                    return (AreNodesEquivalent(multiply1.Factor1, multiply2.Factor1) && AreNodesEquivalent(multiply1.Factor2, multiply2.Factor2))
                        || (AreNodesEquivalent(multiply1.Factor1, multiply2.Factor2) && AreNodesEquivalent(multiply1.Factor2, multiply2.Factor1));
                }
            }

            if ((node1 is IHasComponentIndex) ||
                (node1 is GroupNode) ||
                (node1 is Operation))
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

        public static bool AreNodesEquivalent(ICollection<HlslTreeNode> nodes1, ICollection<HlslTreeNode> nodes2)
        {
            if (nodes1.Count != nodes2.Count)
            {
                return false;
            }
            for (int i = 0; i < nodes1.Count; i++)
            {
                if (!AreNodesEquivalent(nodes1.ElementAt(i), nodes2.ElementAt(i)))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
