namespace HlslDecompiler.Hlsl;

// ftoi, ftou, itof and utof. Writing them as a plain move drops the conversion:
// the truncation disappears, and an expression that has to be an integer - the one
// side of an `and`, say - stops compiling.
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
    /// Whether what is being converted was read as unsigned, where the opcode says:
    /// utof reads its source unsigned and itof signed, and both answer a float, so
    /// the target type cannot tell them apart. Null where nothing says - a cast the
    /// writer puts in, or a conversion out of a float.
    /// </summary>
    public bool? SourceUnsigned { get; init; }

    public override string Mnemonic => TargetType;
}
