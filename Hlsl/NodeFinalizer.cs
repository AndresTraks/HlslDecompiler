using HlslDecompiler.Hlsl.FlowControl;
using HlslDecompiler.Operations;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class NodeFinalizer
    {
        private IList<HlslTreeNode> _nodes;

        private NodeFinalizer(IList<HlslTreeNode> nodes)
        {
            _nodes = nodes;
        }

        public static void Finalize(IList<HlslTreeNode> statements)
        {
            var finalizer = new NodeFinalizer(statements);
            finalizer.FinalizeNodes();
        }

        private void FinalizeNodes()
        {
            AdjustNodeInputOrder();
        }

        private void AdjustNodeInputOrder()
        {
            new NodeVisitor(_nodes).Visit(node =>
            {
                if (node is DotProductOperation dot &&
                    dot.X.Inputs.All(x => !IsConstant(x)) &&
                    dot.Y.Inputs.All(y => IsConstant(y)))
                {
                    SwapInputs(dot);
                }
                else if (node is AddOperation add &&
                    IsConstant(add.Addend1) &&
                    !IsConstant(add.Addend2))
                {
                    SwapInputs(add);
                }
            });
        }

        private static bool IsConstant(HlslTreeNode node)
        {
            return node is RegisterInputNode r && r.RegisterComponentKey.RegisterKey.IsConstant;
        }

        private static void SwapInputs(HlslTreeNode node)
        {
            var temp = node.Inputs[0];
            node.Inputs[0] = node.Inputs[1];
            node.Inputs[1] = temp;
        }
    }
}
