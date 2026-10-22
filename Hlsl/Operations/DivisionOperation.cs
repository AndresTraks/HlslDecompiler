namespace HlslDecompiler.Hlsl;

public class DivisionOperation : Operation
{
    public DivisionOperation(HlslTreeNode dividend, HlslTreeNode divisor)
    {
        AddInput(dividend);
        AddInput(divisor);
    }

    public HlslTreeNode Dividend => Inputs[0];
    public HlslTreeNode Divisor => Inputs[1];

    /// <summary>A signed integer / recovered from fxc's expansion; see ModuloOperation.</summary>
    public bool IsSigned { get; init; }

    protected override bool IsUnsignedVariant => !IsSigned;

    public override string Mnemonic => "div";
}
