using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// `destination[coordinate] = value` into a typed UAV, addressing a texel.
/// </summary>
public class StoreTypedStatement : IStatement
{
    public HlslTreeNode Destination { get; }
    // One per resource dimension.
    public HlslTreeNode[] Coordinates { get; }
    public HlslTreeNode[] Values { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

    public StoreTypedStatement(
        HlslTreeNode destination,
        HlslTreeNode[] coordinates,
        HlslTreeNode[] values,
        IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
    {
        Destination = destination;
        Coordinates = coordinates;
        Values = values;
        Inputs = inputs.ToDictionary();
        Outputs = inputs.ToDictionary();
    }

    public IEnumerable<HeldSlot> HeldSlots =>
    [
        HeldSlot.Named(() => Coordinates),
        HeldSlot.Named(() => Values),
    ];
}
