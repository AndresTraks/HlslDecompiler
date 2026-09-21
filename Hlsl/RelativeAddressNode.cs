using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl;

// A constant read through the address register, `c0[a0.x]` in assembly and
// `floats[address]` in HLSL. The base register names the array and picks the
// component; Inputs[0] is the index expression, whatever wrote the address
// register.
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
    /// Whether the index counts elements of the array rather than registers. A
    /// register index into an array of matrices is the element times the rows,
    /// and where the bytecode's multiplication was visible it has been taken off
    /// at the read - so the index is the element and needs no dividing back.
    /// </summary>
    public bool IndexCountsElements { get; init; }

    public override string ToString()
    {
        return $"{RegisterComponentKey}[{Index}]";
    }
}
