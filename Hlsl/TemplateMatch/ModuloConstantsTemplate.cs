namespace HlslDecompiler.Hlsl.TemplateMatch;

// The remainder of two integer constants is a constant. An instanced fork phase
// unrolled into a copy per run indexes the patch by `(id + 1) % 3`, and with the
// id settled each copy read `patch[1 % 3]`, `patch[3 % 3]`. udiv is unsigned, and
// so is the remainder taken here.
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
