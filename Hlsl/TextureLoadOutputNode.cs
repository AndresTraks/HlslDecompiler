using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class TextureLoadOutputNode : HlslTreeNode, IHasComponentIndex
    {
        public TextureLoadOutputNode(HlslTreeNode sampler, IEnumerable<HlslTreeNode> textureCoords, int componentIndex)
        {
            if (!(sampler is RegisterInputNode samplerInput))
            {
                throw new ArgumentException(nameof(sampler));
            }

            AddChild(sampler);
            foreach (HlslTreeNode textureCoord in textureCoords)
            {
                AddChild(textureCoord);
            }

            ComponentIndex = componentIndex;
        }

        public RegisterInputNode SamplerInput => (RegisterInputNode)Children[0];
        public IEnumerable<HlslTreeNode> TextureCoordinateInputs => Children.Skip(1);
        public int ComponentIndex { get; }
    }
}
