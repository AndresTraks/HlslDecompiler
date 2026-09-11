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

    // Once per node, not once per path. The only visitor swaps a constant onto the
    // right hand side, which stops applying the moment it has, so seeing a node again
    // through another path changes nothing - and a subexpression read in two places
    // is reached twice as often at every level above it. Sixteen instructions built
    // on each other came to twenty-five million visits.
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
