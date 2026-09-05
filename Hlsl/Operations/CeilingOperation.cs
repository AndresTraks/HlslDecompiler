using System;

namespace HlslDecompiler.Hlsl;

public class CeilingOperation : ConsumerOperation
{
    public CeilingOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "ceil";
}
