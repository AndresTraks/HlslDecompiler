namespace HlslDecompiler.Hlsl;

// ishr and ushr. The two differ in what fills the top bits, and HLSL decides that
// from the type of the value rather than from the operator - so the unsigned form
// has to say so at the value, or it shifts in copies of the sign bit instead of
// zeroes and a hash comes out as its own complement.
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
