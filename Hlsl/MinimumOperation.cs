namespace HlslDecompiler.Hlsl
{
    public class MinimumOperation : Operation
    {
        public MinimumOperation(HlslTreeNode value1, HlslTreeNode value2)
        {
            AddChild(value1);
            AddChild(value2);
        }

        public HlslTreeNode Value1 => Children[0];
        public HlslTreeNode Value2 => Children[1];

        public override string Mnemonic => "min";
    }
}
