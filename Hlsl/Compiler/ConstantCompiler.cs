using System.Globalization;
using System.Linq;

namespace HlslDecompiler.Hlsl.Compiler
{
    public sealed class ConstantCompiler
    {
        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;
        private readonly NodeGrouper _nodeGrouper;

        public ConstantCompiler(NodeGrouper nodeGrouper)
        {
            _nodeGrouper = nodeGrouper;
        }

        public string Compile(ConstantNode[] group)
        {
            ConstantNode first = group[0];

            int count = group.Length;
            if (count == 1)
            {
                return CompileConstant(first);
            }

            if (group.All(c => _nodeGrouper.AreNodesEquivalent(c, first)))
            {
                return CompileConstant(first);
            }

            string components = string.Join(", ", group.Select(CompileConstant));
            return $"float{count}({components})";
        }

        private string CompileConstant(ConstantNode firstConstant)
        {
            return firstConstant.Value.ToString(_culture);
        }
    }
}
