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

    // Tracks the nodes on the current path rather than every node seen, so shared
    // subexpressions are still visited once per path as before. Only a genuine
    // cycle is cut.
    private static void Visit(IEnumerable<HlslTreeNode> nodes, Action<HlslTreeNode> action, HashSet<HlslTreeNode> onPath)
    {
        foreach (var node in nodes)
        {
            if (!onPath.Add(node))
            {
                continue;
            }
            action(node);
            Visit(HlslTreeNode.TraversableInputs(node), action, onPath);
            onPath.Remove(node);
        }
    }
}
