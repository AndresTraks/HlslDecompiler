using HlslDecompiler.Util;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
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
            return $"float{count}({components})";
        }

        private string CompileConstant(ConstantNode firstConstant)
        {
            return ConstantFormatter.Format(firstConstant.Value);
        }
    }
}
