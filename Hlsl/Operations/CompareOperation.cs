namespace HlslDecompiler.Hlsl;

public class CompareOperation : Operation
{
    public CompareOperation(HlslTreeNode value, HlslTreeNode greaterEqualValue, HlslTreeNode lessValue)
    {
        AddInput(value);
        AddInput(greaterEqualValue);
        AddInput(lessValue);
    }

    public HlslTreeNode Value => Inputs[0];
    public HlslTreeNode GreaterEqualValue => Inputs[1];
    public HlslTreeNode LessValue => Inputs[2];

    public override string Mnemonic => "cmp";
}
