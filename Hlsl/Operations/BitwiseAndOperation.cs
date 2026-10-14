namespace HlslDecompiler.Hlsl;

// Bitwise & on integers, not the logical use on comparison masks.
public class BitwiseAndOperation : Operation
{
    public BitwiseAndOperation(HlslTreeNode value1, HlslTreeNode value2)
    {
        AddInput(value1);
        AddInput(value2);
    }

    public HlslTreeNode Value1 => Inputs[0];
    public HlslTreeNode Value2 => Inputs[1];

    public override string Mnemonic => "and";
}
