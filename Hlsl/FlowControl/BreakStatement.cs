using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.FlowControl
{
    public class BreakStatement : IStatement
    {
        public BreakStatement(HlslTreeNode left, HlslTreeNode right, IfComparison comparison, Closure closure)
        {
            Left = left;
            Right = right;
            Comparison = comparison;
            Closure = closure;
        }

        public HlslTreeNode Left { get; }
        public HlslTreeNode Right { get; }
        public IfComparison Comparison { get; }
        public Closure Closure { get; }
    }
}
