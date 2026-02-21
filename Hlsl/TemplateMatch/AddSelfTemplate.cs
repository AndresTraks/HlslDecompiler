namespace HlslDecompiler.Hlsl.TemplateMatch;

public class AddSelfTemplate : NodeTemplate<AddOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is AddOperation add && add.Addend1 == add.Addend2;
    }

    public override HlslTreeNode Reduce(AddOperation node)
    {
        return new MultiplyOperation(new ConstantNode(2), node.Addend1);
    }
}
