using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public class StoreStructuredStatement : IStatement
{
    public HlslTreeNode Destination { get; }
    public HlslTreeNode Address { get; set; }
    // One per component written.
    public HlslTreeNode[] Values { get; }
    // store_raw: the address is a byte offset, written with Store, Store2 and so on.
    public bool IsRaw { get; init; }

    // Raw groupshared has no HLSL form, so it is written as a subscript into an
    // array, addressed by element. Its values are still bits.
    public bool IsGroupShared { get; init; }

    // Which struct members the store reaches.
    public int ElementByteOffset { get; init; }

    // As LoadStructuredNode's: the member, taken before the offset is a variable.
    public int MemberByteOffset { get; init; }

    // The byte offset as a value, where it is not an immediate (an array member
    // indexed by a variable); null otherwise.
    public HlslTreeNode ByteOffset { get; set; }
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
        HeldSlot.Named(() => ByteOffset, value => ByteOffset = value),
        HeldSlot.Named(() => Values),
    ];
}
