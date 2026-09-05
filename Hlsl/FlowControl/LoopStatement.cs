using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

public class LoopStatement : IStatement
{
    // Null if unbounded
    public uint? RepeatCount { get; }

    // The trip count when it is only known at run time, as `loop aL, iN` has when
    // iN is a uniform rather than a defi.
    public HlslTreeNode RepeatCountNode { get; set; }

    // `loop aL, iN` counts in aL, which the body can index constants by. `rep` and
    // the DXBC loops have no such register.
    public bool HasLoopCounter { get; set; }
    public IList<IStatement> Body { get; } = [];
    public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

    public bool IsParsed { get; set; } = false;

    public TempAssignmentNode Initializer { get; set; }
    public HlslTreeNode ContinueCondition { get; set; }
    public TempAssignmentNode Increment { get; set; }

    public bool IsCountedLoop => Initializer != null;

    public LoopStatement(uint? repeatCount, IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
    {
        RepeatCount = repeatCount;
        Inputs = inputs.ToDictionary();
        Outputs = inputs.ToDictionary();
    }
}
