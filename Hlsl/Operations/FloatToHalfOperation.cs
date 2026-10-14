namespace HlslDecompiler.Hlsl;

// f32tof16: the nearest half's bits in the low 16 bits of an integer.
public class FloatToHalfOperation : ConsumerOperation
{
    public FloatToHalfOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "f32tof16";
}
