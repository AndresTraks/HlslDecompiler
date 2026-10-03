using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// One component of a vector by matrix multiplication - one row's worth. fxc writes
/// `mul(v, M)` as a dot product of the vector against each row of the matrix, so the
/// idiom is as many of these as the matrix has rows, all sharing the vector and the
/// matrix.
///
/// A node rather than something the compiler recognises out of the components it is
/// about to write, for the reason IdiomRecovery gives. This is the one where that
/// matters most: a matrix multiply is the idiom the naming is most apt to take apart,
/// because its components are dot products that read well on their own, and the
/// writer cannot see that the four of them are one thing until it compiles them.
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
        // The operands that are expressions rather than names go in as edges too, so
        // that a fold or a name reaching one of them reaches this.
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

    /// <summary>How many components the multiplication has, which is a row each.</summary>
    public int RowCount => _matrix.MatrixRowCount;

    /// <summary>
    /// The multiplication as it stands now, with the operands read back out of the
    /// graph rather than out of the context this was built from. A name given to
    /// something in the vector after the idiom was recovered has to reach the text,
    /// and a context built once would still be holding the node the name replaced -
    /// which is the same trap IStatement.HeldSlots is about.
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
