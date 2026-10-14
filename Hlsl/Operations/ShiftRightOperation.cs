namespace HlslDecompiler.Hlsl;

// ishr and ushr. HLSL picks the fill from the value's type, so ushr must cast it.
public class ShiftRightOperation : Operation
{
    public ShiftRightOperation(HlslTreeNode value, HlslTreeNode amount, bool isUnsigned)
    {
        AddInput(value);
        AddInput(amount);
        IsUnsigned = isUnsigned;
    }

    public HlslTreeNode Value => Inputs[0];
    public HlslTreeNode Amount => Inputs[1];
    public bool IsUnsigned { get; }

    public override string Mnemonic => IsUnsigned ? "ushr" : "ishr";
}
