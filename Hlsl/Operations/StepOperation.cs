namespace HlslDecompiler.Hlsl;

// step(edge, x), which is `x >= edge ? 1 : 0`. Shader model 3 has the
// instruction under the name sge; shader model 4 writes the comparison and
// then picks between the two constants.
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
