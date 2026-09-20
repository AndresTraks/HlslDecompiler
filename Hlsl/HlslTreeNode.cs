using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public class HlslTreeNode
{
    public IList<HlslTreeNode> Inputs { get; } = [];
    public IList<HlslTreeNode> Outputs { get; } = [];

    /// <summary>
    /// Whether the instruction this node came from reads its operands as integers
    /// (iadd, itof, ld), as floats (add, mad, sample, ftoi), or says nothing about
    /// them - a mov passes its bits along, and and/or/xor work on bits as bits.
    /// A value that could be either, the immediate a mov writes, is typed by the
    /// nodes that go on to read it, which is what this is for.
    /// </summary>
    public bool? ConsumesInteger { get; set; }

    /// <summary>
    /// Which instruction made this value, as its position in the shader. The
    /// components of one instruction share it, and that is the only question it is
    /// there to answer: four loads into one register are four instructions and the
    /// four components of one load are one. Zero where nothing set it - a node a
    /// template built, or an operand read rather than computed - and zero is never
    /// the same instruction as anything, including another zero.
    /// </summary>
    public int SourceInstruction { get; set; }

    /// <summary>
    /// Which component of that instruction's destination this value is, so that
    /// the components of one instruction can be put back in the order it wrote
    /// them. Meaningless where <see cref="SourceInstruction"/> is zero.
    /// </summary>
    public int SourceComponent { get; set; }

    /// <summary>
    /// Whether two values were made by one instruction, which is what makes them
    /// components of one thing rather than two that share a register.
    /// </summary>
    public static bool IsSameInstruction(HlslTreeNode a, HlslTreeNode b)
    {
        return a.SourceInstruction != 0 && a.SourceInstruction == b.SourceInstruction;
    }

    public void Replace(HlslTreeNode with)
    {
        // Replacing a node with itself would drop its own back-references and then
        // append to the very list being enumerated.
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
