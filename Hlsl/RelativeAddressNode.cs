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

    public override string ToString()
    {
        return $"{RegisterComponentKey}[{Index}]";
    }
}
