using HlslDecompiler.Hlsl.FlowControl;
using System.Collections.Generic;

namespace HlslDecompiler.Hlsl
{
    public class HlslAst
    {
        public List<IStatement> Statements { get; private set; }
        public RegisterState RegisterState { get; private set; }

        public HlslAst(List<IStatement> statements, RegisterState registerState)
        {
            Statements = statements;
            RegisterState = registerState;
        }
    }
}
