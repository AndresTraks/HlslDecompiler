namespace HlslDecompiler.Hlsl;

// ftoi, ftou, itof and utof; not a plain move, which would lose truncation and
// integer typing.
public class ConvertOperation : Operation
{
    public ConvertOperation(HlslTreeNode value, string targetType)
    {
        AddInput(value);
        TargetType = targetType;
    }

    public HlslTreeNode Value => Inputs[0];

    public string TargetType { get; }

    /// <summary>
    /// True for utof, false for itof; null where the opcode does not say.
    /// </summary>
    public bool? SourceUnsigned { get; init; }

    public override string Mnemonic => TargetType;
}
