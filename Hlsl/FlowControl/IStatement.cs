using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

public interface IStatement
{
    public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

    /// <summary>
    /// Values the statement holds outside its maps (a store's address and values, a
    /// break's comparison, a counted loop's clauses). They are plain references, not
    /// graph edges, so graph walks and Replace do not reach them. No default: every
    /// statement must declare its slots; the members below derive from this.
    /// They are not anchor-node edges because an edge counts as a reader and would
    /// keep re-printed values (a loop's exit test) alive as dead assignments.
    /// </summary>
    public IEnumerable<HeldSlot> HeldSlots { get; }

    /// <summary>
    /// Everything held, whether or not a writer prints it.
    /// </summary>
    public IEnumerable<HlslTreeNode> HeldNodes => HeldSlots.SelectMany(slot => slot.Nodes);

    /// <summary>
    /// Held values printed by name: those <see cref="ReplaceHeldNode"/> redirects and
    /// whose assignments must stay alive. The rest are written out again. Predicates
    /// are not named: naming a mask goes through an int variable, and HLSL's
    /// conversion to 0 or 1 adds an `and` the bytecode did not have.
    /// </summary>
    public IEnumerable<HlslTreeNode> NamedHeldNodes =>
        HeldSlots.Where(slot => slot.IsNamed).SelectMany(slot => slot.Nodes);

    /// <summary>
    /// Replace for held references, which are not input edges.
    /// </summary>
    public void ReplaceHeldNode(HlslTreeNode node, HlslTreeNode replacement)
    {
        foreach (HeldSlot slot in HeldSlots)
        {
            slot.Replace(node, replacement);
        }
    }
}
