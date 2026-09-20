namespace HlslDecompiler.Hlsl;

// HLSL's firstbithigh: the position of the highest set bit, counted from the
// lowest, or -1 where there is none. The instructions it compiles to count the
// other way - firstbit_hi over a uint, firstbit_shi over an int, both of them
// the number of bits above the one that matters - so one instruction is
// `31 - firstbithigh(x)` and only the whole idiom fxc writes is the call.
public class FirstBitHighOperation : ConsumerOperation
{
    public FirstBitHighOperation(HlslTreeNode value, bool isUnsigned)
    {
        AddInput(value);
        IsUnsigned = isUnsigned;
    }

    /// <summary>
    /// Whether this was firstbit_hi rather than firstbit_shi. HLSL has one name
    /// for the two and picks between them by the type of the operand, so an
    /// unsigned one has to say so where the value does not already.
    /// </summary>
    public bool IsUnsigned { get; }

    public override string Mnemonic => "firstbithigh";
}
