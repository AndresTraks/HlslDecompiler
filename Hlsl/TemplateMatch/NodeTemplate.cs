namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public abstract class NodeTemplate<T> : INodeTemplate where T : HlslTreeNode
    {
        public virtual bool Match(HlslTreeNode node)
        {
            return node is T;
        }

        public abstract HlslTreeNode Reduce(T node);

        public HlslTreeNode Reduce(HlslTreeNode node)
        {
            return Reduce(node as T);
        }
    }
}
