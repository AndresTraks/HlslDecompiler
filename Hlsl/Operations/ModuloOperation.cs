namespace HlslDecompiler.Hlsl;

// The remainder half of an integer divide. udiv produces both at once.
public class ModuloOperation : Operation
{
    public ModuloOperation(HlslTreeNode dividend, HlslTreeNode divisor)
    {
        AddInput(dividend);
        AddInput(divisor);
    }

    public HlslTreeNode Dividend => Inputs[0];
    public HlslTreeNode Divisor => Inputs[1];

    public override string Mnemonic => "mod";
}
