using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public class StoreStructuredStatement : IStatement
{
    public HlslTreeNode Destination { get; }
    public HlslTreeNode Address { get; set; }
    // One per component of the element written: a StructuredBuffer<float4> store
    // writes four, and carrying a single value silently kept only one of them.
    public HlslTreeNode[] Values { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

    public StoreStructuredStatement(HlslTreeNode destination, HlslTreeNode address, HlslTreeNode[] values, IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
    {
        Destination = destination;
        Address = address;
        Values = values;
        Inputs = inputs.ToDictionary();
        Outputs = inputs.ToDictionary();
    }
}
