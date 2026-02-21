namespace HlslDecompiler.Hlsl.TemplateMatch;

public class AddNegativeTemplate : NodeTemplate<AddOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is AddOperation add &&
            ((ConstantMatcher.IsNegative(add.Addend1) && !ConstantMatcher.IsNegative(add.Addend2)) ||
            (!ConstantMatcher.IsNegative(add.Addend1) && ConstantMatcher.IsNegative(add.Addend2)));
    }

    public override HlslTreeNode Reduce(AddOperation node)
    {
        if (ConstantMatcher.IsNegative(node.Addend1))
        {
            return new SubtractOperation(node.Addend2, new ConstantNode(-(node.Addend1 as ConstantNode).Value));
        }
        return new SubtractOperation(node.Addend1, new ConstantNode(-(node.Addend2 as ConstantNode).Value));
    }
}
