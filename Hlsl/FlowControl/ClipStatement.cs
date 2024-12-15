namespace HlslDecompiler.Hlsl.FlowControl
{
    public class ClipStatement : IStatement
    {
        public HlslTreeNode Value { get; }

        public ClipStatement(HlslTreeNode value)
        {
            Value = value;
        }
    }
}
