namespace HlslDecompiler.Hlsl;

public class MoveConditionalOperation : Operation
{
    public MoveConditionalOperation(HlslTreeNode condition, HlslTreeNode source1, HlslTreeNode source2)
    {
        AddInput(condition);
        AddInput(source1);
        AddInput(source2);
    }

    public HlslTreeNode Condition => Inputs[0];
    public HlslTreeNode Source1 => Inputs[1];
    public HlslTreeNode Source2 => Inputs[2];

    public override string Mnemonic => "movc";
}
