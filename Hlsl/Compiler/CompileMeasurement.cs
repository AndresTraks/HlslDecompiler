using System.Collections.Generic;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// What a trial compile of a statement tells the writer: each subexpression's text,
/// the nodes groupers absorbed, and the groups matched whole. Kept together so
/// write counts are never read without the grouped set.
/// </summary>
public class CompileMeasurement
{
    public List<(HlslTreeNode[] Nodes, string Text)> Recording { get; } = [];

    /// <summary>See <see cref="NodeCompiler.Grouped"/>.</summary>
    public HashSet<HlslTreeNode> Grouped { get; } = HlslTreeNode.NewNodeSet();

    /// <summary>See <see cref="NodeCompiler.GroupMatches"/>.</summary>
    public List<HlslTreeNode[]> GroupMatches { get; } = [];

    /// <summary>
    /// How many times each node was written; nodes a grouper absorbed (a
    /// normalize's length) are absent whatever their reader count.
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
