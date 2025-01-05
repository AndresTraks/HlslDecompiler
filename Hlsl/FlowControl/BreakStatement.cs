namespace HlslDecompiler.Hlsl.FlowControl
{
    public class BreakStatement : IStatement
    {
        public BreakStatement(HlslTreeNode comparison, Closure closure)
        {
            Comparison = comparison;
            Closure = closure;
        }

        public HlslTreeNode Comparison { get; }
        public Closure Closure { get; }
    }
}
