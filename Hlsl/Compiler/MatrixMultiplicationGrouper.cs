using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

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
    // Note: mul(float2xM, floatN) is compiled as mul((float2xN)float2xM, floatN)
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
                if (wColumnNodes.Length == components.Count && wColumnNodes.All(wColumnNode =>
                {
                    ConstantDeclaration matrixDeclaration = _registers.FindConstant(wColumnNode);
                    if (!submatrixGroup.MatrixDeclaration.Equals(matrixDeclaration))
                    {
                        return false;
                    }
                    if (wColumnNode.RegisterComponentKey.RegisterKey is D3D10RegisterKey d3d10RegisterKey && d3d10RegisterKey.OperandType == OperandType.ConstantBuffer)
                    {
                        return d3d10RegisterKey.ConstantBufferOffset == wRowIndex;
                    }
                    return wColumnNode.RegisterComponentKey.Number == wRowIndex || wColumnNode.RegisterComponentKey.ComponentIndex == wRowIndex;
                }))
                {
                    var extendedVector = submatrixGroup.Vector.ToList();
                    extendedVector.Add(new ConstantNode(1));
                    return new MatrixMultiplicationContext(
                        extendedVector.ToArray(),
                        submatrixGroup.MatrixDeclaration,
                        submatrixGroup.IsMatrixByVector,
                        submatrixGroup.MatrixRowCount,
                        extendedVector.Count);
                }
                else
                {
                    //throw new NotImplementedException();
                }
            }

            submatrixNodes = components.Select(g => g.Inputs[1]).ToArray();
            submatrixGroup = TryGetMultiplicationGroup(submatrixNodes);
            if (submatrixGroup != null)
            {
                RegisterInputNode[] wColumnNodes = components
                    .Select(g => g.Inputs[0])
                    .OfType<RegisterInputNode>()
                    .ToArray();
                int wRowIndex = submatrixGroup.MatrixDeclaration.RegisterIndex + submatrixGroup.Vector.Length;
                if (wColumnNodes.Length == components.Count && wColumnNodes.All(wColumnNode =>
                {
                    ConstantDeclaration matrixDeclaration = _registers.FindConstant(wColumnNode);
                    if (!submatrixGroup.MatrixDeclaration.Equals(matrixDeclaration))
                    {
                        return false;
                    }
                    if (wColumnNode.RegisterComponentKey.RegisterKey is D3D10RegisterKey d3d10RegisterKey && d3d10RegisterKey.OperandType == OperandType.ConstantBuffer)
                    {
                        return d3d10RegisterKey.ConstantBufferOffset == wRowIndex;
                    }
                    return wColumnNode.RegisterComponentKey.Number == wRowIndex || wColumnNode.RegisterComponentKey.ComponentIndex == wRowIndex;
                }))
                {
                    var extendedVector = submatrixGroup.Vector.ToList();
                    extendedVector.Add(new ConstantNode(1));
                    return new MatrixMultiplicationContext(
                        extendedVector.ToArray(),
                        submatrixGroup.MatrixDeclaration,
                        submatrixGroup.IsMatrixByVector,
                        submatrixGroup.MatrixRowCount,
                        extendedVector.Count);
                }
                else
                {
                    //throw new NotImplementedException();
                }
            }
        }

        if (components[0] is not DotProductOperation firstDot)
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
            .Cast<IHasComponentIndex>()
            .All(row => row.ComponentIndex == 0);

        vector = SwizzleVector(vector, firstMatrixRow, matrixByVector);

        RowMatrix rowMatrix = GetRowMatrix(firstMatrixRow[0]);
        return new MatrixMultiplicationContext(vector.ToArray(), matrix, matrixByVector, matrixRows.Count, firstMatrixRow.Count)
        {
            ElementIndex = GetElementIndex(matrix, firstMatrixRow[0]),
            ElementIndexNode = RowIndex(firstMatrixRow[0]),
            MemberPath = rowMatrix.MemberPath,
            MatrixTypeInfo = rowMatrix.MatrixType,
        };
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
            var component = ((IHasComponentIndex)firstMatrixRow[i]).ComponentIndex;
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
            var component = ((IHasComponentIndex)firstMatrixRow[i]).ComponentIndex;
            if (i != component)
            {
                vectorSwizzled[i] = vector[component];
            }
        }
        return vectorSwizzled;
    }

    // Which element of an array of matrices the rows belong to, or null for a
    // matrix that is not in an array. The rows' register says: so many registers
    // past the declaration, over the registers an element takes.
    private int? GetElementIndex(ConstantDeclaration matrix, HlslTreeNode firstRow)
    {
        if (matrix.TypeInfo.NumElements <= 1)
        {
            return null;
        }
        RegisterKey key = RowKey(firstRow).RegisterKey;
        int registerOffset = key is D3D10RegisterKey d3d10Key
            ? _registers.GetConstantBufferElementOffset(d3d10Key, matrix)
            : ((D3D9RegisterKey)key).Number - matrix.RegisterIndex;
        return registerOffset / matrix.RegistersPerElement;
    }

    private ConstantDeclaration TryGetMatrixDeclaration(IList<HlslTreeNode[]> dotProductNodes)
    {
        int dimension = dotProductNodes.Count;
        var first = dotProductNodes[0];
        if (IsRow(first[0]))
        {
            RowMatrix matrix = GetRowMatrix(first[0]);
            if (matrix != null
                && (matrix.MatrixType.Rows == dimension || matrix.MatrixType.Columns == dimension))
            {
                return matrix.Declaration;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether two dot products are rows of one matrix - the same declaration and,
    /// in an array, the same element - against the same vector: two components of
    /// one matrix multiply, and the only way two dot products are components of
    /// anything. In no particular order; the whole is checked at compile time.
    /// </summary>
    public bool AreRowsOfOneMatrix(DotProductOperation a, DotProductOperation b)
    {
        IList<HlslTreeNode> rowA = TryGetMatrixRow(a, a, 0);
        IList<HlslTreeNode> rowB = TryGetMatrixRow(b, b, 0);
        if (rowA == null || rowB == null)
        {
            return false;
        }
        RowMatrix matrixA = GetRowMatrix(rowA[0]);
        RowMatrix matrixB = GetRowMatrix(rowB[0]);
        if (matrixA == null || matrixB == null
            || matrixA.Declaration != matrixB.Declaration
            || matrixA.MemberPath != matrixB.MemberPath
            || GetElementIndex(matrixA.Declaration, rowA[0]) != GetElementIndex(matrixA.Declaration, rowB[0])
            || !ReferenceEquals(RowIndex(rowA[0]), RowIndex(rowB[0])))
        {
            return false;
        }
        IList<HlslTreeNode> vectorA = a.X.Inputs == rowA ? a.Y.Inputs : a.X.Inputs;
        IList<HlslTreeNode> vectorB = b.X.Inputs == rowB ? b.Y.Inputs : b.X.Inputs;
        return NodeGrouper.AreNodesEquivalent(vectorA, vectorB);
    }

    // A matrix row is read either straight from its register or through the
    // address register - `c1` or `c1[a0.x]` - and a row of the same element is
    // one register on from the row before it, read the same way.
    private static bool IsRow(HlslTreeNode node)
    {
        return node is RegisterInputNode or RelativeAddressNode;
    }

    private static RegisterComponentKey RowKey(HlslTreeNode node)
    {
        return node is RelativeAddressNode relative
            ? relative.RegisterComponentKey
            : ((RegisterInputNode)node).RegisterComponentKey;
    }

    private static HlslTreeNode RowIndex(HlslTreeNode node)
    {
        return node is RelativeAddressNode relative ? relative.Index : null;
    }

    /// <summary>
    /// The matrix a row register belongs to: a matrix constant, an element of an
    /// array of them, or a matrix member of a struct element - `instances[id].world`
    /// read as cb0[r0.x + 2] - or null for a register that is no row of anything.
    /// </summary>
    private RowMatrix GetRowMatrix(HlslTreeNode rowNode)
    {
        RegisterComponentKey key = RowKey(rowNode);
        ConstantDeclaration constant = _registers.FindConstant(key.RegisterKey);
        if (constant == null)
        {
            return null;
        }
        if (constant.TypeInfo.Rows > 1)
        {
            return new RowMatrix(constant, constant.TypeInfo, null);
        }
        if (constant.TypeInfo.MemberInfo != null
            && constant.TypeInfo.NumElements > 1
            && key.RegisterKey is D3D10RegisterKey d3d10Key)
        {
            int stride = constant.RegistersPerElement;
            int registerOffset = _registers.GetConstantBufferElementOffset(d3d10Key, constant);
            if (RegisterState.TryGetStructMemberAt(constant, "", registerOffset % stride, key.ComponentIndex,
                    out StructMemberAccess member)
                && member.IsMatrix)
            {
                return new RowMatrix(constant, member.TypeInfo, member.Name);
            }
        }
        return null;
    }

    private sealed record RowMatrix(ConstantDeclaration Declaration, ShaderTypeInfo MatrixType, string MemberPath);

    private IList<HlslTreeNode> TryGetMatrixRow(DotProductOperation dot, DotProductOperation firstDot, int row)
    {
        foreach ((IList<HlslTreeNode> candidate, IList<HlslTreeNode> firstCandidate) in
            new[] { (dot.X.Inputs, firstDot.X.Inputs), (dot.Y.Inputs, firstDot.Y.Inputs) })
        {
            if (!IsRow(candidate[0]))
            {
                continue;
            }
            RegisterComponentKey key = RowKey(candidate[0]);
            if (GetRowMatrix(candidate[0]) == null)
            {
                continue;
            }
            if (row == 0)
            {
                return candidate;
            }
            if (!IsRow(firstCandidate[0]) || !ReferenceEquals(RowIndex(candidate[0]), RowIndex(firstCandidate[0])))
            {
                continue;
            }
            RegisterComponentKey firstKey = RowKey(firstCandidate[0]);
            if (!firstKey.RegisterKey.TypeEquals(key.RegisterKey))
            {
                continue;
            }
            bool nextRegister = firstKey.RegisterKey.Number + row == key.RegisterKey.Number
                && firstKey.ComponentIndex == key.ComponentIndex;
            bool nextComponent = firstKey.RegisterKey.Number == key.RegisterKey.Number
                && firstKey.ComponentIndex + row == key.ComponentIndex;
            bool nextBufferRegister = firstKey.RegisterKey is D3D10RegisterKey firstD3D10
                && key.RegisterKey is D3D10RegisterKey d3d10
                && firstD3D10.Number == d3d10.Number
                && firstKey.ComponentIndex == key.ComponentIndex
                && firstD3D10.ConstantBufferOffset + row == d3d10.ConstantBufferOffset;
            if (nextRegister || nextComponent || nextBufferRegister)
            {
                return candidate;
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
        int matrixRowCount,
        int matrixColumnCount)
    {
        Vector = vector;
        MatrixDeclaration = matrix;
        IsMatrixByVector = matrixByVector;
        MatrixRowCount = matrixRowCount;
        MatrixColumnCount = matrixColumnCount;
    }

    public HlslTreeNode[] Vector { get; }

    public ConstantDeclaration MatrixDeclaration { get; }
    public int? ElementIndex { get; init; }
    // The index expression of rows read through the address register, or null.
    public HlslTreeNode ElementIndexNode { get; init; }
    // For a matrix member of a struct element, the member's name - ".world" -
    // following the element subscript, and the member's own type.
    public string MemberPath { get; init; }
    public ShaderTypeInfo MatrixTypeInfo { get; init; }
    public bool IsMatrixByVector { get; }
    public int MatrixRowCount { get; }
    public int MatrixColumnCount { get; }
}
