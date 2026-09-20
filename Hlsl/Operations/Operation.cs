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

    /// <summary>
    /// Whether two nodes are the same operation down to what they write. The class
    /// alone does not say it: ddx_coarse and ddx_fine are one class told apart by
    /// the call, and compiling a group of them writes one call for all of them.
    /// </summary>
    public static bool IsSameKind(HlslTreeNode node1, HlslTreeNode node2)
    {
        if (node1.GetType() != node2.GetType())
        {
            return false;
        }
        return node1 is not Operation operation1 || node2 is not Operation operation2
            || operation1.HlslFunction == operation2.HlslFunction;
    }

    public override string ToString()
    {
        string parameters = string.Join(", ", Inputs.Select(c => c.ToString()));
        return $"{Mnemonic}({parameters})";
    }
}
