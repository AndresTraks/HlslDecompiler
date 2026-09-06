using HlslDecompiler.Hlsl.FlowControl;
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

    public HlslTreeNode Reduce(HlslTreeNode node)
    {
        return ReduceDepthFirst(node, HlslTreeNode.NewNodeSet());
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

    private HlslTreeNode ReduceDepthFirst(HlslTreeNode node, HashSet<HlslTreeNode> onPath)
    {
        // A phi is opaque: nothing may be folded across a loop backedge.
        if (ConstantMatcher.IsConstant(node) || IsRegister(node) || node is PhiNode)
        {
            return node;
        }
        if (!onPath.Add(node))
        {
            return node;
        }
        try
        {
            for (int i = 0; i < node.Inputs.Count; i++)
            {
                HlslTreeNode input = node.Inputs[i];
                node.Inputs[i] = ReduceDepthFirst(input, onPath);
            }
            foreach (INodeTemplate template in _templates)
            {
                if (template.Match(node))
                {
                    var replacement = template.Reduce(node);
                    Replace(node, replacement);
                    return ReduceDepthFirst(replacement, onPath);
                }
            }
            foreach (IGroupTemplate template in _groupTemplates)
            {
                IGroupContext groupContext = template.Match(node);
                if (groupContext != null)
                {
                    var replacement = template.Reduce(node, groupContext);
                    Replace(node, replacement);
                    return ReduceDepthFirst(replacement, onPath);
                }
            }
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

    private static bool IsRegister(HlslTreeNode node)
    {
        return node is RegisterInputNode;
    }
}
