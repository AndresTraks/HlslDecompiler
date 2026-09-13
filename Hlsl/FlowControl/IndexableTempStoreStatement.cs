using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

/// <summary>
/// A write to an element of an indexable temp, x0[i].xy = value. A statement rather
/// than an assignment, for the reason IndexableTempLoadNode gives: what it
/// overwrites cannot be told from the index alone, so every value read from the
/// array before it has to be read before it.
/// </summary>
public class IndexableTempStoreStatement : IStatement
{
    public int Register { get; }
    public HlslTreeNode Index { get; set; }
    // Which components of the element are written, in order, with their values.
    public int[] Components { get; }
    public HlslTreeNode[] Values { get; }
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
}
