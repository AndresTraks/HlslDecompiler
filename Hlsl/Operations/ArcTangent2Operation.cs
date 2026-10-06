namespace HlslDecompiler.Hlsl;

/// <summary>
/// The angle of the point (x, y), which shader bytecode has no instruction for and
/// an fx_2_0 preshader does.
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
