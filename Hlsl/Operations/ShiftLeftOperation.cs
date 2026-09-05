namespace HlslDecompiler.Hlsl;

// ishl. fxc emits it for a multiplication by a power of two, among other things.
public class ShiftLeftOperation : Operation
{
    public ShiftLeftOperation(HlslTreeNode value, HlslTreeNode amount)
    {
        AddInput(value);
        AddInput(amount);
    }

    public HlslTreeNode Value => Inputs[0];
    public HlslTreeNode Amount => Inputs[1];

    public override string Mnemonic => "ishl";
}
