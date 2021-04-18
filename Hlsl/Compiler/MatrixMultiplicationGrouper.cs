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

            GroupNode firstMatrixRow = TryGetMatrixRow(firstDot);
            if (firstMatrixRow == null)
            {
                return null;
            }

            GroupNode vector = firstDot.X == firstMatrixRow
                    ? firstDot.Y
                    : firstDot.X;

            var matrixRows = new GroupNode[dimension];
            matrixRows[0] = firstMatrixRow;
            for (int i = 1; i < dimension; i++)
            {
                if (!(components[i] is DotProductOperation nextDot))
                {
                    return null;
                }

                GroupNode matrixRow = TryGetMatrixRow(nextDot);
                if (matrixRow == null)
                {
                    return null;
                }

                GroupNode nextVector = nextDot.X == matrixRow
                    ? nextDot.Y
                    : nextDot.X;
                if (!NodeGrouper.AreNodesEquivalent(vector, nextVector))
                {
                    return null;
                }

                matrixRows[i] = matrixRow;
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

            return new MatrixMultiplicationContext(vector, matrix, matrixByVector);
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

        private ConstantDeclaration TryGetMatrixDeclaration(GroupNode[] dotProductNodes)
        {
            int dimension = dotProductNodes.Length;
            var first = dotProductNodes[0];
            if (first[0] is RegisterInputNode register1)
            {
                var matrixBaseConstant = _registers.FindConstant(register1);
                if (matrixBaseConstant != null && 
                    matrixBaseConstant.Rows == dimension && 
                    matrixBaseConstant.Columns == dimension)
                {
                    return matrixBaseConstant;
                }
            }

            return null;
        }

        private GroupNode TryGetMatrixRow(DotProductOperation dot)
        {
            if (dot.X.Inputs[0] is RegisterInputNode value1)
            {
                ConstantDeclaration constant = _registers.FindConstant(value1);
                if( constant != null && constant.Rows > 1)
                {
                    return dot.X;
                }
            }

            if (dot.Y.Inputs[0] is RegisterInputNode value2)
            {
                ConstantDeclaration constant = _registers.FindConstant(value2);
                if (constant != null && constant.Rows > 1)
                {
                    return dot.Y;
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
            bool matrixByVector)
        {
            Vector = vector;
            MatrixDeclaration = matrix;
            IsMatrixByVector = matrixByVector;
        }

        public GroupNode Vector { get; }

        public ConstantDeclaration MatrixDeclaration { get; }
        public bool IsMatrixByVector { get; }
    }
}
