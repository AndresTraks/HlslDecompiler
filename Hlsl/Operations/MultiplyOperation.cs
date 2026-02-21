namespace HlslDecompiler.Hlsl;

public class MultiplyOperation : Operation
{
    public MultiplyOperation(HlslTreeNode factor1, HlslTreeNode factor2)
    {
        AddInput(factor1);
        AddInput(factor2);
    }

    public HlslTreeNode Factor1 => Inputs[0];
    public HlslTreeNode Factor2 => Inputs[1];

    public override string Mnemonic => "mul";
}
