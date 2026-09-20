namespace HlslDecompiler.Hlsl;

/// <summary>
/// ubfe and ibfe: a run of bits out of a value, as a number. Which bits is a width
/// and an offset, and the two instructions differ in what fills the top of the
/// result - zeroes, or copies of the field's own top bit. HLSL has no intrinsic
/// for either, so it is written as the shift and the mask it means.
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
