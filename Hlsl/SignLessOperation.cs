namespace HlslDecompiler.Hlsl
{
    public class SignLessOperation : Operation
    {
        public SignLessOperation(HlslTreeNode value1, HlslTreeNode value2)
        {
            AddChild(value1);
            AddChild(value2);
        }

        public override string Mnemonic => "slt";
    }
}
