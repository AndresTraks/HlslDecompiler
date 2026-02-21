using HlslDecompiler.Hlsl.FlowControl;
using System.Collections.Generic;

namespace HlslDecompiler.Hlsl;

public class HlslAst
{
    public IList<IStatement> Statements { get; private set; }
    public RegisterState RegisterState { get; private set; }

    public HlslAst(IList<IStatement> statements, RegisterState registerState)
    {
        Statements = statements;
        RegisterState = registerState;
    }
}
