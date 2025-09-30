using System;
using System.Collections.Generic;

namespace HlslDecompiler.Hlsl.FlowControl
{
    public class NodeVisitor
    {
        private IList<HlslTreeNode> _nodes;

        public NodeVisitor(IList<HlslTreeNode> statements)
        {
            _nodes = statements;
        }

        public void Visit(Action<HlslTreeNode> action)
        {
            Visit(_nodes, action);
        }

        private static void Visit(IList<HlslTreeNode> nodes, Action<HlslTreeNode> action)
        {
            foreach (var node in nodes)
            {
                action(node);
                Visit(node.Inputs, action);
            }
        }
    }
}
