using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

public interface IStatement
{
    public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

    /// <summary>
    /// Where this statement holds values of its own, outside its input and output
    /// maps: the address and the values of a store, the values of a clip, the
    /// comparison a break tests, the clauses of a counted loop. They are plain
    /// references and not edges in the value graph - nothing in a held node's
    /// Outputs points back at the statement - so a walk or a rewrite of the graph
    /// does not reach them, and everything that needs to reach them comes here.
    ///
    /// This is the only one of these members a statement writes. The three below
    /// are derived from it, so that a slot cannot be reported to one caller and
    /// missed by another: the answer used to be open-coded as a switch over the
    /// statement types in seven places across the resolver, the finalizer and the
    /// AST writer, kept in sync by hand, and three of them had fallen out of sync -
    /// which is what cbadf26 fixed, one site at a time.
    ///
    /// There is deliberately no default: a new statement type does not compile
    /// until it says where it holds things, and a new slot on an existing one is
    /// one line in one place.
    ///
    /// The other way round was tried: hold these as ordinary input edges of an
    /// anchor node the statement owns, so that HlslTreeNode.Replace reaches them
    /// and nothing has to ask at all. It does not work, because an edge is a
    /// consumer and half of these slots do not hold one. A loop's exit comparison
    /// is written in the `for` clause the loop prints and nowhere else; given an
    /// edge it had a reader, survived as a live assignment, and came back as a dead
    /// `int t2 = t1 &gt;= n;` on the first line of every loop body. 137 of the text
    /// fixtures moved that way. Paying for it would mean teaching every place that
    /// reads a node's Outputs to discount the edges a statement re-prints - the
    /// IsNamed distinction again, in the one place it is hardest to make - so the
    /// knowledge lives here and the graph stays as it was.
    /// </summary>
    public IEnumerable<HeldSlot> HeldSlots { get; }

    /// <summary>
    /// Everything this statement holds outside its maps, complete and
    /// unconditional: what it holds, not what it will decide to print. A writer
    /// that prints only some of them keeps that decision where it is made.
    /// </summary>
    public IEnumerable<HlslTreeNode> HeldNodes => HeldSlots.SelectMany(slot => slot.Nodes);

    /// <summary>
    /// The held values this statement prints by whatever name they end up under,
    /// which is the subset <see cref="ReplaceHeldNode"/> can redirect - and so also
    /// the subset whose assignments have to stay alive, because the statement reads
    /// them there.
    ///
    /// The others are the ones a statement writes out again rather than naming: a
    /// branch's comparison, a switch's selector and labels, the clauses of a
    /// counted loop. They are still held, and the walks over <see cref="HeldNodes"/>
    /// still have to reach them; they simply do not need a name.
    ///
    /// A predicate is the interesting one, and it is not an accident of how the
    /// writers grew. A mask named through a variable is named through an integer
    /// one - the variable it shares with whatever integers the branch body goes on
    /// to put there - and HLSL converts a mask to 0 or 1 on the way into an int,
    /// which is an `and` the bytecode did not have. Measured by naming them:
    /// cs_5_0/tile_luminance went from 47 instructions to 48 and groupshared_scan
    /// from 52 to 53, each for an `if (t5)` reading the comparison a line above
    /// instead of testing it again.
    /// </summary>
    public IEnumerable<HlslTreeNode> NamedHeldNodes =>
        HeldSlots.Where(slot => slot.IsNamed).SelectMany(slot => slot.Nodes);

    /// <summary>
    /// Rewrites every named reference to one node so that it names another, which
    /// is what a held reference needs in place of the graph's own Replace: that one
    /// rewrites input edges, and a held reference is not one.
    /// </summary>
    public void ReplaceHeldNode(HlslTreeNode node, HlslTreeNode replacement)
    {
        foreach (HeldSlot slot in HeldSlots)
        {
            slot.Replace(node, replacement);
        }
    }
}
