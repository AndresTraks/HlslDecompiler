using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

public class PhiNode : HlslTreeNode
{
    public PhiNode(params HlslTreeNode[] inputs)
    {
        foreach (HlslTreeNode input in inputs)
        {
            AddInput(input);
        }
    }

    public override string ToString()
    {
        return "phi(" + string.Join(", ", Inputs.Select(i => i.ToString())) + ")";
    }
}
