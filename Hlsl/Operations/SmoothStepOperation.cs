namespace HlslDecompiler.Hlsl;

// smoothstep(edge0, edge1, x); no instruction, recovered by SmoothStepTemplate.
public class SmoothStepOperation : Operation
{
    public SmoothStepOperation(HlslTreeNode edge0, HlslTreeNode edge1, HlslTreeNode value)
    {
        AddInput(edge0);
        AddInput(edge1);
        AddInput(value);
    }

    public HlslTreeNode Edge0 => Inputs[0];
    public HlslTreeNode Edge1 => Inputs[1];
    public HlslTreeNode Value => Inputs[2];

    public override string Mnemonic => "smoothstep";
}
