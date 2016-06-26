using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler
{
    public class HlslAst
    {
        public Dictionary<RegisterKey, HlslTreeNode> Roots { get; private set; }

        public HlslAst(Dictionary<RegisterKey, HlslTreeNode> roots)
        {
            Roots = roots;
        }

        public void ReduceTree()
        {
            Roots = Roots.ToDictionary(r => r.Key, r => r.Value.Reduce());
        }
    }
}
