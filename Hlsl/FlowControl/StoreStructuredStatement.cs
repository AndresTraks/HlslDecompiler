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

    // And whether that raw memory is groupshared. HLSL can declare a raw buffer and
    // not raw groupshared, so this one is written as a subscript into the array it
    // has to be declared as - and its address is the element, not the byte offset
    // the instruction carried. It stays raw for every other purpose: what it reads
    // is bits, and a reader wanting a float reinterprets rather than converts.
    public bool IsGroupShared { get; init; }

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

    public IEnumerable<HeldSlot> HeldSlots =>
    [
        HeldSlot.Named(() => Address, value => Address = value),
        HeldSlot.Named(() => Values),
    ];
}
