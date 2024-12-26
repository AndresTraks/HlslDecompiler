using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.FlowControl
{
    public class IfStatement : IStatement
    {
        public IfStatement(GroupNode left, GroupNode right, IfComparison comparison, Closure closure)
        {
            Left = left;
            Right = right;
            Comparison = comparison;
            Closure = closure;
            
            TrueBody = new StatementSequence(closure);
        }

        public Closure Closure { get; }
        public GroupNode Left { get; }
        public GroupNode Right { get; }
        public IfComparison Comparison { get; }
        
        public StatementSequence TrueBody { get; }
        public Closure EndClosure { get; set; }
    }
}
