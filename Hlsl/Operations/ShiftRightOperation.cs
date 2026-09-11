namespace HlslDecompiler.Hlsl;

// ishr and ushr. The signed and unsigned forms differ in what fills the top bits,
// which HLSL decides from the type of the value rather than from the operator, so
// both write as >>.
public class ShiftRightOperation : Operation
{
    public ShiftRightOperation(HlslTreeNode value, HlslTreeNode amount)
    {
        AddInput(value);
        AddInput(amount);
    }

    public HlslTreeNode Value => Inputs[0];
    public HlslTreeNode Amount => Inputs[1];

    public override string Mnemonic => "ishr";
}
