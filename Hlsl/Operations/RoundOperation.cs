using System;

namespace HlslDecompiler.Hlsl;

public class RoundOperation : ConsumerOperation
{
    public RoundOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "round";
}
