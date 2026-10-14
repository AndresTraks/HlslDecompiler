namespace HlslDecompiler.Hlsl;

// clamp(x, low, high), defined as min(max(x, low), high).
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
