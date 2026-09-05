using System;

namespace HlslDecompiler.Hlsl;

public class TruncateOperation : ConsumerOperation
{
    public TruncateOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "trunc";
}
