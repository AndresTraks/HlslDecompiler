namespace HlslDecompiler.Hlsl;

// Bitwise | on integers, as opposed to the logical use of the same opcode on
// comparison results.
public class BitwiseOrOperation : Operation
{
    public BitwiseOrOperation(HlslTreeNode value1, HlslTreeNode value2)
    {
        AddInput(value1);
        AddInput(value2);
    }

    public HlslTreeNode Value1 => Inputs[0];
    public HlslTreeNode Value2 => Inputs[1];

    public override string Mnemonic => "or";
}
