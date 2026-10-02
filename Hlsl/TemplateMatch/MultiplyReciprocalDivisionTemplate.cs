using System.Linq;

namespace HlslDecompiler.Hlsl.TemplateMatch;

// a * (1 / b) is a / b, whichever side the reciprocal is on: `mul r0, r0, r2.x`
// with r2.x an rsq is the normalize it came from only once it is a division.
//
// An rcp is a reciprocal too, and a multiply by one is the division the source
// wrote: shader model 3 has no divide instruction, so `z / w` is an rcp and a mul,
// and `texcoord.z * rcp(texcoord.w)` is a perspective divide spelled the only way
// the profile allows.
//
// Only where every reader of the rcp is a multiply, though. One fxc computes once
// and multiplies by in several places is still the division the source wrote at
// each of them - three components of a colour over one attenuation read as a
// division of the whole vector - but a reciprocal that is also read as a number in
// its own right is not a division, and rewriting the multiply alone leaves the rcp
// standing beside it. distance_falloff divides by its falloff and raises the same
// falloff to a power: taken without this, it asked for `sign(d) / t1` on one line
// and `pow(saturate(rcp(t1)), light.w)` on the next, where the reader has to work
// out that the rcp and the divide are the one reciprocal.
public class MultiplyReciprocalDivisionTemplate : NodeTemplate<MultiplyOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is MultiplyOperation multiply
            && (IsReciprocal(multiply.Factor1) || IsReciprocal(multiply.Factor2));
    }

    private static bool IsReciprocal(HlslTreeNode node)
    {
        return node switch
        {
            DivisionOperation division => ConstantMatcher.IsOne(division.Dividend),
            ReciprocalOperation => node.Outputs.All(reader => reader is MultiplyOperation),
            _ => false,
        };
    }

    // What the reciprocal is of: the divisor of a `1 / b`, and the operand of an rcp.
    private static HlslTreeNode Divisor(HlslTreeNode reciprocal)
    {
        return reciprocal is DivisionOperation division
            ? division.Divisor
            : ((ReciprocalOperation)reciprocal).Value;
    }

    public override HlslTreeNode Reduce(MultiplyOperation node)
    {
        return IsReciprocal(node.Factor1)
            ? new DivisionOperation(node.Factor2, Divisor(node.Factor1))
            : new DivisionOperation(node.Factor1, Divisor(node.Factor2));
    }
}
