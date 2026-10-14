using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl;

// A constant read through the address register (`c0[a0.x]`). The base register
// names the array and component; Inputs[0] is the index expression.
public class RelativeAddressNode : HlslTreeNode, IHasComponentIndex
{
    public RelativeAddressNode(RegisterComponentKey registerComponentKey, HlslTreeNode index)
    {
        RegisterComponentKey = registerComponentKey;
        AddInput(index);
    }

    public RegisterComponentKey RegisterComponentKey { get; }

    public int ComponentIndex => RegisterComponentKey.ComponentIndex;

    public HlslTreeNode Index => Inputs[0];

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
