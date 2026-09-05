using System;

namespace HlslDecompiler.Hlsl;

public class FloorOperation : ConsumerOperation
{
    public FloorOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "floor";
}
