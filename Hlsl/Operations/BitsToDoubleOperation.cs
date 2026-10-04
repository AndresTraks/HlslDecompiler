namespace HlslDecompiler.Hlsl;

/// <summary>
/// A double made out of the bits of two 32-bit values, which is `asdouble`. A
/// double instruction reads its operands a register pair at a time, and where the
/// pair holds a double already - loaded from a `StructuredBuffer&lt;double&gt;`, or
/// left there by an earlier double instruction - the value at the lower component
/// is that double and the upper one is not read at all.
///
/// Where it does not, the two components are two values of their own and the
/// double is their bits put together. Nothing in the bytecode marks the join:
/// reading a register pair as a double costs no instruction, so `asdouble(lo, hi)`
/// and a double that was in the register anyway look exactly alike.
/// </summary>
public class BitsToDoubleOperation : Operation
{
    public BitsToDoubleOperation(HlslTreeNode low, HlslTreeNode high)
    {
        AddInput(low);
        AddInput(high);
        // Both halves are uints, whatever bits they are: that is what the intrinsic
        // takes, and read as floats a half would come out as the number its bits
        // spell.
        ConsumesInteger = true;
    }

    public HlslTreeNode Low => Inputs[0];
    public HlslTreeNode High => Inputs[1];

    public override string Mnemonic => "asdouble";
}
