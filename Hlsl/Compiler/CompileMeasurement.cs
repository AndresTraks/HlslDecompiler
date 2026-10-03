using System.Collections.Generic;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// What compiling a statement and throwing the text away tells the writer: the text
/// of every subexpression it wrote, the nodes a grouper took the inside of, and the
/// component sets the groupers matched whole.
///
/// One object because they are one measurement. Asking for the text and leaving the
/// grouped set behind is how a name ended up inside a matrix multiply: the writer
/// had the counts that said a node was written twice and not the set that said the
/// node was a component of something written once.
/// </summary>
public class CompileMeasurement
{
    /// <summary>Every group the compile wrote, with the text it wrote for it.</summary>
    public List<(HlslTreeNode[] Nodes, string Text)> Recording { get; } = [];

    /// <summary>See <see cref="NodeCompiler.Grouped"/>.</summary>
    public HashSet<HlslTreeNode> Grouped { get; } = HlslTreeNode.NewNodeSet();

    /// <summary>See <see cref="NodeCompiler.GroupMatches"/>.</summary>
    public List<HlslTreeNode[]> GroupMatches { get; } = [];

    /// <summary>
    /// How many times the compile wrote each node. A node appears here when it is a
    /// component of a group the compiler wrote; one a grouper absorbed - a
    /// normalize's length, a matrix multiply's dot products - appears not at all,
    /// however many readers it has in the graph.
    /// </summary>
    public Dictionary<HlslTreeNode, int> WrittenCounts()
    {
        var counts = new Dictionary<HlslTreeNode, int>(ReferenceEqualityComparer.Instance);
        foreach ((HlslTreeNode[] nodes, _) in Recording)
        {
            foreach (HlslTreeNode node in nodes)
            {
                counts[node] = counts.TryGetValue(node, out int count) ? count + 1 : 1;
            }
        }
        return counts;
    }
}
