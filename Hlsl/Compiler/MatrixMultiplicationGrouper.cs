using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Operations;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class MatrixMultiplicationGrouper
    {
        private readonly RegisterState _registers;

        public MatrixMultiplicationGrouper(RegisterState registers)
        {
            _registers = registers;
        }

        // Vector by matrix multiplication has a pattern of:
        // float2(dot(m_row1, v), dot(m_row2, v))
        // float3(dot(m_row1, v), dot(m_row2, v), dot(m_row3, v))
        // float4(dot(m_row1, v), dot(m_row2, v), dot(m_row3, v), dot(m_row4, v))
        public MatrixMultiplicationContext TryGetMultiplicationGroup(IList<HlslTreeNode> components)
        {
            const bool allowMatrix = true;

            if (!(components[0] is DotProductOperation firstDot))
            {
                return null;
            }

            int dimension = firstDot.X.Length;
            if (components.Count < dimension)
            {
                return null;
            }

            GroupNode firstMatrixRow = TryGetMatrixRow(firstDot, firstDot, 0);
            if (firstMatrixRow == null)
            {
                return null;
            }

            GroupNode vector = firstDot.X == firstMatrixRow
                    ? firstDot.Y
                    : firstDot.X;

            var matrixRows = new List<GroupNode>();
            matrixRows.Add(firstMatrixRow);
            for (int i = 1; i < components.Count; i++)
            {
                if (!(components[i] is DotProductOperation nextDot))
                {
                    break;
                }

                GroupNode matrixRow = TryGetMatrixRow(nextDot, firstDot, i);
                if (matrixRow == null)
                {
                    break;
                }

                GroupNode nextVector = nextDot.X == matrixRow
                    ? nextDot.Y
                    : nextDot.X;
                if (!NodeGrouper.AreNodesEquivalent(vector, nextVector))
                {
                    break;
                }

                matrixRows.Add(matrixRow);
            }

            if (matrixRows.Count < 2)
            {
                return null;
            }

            ConstantDeclaration matrix = TryGetMatrixDeclaration(matrixRows);
            if (matrix == null)
            {
                return null;
            }

            bool matrixByVector = firstMatrixRow.Inputs
                .Cast<RegisterInputNode>()
                .All(row => row.ComponentIndex == 0);

            vector = SwizzleVector(vector, firstMatrixRow, matrixByVector);

            return new MatrixMultiplicationContext(vector, matrix, matrixByVector, matrixRows.Count);
        }

        private GroupNode SwizzleVector(GroupNode vector, GroupNode firstMatrixRow, bool matrixByVector)
        {
            if (matrixByVector)
            {
                // TODO
                return vector;
            }

            bool needsSwizzle = false;
            for (int i = 0; i < firstMatrixRow.Length; i++)
            {
                var component = (firstMatrixRow[i] as RegisterInputNode).RegisterComponentKey.ComponentIndex;
                if (i != component)
                {
                    needsSwizzle = true;
                    break;
                }
            }

            if (!needsSwizzle)
            {
                return vector;
            }

            GroupNode vectorSwizzled = new GroupNode(vector.Inputs.ToArray());
            for (int i = 0; i < firstMatrixRow.Length; i++)
            {
                var component = (firstMatrixRow[i] as RegisterInputNode).RegisterComponentKey.ComponentIndex;
                if (i != component)
                {
                    vectorSwizzled[i] = vector[component];
                }
            }
            return vectorSwizzled;
        }

        private ConstantDeclaration TryGetMatrixDeclaration(IList<GroupNode> dotProductNodes)
        {
            int dimension = dotProductNodes.Count;
            var first = dotProductNodes[0];
            if (first[0] is RegisterInputNode register1)
            {
                var matrixBaseConstant = _registers.FindConstant(register1);
                if (matrixBaseConstant != null && 
                    (matrixBaseConstant.Rows == dimension ||
                    matrixBaseConstant.Columns == dimension))
                {
                    return matrixBaseConstant;
                }
            }

            return null;
        }

        private GroupNode TryGetMatrixRow(DotProductOperation dot, DotProductOperation firstDot, int row)
        {
            if (dot.X.Inputs[0] is RegisterInputNode constantRegister)
            {
                ConstantDeclaration constant = _registers.FindConstant(constantRegister);
                if(constant != null && constant.Rows > 1)
                {
                    if (row == 0)
                    {
                        return dot.X;
                    }
                    var firstConstantRegister =  firstDot.X.Inputs[0] as RegisterInputNode;
                    if (firstConstantRegister.RegisterComponentKey.RegisterKey.TypeEquals(constantRegister.RegisterComponentKey.RegisterKey) &&
                        firstConstantRegister.RegisterComponentKey.RegisterKey.Number + row == constantRegister.RegisterComponentKey.RegisterKey.Number &&
                        firstConstantRegister.RegisterComponentKey.ComponentIndex == constantRegister.RegisterComponentKey.ComponentIndex)
                    {
                        return dot.X;
                    }
                    if (firstConstantRegister.RegisterComponentKey.RegisterKey.TypeEquals(constantRegister.RegisterComponentKey.RegisterKey) &&
                        firstConstantRegister.RegisterComponentKey.RegisterKey.Number == constantRegister.RegisterComponentKey.RegisterKey.Number &&
                        firstConstantRegister.RegisterComponentKey.ComponentIndex + row == constantRegister.RegisterComponentKey.ComponentIndex)
                    {
                        return dot.X;
                    }
                }
            }

            if (dot.Y.Inputs[0] is RegisterInputNode constantRegister1)
            {
                ConstantDeclaration constant = _registers.FindConstant(constantRegister1);
                if (constant != null && constant.Rows > 1)
                {
                    if (row == 0)
                    {
                        return dot.Y;
                    }
                    var firstConstantRegister = firstDot.Y.Inputs[0] as RegisterInputNode;
                    if (firstConstantRegister.RegisterComponentKey.RegisterKey.TypeEquals(constantRegister1.RegisterComponentKey.RegisterKey) &&
                        firstConstantRegister.RegisterComponentKey.RegisterKey.Number + row == constantRegister1.RegisterComponentKey.RegisterKey.Number &&
                        firstConstantRegister.RegisterComponentKey.ComponentIndex == constantRegister1.RegisterComponentKey.ComponentIndex)
                    {
                        return dot.Y;
                    }
                    if (firstConstantRegister.RegisterComponentKey.RegisterKey.TypeEquals(constantRegister1.RegisterComponentKey.RegisterKey) &&
                        firstConstantRegister.RegisterComponentKey.RegisterKey.Number == constantRegister1.RegisterComponentKey.RegisterKey.Number &&
                        firstConstantRegister.RegisterComponentKey.ComponentIndex + row == constantRegister1.RegisterComponentKey.ComponentIndex)
                    {
                        return dot.Y;
                    }
                }
            }

            return null;
        }
    }

    public class MatrixMultiplicationContext
    {
        public MatrixMultiplicationContext(
            GroupNode vector,
            ConstantDeclaration matrix,
            bool matrixByVector,
            int matrixRowCount)
        {
            Vector = vector;
            MatrixDeclaration = matrix;
            IsMatrixByVector = matrixByVector;
            MatrixRowCount = matrixRowCount;
        }

        public GroupNode Vector { get; }

        public ConstantDeclaration MatrixDeclaration { get; }
        public bool IsMatrixByVector { get; }
        public int MatrixRowCount { get; }
    }
}
