using HlslDecompiler.DirectXShaderModel;
using System;
using System.Linq;
using System.Reflection.Metadata;

namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public static class ConstantMatcher
    {
        public static bool IsConstant(HlslTreeNode node)
        {
            return node is ConstantNode;
        }

        public static bool IsZero(HlslTreeNode node)
        {
            return node is ConstantNode constant && constant.Value == 0;
        }

        public static bool IsOne(HlslTreeNode node)
        {
            return node is ConstantNode constant && constant.Value == 1;
        }

        public static bool IsNegativeOne(HlslTreeNode node)
        {
            return node is ConstantNode constant && constant.Value == -1;
        }

        public static bool IsNegative(HlslTreeNode node)
        {
            return node is ConstantNode constant && constant.Value < 0;
        }

        public static bool? TryEvaluateComparison(HlslTreeNode node)
        {
            if (node is GroupNode group)
            {
                var firstValue = TryEvaluateComparison(group.Inputs.First());
                if (firstValue == null)
                {
                    return null;
                }
                if (group.Inputs.All(i => TryEvaluateComparison(i) == firstValue))
                {
                    return firstValue;
                }
            }
            else if (node is ComparisonNode comparison)
            {
                int? left = TryEvaluateValue(comparison.Left);
                if (left.HasValue)
                {
                    int? right = TryEvaluateValue(comparison.Right);
                    if (right.HasValue)
                    {
                        switch (comparison.Comparison)
                        {
                            case IfComparison.GT: return left > right;
                            case IfComparison.EQ: return left == right;
                            case IfComparison.GE: return left >= right;
                            case IfComparison.LT: return left < right;
                            case IfComparison.NE: return left != right;
                            case IfComparison.LE: return left <= right;
                        }
                    }
                }
            }
            return null;
        }

        public static int? TryEvaluateValue(HlslTreeNode node)
        {
            if (IsOne(node))
            {
                return 1;
            }
            if (IsNegativeOne(node))
            {
                return -1;
            }
            else if (node is NegateOperation negate)
            {
                var negatedValue = TryEvaluateValue(negate.Value);
                if (negatedValue.HasValue)
                {
                    return -negatedValue.Value;
                }
            }
            return null;
        }
    }
}
