namespace HlslDecompiler.Hlsl
{
    public class RegisterInputNode : HlslTreeNode, IHasComponentIndex
    {
        public RegisterInputNode(RegisterComponentKey registerComponentKey, int componentIndex)
        {
            RegisterComponentKey = registerComponentKey;
            ComponentIndex = componentIndex;
        }

        public RegisterComponentKey RegisterComponentKey { get; }
        public int ComponentIndex { get; }
        public int SamplerTextureDimension { get; set; }

        public override string ToString()
        {
            return $"{RegisterComponentKey}->{ComponentIndex}";
        }
    }
}
