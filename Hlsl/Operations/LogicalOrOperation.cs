namespace HlslDecompiler.Hlsl;

public class LogicalOrOperation : Operation
{
    public LogicalOrOperation(HlslTreeNode value1, HlslTreeNode value2)
    {
        AddInput(value1);
        AddInput(value2);
    }

    public override string Mnemonic => "or";
}
