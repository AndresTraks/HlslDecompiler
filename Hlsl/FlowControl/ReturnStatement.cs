using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

public class ReturnStatement : IStatement
{
    public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

    // A return added after a statement returns what is already live, so nothing
    // is assigned on the way out.
    public ReturnStatement(IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
        : this(inputs, inputs)
    {
    }

    // A return that replaces an assignment statement inherits its assignments too,
    // and they still have to be written before the returned expression reads them.
    public ReturnStatement(
        IDictionary<RegisterComponentKey, HlslTreeNode> inputs,
        IDictionary<RegisterComponentKey, HlslTreeNode> outputs)
    {
        Inputs = inputs.ToDictionary();
        Outputs = outputs.ToDictionary();
    }
}
