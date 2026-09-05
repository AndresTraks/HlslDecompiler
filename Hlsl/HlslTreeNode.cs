using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public class HlslTreeNode
{
    public IList<HlslTreeNode> Inputs { get; } = [];
    public IList<HlslTreeNode> Outputs { get; } = [];

    public void Replace(HlslTreeNode with)
    {
        foreach (var input in Inputs)
        {
            input.Outputs.Remove(this);
        }
        foreach (var output in Outputs)
        {
            for (int i = 0; i < output.Inputs.Count; i++)
            {
                if (output.Inputs[i] == this)
                {
                    output.Inputs[i] = with;
                }
            }
            with.Outputs.Add(output);
        }
    }

    public void Remove()
    {
        foreach (var input in Inputs)
        {
            input.Outputs.Remove(this);
        }
        if (Outputs.Count != 0)
        {
            throw new NotImplementedException();
        }
    }

    public bool IsInputOf(IEnumerable<HlslTreeNode> nodes)
    {
        var visited = NewNodeSet();
        return nodes.Any(node => IsInputOf(node, visited));
    }

    public bool IsInputOf(HlslTreeNode node)
    {
        return IsInputOf(node, NewNodeSet());
    }

    private bool IsInputOf(HlslTreeNode node, HashSet<HlslTreeNode> visited)
    {
        if (node == this)
        {
            return true;
        }
        // Memoizing on reference identity both terminates on loop-carried values
        // and keeps shared subexpressions from being walked repeatedly.
        if (!visited.Add(node))
        {
            return false;
        }
        foreach (HlslTreeNode input in TraversableInputs(node))
        {
            if (IsInputOf(input, visited))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Inputs that expression traversals may descend into. A phi's operands describe
    /// how a loop-carried variable is updated, not how its value is composed, so the
    /// phi is a leaf to everything that walks expressions.
    /// </summary>
    public static IEnumerable<HlslTreeNode> TraversableInputs(HlslTreeNode node)
    {
        if (node is FlowControl.PhiNode)
        {
            return Array.Empty<HlslTreeNode>();
        }
        return node.Inputs;
    }

    public static HashSet<HlslTreeNode> NewNodeSet()
    {
        return new HashSet<HlslTreeNode>(ReferenceEqualityComparer.Instance);
    }

    protected void AddInput(HlslTreeNode node)
    {
        Inputs.Add(node);
        node.Outputs.Add(this);
        AssertLoopFree();
    }

    /// <summary>
    /// Adds an input without the acyclic check. Only a loop backedge may use this:
    /// the value flowing back is by definition derived from the node it feeds, so
    /// the cycle is intended. Expression traversals treat a phi as a leaf, so the
    /// cycle is never walked - see <see cref="TraversableInputs"/>.
    /// </summary>
    protected void AddBackedgeInput(HlslTreeNode node)
    {
        Inputs.Add(node);
        node.Outputs.Add(this);
    }

    private void AssertLoopFree()
    {
        foreach (HlslTreeNode output in Outputs)
        {
            AssertLoopFree(output);
            if (this == output)
            {
                throw new InvalidOperationException();
            }
        }
    }

    private void AssertLoopFree(HlslTreeNode parent)
    {
        foreach (HlslTreeNode upperParent in parent.Outputs)
        {
            if (this == upperParent)
            {
                throw new InvalidOperationException();
            }
        }
    }
}
