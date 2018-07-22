using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class DotProductOutputNode : HlslTreeNode, IHasComponentIndex
    {
        public DotProductOutputNode(IEnumerable<HlslTreeNode> inputs, int componentIndex)
        {
            foreach (HlslTreeNode input in inputs)
            {
                AddChild(input);
            }

            ComponentIndex = componentIndex;
        }

        public IEnumerable<HlslTreeNode> Inputs1 => Children.Take(ComponentCount);
        public IEnumerable<HlslTreeNode> Inputs2 => Children.Skip(ComponentCount);
        public int ComponentIndex { get; }
        public int ComponentCount => Children.Count / 2;
    }
}
