using HlslDecompiler.Hlsl.FlowControl;
using System.Collections.Generic;

namespace HlslDecompiler.Hlsl;

public class HlslAst
{
    public IList<IStatement> Statements { get; private set; }
    public RegisterState RegisterState { get; private set; }

    /// <summary>
    /// The values that are doubles, recorded by the parser as it made them. Neither
    /// the value nor the register it came out of can be asked afterwards - an
    /// addition of two doubles is the same node as an addition of two floats, and a
    /// register fxc reuses holds a double at one point and a float at another - so
    /// what knows is the instruction that wrote it, and this is what it knew.
    /// </summary>
    public ISet<HlslTreeNode> DoubleValues { get; private set; }

    public HlslAst(IList<IStatement> statements, RegisterState registerState,
        ISet<HlslTreeNode> doubleValues = null)
    {
        Statements = statements;
        RegisterState = registerState;
        DoubleValues = doubleValues ?? new HashSet<HlslTreeNode>();
    }
}
