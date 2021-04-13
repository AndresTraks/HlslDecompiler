namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class MoveTemplate : NodeTemplate<MoveOperation>
    {
        public override HlslTreeNode Reduce(MoveOperation node)
        {
            return node.Value;
        }
    }
}
