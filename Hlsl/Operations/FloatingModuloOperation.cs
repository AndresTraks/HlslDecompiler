namespace HlslDecompiler.Hlsl;

// A floating point remainder. Kept apart from ModuloOperation, which writes %:
// HLSL leaves % on floats undefined where the operands disagree in sign, and fmod
// is the one that says what the shader means.
public class FloatingModuloOperation : Operation
{
    public FloatingModuloOperation(HlslTreeNode dividend, HlslTreeNode divisor)
    {
        AddInput(dividend);
        AddInput(divisor);
    }

    public HlslTreeNode Dividend => Inputs[0];
    public HlslTreeNode Divisor => Inputs[1];

    public override string Mnemonic => "fmod";
}
