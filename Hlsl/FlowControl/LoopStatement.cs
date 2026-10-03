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

    /// <summary>
    /// The clauses of a counted loop and the trip count of one counted by a
    /// register - every one of them, whether or not the loop is counted and whether
    /// or not a writer will print it. None are named: the initializer and the
    /// increment are assignments already, so they are where a name comes from
    /// rather than somewhere one is read; the condition is a predicate, which pays
    /// an `and` for being named (see <see cref="IStatement.NamedHeldNodes"/>); and
    /// the trip count is read before the loop by the loop itself.
    /// </summary>
    public IEnumerable<HeldSlot> HeldSlots =>
    [
        HeldSlot.WrittenAgain(() => Initializer),
        HeldSlot.WrittenAgain(() => ContinueCondition),
        HeldSlot.WrittenAgain(() => Increment),
        HeldSlot.WrittenAgain(() => RepeatCountNode),
    ];
}
