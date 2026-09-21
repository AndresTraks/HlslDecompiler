namespace HlslDecompiler.Hlsl;

public class MaximumOperation : Operation
{
    public MaximumOperation(HlslTreeNode value1, HlslTreeNode value2, bool isUnsigned = false)
    {
        AddInput(value1);
        AddInput(value2);
        IsUnsigned = isUnsigned;
    }

    public HlslTreeNode Value1 => Inputs[0];
    public HlslTreeNode Value2 => Inputs[1];

    // umax rather than imax: the two order the top bit differently, and HLSL picks
    // which from the type of the operands, so the unsigned one has to say so at
    // the value or a value above 2^31 comes out the smaller.
    public bool IsUnsigned { get; }

    public override string Mnemonic => "max";
}
