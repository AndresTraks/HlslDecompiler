namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// An itof or utof of a movc choosing between two integer constants is a choice
/// between the two floats: `(float2)(id & int2(1, 2) ? 1 : -1)` is
/// `id & int2(1, 2) ? 1.0 : -1.0`. fxc folds the conversion into the constants
/// when it compiles either, so the cast was the bytecode's order of operations
/// and not anything the shader said - and written with it, the recompile came
/// back without it.
///
/// Only for integers a float holds exactly, which is every one a shader picks
/// between this way; past 2^24 the conversion rounds, and is left to say so.
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
