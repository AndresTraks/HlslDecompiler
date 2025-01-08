using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl
{
    public class RegisterInputNode : HlslTreeNode, IHasComponentIndex
    {
        public RegisterInputNode(RegisterComponentKey registerComponentKey)
        {
            RegisterComponentKey = registerComponentKey;
        }

        public RegisterComponentKey RegisterComponentKey { get; }

        public int ComponentIndex => RegisterComponentKey.ComponentIndex;

        public override string ToString()
        {
            return RegisterComponentKey.ToString();
        }
    }
}
