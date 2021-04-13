namespace HlslDecompiler.Hlsl
{
    public class SubtractOperation : Operation
    {
        public SubtractOperation(HlslTreeNode minuend, HlslTreeNode subtrahend)
        {
            AddInput(minuend);
            AddInput(subtrahend);
        }

        public HlslTreeNode Minuend => Inputs[0];
        public HlslTreeNode Subtrahend => Inputs[1];

        public override string Mnemonic => "sub";
    }
}
