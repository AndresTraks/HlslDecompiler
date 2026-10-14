namespace HlslDecompiler.Hlsl;

// step(edge, x) = `x >= edge ? 1 : 0` (SM3 sge).
public class StepOperation : Operation
{
    public StepOperation(HlslTreeNode edge, HlslTreeNode value)
    {
        AddInput(edge);
        AddInput(value);
    }

    public HlslTreeNode Edge => Inputs[0];
    public HlslTreeNode Value => Inputs[1];

    public override string Mnemonic => "step";
}
