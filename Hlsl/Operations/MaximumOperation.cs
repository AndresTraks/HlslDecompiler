namespace HlslDecompiler.Hlsl
{
    public class MaximumOperation : Operation
    {
        public MaximumOperation(HlslTreeNode value1, HlslTreeNode value2)
        {
            AddChild(value1);
            AddChild(value2);
        }

        public HlslTreeNode Value1 => Children[0];
        public HlslTreeNode Value2 => Children[1];

        public override string Mnemonic => "max";
    }
}
