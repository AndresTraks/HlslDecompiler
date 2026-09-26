using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

public class RestartStripStatement : IStatement
{
    public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

    /// <summary>Which stream's strip is restarted, where the shader writes several.</summary>
    public int? Stream { get; init; }

    public RestartStripStatement(IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
    {
        Inputs = inputs.ToDictionary();
        Outputs = inputs.ToDictionary();
    }
}
