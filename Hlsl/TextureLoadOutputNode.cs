using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class TextureLoadOutputNode : HlslTreeNode, IHasComponentIndex
    {
        private int _numTextureCoordinates;

        private TextureLoadOutputNode(RegisterInputNode sampler, HlslTreeNode[] textureCoords, int componentIndex)
        {
            AddInput(sampler);
            foreach (HlslTreeNode textureCoord in textureCoords)
            {
                AddInput(textureCoord);
            }
            _numTextureCoordinates = textureCoords.Length;
            ComponentIndex = componentIndex;
        }

        public static TextureLoadOutputNode Create(RegisterInputNode sampler, HlslTreeNode[] textureCoords, int componentIndex)
        {
            return new TextureLoadOutputNode(sampler, textureCoords, componentIndex);
        }

        public static TextureLoadOutputNode CreateLod(RegisterInputNode sampler, HlslTreeNode[] textureCoords, int componentIndex)
        {
            return new TextureLoadOutputNode(sampler, textureCoords, componentIndex)
            {
                Controls = TextureLoadControls.Lod
            };
        }

        public static TextureLoadOutputNode CreateProj(RegisterInputNode sampler, HlslTreeNode[] textureCoords, int componentIndex)
        {
            return new TextureLoadOutputNode(sampler, textureCoords, componentIndex)
            {
                Controls = TextureLoadControls.Project
            };
        }

        public static TextureLoadOutputNode CreateGrad(RegisterInputNode sampler, HlslTreeNode[] textureCoords, int componentIndex,
            HlslTreeNode[] derivativeX, HlslTreeNode[] derivativeY)
        {
            var node = new TextureLoadOutputNode(sampler, textureCoords, componentIndex)
            {
                Controls = TextureLoadControls.Grad
            };
            foreach (HlslTreeNode component in derivativeX)
            {
                node.AddInput(component);
            }
            foreach (HlslTreeNode component in derivativeY)
            {
                node.AddInput(component);
            }
            return node;
        }

        public RegisterInputNode Sampler => (RegisterInputNode)Inputs[0];
        public IEnumerable<HlslTreeNode> TextureCoordinateInputs => Inputs.Skip(1).Take(_numTextureCoordinates);
        public IEnumerable<HlslTreeNode> DerivativeX => Inputs.Skip(1 + _numTextureCoordinates).Take(_numTextureCoordinates);
        public IEnumerable<HlslTreeNode> DerivativeY => Inputs.Skip(1 + 2 * _numTextureCoordinates).Take(_numTextureCoordinates);
        public int ComponentIndex { get; }
        public TextureLoadControls Controls { get; private set; }
    }

    [Flags]
    public enum TextureLoadControls
    {
        None = 0,
        Lod = 1,
        Grad = 2,
        Project = 4
    }
}
