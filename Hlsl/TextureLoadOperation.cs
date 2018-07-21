namespace HlslDecompiler.Hlsl
{
    public class TextureLoadOperation : HlslTreeNode
    {
        public TextureLoadOperation(HlslTreeNode textureCoordinates, HlslTreeNode sampler)
        {
            AddChild(textureCoordinates);
            AddChild(sampler);
        }

        public HlslTreeNode TextureCoordinates => Children[0];
        public HlslTreeNode Sampler => Children[1];
    }
}
