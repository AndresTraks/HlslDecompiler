namespace HlslDecompiler.Hlsl;

// cnd: value > 0.5 ? greaterValue : lessValue. The 0.5 is the instruction's own
// constant, not an operand. It is no CompareOperation: that one tests `>= 0`,
// and shifted to the half its test is true exactly where this one is false -
// at 0.5 itself, which no equivalence trial would ever feed it.
public class CndOperation : Operation
{
    public CndOperation(HlslTreeNode value, HlslTreeNode greaterValue, HlslTreeNode lessValue)
    {
        AddInput(value);
        AddInput(greaterValue);
        AddInput(lessValue);
    }

    public HlslTreeNode Value => Inputs[0];
    public HlslTreeNode GreaterValue => Inputs[1];
    public HlslTreeNode LessValue => Inputs[2];

    public override string Mnemonic => "cnd";
}
