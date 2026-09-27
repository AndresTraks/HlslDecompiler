using HlslDecompiler.Util;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public sealed class ConstantCompiler
{
    public string Compile(ConstantNode[] group, bool unsigned = false)
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
        // compile, and the shift amounts were integers all along. An unsigned one
        // where what it is going into is unsigned: `uint3 t = i + int3(2, 3, 4)`
        // converts silently where it could say what it is. Not for a negative
        // constant, which is a number no uint holds and reads as its wraparound.
        string type = group.All(c => c.IntegerValue != null)
            ? (unsigned && group.All(c => c.IntegerValue >= 0) ? "uint" : "int")
            : "float";
        return $"{type}{count}({components})";
    }

    private string CompileConstant(ConstantNode firstConstant)
    {
        return firstConstant.IntegerValue?.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ?? ConstantFormatter.Format(firstConstant.Value);
    }
}
