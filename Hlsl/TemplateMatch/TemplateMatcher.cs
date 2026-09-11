using HlslDecompiler.Hlsl.FlowControl;
using System;
using System.Collections.Generic;

namespace HlslDecompiler.Hlsl.TemplateMatch;

public class TemplateMatcher
{
    private List<INodeTemplate> _templates;
    private List<IGroupTemplate> _groupTemplates;
    private NodeGrouper _nodeGrouper;

    public TemplateMatcher(NodeGrouper nodeGrouper)
    {
        _templates = new List<INodeTemplate>
        {
            new AddConstantsTemplate(),
            new AddNegateTemplate(),
            new AddNegativeTemplate(),
            new AddSelfTemplate(),
            new AddZeroTemplate(),
            new MoveTemplate(),
            new MultiplyAddTemplate(),
            new MultiplyConstantsTemplate(),
            new MultiplyConstantTemplate(),
            new MultiplyNegativeOneTemplate(),
            new MultiplyOneTemplate(),
            new MultiplyReciprocalDivisionTemplate(),
            new MultiplyZeroTemplate(),
            new NegateConstantTemplate(),
            new NegateNegateTemplate(),
            new ReciprocalReciprocalSquareRootTemplate(),
            new ReciprocalSquareRootTemplate(),
            new SubtractNegateTemplate(),
            new SubtractZeroTemplate(),
            //new NegateSubtractTemplate(),
            new CompareConstantTemplate(),
            new CompareNegativeWithZeroTemplate(),
            new ComparePositiveAndNegativeTemplate(),
            new CompareCompareTemplate(),
            new MaxOfPositiveAndNegativeTemplate()
        };
        _groupTemplates = new List<IGroupTemplate>
        {
            new DotProduct2Template(this),
            new DotProduct3Template(this),
            new DotProduct4Template(this),
            new LengthTemplate()
        };
        _nodeGrouper = nodeGrouper;
    }

    /// <summary>
    /// Nothing makes a template reduce anything. Two that undo each other would match
    /// forever - each match builds a fresh node, so the path set cannot see it coming
    /// round again - and the recursion would take the stack with it. A shader that
    /// reaches this is a template pair that does not terminate, not a large shader:
    /// the limit is far above what any real expression needs. Every shader here stays
    /// under fifty, the largest expression included - memoising means a node reduces
    /// once, so the count is bounded by how many nodes there are.
    /// </summary>
    private const int ReductionLimit = 100000;

    private int _reductionsLeft;

    public HlslTreeNode Reduce(HlslTreeNode node)
    {
        // The memo is per call: the graph is rewritten as it reduces, so what a node
        // reduces to only holds for this pass over it.
        _reductionsLeft = ReductionLimit;
        return ReduceDepthFirst(node, HlslTreeNode.NewNodeSet(),
            new Dictionary<HlslTreeNode, HlslTreeNode>(ReferenceEqualityComparer.Instance));
    }

    public bool CanGroupComponents(HlslTreeNode a, HlslTreeNode b, bool allowMatrixColumn)
    {
        return _nodeGrouper.CanGroupComponents(a, b, allowMatrixColumn);
    }

    public bool SharesMatrixColumnOrRow(HlslTreeNode x, HlslTreeNode y)
    {
        if (x is RegisterInputNode r1 && y is RegisterInputNode r2)
        {
            return _nodeGrouper.SharesMatrixColumnOrRow(r1, r2);
        }
        return false;
    }

    // onPath cuts cycles and has to stay a path set to do it. Reducing is what gets
    // remembered instead: without that, a subexpression read in two places is reduced
    // once per path that reaches it, and the work triples with every instruction that
    // builds on the one before.
    private HlslTreeNode ReduceDepthFirst(HlslTreeNode node, HashSet<HlslTreeNode> onPath,
        Dictionary<HlslTreeNode, HlslTreeNode> reduced)
    {
        // A phi is opaque: nothing may be folded across a loop backedge.
        if (ConstantMatcher.IsConstant(node) || IsRegister(node) || node is PhiNode)
        {
            return node;
        }
        if (reduced.TryGetValue(node, out HlslTreeNode already))
        {
            return already;
        }
        // Not remembered: a node cut here is only unreduced because of where the walk
        // reached it from.
        if (!onPath.Add(node))
        {
            return node;
        }
        try
        {
            for (int i = 0; i < node.Inputs.Count; i++)
            {
                HlslTreeNode input = node.Inputs[i];
                node.Inputs[i] = ReduceDepthFirst(input, onPath, reduced);
            }
            foreach (INodeTemplate template in _templates)
            {
                if (template.Match(node))
                {
                    CountReduction(template.GetType().Name);
                    var replacement = template.Reduce(node);
                    Replace(node, replacement);
                    HlslTreeNode result = ReduceDepthFirst(replacement, onPath, reduced);
                    reduced[node] = result;
                    return result;
                }
            }
            foreach (IGroupTemplate template in _groupTemplates)
            {
                IGroupContext groupContext = template.Match(node);
                if (groupContext != null)
                {
                    CountReduction(template.GetType().Name);
                    var replacement = template.Reduce(node, groupContext);
                    Replace(node, replacement);
                    HlslTreeNode result = ReduceDepthFirst(replacement, onPath, reduced);
                    reduced[node] = result;
                    return result;
                }
            }
            reduced[node] = node;
            return node;
        }
        finally
        {
            onPath.Remove(node);
        }
    }

    private static void Replace(HlslTreeNode node, HlslTreeNode with)
    {
        if (node == with)
        {
            return;
        }
        foreach (var input in node.Inputs)
        {
            input.Outputs.Remove(node);
        }
        foreach (var output in node.Outputs)
        {
            for (int i = 0; i < output.Inputs.Count; i++)
            {
                if (output.Inputs[i] == node)
                {
                    output.Inputs[i] = with;
                }
            }
            with.Outputs.Add(output);
        }
    }

    private void CountReduction(string templateName)
    {
        if (--_reductionsLeft >= 0)
        {
            return;
        }
        throw new InvalidOperationException(
            $"Reducing one expression took more than {ReductionLimit} steps, last by "
            + $"{templateName}. Two templates that undo each other look like this.");
    }

    private static bool IsRegister(HlslTreeNode node)
    {
        return node is RegisterInputNode;
    }
}
