using System;
using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch;

public class CompareCompareTemplate : NodeTemplate<ComparisonNode>
{
    public override bool Match(HlslTreeNode node)
    {
        if (node is not ComparisonNode comparison)
        {
            return false;
        }
        if (comparison.Comparison != IfComparison.EQ && comparison.Comparison != IfComparison.NE)
        {
            return false;
        }
        return
            (comparison.Left is CompareOperation compareLeft
                && !ConstantMatcher.IsEqual(compareLeft.GreaterEqualValue, compareLeft.LessValue)
                && ConstantMatcher.IsAnyEqual(comparison.Right, compareLeft.GreaterEqualValue, compareLeft.LessValue)) ||
            (comparison.Right is CompareOperation compareRight
                && !ConstantMatcher.IsEqual(compareRight.GreaterEqualValue, compareRight.LessValue)
                && ConstantMatcher.IsAnyEqual(comparison.Left, compareRight.GreaterEqualValue, compareRight.LessValue));
    }

    public override HlslTreeNode Reduce(ComparisonNode comparison)
    {
        CompareOperation compare = comparison.Left as CompareOperation;
        HlslTreeNode compareTo;
        if (compare != null)
        {
            compareTo = comparison.Right;
        }
        else
        {
            compare = comparison.Right as CompareOperation;
            compareTo = comparison.Left;
        }

        IfComparison ifComparison;
        if ((comparison.Comparison == IfComparison.EQ && ConstantMatcher.IsEqual(compare.GreaterEqualValue, compareTo)) ||
            (comparison.Comparison == IfComparison.NE && ConstantMatcher.IsEqual(compare.LessValue, compareTo)))
        {
            ifComparison = IfComparison.GE;
        }
        else if ((comparison.Comparison == IfComparison.EQ && ConstantMatcher.IsEqual(compare.LessValue, compareTo)) ||
            (comparison.Comparison == IfComparison.NE && ConstantMatcher.IsEqual(compare.GreaterEqualValue, compareTo)))
        {
            ifComparison = IfComparison.LT;
        }
        else
        {
            throw new InvalidOperationException();
        }
        return new ComparisonNode(compare.Value, new ConstantNode(0), ifComparison);
    }
}
