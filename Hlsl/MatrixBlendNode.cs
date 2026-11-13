using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// A weighted sum of matrices, `A * w1 + B * w2`, as skinning blends bones. One
/// node for every multiplication by the same blend, so naming can give it one
/// matrix variable.
/// </summary>
public class MatrixBlendNode : HlslTreeNode
{
    private readonly IReadOnlyList<(MatrixMultiplicationContext Matrix, HlslTreeNode Weight)> _terms;
    // Per term, where its element index (or -1) and its weight are.
    private readonly (int ElementIndexAt, int WeightAt)[] _at;

    public MatrixBlendNode(IReadOnlyList<(MatrixMultiplicationContext Matrix, HlslTreeNode Weight)> terms)
    {
        _terms = terms;
        _at = new (int, int)[terms.Count];
        for (int i = 0; i < terms.Count; i++)
        {
            int elementIndexAt = -1;
            if (terms[i].Matrix.ElementIndexNode is HlslTreeNode elementIndex)
            {
                elementIndexAt = Inputs.Count;
                AddInput(elementIndex);
            }
            _at[i] = (elementIndexAt, Inputs.Count);
            AddInput(terms[i].Weight);
        }
    }

    /// <summary>The declared type of the matrices, `float4x3`.</summary>
    public string TypeName
    {
        get
        {
            DirectXShaderModel.ShaderTypeInfo type = _terms[0].Matrix.MatrixTypeInfo;
            return $"float{type.Rows}x{type.Columns}";
        }
    }

    /// <summary>Rebuilt from the current inputs, as MatrixMultiplyOutputNode.Matrix.</summary>
    public IReadOnlyList<(MatrixMultiplicationContext Matrix, HlslTreeNode Weight)> Terms =>
        [.. _at.Select((at, i) => (Term(_terms[i].Matrix, at.ElementIndexAt), Inputs[at.WeightAt]))];

    private MatrixMultiplicationContext Term(MatrixMultiplicationContext term, int elementIndexAt) => new(
        [], term.MatrixDeclaration, term.IsMatrixByVector, term.MatrixRowCount, term.MatrixColumnCount)
    {
        ElementIndex = term.ElementIndex,
        ElementIndexNode = elementIndexAt < 0 ? null : Inputs[elementIndexAt],
        ElementIndexCountsElements = term.ElementIndexCountsElements,
        MemberPath = term.MemberPath,
        MatrixTypeInfo = term.MatrixTypeInfo,
        FirstRowRegister = term.FirstRowRegister,
    };

    // The same matrices, elements and weights.
    public static bool AreSameBlend(
        IReadOnlyList<(MatrixMultiplicationContext Matrix, HlslTreeNode Weight)> a,
        IReadOnlyList<(MatrixMultiplicationContext Matrix, HlslTreeNode Weight)> b)
    {
        return a.Count == b.Count && a.Zip(b).All(pair =>
            pair.First.Matrix.MatrixDeclaration == pair.Second.Matrix.MatrixDeclaration
            && pair.First.Matrix.ElementIndex == pair.Second.Matrix.ElementIndex
            && pair.First.Matrix.MatrixTypeInfo.Rows == pair.Second.Matrix.MatrixTypeInfo.Rows
            && pair.First.Matrix.MatrixTypeInfo.Columns == pair.Second.Matrix.MatrixTypeInfo.Columns
            && (pair.First.Matrix.ElementIndexNode == null
                ? pair.Second.Matrix.ElementIndexNode == null
                : pair.Second.Matrix.ElementIndexNode != null
                    && NodeGrouper.AreNodesEquivalent(pair.First.Matrix.ElementIndexNode, pair.Second.Matrix.ElementIndexNode))
            && NodeGrouper.AreNodesEquivalent(pair.First.Weight, pair.Second.Weight));
    }

    public override string ToString()
    {
        return $"blend({_terms[0].Matrix.MatrixDeclaration.Name})";
    }
}
