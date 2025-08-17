using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl
{
    public class BreakStatement : IStatement
    {
        public HlslTreeNode Comparison { get; }
        public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
        public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

        public BreakStatement(HlslTreeNode comparison, IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
        {
            Comparison = comparison;
            Inputs = inputs.ToDictionary();
            Outputs = inputs.ToDictionary();
        }
    }
}
