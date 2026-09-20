using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// A write into a typed unordered access view: `destination[coordinate] = value`,
/// where the coordinate addresses a texel rather than an element of a buffer. A
/// statement for the reason a structured store is one - it has an effect and no
/// value, and when it runs relative to the loads around it is the point of it.
/// </summary>
public class StoreTypedStatement : IStatement
{
    public HlslTreeNode Destination { get; }
    // As many as the resource has dimensions: two for a Texture2D, three for a
    // Texture2DArray or a Texture3D.
    public HlslTreeNode[] Coordinates { get; }
    // One per component written, the way a structured store carries them.
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
}
