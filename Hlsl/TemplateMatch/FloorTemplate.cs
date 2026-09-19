namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Puts floor back together.
///
/// Shader model 3 has no floor instruction, so fxc writes it as the number less
/// its fractional part: `frc r0, v0` and `add r0, -r0, v0`. Recovered as source
/// that reads `x - frac(x)`, which is what the shader does and not what it says -
/// and where the result is a subscript it costs two instructions, because fxc
/// floors a float subscript again on the way into the address register. Written
/// `floor(x)` it is the same value, the same text length, and fxc does it once.
/// </summary>
public class FloorTemplate : NodeTemplate<SubtractOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is SubtractOperation subtract && TryGetValue(subtract) != null;
    }

    public override HlslTreeNode Reduce(SubtractOperation node)
    {
        return new FloorOperation(TryGetValue(node));
    }

    private static HlslTreeNode TryGetValue(SubtractOperation subtract)
    {
        return subtract.Subtrahend is FractionalOperation fraction
            && ReferenceEquals(fraction.Value, subtract.Minuend)
            ? subtract.Minuend
            : null;
    }
}
