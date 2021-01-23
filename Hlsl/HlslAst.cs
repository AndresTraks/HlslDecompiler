using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class HlslAst
    {
        public Dictionary<RegisterComponentKey, HlslTreeNode> Roots { get; private set; }

        public HlslAst(Dictionary<RegisterComponentKey, HlslTreeNode> roots)
        {
            Roots = roots;
        }

        public void ReduceTree()
        {
            Roots = Roots.ToDictionary(r => r.Key, r => r.Value.Reduce());
        }
    }
}
