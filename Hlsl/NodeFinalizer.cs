using HlslDecompiler.DirectXShaderModel;
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

    // Lanes of one instruction must be swapped alike or not at all, or the two
    // sides stop lining up. Asks what the siblings are, not whether they still
    // want swapping: an already swapped lane no longer would.
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

    // Components of one value go lower first (`t0.x + t0.y`); fxc's operand
    // order varies between compiles.
    private static bool IsCommutative(HlslTreeNode node) =>
        node is AddOperation or MultiplyOperation or MinimumOperation or MaximumOperation
            or BitwiseAndOperation or BitwiseOrOperation or BitwiseXorOperation;

    private static (int, int)? ComponentsOfOneValue(HlslTreeNode a, HlslTreeNode b)
    {
        if (a is not IHasComponentIndex first || b is not IHasComponentIndex second)
        {
            return null;
        }
        if (a is RegisterInputNode { RegisterComponentKey.RegisterKey: D3D10RegisterKey { GSVertex: int vertex1 } key1 }
            && b is RegisterInputNode { RegisterComponentKey.RegisterKey: D3D10RegisterKey { GSVertex: int vertex2 } key2 }
            && vertex1 != vertex2
            && first.ComponentIndex == second.ComponentIndex
            && key1.OperandType == key2.OperandType && key1.Number == key2.Number)
        {
            // One attribute of two vertices: patch[0].position + patch[1].position.
            return (vertex1, vertex2);
        }
        bool oneValue = (a, b) switch
        {
            (RegisterInputNode x, RegisterInputNode y) =>
                x.RegisterComponentKey.RegisterKey.Equals(y.RegisterComponentKey.RegisterKey),
            (TempVariableNode x, TempVariableNode y) =>
                x.DeclarationIndex != null && x.DeclarationIndex == y.DeclarationIndex,
            (RegisterInputNode, _) or (_, RegisterInputNode) or (TempVariableNode, _) or (_, TempVariableNode) => false,
            // Loads etc. are one value when one instruction made them.
            _ => a.GetType() == b.GetType() && HlslTreeNode.IsSameInstruction(a, b),
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
