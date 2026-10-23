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

    // floatN(dot(m_row1, v), ..., dot(m_rowN, v)), optionally with dots of v.xyz
    // plus a fourth column (v.w == 1).
    // mul(float2xM, floatN) is compiled as mul((float2xN)float2xM, floatN).
    public MatrixMultiplicationContext TryGetMultiplicationGroup(IList<HlslTreeNode> components)
    {
        if (components.All(c => c is AddOperation))
        {
            HlslTreeNode[] submatrixNodes = components.Select(g => g.Inputs[0]).ToArray();
            MatrixMultiplicationContext submatrixGroup = TryGetMultiplicationGroup(submatrixNodes);
            // A struct member matrix has no extra column: the next register is the
            // next member.
            if (submatrixGroup != null && submatrixGroup.MemberPath == null)
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
            if (submatrixGroup != null && submatrixGroup.MemberPath == null)
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

        // Structured buffer rows are loads, never register reads, so this cannot
        // collide with the register matching below.
        MatrixMultiplicationContext structured = TryGetStructuredMultiplicationGroup(components);
        if (structured != null)
        {
            return structured;
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

        bool matrixByVector = firstMatrixRow
            .Cast<IHasComponentIndex>()
            .All(row => row.ComponentIndex == 0);

        ConstantDeclaration matrix = TryGetMatrixDeclaration(matrixRows, matrixByVector);
        if (matrix == null)
        {
            return null;
        }

        vector = SwizzleVector(vector, firstMatrixRow, matrixByVector);

        RowMatrix rowMatrix = GetRowMatrix(firstMatrixRow[0]);
        return new MatrixMultiplicationContext(vector.ToArray(), matrix, matrixByVector, matrixRows.Count, firstMatrixRow.Count)
        {
            ElementIndex = GetElementIndex(matrix, firstMatrixRow[0]),
            ElementIndexNode = RowIndex(firstMatrixRow[0]),
            ElementIndexCountsElements = firstMatrixRow[0] is RelativeAddressNode { IndexCountsElements: true },
            MemberPath = rowMatrix.MemberPath,
            MatrixTypeInfo = rowMatrix.MatrixType,
            FirstRowRegister = GetRegisterOffset(matrix, firstMatrixRow[0]),
        };
    }

    /// <summary>
    /// A matrix in a structured buffer: one ld_structured per row, consecutive
    /// within one element, each dotted with the vector.
    /// </summary>
    private MatrixMultiplicationContext TryGetStructuredMultiplicationGroup(
        IList<HlslTreeNode> components)
    {
        var rows = new List<IList<HlslTreeNode>>();
        IList<HlslTreeNode> vector = null;
        foreach (HlslTreeNode component in components)
        {
            if (component is not DotProductOperation dot)
            {
                break;
            }
            IList<HlslTreeNode> row = TryGetStructuredRow(dot);
            if (row == null)
            {
                break;
            }
            IList<HlslTreeNode> other = ReferenceEquals(dot.X.Inputs, row)
                ? dot.Y.Inputs
                : dot.X.Inputs;
            if (vector == null)
            {
                vector = other;
            }
            else if (!NodeGrouper.AreNodesEquivalent(vector, other))
            {
                break;
            }
            rows.Add(row);
        }
        if (rows.Count < 2)
        {
            return null;
        }

        var first = (LoadStructuredNode)rows[0][0];
        RegisterKey resourceKey = ((RegisterInputNode)first.Value).RegisterComponentKey.RegisterKey;
        if (_registers.FindStructuredMatrixAt(resourceKey, first.ElementByteOffset)
            is not (string memberPath, ShaderTypeInfo matrixType))
        {
            return null;
        }

        // Same element and buffer, rows in order and tightly packed.
        int bytesPerRow = 4 * (matrixType.ParameterClass == ParameterClass.MatrixRows
            ? matrixType.Columns
            : matrixType.Rows);
        for (int i = 1; i < rows.Count; i++)
        {
            var row = (LoadStructuredNode)rows[i][0];
            if (!ReferenceEquals(row.Address, first.Address)
                || row.ElementByteOffset != first.ElementByteOffset + i * bytesPerRow
                || !((RegisterInputNode)row.Value).RegisterComponentKey.RegisterKey.Equals(resourceKey))
            {
                return null;
            }
        }
        // Three rows of a float4x4 are not the whole matrix.
        if (matrixType.Rows != rows.Count)
        {
            return null;
        }

        // The component index is on the load's resource operand.
        bool matrixByVector = rows[0].All(
            c => ((IHasComponentIndex)((LoadStructuredNode)c).Value).ComponentIndex == 0);
        return new MatrixMultiplicationContext(
            [.. vector], null, matrixByVector, rows.Count, rows[0].Count)
        {
            StructuredBufferName = _registers.GetRegisterName(resourceKey),
            StructuredElement = first.Address,
            StructuredMemberPath = memberPath,
            MatrixTypeInfo = matrixType,
        };
    }

    /// <summary>
    /// The dot operand whose components all load one element at one byte offset.
    /// </summary>
    private static IList<HlslTreeNode> TryGetStructuredRow(DotProductOperation dot)
    {
        foreach (IList<HlslTreeNode> candidate in new[] { dot.X.Inputs, dot.Y.Inputs })
        {
            if (candidate.Count == 0
                || candidate[0] is not LoadStructuredNode { IsRaw: false } head
                || head.Value is not RegisterInputNode)
            {
                continue;
            }
            if (candidate.All(c => c is LoadStructuredNode { IsRaw: false } load
                && load.Value is RegisterInputNode
                && ReferenceEquals(load.Address, head.Address)
                && load.ElementByteOffset == head.ElementByteOffset))
            {
                return candidate;
            }
        }
        return null;
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

    // The first row's register within the variable.
    private int GetRegisterOffset(ConstantDeclaration matrix, HlslTreeNode firstRow)
    {
        RegisterKey key = RowKey(firstRow).RegisterKey;
        return key is D3D10RegisterKey d3d10Key
            ? _registers.GetConstantBufferElementOffset(d3d10Key, matrix)
            : ((D3D9RegisterKey)key).Number - matrix.RegisterIndex;
    }

    // The array element the rows belong to, or null if not an array.
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

    /// <summary>
    /// The whole matrix or a cast of it (`mul((float3x3)world, normal)`). A cast
    /// takes the upper left, so a smaller one must start at register and component 0.
    /// </summary>
    private ConstantDeclaration TryGetMatrixDeclaration(IList<HlslTreeNode[]> dotProductNodes, bool matrixByVector)
    {
        var first = dotProductNodes[0];
        if (!IsRow(first[0]))
        {
            return null;
        }
        RowMatrix matrix = GetRowMatrix(first[0]);
        if (matrix == null)
        {
            return null;
        }

        // In mul(M, v) each dot is a row; in mul(v, M) a column. Row major flips
        // which, as in the compiler; it matters for non-square matrices.
        bool byVector = matrix.MatrixType.ParameterClass == ParameterClass.MatrixRows
            ? !matrixByVector
            : matrixByVector;
        int rows = byVector ? dotProductNodes.Count : first.Length;
        int columns = byVector ? first.Length : dotProductNodes.Count;
        if (rows > matrix.MatrixType.Rows || columns > matrix.MatrixType.Columns)
        {
            return null;
        }
        if (rows == matrix.MatrixType.Rows && columns == matrix.MatrixType.Columns)
        {
            return matrix.Declaration;
        }
        return IsLeadingRow(matrix, first) ? matrix.Declaration : null;
    }

    // Consecutive registers from the matrix's first, or the first register's
    // components in any order (a permutation is a vector swizzle, see SwizzleVector).
    private bool IsLeadingRow(RowMatrix matrix, HlslTreeNode[] row)
    {
        RegisterComponentKey firstKey = RowKey(row[0]);
        int registerOffset = firstKey.RegisterKey is D3D10RegisterKey d3d10Key
            ? _registers.GetConstantBufferElementOffset(d3d10Key, matrix.Declaration)
            : ((D3D9RegisterKey)firstKey.RegisterKey).Number - matrix.Declaration.RegisterIndex;
        if (registerOffset % matrix.Declaration.RegistersPerElement != matrix.StartRegister)
        {
            return false;
        }
        if (row.Any(node => !IsRow(node) || !ReferenceEquals(RowIndex(node), RowIndex(row[0]))))
        {
            return false;
        }
        RegisterComponentKey[] keys = [.. row.Select(RowKey)];
        bool registersOn = keys.Select((key, i) => key.ComponentIndex == 0 && IsRegistersOn(firstKey, key, i)).All(on => on);
        bool componentsOfFirst = keys.All(key => IsRegistersOn(firstKey, key, 0))
            && keys.Select(key => key.ComponentIndex).OrderBy(c => c).SequenceEqual(Enumerable.Range(0, row.Length));
        return registersOn || componentsOfFirst;
    }

    private static bool IsRegistersOn(RegisterComponentKey first, RegisterComponentKey key, int count)
    {
        if (!first.RegisterKey.TypeEquals(key.RegisterKey))
        {
            return false;
        }
        if (first.RegisterKey is D3D10RegisterKey firstD3D10 && key.RegisterKey is D3D10RegisterKey d3d10)
        {
            return firstD3D10.Number == d3d10.Number
                && firstD3D10.ConstantBufferOffset + count == d3d10.ConstantBufferOffset;
        }
        return first.RegisterKey.Number + count == key.RegisterKey.Number;
    }

    /// <summary>
    /// Rows of the same matrix (and array element) against the same vector, in any
    /// order; the whole group is checked at compile time.
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

    /// <summary>
    /// The row's register, for ordering merged rows as they sit in the matrix.
    /// </summary>
    public int? MatrixRowRegister(DotProductOperation dot)
    {
        IList<HlslTreeNode> row = TryGetMatrixRow(dot, dot, 0);
        if (row == null)
        {
            return null;
        }
        // A constant buffer register is its offset; its number is the buffer.
        RegisterComponentKey key = RowKey(row[0]);
        return key.RegisterKey is D3D10RegisterKey { ConstantBufferOffset: int offset }
            ? offset
            : key.RegisterKey.Number;
    }

    // `c1` or `c1[a0.x]`.
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
    /// A matrix constant (or array element), or a struct's matrix member such as
    /// `g_x.world`; null otherwise.
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
            return new RowMatrix(constant, constant.TypeInfo, null, 0);
        }
        if (_registers.TryGetStructMatrixAt(key, constant, out StructMemberAccess member, out _))
        {
            return new RowMatrix(constant, member.TypeInfo, member.Name, member.StartOffset / 4);
        }
        return null;
    }

    // StartRegister is the register within the element where the matrix starts.
    private sealed record RowMatrix(ConstantDeclaration Declaration, ShaderTypeInfo MatrixType, string MemberPath, int StartRegister);

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

    // Null for a structured buffer matrix, named by the Structured* properties.
    public ConstantDeclaration MatrixDeclaration { get; }

    public string StructuredBufferName { get; init; }
    public HlslTreeNode StructuredElement { get; init; }
    public string StructuredMemberPath { get; init; }

    public int? ElementIndex { get; init; }
    public HlslTreeNode ElementIndexNode { get; init; }
    // The register stride was already stripped at the read.
    public bool ElementIndexCountsElements { get; init; }
    // ".world" for a struct's matrix member.
    public string MemberPath { get; init; }
    // The first row's register within the variable, which picks the member of a
    // struct whose array the run-time index subscripts.
    public int FirstRowRegister { get; init; }
    public ShaderTypeInfo MatrixTypeInfo { get; init; }
    public bool IsMatrixByVector { get; }
    public int MatrixRowCount { get; }
    public int MatrixColumnCount { get; }
}
