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
        // Two integers add up to an integer, and stay one: a sum folded into a float
        // lost the IntegerValue every integer use of it asks for - the index of a
        // control point, a shift - and whatever folds after it stopped folding.
        if ((node.Addend1 as ConstantNode).IntegerValue is int a
            && (node.Addend2 as ConstantNode).IntegerValue is int b)
        {
            return new ConstantNode(unchecked(a + b));
        }
        var value = (node.Addend1 as ConstantNode).Value + (node.Addend2 as ConstantNode).Value;
        return new ConstantNode(value);
    }
}
