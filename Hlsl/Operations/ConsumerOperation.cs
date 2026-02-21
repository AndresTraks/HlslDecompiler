namespace HlslDecompiler.Hlsl;

public abstract class ConsumerOperation : Operation
{
    public HlslTreeNode Value => Inputs[0];
}
