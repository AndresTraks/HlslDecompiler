using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class TextureLoadOutputNode : HlslTreeNode, IHasComponentIndex
    {
        public TextureLoadOutputNode(IEnumerable<HlslTreeNode> inputNodes, int componentIndex)
        {
            foreach (HlslTreeNode inputNode in inputNodes)
            {
                AddChild(inputNode);
            }

            ComponentIndex = componentIndex;
        }

        public IEnumerable<HlslTreeNode> TextureCoordinateInputs => Children.Where(c => !IsSamplerInput(c));
        public RegisterInputNode SamplerInput => (RegisterInputNode)Children.Single(IsSamplerInput);
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
