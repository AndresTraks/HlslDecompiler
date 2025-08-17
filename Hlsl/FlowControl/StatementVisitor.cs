using System;
using System.Collections.Generic;

namespace HlslDecompiler.Hlsl.FlowControl
{
    public class StatementVisitor
    {
        private IList<IStatement> _statements;

        public StatementVisitor(IList<IStatement> statements)
        {
            _statements = statements;
        }

        public void Visit(Action<IStatement> action)
        {
            Visit(_statements, action);
        }

        private static void Visit(IList<IStatement> statements, Action<IStatement> action)
        {
            foreach (var statement in statements)
            {
                action(statement);
                if (statement is IfStatement ifStatement)
                {
                    Visit(ifStatement.TrueBody, action);
                    if (ifStatement.FalseBody != null)
                    {
                        Visit(ifStatement.FalseBody, action);
                    }
                }
                else
                {
                    if (statement is LoopStatement loopStatement)
                    {
                        Visit(loopStatement.Body, action);
                    }
                }
            }
        }
    }
}
