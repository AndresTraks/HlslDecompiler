namespace HlslDecompiler.Hlsl;

// HLSL's firstbithigh (from the low end, -1 if none). firstbit_hi/shi count
// from the top, so one instruction parses as `31 - firstbithigh(x)`.
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
