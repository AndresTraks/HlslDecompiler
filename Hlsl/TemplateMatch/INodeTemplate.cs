namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public interface INodeTemplate
    {
        bool Match(HlslTreeNode node);
        HlslTreeNode Reduce(HlslTreeNode node);
    }
}
