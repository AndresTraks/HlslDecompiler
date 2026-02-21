namespace HlslDecompiler.Hlsl;

public class LinearInterpolateOperation : Operation
{
    public LinearInterpolateOperation(HlslTreeNode amount, HlslTreeNode value1, HlslTreeNode value2)
    {
        AddInput(amount);
        AddInput(value1);
        AddInput(value2);
    }

    public HlslTreeNode Amount => Inputs[0];
    public HlslTreeNode Value1 => Inputs[1];
    public HlslTreeNode Value2 => Inputs[2];

    public override string Mnemonic => "lrp";
}
