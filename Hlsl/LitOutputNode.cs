namespace HlslDecompiler.Hlsl;

// One component of a `lit` result. The four components are four different formulas
// over the same three source components - 1, max(n.l, 0), the specular term and 1
// again - so unlike most instructions the destination component decides the value
// rather than which part of the source is read.
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
