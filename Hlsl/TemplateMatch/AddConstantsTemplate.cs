namespace HlslDecompiler.Hlsl.TemplateMatch;

public class AddConstantsTemplate : NodeTemplate<AddOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is AddOperation add
            && ConstantMatcher.IsConstant(add.Addend1)
            && ConstantMatcher.IsConstant(add.Addend2);
    }

    public override HlslTreeNode Reduce(AddOperation node)
    {
        // Integers stay integers: later integer uses and folds need IntegerValue.
        if ((node.Addend1 as ConstantNode).IntegerValue is int a
            && (node.Addend2 as ConstantNode).IntegerValue is int b)
        {
            return new ConstantNode(unchecked(a + b));
        }
        var value = (node.Addend1 as ConstantNode).Value + (node.Addend2 as ConstantNode).Value;
        return new ConstantNode(value);
    }
}
