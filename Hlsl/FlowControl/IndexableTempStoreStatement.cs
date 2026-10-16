using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

/// <summary>
/// `x0[i].xy = value`. A statement because what it overwrites is unknown from the
/// index, so every earlier read of the array must stay before it.
/// </summary>
public class IndexableTempStoreStatement : IStatement
{
    public int Register { get; }
    public HlslTreeNode Index { get; set; }
    public int[] Components { get; }
    public HlslTreeNode[] Values { get; }
    // Declared int: a float stored into it is its bits.
    public bool IsIntegerElement { get; init; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

    public IndexableTempStoreStatement(int register, HlslTreeNode index, int[] components,
        HlslTreeNode[] values, IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
    {
        Register = register;
        Index = index;
        Components = components;
        Values = values;
        Inputs = inputs.ToDictionary();
        Outputs = inputs.ToDictionary();
    }

    public IEnumerable<HeldSlot> HeldSlots =>
    [
        HeldSlot.Named(() => Index, value => Index = value),
        HeldSlot.Named(() => Values),
    ];
}
