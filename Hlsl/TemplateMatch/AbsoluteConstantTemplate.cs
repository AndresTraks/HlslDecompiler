namespace HlslDecompiler.Hlsl.TemplateMatch;

// The magnitude of a constant is a constant. fxc folds a sign it can prove
// into an abs of one once it unrolls a loop over sign(), and read back that
// was `abs(0)` and `abs(-1)` (loop_counter_reuse, unrolled).
public class AbsoluteConstantTemplate : NodeTemplate<AbsoluteOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is AbsoluteOperation { Value: ConstantNode };
    }

    public override HlslTreeNode Reduce(AbsoluteOperation node)
    {
        var constant = (ConstantNode)node.Value;
        return constant.Value < 0 || constant.IntegerValue < 0 ? constant.Negated() : constant;
    }
}
