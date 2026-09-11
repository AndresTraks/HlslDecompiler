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

    public override string Mnemonic => TargetType;
}
