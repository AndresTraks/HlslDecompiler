using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public class StoreStructuredStatement : IStatement
{
    public HlslTreeNode Destination { get; }
    public HlslTreeNode Address { get; }
    public HlslTreeNode Value { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

    public StoreStructuredStatement(HlslTreeNode destination, HlslTreeNode address, HlslTreeNode value, IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
    {
        Destination = destination;
        Address = address;
        Value = value;
        Inputs = inputs.ToDictionary();
        Outputs = inputs.ToDictionary();
    }
}
