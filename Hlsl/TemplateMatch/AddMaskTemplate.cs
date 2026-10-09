namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// A comparison mask added to an integer is a count of the times the test held:
/// the mask is -1 where it did, so `x - mask` is one more and `x + mask` one less.
/// That is how fxc compiles `c ? n + 1 : n` - `iadd r, -mask, r` - and read back
/// as arithmetic it was `n - (c ? -1 : 0)`. Only an integer adds a mask; a float
/// add of one would add the bits of a NaN.
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
