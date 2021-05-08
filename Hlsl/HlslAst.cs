using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.TemplateMatch;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class HlslAst
    {
        public Dictionary<RegisterKey, HlslTreeNode> Roots { get; private set; }
        public Dictionary<RegisterKey, HlslTreeNode> NoOutputInstructions { get; private set; }

        public HlslAst(Dictionary<RegisterKey, HlslTreeNode> roots,
            Dictionary<RegisterKey, HlslTreeNode> noOutputInstructions)
        {
            Roots = roots;
            NoOutputInstructions = noOutputInstructions;
        }

        public void ReduceTree(NodeGrouper nodeGrouper)
        {
            var templateMatcher = new TemplateMatcher(nodeGrouper);
            Roots = Roots.ToDictionary(r => r.Key,
                r => templateMatcher.Reduce(r.Value));
            NoOutputInstructions = NoOutputInstructions.ToDictionary(r => r.Key,
                r => templateMatcher.Reduce(r.Value));
        }
    }
}
