using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

public class SwitchStatement : IStatement
{
    public HlslTreeNode Selector { get; }
    public IList<SwitchCase> Cases { get; } = [];
    public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

    public bool IsParsed { get; set; } = false;

    /// <summary>The case currently being parsed, or null before the first one.</summary>
    public SwitchCase CurrentCase => Cases.Count == 0 ? null : Cases[Cases.Count - 1];

    public SwitchStatement(HlslTreeNode selector, IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
    {
        Selector = selector;
        Inputs = inputs.ToDictionary();
        Outputs = inputs.ToDictionary();
    }

    public override string ToString()
    {
        return $"switch ({Selector})";
    }
}

public class SwitchCase
{
    /// <summary>The case label, or null for <c>default</c>.</summary>
    public HlslTreeNode Label { get; }
    public IList<IStatement> Body { get; } = [];

    public SwitchCase(HlslTreeNode label)
    {
        Label = label;
    }

    public bool IsDefault => Label == null;
}
