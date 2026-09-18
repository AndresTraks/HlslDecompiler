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
    // store_raw: the address is a byte offset, written with Store, Store2 and so on.
    public bool IsRaw { get; init; }

    // The offset within the element, and which of its components are written. A
    // struct element is addressed by the offset alone, so these are what say which
    // members the store reaches.
    public int ElementByteOffset { get; init; }
    public int[] Components { get; init; } = [];
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
