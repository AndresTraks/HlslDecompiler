namespace HlslDecompiler.Hlsl
{
    public class ReciprocalSquareRootOperation : Operation
    {
        public ReciprocalSquareRootOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public HlslTreeNode Value => Children[0];

        public override string Mnemonic => "rsqrt";
    }
}
