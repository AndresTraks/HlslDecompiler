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

    // umax: HLSL picks signedness from the operand type, so it must be cast.
    public bool IsUnsigned { get; }

    protected override bool IsUnsignedVariant => IsUnsigned;

    public override string Mnemonic => "max";
}
