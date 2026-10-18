using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using System.Collections.Generic;

namespace HlslDecompiler.Hlsl;

public class HlslAst
{
    public IList<IStatement> Statements { get; private set; }
    public RegisterState RegisterState { get; private set; }

    /// <summary>
    /// Double values, recorded by the parser: a double add is the same node type as
    /// a float add, and registers are reused, so only the instruction knows.
    /// </summary>
    public ISet<HlslTreeNode> DoubleValues { get; private set; }

    /// <summary>
    /// Constant registers an fx_2_0 preshader writes, and their values; the
    /// instruction writer declares them.
    /// </summary>
    public IDictionary<RegisterComponentKey, HlslTreeNode> PreshaderOutputs { get; init; }
        = new Dictionary<RegisterComponentKey, HlslTreeNode>();

    /// <summary>
    /// Dynamic linkage method bodies, parsed apart from main. Null without linkage.
    /// </summary>
    public IList<(LinkageModel.FunctionBodyInfo Body, IList<IStatement> Statements)> LinkageBodies
    { get; init; }

    /// <summary>
    /// msad instructions whose windows the parser read as the four true sliding
    /// windows of one uint2. The instruction writer writes only those as a single
    /// msad4(); a packed or duplicated lane set becomes one call per component.
    /// </summary>
    public ISet<D3D10Instruction> StandardMsadWindows { get; init; }
        = new HashSet<D3D10Instruction>(ReferenceEqualityComparer.Instance);

    public HlslAst(IList<IStatement> statements, RegisterState registerState,
        ISet<HlslTreeNode> doubleValues = null)
    {
        Statements = statements;
        RegisterState = registerState;
        DoubleValues = doubleValues ?? new HashSet<HlslTreeNode>();
    }
}
