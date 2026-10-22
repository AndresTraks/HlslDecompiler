namespace HlslDecompiler.Hlsl;

// clamp(x, low, high), defined as min(max(x, low), high).
public class ClampOperation : Operation
{
    public ClampOperation(HlslTreeNode value, HlslTreeNode low, HlslTreeNode high, bool isUnsigned = false)
    {
        AddInput(value);
        AddInput(low);
        AddInput(high);
        IsUnsigned = isUnsigned;
    }

    public HlslTreeNode Value => Inputs[0];
    public HlslTreeNode Low => Inputs[1];
    public HlslTreeNode High => Inputs[2];

    // From umin/umax; HLSL picks the overload from the operands.
    public bool IsUnsigned { get; }

    protected override bool IsUnsignedVariant => IsUnsigned;

    public override string Mnemonic => "clamp";
}
