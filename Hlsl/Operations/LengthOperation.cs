namespace HlslDecompiler.Hlsl
{
    public class LengthOperation : Operation
    {
        public LengthOperation(GroupNode node)
        {
            AddInput(node);
        }

        public GroupNode X => Inputs[0] as GroupNode;

        public override string Mnemonic => "length";
    }
}
