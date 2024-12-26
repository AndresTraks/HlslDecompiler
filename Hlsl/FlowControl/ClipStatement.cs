namespace HlslDecompiler.Hlsl.FlowControl
{
    public class ClipStatement : IStatement
    {
        public HlslTreeNode Value { get; }

        public ClipStatement(HlslTreeNode value, Closure closure)
        {
            Value = value;
            Closure = closure;
        }

        public Closure Closure { get; }
    }
}
