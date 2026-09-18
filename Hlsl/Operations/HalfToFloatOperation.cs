namespace HlslDecompiler.Hlsl;

public class HalfToFloatOperation : ConsumerOperation
{
    public HalfToFloatOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "f16tof32";
}
