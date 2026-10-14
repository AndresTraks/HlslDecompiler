namespace HlslDecompiler.Hlsl.TemplateMatch;

// Folds `%` of two integer constants (e.g. unrolled fork phases' `patch[3 % 3]`).
// Unsigned, as udiv is.
public class ModuloConstantsTemplate : NodeTemplate<ModuloOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is ModuloOperation
            {
                Dividend: ConstantNode { IntegerValue: not null },
                Divisor: ConstantNode { IntegerValue: not null and not 0 },
            };
    }

    public override HlslTreeNode Reduce(ModuloOperation node)
    {
        uint dividend = (uint)((ConstantNode)node.Dividend).IntegerValue.Value;
        uint divisor = (uint)((ConstantNode)node.Divisor).IntegerValue.Value;
        return new ConstantNode((int)(dividend % divisor));
    }
}
