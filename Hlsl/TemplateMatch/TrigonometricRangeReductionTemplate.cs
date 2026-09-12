using System;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Removes the range reduction in front of sin and cos.
///
/// Shader model 3 has one sincos instruction and it wants its angle in
/// [-pi, pi), so fxc puts a reduction in front of it: frac of the angle over two
/// pi, shifted by a half, scaled back out. Both functions have period two pi, so
/// that is the angle again - and leaving it in the output means fxc applies its
/// own reduction on top of the one already written, which is five instructions
/// for nothing.
/// </summary>
public class TrigonometricRangeReductionTemplate : INodeTemplate
{
    private const float Pi = 3.14159274f;
    private const float TwoPi = 6.28318548f;
    private const float OverTwoPi = 0.159154937f;

    /// <summary>
    /// The constants are the ones fxc writes, so they are compared as written
    /// rather than recomputed - a reduction built by hand with a rounder pi is not
    /// this pattern and is left alone.
    /// </summary>
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
        return shifted == null ? null : TryRemoveFactor(shifted, OverTwoPi);
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
