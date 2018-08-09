namespace HlslDecompiler.Hlsl
{
    public class ReciprocalOperation : Operation
    {
        public ReciprocalOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public HlslTreeNode Value => Children[0];

        public override string Mnemonic => "rcp";
    }
}
