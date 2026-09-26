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
        if (components.All(c => c is AddOperation))
        {
            HlslTreeNode[] submatrixNodes = components.Select(g => g.Inputs[0]).ToArray();
            MatrixMultiplicationContext submatrixGroup = TryGetMultiplicationGroup(submatrixNodes);
            // A matrix that is a member of a struct has no column past its last for
            // the addend to be: what sits at that register is the next member, and
            // taking it for a column makes a float4x5 of a struct and a float5 of
            // the vector. The registers either side of a matrix member belong to
            // something else by construction, so the addend is simply an addend.
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

        // A matrix in a structured buffer is read as one load per row rather than as a
        // run of constant registers, so none of the register arithmetic below sees it.
        // It is recognised on its own terms instead, and cannot collide with that: a
        // row there is a RegisterInputNode or a RelativeAddressNode, and never a load.
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
        };
    }

    /// <summary>
    /// A vector multiplied by a matrix a structured buffer holds. fxc reads such a
    /// matrix a row at a time - one ld_structured per row, at byte offsets sixteen
    /// apart in the one element - and dots the vector against each, which is the same
    /// shape a constant buffer matrix makes out of consecutive registers. Written as
    /// the dot products they are, an instance transform came out as four lines of
    /// `dot(v, transpose(instances[id].world)[row])` where the source said mul.
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

        // One element of one buffer, and the rows in order: sixteen bytes apart, the
        // first of them where the matrix begins. Anything else is a run of loads that
        // happens to be dotted, not a matrix multiplication.
        const int BytesPerRow = 16;
        var first = (LoadStructuredNode)rows[0][0];
        RegisterKey resourceKey = ((RegisterInputNode)first.Value).RegisterComponentKey.RegisterKey;
        for (int i = 1; i < rows.Count; i++)
        {
            var row = (LoadStructuredNode)rows[i][0];
            if (!ReferenceEquals(row.Address, first.Address)
                || row.ElementByteOffset != first.ElementByteOffset + i * BytesPerRow
                || !((RegisterInputNode)row.Value).RegisterComponentKey.RegisterKey.Equals(resourceKey))
            {
                return null;
            }
        }

        if (_registers.FindStructuredMatrixAt(resourceKey, first.ElementByteOffset)
            is not (string memberPath, ShaderTypeInfo matrixType))
        {
            return null;
        }
        // As many rows as the matrix has, so that a shader dotting three rows of a
        // float4x4 is not written as the whole of it.
        if (matrixType.Rows != rows.Count)
        {
            return null;
        }

        // A load carries no component index of its own; the resource operand it reads
        // does. Which way the row lies is the same question the constant buffer case
        // asks - every component the register's first is a matrix read by column - and
        // one load per row answers it with components zero upwards.
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
    /// The side of a dot product that is one row of a structured buffer element: every
    /// component a load from the one element at the one byte offset. The other side is
    /// the vector.
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

    /// <summary>
    /// The matrix the dot products multiply by: the whole of one, or the rows and
    /// columns a cast names - `mul((float3x3)world, normal)` is three dots of three
    /// registers, and `mul(v, (float4x3)m)` three dots of four. A cast takes the
    /// upper left of the matrix, so anything smaller than the declaration has to
    /// start at its first register and first component.
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

        // In mul(matrix, vector) each dot is a row and its width the columns; in
        // mul(vector, matrix) each dot is a column and its width the rows. Which it
        // is was read off the registers, and those are the matrix's rows where it
        // was packed by row - so the same dots are the other multiplication, the
        // way the compiler reads them back. Square, the two agree and nothing shows;
        // a row major float4x3 measured as a float3x4 is no float4x3 and did not
        // group at all.
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

    // Whether a row reads the matrix from its first register and component on -
    // the registers one after another from the matrix's first, or the first
    // register's components from .x, in any order: a permuted row is a swizzle of
    // the vector, which SwizzleVector applies.
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

    // Whether a register is so many registers past another of the same kind.
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

    /// <summary>
    /// The register a dot product's matrix row is read from, or null where the dot
    /// is not a row of a matrix against a vector. Four rows merged into one
    /// variable in the order they were read come out as a matrix multiply only in
    /// the order they sit in the matrix.
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
    /// array of them, or a matrix member of a struct - `instances[id].world` read as
    /// cb0[r0.x + 2], or `g_x.world` - or null for a register that is no row of
    /// anything. A struct that is not an array has the one element, which is what
    /// the element arithmetic here already amounts to for it.
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

    // A matrix constant, or a matrix member of a struct element, and the register
    // within the element it starts at.
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

    // Null for a matrix in a structured buffer, which no constant declares: the
    // three properties below name it instead.
    public ConstantDeclaration MatrixDeclaration { get; }

    // A matrix in a structured buffer: the buffer's name, the node picking the
    // element, and the member within it. `instances`, `i.sv_instanceid`, `.world`.
    public string StructuredBufferName { get; init; }
    public HlslTreeNode StructuredElement { get; init; }
    public string StructuredMemberPath { get; init; }

    public int? ElementIndex { get; init; }
    // The index expression of rows read through the address register, or null.
    public HlslTreeNode ElementIndexNode { get; init; }
    // Whether that index already counts elements, the stride having been taken
    // off at the read.
    public bool ElementIndexCountsElements { get; init; }
    // For a matrix member of a struct element, the member's name - ".world" -
    // following the element subscript, and the member's own type.
    public string MemberPath { get; init; }
    public ShaderTypeInfo MatrixTypeInfo { get; init; }
    public bool IsMatrixByVector { get; }
    public int MatrixRowCount { get; }
    public int MatrixColumnCount { get; }
}
