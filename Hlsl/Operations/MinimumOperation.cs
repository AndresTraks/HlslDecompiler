namespace HlslDecompiler.Hlsl;

public class MinimumOperation : Operation
{
    public MinimumOperation(HlslTreeNode value1, HlslTreeNode value2, bool isUnsigned = false)
    {
        AddInput(value1);
        AddInput(value2);
        IsUnsigned = isUnsigned;
    }

    public HlslTreeNode Value1 => Inputs[0];
    public HlslTreeNode Value2 => Inputs[1];

    // umin: HLSL picks signedness from the operand type, so it must be cast.
    public bool IsUnsigned { get; }

    public override string Mnemonic => "min";
}
