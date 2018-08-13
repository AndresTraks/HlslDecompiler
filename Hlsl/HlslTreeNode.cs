using System;
using System.Collections.Generic;

namespace HlslDecompiler
{
    public class HlslTreeNode
    {
        public IList<HlslTreeNode> Inputs { get; } = new List<HlslTreeNode>();
        public IList<HlslTreeNode> Outputs { get; } = new List<HlslTreeNode>();

        public virtual HlslTreeNode Reduce()
        {
            for (int i = 0; i < Inputs.Count; i++)
            {
                Inputs[i] = Inputs[i].Reduce();
            }
            return this;
        }

        public void Replace(HlslTreeNode with)
        {
            foreach (var input in Inputs)
            {
                input.Outputs.Remove(this);
            }
            foreach (var output in Outputs)
            {
                for (int i = 0; i < output.Inputs.Count; i++)
                {
                    if (output.Inputs[i] == this)
                    {
                        output.Inputs[i] = with;
                    }
                }
                with.Outputs.Add(output);
            }
        }

        protected void AddInput(HlslTreeNode node)
        {
            Inputs.Add(node);
            node.Outputs.Add(this);
            AssertLoopFree();
        }

        private void AssertLoopFree()
        {
            foreach (HlslTreeNode output in Outputs)
            {
                AssertLoopFree(output);
                if (this == output)
                {
                    throw new InvalidOperationException();
                }
            }
        }

        private void AssertLoopFree(HlslTreeNode parent)
        {
            foreach (HlslTreeNode upperParent in parent.Outputs)
            {
                if (this == upperParent)
                {
                    throw new InvalidOperationException();
                }
            }
        }
    }
}
