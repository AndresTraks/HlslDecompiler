using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

public class AppendStatement : IStatement
{
    public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

    public AppendStatement(IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
    {
        Inputs = inputs.ToDictionary();
        Outputs = inputs.ToDictionary();
    }
}
