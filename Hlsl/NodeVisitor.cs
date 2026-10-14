using System;
using System.Collections.Generic;

namespace HlslDecompiler.Hlsl.FlowControl;

public class NodeVisitor
{
    private IList<HlslTreeNode> _nodes;

    public NodeVisitor(IList<HlslTreeNode> statements)
    {
        _nodes = statements;
    }

    public void Visit(Action<HlslTreeNode> action)
    {
        Visit(_nodes, action, HlslTreeNode.NewNodeSet());
    }

    // Once per node, not per path: per-path visits grow exponentially with shared
    // subexpressions.
    private static void Visit(IEnumerable<HlslTreeNode> nodes, Action<HlslTreeNode> action, HashSet<HlslTreeNode> seen)
    {
        foreach (var node in nodes)
        {
            if (!seen.Add(node))
            {
                continue;
            }
            action(node);
            Visit(HlslTreeNode.TraversableInputs(node), action, seen);
        }
    }
}
