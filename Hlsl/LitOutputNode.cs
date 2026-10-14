namespace HlslDecompiler.Hlsl;

// One component of `lit`; the destination component picks the formula
// (1, max(n.l, 0), specular, 1), not the source component.
public class LitOutputNode : HlslTreeNode, IHasComponentIndex
{
    public LitOutputNode(
        HlslTreeNode nDotL, HlslTreeNode nDotH, HlslTreeNode specularPower, int componentIndex)
    {
        AddInput(nDotL);
        AddInput(nDotH);
        AddInput(specularPower);

        ComponentIndex = componentIndex;
    }

    public HlslTreeNode NDotL => Inputs[0];
    public HlslTreeNode NDotH => Inputs[1];
    public HlslTreeNode SpecularPower => Inputs[2];

    public int ComponentIndex { get; }
}
