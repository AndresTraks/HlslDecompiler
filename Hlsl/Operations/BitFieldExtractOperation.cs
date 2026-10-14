namespace HlslDecompiler.Hlsl;

/// <summary>
/// ubfe/ibfe: extract width bits at offset, zero- or sign-extended. No HLSL
/// intrinsic, so written as shifts and a mask.
/// </summary>
public class BitFieldExtractOperation : Operation
{
    public BitFieldExtractOperation(HlslTreeNode width, HlslTreeNode offset, HlslTreeNode value, bool isUnsigned)
    {
        AddInput(width);
        AddInput(offset);
        AddInput(value);
        IsUnsigned = isUnsigned;
    }

    public HlslTreeNode Width => Inputs[0];
    public HlslTreeNode Offset => Inputs[1];
    public HlslTreeNode Value => Inputs[2];
    public bool IsUnsigned { get; }

    public override string Mnemonic => IsUnsigned ? "ubfe" : "ibfe";
}
