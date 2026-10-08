using HlslDecompiler.Hlsl.FlowControl;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

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
            else if (WouldPutLowerComponentFirst(node) && SiblingLanesAgree(node))
            {
                SwapInputs(node);
            }
        });
    }

    private static bool WouldPutLowerComponentFirst(HlslTreeNode node) =>
        IsCommutative(node) && node.Inputs.Count == 2
            && ComponentsOfOneValue(node.Inputs[0], node.Inputs[1]) is (int first, int second)
            && first > second;

    // The other lanes of the instruction this one came from, found through the
    // operands they share, have to be put in order alike or not at all. A float4
    // multiplied by a broadcast texcoord1.x had only the lanes whose other operand
    // was texcoord1 too turned round, and the two sides no longer lined up:
    // float4(i.texcoord, i.texcoord1.xx) * i.texcoord1.xxzw. Asked of what the
    // siblings are - two components of one value each, so each ends lower first
    // whichever way it stands - and not of whether they still want turning: the
    // lanes are finalized one at a time, a lane already turned no longer wanted
    // it, and asked that way the lanes after it came out the other way round -
    // direction.xww, which is no subscript at all.
    private static bool SiblingLanesAgree(HlslTreeNode node)
    {
        return node.Inputs
            .SelectMany(input => input.Outputs)
            .Where(sibling => !ReferenceEquals(sibling, node)
                && sibling.GetType() == node.GetType()
                && HlslTreeNode.IsSameInstruction(sibling, node))
            .All(sibling => sibling.Inputs.Count == 2
                && ComponentsOfOneValue(sibling.Inputs[0], sibling.Inputs[1]) != null);
    }

    // Where the two sides of a commutative operation are components of the one
    // value, the lower component goes first: `t0.x + t0.y`, `texcoord.x &
    // texcoord.y`. Which comes first in the bytecode is the order fxc wrote the
    // operands in, so the text otherwise changed from one compile to the next
    // without the shader changing - and x before y is how it is written anyway.
    private static bool IsCommutative(HlslTreeNode node) =>
        node is AddOperation or MultiplyOperation or MinimumOperation or MaximumOperation
            or BitwiseAndOperation or BitwiseOrOperation or BitwiseXorOperation;

    private static (int, int)? ComponentsOfOneValue(HlslTreeNode a, HlslTreeNode b)
    {
        if (a is not IHasComponentIndex first || b is not IHasComponentIndex second)
        {
            return null;
        }
        bool oneValue = (a, b) switch
        {
            (RegisterInputNode x, RegisterInputNode y) =>
                x.RegisterComponentKey.RegisterKey.Equals(y.RegisterComponentKey.RegisterKey),
            (TempVariableNode x, TempVariableNode y) =>
                x.DeclarationIndex != null && x.DeclarationIndex == y.DeclarationIndex,
            (TextureLoadOutputNode or ResourceLoadNode, TextureLoadOutputNode or ResourceLoadNode) =>
                a.GetType() == b.GetType() && HlslTreeNode.IsSameInstruction(a, b),
            _ => false,
        };
        return oneValue ? (first.ComponentIndex, second.ComponentIndex) : null;
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
