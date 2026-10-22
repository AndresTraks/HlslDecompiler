namespace HlslDecompiler.Hlsl;

// The remainder half of an integer divide. udiv produces both at once.
public class ModuloOperation : Operation
{
    public ModuloOperation(HlslTreeNode dividend, HlslTreeNode divisor)
    {
        AddInput(dividend);
        AddInput(divisor);
    }

    public HlslTreeNode Dividend => Inputs[0];
    public HlslTreeNode Divisor => Inputs[1];

    /// <summary>
    /// A signed % recovered from fxc's abs/udiv/negate expansion: an operand HLSL
    /// types uint (a resinfo size) would make it unsigned again.
    /// </summary>
    public bool IsSigned { get; init; }

    public override string Mnemonic => "mod";
}
