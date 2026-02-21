namespace HlslDecompiler.Hlsl;

public class MultiplyAddOperation : Operation
{
    public MultiplyAddOperation(HlslTreeNode factor1, HlslTreeNode factor2, HlslTreeNode addend)
    {
        AddInput(factor1);
        AddInput(factor2);
        AddInput(addend);
    }

    public HlslTreeNode Factor1 => Inputs[0];
    public HlslTreeNode Factor2 => Inputs[1];
    public HlslTreeNode Addend => Inputs[2];

    public override string Mnemonic => "madd";
}
