namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Folds itof/utof of a movc between integer constants into a choice between
/// floats: `(float)(c ? 1 : -1)` is `c ? 1.0 : -1.0`. Only for exactly
/// representable integers (below 2^24).
/// </summary>
public class ConvertSelectedConstantTemplate : NodeTemplate<ConvertOperation>
{
    private const long ExactInFloat = 1 << 24;

    public override bool Match(HlslTreeNode node)
    {
        return node is ConvertOperation
        {
            TargetType: "float",
            SourceUnsigned: bool unsigned,
            Value: MoveConditionalOperation { Source1: ConstantNode source1, Source2: ConstantNode source2 }
        }
            && AsFloat(source1, unsigned) != null && AsFloat(source2, unsigned) != null;
    }

    public override HlslTreeNode Reduce(ConvertOperation node)
    {
        var select = (MoveConditionalOperation)node.Value;
        bool unsigned = node.SourceUnsigned.Value;
        return new MoveConditionalOperation(select.Condition,
            new ConstantNode(AsFloat((ConstantNode)select.Source1, unsigned).Value),
            new ConstantNode(AsFloat((ConstantNode)select.Source2, unsigned).Value));
    }

    private static float? AsFloat(ConstantNode constant, bool unsigned)
    {
        if (constant.IntegerValue is not int integer)
        {
            return null;
        }
        long value = unsigned ? (uint)integer : integer;
        return value > -ExactInFloat && value < ExactInFloat ? value : null;
    }
}
