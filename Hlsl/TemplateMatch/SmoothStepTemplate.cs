using System.Collections.Generic;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Puts smoothstep back together.
///
/// fxc writes it as the position between the edges, clamped, and then the
/// polynomial that eases it: t * t * (3 - 2t), where t is
/// saturate((x - edge0) / (edge1 - edge0)). Three factors and a subtraction of
/// the same t, which is a shape nothing else arrives at by accident - the edge
/// has to be the same node in both the numerator and the denominator for the
/// algebra to be smoothstep at all, and that is what makes this safe to match.
/// </summary>
public class SmoothStepTemplate : NodeTemplate<MultiplyOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is MultiplyOperation multiply && TryReduce(multiply) != null;
    }

    public override HlslTreeNode Reduce(MultiplyOperation node)
    {
        return TryReduce(node);
    }

    private static HlslTreeNode TryReduce(MultiplyOperation multiply)
    {
        List<HlslTreeNode> factors = [];
        Flatten(multiply, factors);
        if (factors.Count != 3)
        {
            return null;
        }

        // Two of the three are the position itself and the third is the easing.
        for (int easing = 0; easing < 3; easing++)
        {
            HlslTreeNode first = factors[(easing + 1) % 3];
            HlslTreeNode second = factors[(easing + 2) % 3];
            if (!ReferenceEquals(first, second) || first is not SaturateOperation position)
            {
                continue;
            }
            if (!IsEasing(factors[easing], first))
            {
                continue;
            }
            return TryGetSmoothStep(position);
        }
        return null;
    }

    private static void Flatten(HlslTreeNode node, List<HlslTreeNode> factors)
    {
        if (node is MultiplyOperation multiply && factors.Count < 3)
        {
            Flatten(multiply.Factor1, factors);
            Flatten(multiply.Factor2, factors);
            return;
        }
        factors.Add(node);
    }

    /// <summary>3 - 2t, however it was folded.</summary>
    private static bool IsEasing(HlslTreeNode node, HlslTreeNode position)
    {
        if (node is AddOperation add)
        {
            return (IsThree(add.Addend1) && IsTwiceNegated(add.Addend2, position))
                || (IsThree(add.Addend2) && IsTwiceNegated(add.Addend1, position));
        }
        return node is SubtractOperation subtract
            && IsThree(subtract.Minuend)
            && IsTwice(subtract.Subtrahend, position);
    }

    private static bool IsThree(HlslTreeNode node)
    {
        return node is ConstantNode constant && constant.Value == 3;
    }

    private static bool IsTwiceNegated(HlslTreeNode node, HlslTreeNode position)
    {
        return IsProduct(node, -2, position)
            || (node is NegateOperation negate && IsTwice(negate.Value, position));
    }

    private static bool IsTwice(HlslTreeNode node, HlslTreeNode position)
    {
        return IsProduct(node, 2, position);
    }

    private static bool IsProduct(HlslTreeNode node, float factor, HlslTreeNode position)
    {
        if (node is not MultiplyOperation multiply)
        {
            return false;
        }
        return (IsConstant(multiply.Factor1, factor) && ReferenceEquals(multiply.Factor2, position))
            || (IsConstant(multiply.Factor2, factor) && ReferenceEquals(multiply.Factor1, position));
    }

    private static bool IsConstant(HlslTreeNode node, float value)
    {
        return node is ConstantNode constant && constant.Value == value;
    }

    /// <summary>
    /// The edges, from the position between them. edge0 has to be the same node in
    /// `x - edge0` and `edge1 - edge0`, or this is a different expression that
    /// happens to be cubed.
    /// </summary>
    private static HlslTreeNode TryGetSmoothStep(SaturateOperation position)
    {
        if (position.Value is not DivisionOperation division
            || division.Dividend is not SubtractOperation numerator
            || division.Divisor is not SubtractOperation denominator)
        {
            return null;
        }

        if (!ReferenceEquals(numerator.Subtrahend, denominator.Subtrahend))
        {
            return null;
        }

        return new SmoothStepOperation(
            numerator.Subtrahend, denominator.Minuend, numerator.Minuend);
    }
}
