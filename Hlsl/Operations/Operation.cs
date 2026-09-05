using System.Linq;

namespace HlslDecompiler.Hlsl;

public abstract class Operation : HlslTreeNode
{
    // The bytecode mnemonic, as it appears in an assembly listing.
    public abstract string Mnemonic { get; }

    // The HLSL function this compiles to. Usually the same word, but not always:
    // bytecode exp and log are base 2 where HLSL exp and log are base e, and frc
    // is spelled frac.
    public virtual string HlslFunction => Mnemonic;

    public override string ToString()
    {
        string parameters = string.Join(", ", Inputs.Select(c => c.ToString()));
        return $"{Mnemonic}({parameters})";
    }
}
