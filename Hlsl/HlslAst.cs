using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.TemplateMatch;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class HlslAst
    {
        public Dictionary<RegisterComponentKey, HlslTreeNode> Roots { get; private set; }
        public Dictionary<RegisterComponentKey, HlslTreeNode> NoOutputInstructions { get; private set; }

        public HlslAst(Dictionary<RegisterComponentKey, HlslTreeNode> roots,
            Dictionary<RegisterComponentKey, HlslTreeNode> noOutputInstructions)
        {
            Roots = roots;
            NoOutputInstructions = noOutputInstructions;
        }

        public void ReduceTree()
        {
            var templateMatcher = new TemplateMatcher();
            Roots = Roots.ToDictionary(r => r.Key,
                r => templateMatcher.Reduce(r.Value));
            NoOutputInstructions = NoOutputInstructions.ToDictionary(r => r.Key,
                r => templateMatcher.Reduce(r.Value));
        }
    }
}
