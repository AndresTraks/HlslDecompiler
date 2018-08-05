using System;
using System.Collections.Generic;

namespace HlslDecompiler.Hlsl
{
    public class MatrixMultiplicationGrouper
    {
        private readonly NodeGrouper _nodeGrouper;
        private readonly RegisterState _registers;

        public MatrixMultiplicationGrouper(NodeGrouper nodeGrouper, RegisterState registers)
        {
            _nodeGrouper = nodeGrouper;
            _registers = registers;
        }

        public MatrixMultiplicationContext TryGetMultiplicationGroup(IList<HlslTreeNode> nodes)
        {
            const bool allowMatrix = true;

            var first = nodes[0];
            var firstDotProductNode = _nodeGrouper.DotProductGrouper.TryGetDotProductGroup(first, allowMatrix);
            if (firstDotProductNode == null)
            {
                return null;
            }

            int dimension = firstDotProductNode.Dimension;
            if (nodes.Count < dimension)
            {
                return null;
            }

            HlslTreeNode[] firstMatrixRow = TryGetMatrixRow(firstDotProductNode);
            if (firstMatrixRow == null)
            {
                return null;
            }

            HlslTreeNode[] vector = null;
            var matrixRows = new HlslTreeNode[dimension][];
            matrixRows[0] = firstMatrixRow;
            for (int i = 1; i < dimension; i++)
            {
                var next = nodes[i];
                var dotProductNode = _nodeGrouper.DotProductGrouper.TryGetDotProductGroup(next, dimension, allowMatrix);
                if (dotProductNode == null)
                {
                    return null;
                }

                HlslTreeNode[] matrixRow = TryGetMatrixRow(dotProductNode);
                if (matrixRow == null)
                {
                    return null;
                }
                matrixRows[i] = matrixRow;

                HlslTreeNode[] nextVector = dotProductNode.Value1 == matrixRow
                    ? dotProductNode.Value2
                    : dotProductNode.Value1;

                if (vector == null)
                {
                    vector = nextVector;
                }
                else
                {
                    if (IsVectorEquivalent(vector, nextVector) == false)
                    {
                        return null;
                    }
                }
            }

            ConstantDeclaration matrix = TryGetMatrixDeclaration(matrixRows);
            if (matrix == null)
            {
                return null;
            }

            SwizzleVector(vector, firstMatrixRow);

            const bool matrixByVector = false;
            return new MatrixMultiplicationContext(vector, matrix, matrixByVector);
        }

        private void SwizzleVector(HlslTreeNode[] vector, HlslTreeNode[] firstMatrixRow)
        {
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

            if (needsSwizzle)
            {
                HlslTreeNode[] vectorCopy = new HlslTreeNode[vector.Length];
                Array.Copy(vector, vectorCopy, vector.Length);

                for (int i = 0; i < firstMatrixRow.Length; i++)
                {
                    var component = (firstMatrixRow[i] as RegisterInputNode).RegisterComponentKey.ComponentIndex;
                    if (i != component)
                    {
                        vector[i] = vectorCopy[component];
                    }
                }
            }
        }

        private ConstantDeclaration TryGetMatrixDeclaration(HlslTreeNode[][] dotProductNodes)
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

        private HlslTreeNode[] TryGetMatrixRow(DotProductContext firstDotProductNode)
        {
            if (firstDotProductNode.Value1[0] is RegisterInputNode value1)
            {
                ConstantDeclaration constant = _registers.FindConstant(value1);
                if( constant != null && constant.Rows > 1)
                {
                    return firstDotProductNode.Value1;
                }
            }

            if (firstDotProductNode.Value2[0] is RegisterInputNode value2)
            {
                ConstantDeclaration constant = _registers.FindConstant(value2);
                if (constant != null && constant.Rows > 1)
                {
                    return firstDotProductNode.Value2;
                }
            }

            return null;
        }

        private bool IsVectorEquivalent(HlslTreeNode[] vector1, HlslTreeNode[] vector2)
        {
            int dimension = vector1.Length;
            if (dimension != vector2.Length)
            {
                return false;
            }

            for (int i = 0; i < dimension; i++)
            {
                if (_nodeGrouper.AreNodesEquivalent(vector1[i], vector2[i]) == false)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public class MatrixMultiplicationContext
    {
        public MatrixMultiplicationContext(
            HlslTreeNode[] vector,
            ConstantDeclaration matrix,
            bool matrixByVector)
        {
            Vector = vector;
            MatrixDeclaration = matrix;
            IsMatrixByVector = matrixByVector;
        }

        public HlslTreeNode[] Vector { get; }

        public ConstantDeclaration MatrixDeclaration { get; }
        public bool IsMatrixByVector { get; }
    }
}
