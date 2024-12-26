using System.Collections.Generic;

namespace HlslDecompiler.Hlsl.FlowControl
{
    public class StatementSequence : IStatement
    {
        public StatementSequence(Closure closure)
        {
            Closure = closure;
        }

        public List<IStatement> Statements { get; } = new List<IStatement>();
        public Closure Closure { get; }
    }
}
