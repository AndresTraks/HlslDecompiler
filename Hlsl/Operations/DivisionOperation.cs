namespace HlslDecompiler.Hlsl;

public class DivisionOperation : Operation
{
    public DivisionOperation(HlslTreeNode dividend, HlslTreeNode divisor)
    {
        AddInput(dividend);
        AddInput(divisor);
    }

    public HlslTreeNode Dividend => Inputs[0];
    public HlslTreeNode Divisor => Inputs[1];

    public override string Mnemonic => "div";
}
