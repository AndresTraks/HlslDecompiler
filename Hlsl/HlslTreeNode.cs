using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class HlslTreeNode
    {
        public IList<HlslTreeNode> Inputs { get; } = [];
        public IList<HlslTreeNode> Outputs { get; } = [];

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

        public void Remove()
        {
            foreach (var input in Inputs)
            {
                input.Outputs.Remove(this);
            }
            if (Outputs.Count != 0)
            {
                throw new NotImplementedException();
            }
        }

        public bool IsInputOf(IEnumerable<HlslTreeNode> nodes)
        {
            return nodes.Any(IsInputOf);
        }

        public bool IsInputOf(HlslTreeNode node)
        {
            return node == this || IsInputOf(node.Inputs);
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
