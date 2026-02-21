using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

public class ReturnStatement : IStatement
{
    public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

    public ReturnStatement(IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
    {
        Inputs = inputs.ToDictionary();
        Outputs = inputs.ToDictionary();
    }
}
