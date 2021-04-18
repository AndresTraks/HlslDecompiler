using System.Collections.Generic;

namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class TemplateMatcher
    {
        private List<INodeTemplate> _templates;
        private List<IGroupTemplate> _groupTemplates;
        private NodeGrouper _nodeGrouper;

        public TemplateMatcher(NodeGrouper nodeGrouper)
        {
            _templates = new List<INodeTemplate>
            {
                new AddConstantsTemplate(this),
                new AddNegateTemplate(),
                new AddNegativeTemplate(this),
                new AddSelfTemplate(),
                new AddZeroTemplate(this),
                new MoveTemplate(),
                new MultiplyAddTemplate(),
                new MultiplyConstantsTemplate(this),
                new MultiplyConstantTemplate(this),
                new MultiplyNegativeOneTemplate(this),
                new MultiplyOneTemplate(this),
                new MultiplyReciprocalDivisionTemplate(this),
                new MultiplyZeroTemplate(this),
                new NegateConstantTemplate(this),
                new NegateNegateTemplate(),
                new ReciprocalReciprocalSquareRootTemplate(this),
                new ReciprocalSquareRootTemplate(),
                new SubtractNegateTemplate(),
                new SubtractZeroTemplate(this)
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
            return ReduceDepthFirst(node);
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

        private HlslTreeNode ReduceDepthFirst(HlslTreeNode node)
        {
            if (IsConstant(node) || IsRegister(node))
            {
                return node;
            }
            for (int i = 0; i < node.Inputs.Count; i++)
            {
                HlslTreeNode input = node.Inputs[i];
                node.Inputs[i] = ReduceDepthFirst(input);
            }
            foreach (INodeTemplate template in _templates)
            {
                if (template.Match(node))
                {
                    var replacement = template.Reduce(node);
                    Replace(node, replacement);
                    return ReduceDepthFirst(replacement);
                }
            }
            foreach (IGroupTemplate template in _groupTemplates)
            {
                IGroupContext groupContext = template.Match(node);
                if (groupContext != null)
                {
                    var replacement = template.Reduce(node, groupContext);
                    Replace(node, replacement);
                    return ReduceDepthFirst(replacement);
                }
            }
            return node;
        }

        private void Replace(HlslTreeNode node, HlslTreeNode with)
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

        public bool IsConstant(HlslTreeNode node)
        {
            return node is ConstantNode;
        }

        public bool IsZero(HlslTreeNode node)
        {
            return node is ConstantNode constant && constant.Value == 0;
        }

        public bool IsOne(HlslTreeNode node)
        {
            return node is ConstantNode constant && constant.Value == 1;
        }

        public bool IsNegativeOne(HlslTreeNode node)
        {
            return node is ConstantNode constant && constant.Value == -1;
        }

        public bool IsNegative(HlslTreeNode node)
        {
            return node is ConstantNode constant && constant.Value < 0;
        }

        private bool IsRegister(HlslTreeNode node)
        {
            return node is RegisterInputNode;
        }
    }
}
