namespace HlslDecompiler.Hlsl;

// f32tof16: a float as the bits of the half float nearest it, in the low sixteen
// bits of an integer. f16tof32 is the other way round.
public class FloatToHalfOperation : ConsumerOperation
{
    public FloatToHalfOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "f32tof16";
}
