namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// `&amp;`, `|` and `^` of two integer constants, as an unrolled hull phase leaves
/// them (`(3 + 1) &amp; 3`).
/// </summary>
public class BitwiseConstantsTemplate : INodeTemplate
{
    public bool Match(HlslTreeNode node)
    {
        return node is BitwiseAndOperation or BitwiseOrOperation or BitwiseXorOperation
            && node.Inputs.Count == 2
            && node.Inputs[0] is ConstantNode { IntegerValue: not null }
            && node.Inputs[1] is ConstantNode { IntegerValue: not null };
    }

    public HlslTreeNode Reduce(HlslTreeNode node)
    {
        int a = ((ConstantNode)node.Inputs[0]).IntegerValue.Value;
        int b = ((ConstantNode)node.Inputs[1]).IntegerValue.Value;
        return new ConstantNode(node switch
        {
            BitwiseAndOperation => a & b,
            BitwiseOrOperation => a | b,
            _ => a ^ b,
        });
    }
}
