using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class TextureLoadOutputNode : HlslTreeNode, IHasComponentIndex
    {
        public TextureLoadOutputNode(RegisterInputNode sampler, IEnumerable<HlslTreeNode> textureCoords, int componentIndex)
        {
            AddInput(sampler);
            foreach (HlslTreeNode textureCoord in textureCoords)
            {
                AddInput(textureCoord);
            }

            ComponentIndex = componentIndex;
        }

        public RegisterInputNode SamplerInput => (RegisterInputNode)Inputs[0];
        public IEnumerable<HlslTreeNode> TextureCoordinateInputs => Inputs.Skip(1);
        public int ComponentIndex { get; }
    }
}
