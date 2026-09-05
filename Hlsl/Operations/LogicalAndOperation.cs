namespace HlslDecompiler.Hlsl;

public class LogicalAndOperation : Operation
{
    public LogicalAndOperation(HlslTreeNode value1, HlslTreeNode value2)
    {
        AddInput(value1);
        AddInput(value2);
    }

    public override string Mnemonic => "and";
}
