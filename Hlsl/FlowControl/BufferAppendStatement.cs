using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// `buffer.Append(value);` - one element added to an append buffer, which the
/// bytecode writes as two instructions: imm_atomic_alloc takes the next slot from
/// the buffer's counter, and a structured store puts the element in it.
///
/// An AppendStructuredBuffer has no other spelling. There is no subscript on one
/// and no counter to read, so the pair has to come back as the call, and the slot
/// the alloc named is a register nothing else was ever going to read.
/// </summary>
public class BufferAppendStatement : IStatement
{
    public HlslTreeNode Destination { get; }
    // One per component of the element, the way a structured store carries them.
    public HlslTreeNode[] Values { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

    public BufferAppendStatement(
        HlslTreeNode destination,
        HlslTreeNode[] values,
        IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
    {
        Destination = destination;
        Values = values;
        Inputs = inputs.ToDictionary();
        Outputs = inputs.ToDictionary();
    }

    // The destination is the resource appended to and not a value: the writer names
    // it from its register, and no pass rewrites or reduces it. The same goes for
    // the destination of a typed or structured store and of an atomic.
    public IEnumerable<HeldSlot> HeldSlots => [HeldSlot.Named(() => Values)];
}
