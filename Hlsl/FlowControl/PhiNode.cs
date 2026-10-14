using System;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

public class PhiNode : HlslTreeNode
{
    public PhiNode(params HlslTreeNode[] inputs)
    {
        foreach (HlslTreeNode input in inputs)
        {
            AddInput(input);
        }
    }

    // A loop header phi rather than a branch join.
    public bool IsLoopHeader { get; private set; }

    public HlslTreeNode PreLoopValue => Inputs[0];

    public HlslTreeNode BackedgeValue => IsLoopHeader ? Inputs[1] : null;

    // The one place the node graph deliberately gets a cycle.
    public void SetBackedgeValue(HlslTreeNode value)
    {
        if (Inputs.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected a loop header phi with one operand, found {Inputs.Count}.");
        }
        AddBackedgeInput(value);
        IsLoopHeader = true;
    }

    public override string ToString()
    {
        if (IsLoopHeader)
        {
            // Do not print the backedge - it refers back to this node.
            return $"phi(loop: {PreLoopValue}, ...)";
        }
        return "phi(" + string.Join(", ", Inputs.Select(i => i.ToString())) + ")";
    }
}
