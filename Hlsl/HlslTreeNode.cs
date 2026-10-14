using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public class HlslTreeNode
{
    public IList<HlslTreeNode> Inputs { get; } = [];
    public IList<HlslTreeNode> Outputs { get; } = [];

    /// <summary>
    /// Whether the source instruction reads its operands as integers (iadd, ld), as
    /// floats (add, sample), or neither (mov, and/or/xor). Types ambiguous values
    /// such as a mov'd immediate by their readers.
    /// </summary>
    public bool? ConsumesInteger { get; set; }

    /// <summary>
    /// Position of the instruction that made this value, shared by its components.
    /// Zero where unset (template-built, operand reads); zero never matches anything.
    /// </summary>
    public int SourceInstruction { get; set; }

    /// <summary>
    /// Destination component within <see cref="SourceInstruction"/>.
    /// </summary>
    public int SourceComponent { get; set; }

    /// <summary>
    /// Facts recorded while all readers still existed (<see cref="ValueTypes.Record"/>).
    /// Null for later values, which are then asked live.
    /// </summary>
    internal TypeFacts Types { get; set; }

    /// <summary>
    /// Use this after the parse, not <see cref="ConsumesInteger"/>: template-built
    /// nodes have no flag of their own.
    /// </summary>
    internal bool? ReadsIntegers => Types != null ? Types.Reads : ConsumesInteger;

    /// <summary>
    /// Made by one instruction, so components of one value rather than register mates.
    /// </summary>
    public static bool IsSameInstruction(HlslTreeNode a, HlslTreeNode b)
    {
        return a.SourceInstruction != 0 && a.SourceInstruction == b.SourceInstruction;
    }

    public void Replace(HlslTreeNode with)
    {
        // Self-replacement would modify the list being enumerated.
        if (ReferenceEquals(this, with))
        {
            return;
        }

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
        // Terminates on loop-carried values and skips shared subexpressions.
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
    /// Inputs expression walks descend into. A phi is a leaf: its operands describe
    /// a loop-carried update, not its value's composition.
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
    /// Adds an input without the acyclic check; only for a loop backedge. The cycle
    /// is never walked (<see cref="TraversableInputs"/>).
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
