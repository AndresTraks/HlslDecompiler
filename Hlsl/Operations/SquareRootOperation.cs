namespace HlslDecompiler.Hlsl;

public class SquareRootOperation : ConsumerOperation
{
    public SquareRootOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    /// <summary>The root of an rsq, which a division by it is written back as.</summary>
    public bool IsReciprocalRoot { get; init; }

    public override string Mnemonic => "sqrt";
}
