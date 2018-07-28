using System.Globalization;
using System.Linq;

namespace HlslDecompiler.Hlsl.Compiler
{
    public sealed class ConstantCompiler
    {
        private static readonly CultureInfo _culture = CultureInfo.InvariantCulture;

        public static string Compile(ConstantNode[] group)
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
            return $"float{count}({components})";
        }

        private static string CompileConstant(ConstantNode firstConstant)
        {
            return firstConstant.Value.ToString(_culture);
        }
    }
}
