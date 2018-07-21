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
        public IEnumerable<HlslTreeNode> TextureCoordinateInputs => Children.Where(c => !IsSamplerInput(c));
        public int ComponentIndex { get; }

        private static bool IsSamplerInput(HlslTreeNode node)
        {
            if (node is RegisterInputNode registerInput)
            {
                return registerInput.InputDecl.RegisterType == RegisterType.Sampler;
            }
            return false;
        }
    }
}
