using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class TextureLoadOutputNode : HlslTreeNode, IHasComponentIndex
    {
        public TextureLoadOutputNode(RegisterInputNode sampler, IEnumerable<HlslTreeNode> textureCoords, int componentIndex)
        {
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
