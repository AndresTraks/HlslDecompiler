namespace HlslDecompiler.Hlsl
{
    public class DotProductOperation : Operation
    {
        public DotProductOperation(GroupNode x, GroupNode y)
        {
            AddInput(x);
            AddInput(y);
        }

        public override string Mnemonic => "dot";

        public GroupNode X => Inputs[0] as GroupNode;
        public GroupNode Y => Inputs[1] as GroupNode;

        public override string ToString()
        {
            return $"dot({X}, {Y})";
        }
    }
}
