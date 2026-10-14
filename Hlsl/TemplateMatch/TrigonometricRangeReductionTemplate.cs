using System;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Removes the [-pi, pi) range reduction fxc puts before SM3 sincos
/// (`frac(x / 2pi + 0.5) * 2pi - pi`); fxc would otherwise reduce twice.
/// </summary>
public class TrigonometricRangeReductionTemplate : INodeTemplate
{
    private const float Pi = 3.14159274f;
    private const float TwoPi = 6.28318548f;
    private const float OverTwoPi = 0.159154937f;

    /// <summary>Tight: a hand-written reduction with another pi is left alone.</summary>
    private const float Tolerance = 1e-6f;

    public bool Match(HlslTreeNode node)
    {
        return TryGetAngle(node) != null;
    }

    public HlslTreeNode Reduce(HlslTreeNode node)
    {
        HlslTreeNode angle = TryGetAngle(node);
        return node is SineOperation
            ? new SineOperation(angle)
            : new CosineOperation(angle);
    }

    private static HlslTreeNode TryGetAngle(HlslTreeNode node)
    {
        if (node is not SineOperation and not CosineOperation)
        {
            return null;
        }

        HlslTreeNode scaled = TryRemoveAddend(node.Inputs[0], -Pi);
        if (scaled == null)
        {
            return null;
        }

        if (TryRemoveFactor(scaled, TwoPi) is not FractionalOperation fraction)
        {
            return null;
        }

        HlslTreeNode shifted = TryRemoveAddend(fraction.Value, 0.5f);
        if (shifted is not MultiplyOperation inner)
        {
            return null;
        }

        // 1/2pi, or a multiple where fxc folded a scale in: cos(x * 0.5) uses 0.5/2pi.
        ConstantNode scale = inner.Factor1 as ConstantNode ?? inner.Factor2 as ConstantNode;
        if (scale == null)
        {
            return null;
        }
        HlslTreeNode angle = ReferenceEquals(scale, inner.Factor1) ? inner.Factor2 : inner.Factor1;
        float factor = scale.Value / OverTwoPi;
        if (!float.IsFinite(factor) || factor == 0)
        {
            return null;
        }
        return Math.Abs(factor - 1) < Tolerance
            ? angle
            : new MultiplyOperation(new ConstantNode(factor), angle);
    }

    /// <returns>The other side of the sum, or null if this is not that sum.</returns>
    private static HlslTreeNode TryRemoveAddend(HlslTreeNode node, float addend)
    {
        if (node is AddOperation add)
        {
            if (IsConstant(add.Addend1, addend))
            {
                return add.Addend2;
            }
            if (IsConstant(add.Addend2, addend))
            {
                return add.Addend1;
            }
        }
        // The negative addend is as likely to have become a subtraction.
        if (node is SubtractOperation subtract && IsConstant(subtract.Subtrahend, -addend))
        {
            return subtract.Minuend;
        }
        return null;
    }

    /// <returns>The other factor, or null if this is not that product.</returns>
    private static HlslTreeNode TryRemoveFactor(HlslTreeNode node, float factor)
    {
        if (node is not MultiplyOperation multiply)
        {
            return null;
        }
        if (IsConstant(multiply.Factor1, factor))
        {
            return multiply.Factor2;
        }
        return IsConstant(multiply.Factor2, factor) ? multiply.Factor1 : null;
    }

    private static bool IsConstant(HlslTreeNode node, float value)
    {
        return node is ConstantNode constant && Math.Abs(constant.Value - value) < Tolerance;
    }
}
