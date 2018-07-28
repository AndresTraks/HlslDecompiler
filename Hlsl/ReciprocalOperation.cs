namespace HlslDecompiler.Hlsl
{
    public class ReciprocalOperation : Operation
    {
        public ReciprocalOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public override string Mnemonic => "rcp";
    }
}
