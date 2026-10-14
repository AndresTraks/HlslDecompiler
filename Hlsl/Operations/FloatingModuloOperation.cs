namespace HlslDecompiler.Hlsl;

// fmod; not ModuloOperation, since float % is undefined for mixed signs.
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
