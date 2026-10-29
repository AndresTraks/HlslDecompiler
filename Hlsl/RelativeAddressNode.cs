using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl;

// A constant read through the address register (`c0[a0.x]`). The base register
// names the array and component; Inputs[0] is the index expression.
public class RelativeAddressNode : HlslTreeNode, IHasComponentIndex
{
    public RelativeAddressNode(
        RegisterComponentKey registerComponentKey, HlslTreeNode index,
        HlslTreeNode fieldIndex = null)
    {
        RegisterComponentKey = registerComponentKey;
        AddInput(index);
        if (fieldIndex != null)
        {
            AddInput(fieldIndex);
        }
    }

    public RegisterComponentKey RegisterComponentKey { get; }

    public int ComponentIndex => RegisterComponentKey.ComponentIndex;

    public HlslTreeNode Index => Inputs[0];

    /// <summary>
    /// The second dynamic index of a two-index read whose member run became an
    /// array: `patch[i].edge[j]` selects the field, not the vertex.
    /// </summary>
    public HlslTreeNode FieldIndex => Inputs.Count > 1 ? Inputs[1] : null;

    /// <summary>
    /// Whether the index counts array elements, not registers: the bytecode's
    /// multiply by the matrix size was already removed.
    /// </summary>
    public bool IndexCountsElements { get; init; }

    public override string ToString()
    {
        return $"{RegisterComponentKey}[{Index}]";
    }
}
