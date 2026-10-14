namespace HlslDecompiler.Hlsl;

// fma(a, b, c) from dfma only; float mad is written as arithmetic.
public class FusedMultiplyAddOperation : Operation
{
    public FusedMultiplyAddOperation(HlslTreeNode factor1, HlslTreeNode factor2, HlslTreeNode addend)
    {
        AddInput(factor1);
        AddInput(factor2);
        AddInput(addend);
    }

    public HlslTreeNode Factor1 => Inputs[0];
    public HlslTreeNode Factor2 => Inputs[1];
    public HlslTreeNode Addend => Inputs[2];

    public override string Mnemonic => "fma";
}
