using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl
{
    public class ClipStatement : IStatement
    {
        public HlslTreeNode[] Values { get; }
        public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
        public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

        public ClipStatement(HlslTreeNode[] values, IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
        {
            Values = values;
            Inputs = inputs.ToDictionary();
            Outputs = inputs.ToDictionary();
        }
    }
}
