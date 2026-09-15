namespace HlslDecompiler.Hlsl;

// Bitwise ~ on an integer. A not of a comparison is the comparison inverted, and
// never reaches here.
public class BitwiseNotOperation : Operation
{
    public BitwiseNotOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public HlslTreeNode Value => Inputs[0];

    public override string Mnemonic => "not";
}
