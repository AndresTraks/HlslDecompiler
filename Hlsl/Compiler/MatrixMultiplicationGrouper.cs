using HlslDecompiler.DirectXShaderModel;
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
        // float4(dot(m_row1.xyz, v.xyz), dot(m_row2.xyz, v.xyz), dot(m_row3.xyz, v.xyz), dot(m_row4.xyz, v.xyz)) + m_column4
        public MatrixMultiplicationContext TryGetMultiplicationGroup(IList<HlslTreeNode> components)
        {
            const bool allowMatrix = true;

            if (components.All(c => c is AddOperation))
            {
                HlslTreeNode[] submatrixNodes = components.Select(g => g.Inputs[0]).ToArray();
                MatrixMultiplicationContext submatrixGroup = TryGetMultiplicationGroup(submatrixNodes);
                if (submatrixGroup != null)
                {
                    RegisterInputNode[] wColumnNodes = components
                        .Select(g => g.Inputs[1])
                        .OfType<RegisterInputNode>()
                        .ToArray();
                    int wRowIndex = submatrixGroup.MatrixDeclaration.RegisterIndex + submatrixGroup.Vector.Length;
                    if (wColumnNodes.All(wColumnNode =>
                    {
                        ConstantDeclaration matrixDeclaration = _registers.FindConstant(wColumnNode);
                        if (!submatrixGroup.MatrixDeclaration.Equals(matrixDeclaration))
                        {
                            return false;
                        }
                        if (wColumnNode.RegisterComponentKey.RegisterKey is not D3D9RegisterKey d3d9RegisterKey)
                        {
                            return false;
                        }
                        return d3d9RegisterKey.Number == wRowIndex || wColumnNode.RegisterComponentKey.ComponentIndex == wRowIndex;
                    }))
                    {
                        var extendedVector = submatrixGroup.Vector.ToList();
                        extendedVector.Add(new ConstantNode(1));
                        return new MatrixMultiplicationContext(
                            extendedVector.ToArray(),
                            submatrixGroup.MatrixDeclaration,
                            submatrixGroup.IsMatrixByVector,
                            submatrixGroup.MatrixRowCount);
                    }
                }
            }

            if (components[0] is not DotProductOperation firstDot)
            {
                return null;
            }

            int dimension = firstDot.X.Length;
            if (components.Count < dimension)
            {
                return null;
            }

            IList<HlslTreeNode> firstMatrixRow = TryGetMatrixRow(firstDot, firstDot, 0);
            if (firstMatrixRow == null)
            {
                return null;
            }

            IList<HlslTreeNode> vector = firstDot.X.Inputs == firstMatrixRow
                    ? firstDot.Y.Inputs
                    : firstDot.X.Inputs;

            var matrixRows = new List<HlslTreeNode[]>
            {
                firstMatrixRow.ToArray()
            };
            for (int i = 1; i < components.Count; i++)
            {
                if (components[i] is not DotProductOperation nextDot)
                {
                    break;
                }

                IList<HlslTreeNode> matrixRow = TryGetMatrixRow(nextDot, firstDot, i);
                if (matrixRow == null)
                {
                    break;
                }

                IList<HlslTreeNode> nextVector = nextDot.X.Inputs == matrixRow
                    ? nextDot.Y.Inputs
                    : nextDot.X.Inputs;
                if (!NodeGrouper.AreNodesEquivalent(vector, nextVector))
                {
                    break;
                }

                matrixRows.Add(matrixRow.ToArray());
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

            bool matrixByVector = firstMatrixRow
                .Cast<RegisterInputNode>()
                .All(row => row.ComponentIndex == 0);

            vector = SwizzleVector(vector, firstMatrixRow, matrixByVector);

            return new MatrixMultiplicationContext(vector.ToArray(), matrix, matrixByVector, matrixRows.Count);
        }

        private static IList<HlslTreeNode> SwizzleVector(IList<HlslTreeNode> vector, IList<HlslTreeNode> firstMatrixRow, bool matrixByVector)
        {
            if (matrixByVector)
            {
                // TODO
                return vector;
            }

            bool needsSwizzle = false;
            for (int i = 0; i < firstMatrixRow.Count; i++)
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

            var vectorSwizzled = vector.ToArray();
            for (int i = 0; i < firstMatrixRow.Count; i++)
            {
                var component = (firstMatrixRow[i] as RegisterInputNode).RegisterComponentKey.ComponentIndex;
                if (i != component)
                {
                    vectorSwizzled[i] = vector[component];
                }
            }
            return vectorSwizzled;
        }

        private ConstantDeclaration TryGetMatrixDeclaration(IList<HlslTreeNode[]> dotProductNodes)
        {
            int dimension = dotProductNodes.Count;
            var first = dotProductNodes[0];
            if (first[0] is RegisterInputNode register1)
            {
                var matrixBaseConstant = _registers.FindConstant(register1);
                if (matrixBaseConstant != null && 
                    (matrixBaseConstant.TypeInfo.Rows == dimension ||
                    matrixBaseConstant.TypeInfo.Columns == dimension))
                {
                    return matrixBaseConstant;
                }
            }

            return null;
        }

        private IList<HlslTreeNode> TryGetMatrixRow(DotProductOperation dot, DotProductOperation firstDot, int row)
        {
            if (dot.X.Inputs[0] is RegisterInputNode constantRegister)
            {
                ConstantDeclaration constant = _registers.FindConstant(constantRegister);
                if(constant != null && constant.TypeInfo.Rows > 1)
                {
                    if (row == 0)
                    {
                        return dot.X.Inputs;
                    }
                    var firstConstantRegister =  firstDot.X.Inputs[0] as RegisterInputNode;
                    if (firstConstantRegister.RegisterComponentKey.RegisterKey.TypeEquals(constantRegister.RegisterComponentKey.RegisterKey) &&
                        firstConstantRegister.RegisterComponentKey.RegisterKey.Number + row == constantRegister.RegisterComponentKey.RegisterKey.Number &&
                        firstConstantRegister.RegisterComponentKey.ComponentIndex == constantRegister.RegisterComponentKey.ComponentIndex)
                    {
                        return dot.X.Inputs;
                    }
                    if (firstConstantRegister.RegisterComponentKey.RegisterKey.TypeEquals(constantRegister.RegisterComponentKey.RegisterKey) &&
                        firstConstantRegister.RegisterComponentKey.RegisterKey.Number == constantRegister.RegisterComponentKey.RegisterKey.Number &&
                        firstConstantRegister.RegisterComponentKey.ComponentIndex + row == constantRegister.RegisterComponentKey.ComponentIndex)
                    {
                        return dot.X.Inputs;
                    }
                    if (firstConstantRegister.RegisterComponentKey.RegisterKey is D3D10RegisterKey firstD3D10RegisterKey &&
                        constantRegister.RegisterComponentKey.RegisterKey is D3D10RegisterKey d3D10RegisterKey)
                    {
                        if (d3D10RegisterKey.TypeEquals(d3D10RegisterKey) &&
                            firstD3D10RegisterKey.Number == d3D10RegisterKey.Number &&
                            firstConstantRegister.RegisterComponentKey.ComponentIndex == constantRegister.RegisterComponentKey.ComponentIndex &&
                            firstD3D10RegisterKey.ConstantBufferOffset + row == d3D10RegisterKey.ConstantBufferOffset)
                        {
                            return dot.X.Inputs;
                        }
                    }
                }
            }

            if (dot.Y.Inputs[0] is RegisterInputNode constantRegister1)
            {
                ConstantDeclaration constant = _registers.FindConstant(constantRegister1);
                if (constant != null && constant.TypeInfo.Rows > 1)
                {
                    if (row == 0)
                    {
                        return dot.Y.Inputs;
                    }
                    var firstConstantRegister = firstDot.Y.Inputs[0] as RegisterInputNode;
                    if (firstConstantRegister.RegisterComponentKey.RegisterKey.TypeEquals(constantRegister1.RegisterComponentKey.RegisterKey) &&
                        firstConstantRegister.RegisterComponentKey.RegisterKey.Number + row == constantRegister1.RegisterComponentKey.RegisterKey.Number &&
                        firstConstantRegister.RegisterComponentKey.ComponentIndex == constantRegister1.RegisterComponentKey.ComponentIndex)
                    {
                        return dot.Y.Inputs;
                    }
                    if (firstConstantRegister.RegisterComponentKey.RegisterKey.TypeEquals(constantRegister1.RegisterComponentKey.RegisterKey) &&
                        firstConstantRegister.RegisterComponentKey.RegisterKey.Number == constantRegister1.RegisterComponentKey.RegisterKey.Number &&
                        firstConstantRegister.RegisterComponentKey.ComponentIndex + row == constantRegister1.RegisterComponentKey.ComponentIndex)
                    {
                        return dot.Y.Inputs;
                    }
                }
            }

            return null;
        }
    }

    public class MatrixMultiplicationContext
    {
        public MatrixMultiplicationContext(
            HlslTreeNode[] vector,
            ConstantDeclaration matrix,
            bool matrixByVector,
            int matrixRowCount)
        {
            Vector = vector;
            MatrixDeclaration = matrix;
            IsMatrixByVector = matrixByVector;
            MatrixRowCount = matrixRowCount;
        }

        public HlslTreeNode[] Vector { get; }

        public ConstantDeclaration MatrixDeclaration { get; }
        public bool IsMatrixByVector { get; }
        public int MatrixRowCount { get; }
    }
}
