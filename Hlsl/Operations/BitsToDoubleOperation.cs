namespace HlslDecompiler.Hlsl;

/// <summary>
/// `asdouble(lo, hi)`: a register pair read as a double when it does not already
/// hold one. The bytecode does not mark the join.
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
