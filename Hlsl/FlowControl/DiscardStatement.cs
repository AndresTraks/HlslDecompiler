using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

// discard_nz <condition>: the pixel is dropped when the condition holds. HLSL clip()
// is the narrower case of dropping it when a value is negative.
public class DiscardStatement : IStatement
{
    public HlslTreeNode Comparison { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

    public DiscardStatement(HlslTreeNode comparison, IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
    {
        Comparison = comparison;
        Inputs = inputs.ToDictionary();
        Outputs = inputs.ToDictionary();
    }

    /// <summary>A predicate is written out again rather than named: see <see
    /// cref="IStatement.NamedHeldNodes"/>.</summary>
    public IEnumerable<HeldSlot> HeldSlots => [HeldSlot.WrittenAgain(() => Comparison)];
}
