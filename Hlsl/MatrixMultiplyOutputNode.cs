using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// One component (one row's dot product) of a matrix multiplication; the idiom is
/// one of these per row, sharing vector and matrix. A node rather than writer-side
/// recognition so naming cannot split the dot products apart (see IdiomRecovery).
/// </summary>
public class MatrixMultiplyOutputNode : HlslTreeNode, IHasComponentIndex
{
    private readonly MatrixMultiplicationContext _matrix;
    private readonly int _vectorLength;
    private readonly int _elementIndexAt = -1;
    private readonly int _structuredElementAt = -1;

    public MatrixMultiplyOutputNode(MatrixMultiplicationContext matrix, int componentIndex)
    {
        _matrix = matrix;
        _vectorLength = matrix.Vector.Length;
        foreach (HlslTreeNode component in matrix.Vector)
        {
            AddInput(component);
        }
        // Expression operands are edges too, so folds and names reach them.
        if (matrix.ElementIndexNode != null)
        {
            _elementIndexAt = Inputs.Count;
            AddInput(matrix.ElementIndexNode);
        }
        if (matrix.StructuredElement != null)
        {
            _structuredElementAt = Inputs.Count;
            AddInput(matrix.StructuredElement);
        }
        ComponentIndex = componentIndex;
    }

    public int ComponentIndex { get; }

    public int RowCount => _matrix.MatrixRowCount;

    /// <summary>
    /// Rebuilt from the current inputs: the original context would still hold nodes
    /// that names have since replaced (cf. IStatement.HeldSlots).
    /// </summary>
    public MatrixMultiplicationContext Matrix => new(
        [.. Inputs.Take(_vectorLength)],
        _matrix.MatrixDeclaration,
        _matrix.IsMatrixByVector,
        _matrix.MatrixRowCount,
        _matrix.MatrixColumnCount)
    {
        StructuredBufferName = _matrix.StructuredBufferName,
        StructuredElement = _structuredElementAt < 0 ? null : Inputs[_structuredElementAt],
        StructuredMemberPath = _matrix.StructuredMemberPath,
        ElementIndex = _matrix.ElementIndex,
        ElementIndexNode = _elementIndexAt < 0 ? null : Inputs[_elementIndexAt],
        ElementIndexCountsElements = _matrix.ElementIndexCountsElements,
        MemberPath = _matrix.MemberPath,
        MatrixTypeInfo = _matrix.MatrixTypeInfo,
    };

    public override string ToString()
    {
        string name = _matrix.MatrixDeclaration?.Name ?? _matrix.StructuredBufferName;
        return $"mul({name}).{"xyzw"[ComponentIndex]}";
    }
}
