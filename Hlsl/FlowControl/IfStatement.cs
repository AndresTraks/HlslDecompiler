﻿namespace HlslDecompiler.Hlsl.FlowControl
{
    public class IfStatement : IStatement
    {
        public IfStatement(HlslTreeNode comparison, Closure closure)
        {
            Comparison = comparison;
            Closure = closure;
            
            TrueBody = new StatementSequence(closure);
        }

        public Closure Closure { get; }
        public HlslTreeNode Comparison { get; }
        
        public StatementSequence TrueBody { get; }
        public StatementSequence FalseBody { get; set; }
        public Closure TrueEndClosure { get; set; }
        public Closure FalseEndClosure { get; set; }
    }
}
