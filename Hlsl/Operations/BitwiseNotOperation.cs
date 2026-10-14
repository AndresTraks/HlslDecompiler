namespace HlslDecompiler.Hlsl;

// Bitwise ~ on an integer; a not of a comparison becomes the inverted comparison.
public class BitwiseNotOperation : Operation
{
    public BitwiseNotOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public HlslTreeNode Value => Inputs[0];

    public override string Mnemonic => "not";
}
