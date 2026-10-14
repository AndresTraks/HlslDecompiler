namespace HlslDecompiler.Hlsl;

/// <summary>
/// atan2, from an fx_2_0 preshader (shader bytecode has no such instruction).
/// </summary>
public class ArcTangent2Operation : Operation
{
    public ArcTangent2Operation(HlslTreeNode y, HlslTreeNode x)
    {
        AddInput(y);
        AddInput(x);
    }

    public HlslTreeNode Y => Inputs[0];
    public HlslTreeNode X => Inputs[1];

    public override string Mnemonic => "atan2";
}
