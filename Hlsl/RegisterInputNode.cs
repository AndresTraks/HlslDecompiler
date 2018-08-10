namespace HlslDecompiler.Hlsl
{
    public class RegisterInputNode : HlslTreeNode, IHasComponentIndex
    {
        public RegisterInputNode(RegisterComponentKey registerComponentKey, int samplerTextureDimension = 0)
        {
            RegisterComponentKey = registerComponentKey;
            SamplerTextureDimension = samplerTextureDimension;
        }

        public RegisterComponentKey RegisterComponentKey { get; }
        public int SamplerTextureDimension { get; }

        public int ComponentIndex => RegisterComponentKey.ComponentIndex;

        public override string ToString()
        {
            return RegisterComponentKey.ToString();
        }
    }
}
