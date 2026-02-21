namespace HlslDecompiler.Hlsl.TemplateMatch;

public interface IGroupTemplate
{
    IGroupContext Match(HlslTreeNode node);
    HlslTreeNode Reduce(HlslTreeNode node, IGroupContext groupContext);
}
