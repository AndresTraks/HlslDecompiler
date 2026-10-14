using System.Linq;

namespace HlslDecompiler.Hlsl;

public abstract class Operation : HlslTreeNode
{
    public abstract string Mnemonic { get; }

    // Differs from the mnemonic for e.g. exp/log (base 2 in bytecode) and frc.
    public virtual string HlslFunction => Mnemonic;

    /// <summary>
    /// Same operation down to the call written: ddx_coarse and ddx_fine share a class.
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
