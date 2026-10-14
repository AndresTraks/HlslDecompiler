namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// `n - mask` on integers is `c ? n + 1 : n` (the mask is -1 where c holds), as
/// fxc compiles it. Integers only.
/// </summary>
public class AddMaskTemplate : NodeTemplate<Operation>
{
    public override bool Match(HlslTreeNode node)
    {
        return Parts(node) != null;
    }

    public override HlslTreeNode Reduce(Operation node)
    {
        (HlslTreeNode value, ComparisonNode mask, int step) = Parts(node).Value;
        return new MoveConditionalOperation(mask, new AddOperation(value, new ConstantNode(step)), value);
    }

    // The value counted, the mask, and what the mask being set adds to it.
    private static (HlslTreeNode, ComparisonNode, int)? Parts(HlslTreeNode node)
    {
        switch (node)
        {
            case SubtractOperation { Minuend: not ComparisonNode, Subtrahend: ComparisonNode mask } subtract:
                return (subtract.Minuend, mask, 1);
            case AddOperation add:
                for (int i = 0; i < 2; i++)
                {
                    HlslTreeNode value = add.Inputs[1 - i];
                    if (value is ComparisonNode or NegateOperation { Value: ComparisonNode })
                    {
                        continue;
                    }
                    switch (add.Inputs[i])
                    {
                        case NegateOperation { Value: ComparisonNode negated }:
                            return (value, negated, 1);
                        case ComparisonNode mask:
                            return (value, mask, -1);
                    }
                }
                return null;
            default:
                return null;
        }
    }
}
