namespace HlslDecompiler.Hlsl;

// clamp(x, low, high), which HLSL defines as min(max(x, low), high) - so the two
// are the same expression and not merely close.
public class ClampOperation : Operation
{
    public ClampOperation(HlslTreeNode value, HlslTreeNode low, HlslTreeNode high)
    {
        AddInput(value);
        AddInput(low);
        AddInput(high);
    }

    public HlslTreeNode Value => Inputs[0];
    public HlslTreeNode Low => Inputs[1];
    public HlslTreeNode High => Inputs[2];

    public override string Mnemonic => "clamp";
}
