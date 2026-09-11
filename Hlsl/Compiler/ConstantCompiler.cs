using HlslDecompiler.Util;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public sealed class ConstantCompiler
{
    public string Compile(ConstantNode[] group)
    {
        ConstantNode first = group[0];

        int count = group.Length;
        if (count == 1)
        {
            return CompileConstant(first);
        }

        if (group.All(c => NodeGrouper.AreNodesEquivalent(c, first)))
        {
            return CompileConstant(first);
        }

        string components = string.Join(", ", group.Select(CompileConstant));
        // A vector of integers is an int vector. `t0 >> float2(8, 16)` does not
        // compile, and the shift amounts were integers all along.
        string type = group.All(c => c.IntegerValue != null) ? "int" : "float";
        return $"{type}{count}({components})";
    }

    private string CompileConstant(ConstantNode firstConstant)
    {
        return firstConstant.IntegerValue?.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ?? ConstantFormatter.Format(firstConstant.Value);
    }
}
