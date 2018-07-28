﻿using System.Collections.Generic;

namespace HlslDecompiler.Hlsl
{
    public class NormalizeOutputNode : HlslTreeNode, IHasComponentIndex
    {
        public NormalizeOutputNode(IEnumerable<HlslTreeNode> inputs, int componentIndex)
        {
            foreach (HlslTreeNode input in inputs)
            {
                AddChild(input);
            }

            ComponentIndex = componentIndex;
        }

        public int ComponentIndex { get; }
    }
}
