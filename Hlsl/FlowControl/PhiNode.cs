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

    // True once SetBackedgeValue has run, i.e. this is a loop header phi rather than
    // a branch join.
    public bool IsLoopHeader { get; private set; }

    // Value on entry to the loop.
    public HlslTreeNode PreLoopValue => Inputs[0];

    // Value carried back from the end of the loop body.
    public HlslTreeNode BackedgeValue => IsLoopHeader ? Inputs[1] : null;

    // Closes the loop: the body's final value for this register flows back into the
    // header. This is the one place a cycle is created, deliberately.
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
