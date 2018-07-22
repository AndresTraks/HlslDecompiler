namespace HlslDecompiler.Hlsl
{
    public class RegisterInputNode : HlslTreeNode, IHasComponentIndex
    {
        public RegisterInputNode(RegisterKey inputDecl, int componentIndex)
        {
            InputDecl = inputDecl;
            ComponentIndex = componentIndex;
        }

        public RegisterKey InputDecl { get; }
        public int ComponentIndex { get; }
        public int SamplerTextureDimension { get; set; }

        public override string ToString()
        {
            return $"{InputDecl}->{ComponentIndex}";
        }
    }
}
