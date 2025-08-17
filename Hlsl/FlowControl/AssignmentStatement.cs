using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl
{
    public class AssignmentStatement : IStatement
    {
        public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
        public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

        public AssignmentStatement(IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
        {
            Inputs = inputs.ToDictionary();
            Outputs = inputs.ToDictionary();
        }
    }
}
