namespace HlslDecompiler.Hlsl.TemplateMatch;

public class ReciprocalSquareRootTemplate : NodeTemplate<ReciprocalSquareRootOperation>
{
    public override HlslTreeNode Reduce(ReciprocalSquareRootOperation node)
    {
        return new DivisionOperation(new ConstantNode(1), new SquareRootOperation(node.Value));
    }
}
