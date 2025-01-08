using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class TextureLoadOutputNode : HlslTreeNode, IHasComponentIndex
    {
        private int _numTextureCoordinates;

        public TextureLoadOutputNode(RegisterInputNode sampler, HlslTreeNode[] textureCoords, int componentIndex, bool isLod)
        {
            AddInput(sampler);
            foreach (HlslTreeNode textureCoord in textureCoords)
            {
                AddInput(textureCoord);
            }

            _numTextureCoordinates = textureCoords.Length;
            ComponentIndex = componentIndex;
            IsLod = isLod;
        }

        public TextureLoadOutputNode(RegisterInputNode sampler, HlslTreeNode[] textureCoords, int outputComponent, HlslTreeNode[] derivativeX, HlslTreeNode[] derivativeY)
            : this(sampler, textureCoords, outputComponent, false)
        {
            foreach (HlslTreeNode component in derivativeX)
            {
                AddInput(component);
            }
            foreach (HlslTreeNode component in derivativeY)
            {
                AddInput(component);
            }
            IsGrad = true;
        }

        public RegisterInputNode Sampler => (RegisterInputNode)Inputs[0];
        public IEnumerable<HlslTreeNode> TextureCoordinateInputs => Inputs.Skip(1).Take(_numTextureCoordinates);
        public IEnumerable<HlslTreeNode> DerivativeX => Inputs.Skip(1 + _numTextureCoordinates).Take(2);
        public IEnumerable<HlslTreeNode> DerivativeY => Inputs.Skip(3 + _numTextureCoordinates).Take(2);
        public int ComponentIndex { get; }
        public bool IsLod { get; }
        public bool IsGrad { get; }
    }
}
