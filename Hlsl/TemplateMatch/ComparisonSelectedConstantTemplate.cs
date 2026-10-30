using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Folds a comparison of a cmp between two constants into the cmp's own test:
/// `(f >= 0 ? 1 : -1) > 0` (vFace read as a sign, then if_lt) is `f >= 0`.
/// vFace is never 0, and is tested `> 0`: fxc compiles a branch on `vface >= 0`
/// as always taken.
/// </summary>
public class ComparisonSelectedConstantTemplate : NodeTemplate<ComparisonNode>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is ComparisonNode { IsInteger: false } comparison
            && Outcomes(comparison) is (bool ge, bool lt) && ge != lt;
    }

    public override HlslTreeNode Reduce(ComparisonNode node)
    {
        (bool ge, _) = Outcomes(node).Value;
        HlslTreeNode value = Inner(node.Left).Value;
        bool face = value is RegisterInputNode { RegisterComponentKey.RegisterKey: D3D9RegisterKey
            {
                Type: RegisterType.MiscType, Number: 1
            } };
        IfComparison comparison = ge
            ? (face ? IfComparison.GT : IfComparison.GE)
            : (face ? IfComparison.LE : IfComparison.LT);
        return new ComparisonNode(value, new ConstantNode(0f), comparison);
    }

    // Whether the comparison holds where the cmp took its >= 0 constant, and where
    // it took the other.
    private static (bool, bool)? Outcomes(ComparisonNode comparison)
    {
        CompareOperation inner = Inner(comparison.Left);
        if (inner == null
            || inner.GreaterEqualValue is not ConstantNode ge
            || inner.LessValue is not ConstantNode lt
            || comparison.Right is not ConstantNode right)
        {
            return null;
        }
        float sign = comparison.Left is NegateOperation ? -1 : 1;
        bool? geHolds = Holds(comparison.Comparison, sign * ge.Value, right.Value);
        bool? ltHolds = Holds(comparison.Comparison, sign * lt.Value, right.Value);
        return geHolds is bool g && ltHolds is bool l ? (g, l) : null;
    }

    private static bool? Holds(IfComparison comparison, float left, float right)
    {
        return comparison switch
        {
            IfComparison.GT => left > right,
            IfComparison.GE => left >= right,
            IfComparison.LT => left < right,
            IfComparison.LE => left <= right,
            IfComparison.EQ => left == right,
            IfComparison.NE => left != right,
            _ => null,
        };
    }

    private static CompareOperation Inner(HlslTreeNode value)
    {
        return value switch
        {
            CompareOperation compare => compare,
            NegateOperation { Value: CompareOperation compare } => compare,
            _ => null,
        };
    }
}
