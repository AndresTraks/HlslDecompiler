using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// `buffer.Append(value);`, compiled as imm_atomic_alloc plus a structured store
/// into the allocated slot. An AppendStructuredBuffer has no other spelling.
/// </summary>
public class BufferAppendStatement : IStatement
{
    public HlslTreeNode Destination { get; }

    // By dword of the element. A later store into the same slot supersedes this
    // statement with one holding both stores' values, and this one is not written.
    public HlslTreeNode[] Values { get; }
    public bool IsSuperseded { get; set; }
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

    // The destination is a resource, not a value, as in the stores and atomics.
    public IEnumerable<HeldSlot> HeldSlots => [HeldSlot.Named(() => Values)];
}
