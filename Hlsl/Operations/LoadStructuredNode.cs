namespace HlslDecompiler.Hlsl;

public class LoadStructuredNode : Operation
{
    public LoadStructuredNode(HlslTreeNode address, HlslTreeNode byteOffset, HlslTreeNode value)
    {
        AddInput(address);
        AddInput(byteOffset);
        AddInput(value);
    }

    public HlslTreeNode Address => Inputs[0];
    public HlslTreeNode ByteOffset => Inputs[1];
    public HlslTreeNode Value => Inputs[2];

    public override string Mnemonic => "ld_structured";
}
