namespace HlslDecompiler.Hlsl;

public class SignLessOperation : Operation
{
    public SignLessOperation(HlslTreeNode value1, HlslTreeNode value2)
    {
        AddInput(value1);
        AddInput(value2);
    }

    public override string Mnemonic => "slt";
}
