using System;
using System.Collections.Generic;

namespace HlslDecompiler
{
    public class HlslTreeNode
    {
        public IList<HlslTreeNode> Children { get; } = new List<HlslTreeNode>();
        public IList<HlslTreeNode> Parents { get; } = new List<HlslTreeNode>();

        protected void AddChild(HlslTreeNode node)
        {
            Children.Add(node);
            node.Parents.Add(this);
            AssertLoopFree();
        }

        public virtual HlslTreeNode Reduce()
        {
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i] = Children[i].Reduce();
            }
            return this;
        }

        public void Replace(HlslTreeNode with)
        {
            foreach (var child in Children)
            {
                child.Parents.Remove(this);
            }
            foreach (var parent in Parents)
            {
                for (int i = 0; i < parent.Children.Count; i++)
                {
                    if (parent.Children[i] == this)
                    {
                        parent.Children[i] = with;
                    }
                }
                with.Parents.Add(parent);
            }
        }

        private void AssertLoopFree()
        {
            foreach (HlslTreeNode parent in Parents)
            {
                AssertLoopFree(parent);
                if (this == parent)
                {
                    throw new InvalidOperationException();
                }
            }
        }

        private void AssertLoopFree(HlslTreeNode parent)
        {
            foreach (HlslTreeNode upperParent in parent.Parents)
            {
                if (this == upperParent)
                {
                    throw new InvalidOperationException();
                }
            }
        }
    }
}
