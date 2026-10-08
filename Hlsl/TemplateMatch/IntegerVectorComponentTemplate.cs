using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// An integer vector read at a computed component. A float vector is dotted with a
/// row of an identity (see VectorComponentNode), but a dot is no use on integers,
/// so fxc masks each component with a test of the index and ors them together:
/// `blendindices[i]` became
/// `((i < 3) == 0 ? w : 0) | ((i < 2 ? 0 : i - 3) & z) | ((i < 2 ? -i : 0) & y) | ((i < 1) & x)`,
/// each mask all ones for its own component and zero for the rest.
///
/// The masks are fxc's to choose and differ from component to component, so they
/// are not matched by shape. The or is worked out instead for each index the
/// vector has, with its components standing for values that cannot be mistaken
/// for one another, and is the subscript where every index gives back the
/// component it names - which is what the subscript is, over the indices that
/// read anything.
/// </summary>
public class IntegerVectorComponentTemplate : NodeTemplate<BitwiseOrOperation>
{
    // Two sets, so that a mask agreeing with one by chance is not taken for a
    // selection.
    private static readonly int[][] StandIns =
    [
        [0x13579BDF, 0x2468ACE0, 0x0F1E2D3C, 0x7A6B5C4D],
        [unchecked((int)0xFEDCBA98), 0x01234567, 0x55AA33CC, unchecked((int)0x8001F00F)],
    ];

    // The whole of the or, and not a part of it. The templates reduce from the
    // leaves up, and `(y & m1) | (x & m0)` is x and y picked by an index of 0 or 1
    // - and zero past them, where `blendindices.xy[i]` reads out of range. Only the
    // or with all of the vector in it agrees with the subscript wherever it is
    // defined.
    public override bool Match(HlslTreeNode node)
    {
        return node is BitwiseOrOperation
            && !node.Outputs.Any(output => output is BitwiseOrOperation)
            && Find(node) != null;
    }

    public override HlslTreeNode Reduce(BitwiseOrOperation node)
    {
        (RegisterInputNode[] vector, HlslTreeNode index) = Find(node).Value;
        return new VectorComponentNode(new GroupNode(vector), index);
    }

    private static (RegisterInputNode[], HlslTreeNode)? Find(HlslTreeNode or)
    {
        var reads = new List<RegisterInputNode>();
        var others = new List<HlslTreeNode>();
        if (!CollectLeaves(or, reads, others, HlslTreeNode.NewNodeSet()) || reads.Count == 0)
        {
            return null;
        }
        RegisterKey register = reads[0].RegisterComponentKey.RegisterKey;
        if (reads.Any(read => !read.RegisterComponentKey.RegisterKey.Equals(register)))
        {
            return null;
        }
        int width = reads.Max(read => read.ComponentIndex) + 1;
        var vector = new RegisterInputNode[width];
        foreach (RegisterInputNode read in reads)
        {
            vector[read.ComponentIndex] = read;
        }
        if (width < 2 || vector.Any(read => read == null))
        {
            return null;
        }
        HlslTreeNode index = others.FirstOrDefault();
        if (index == null || others.Any(other => !IsSameValue(other, index)))
        {
            return null;
        }
        foreach (int[] standIns in StandIns)
        {
            for (int component = 0; component < width; component++)
            {
                int? value = Evaluate(or, node => node is RegisterInputNode read
                    && read.RegisterComponentKey.RegisterKey.Equals(register)
                        ? standIns[read.ComponentIndex]
                        : IsSameValue(node, index) ? component : null);
                if (value != standIns[component])
                {
                    return null;
                }
            }
        }
        return (vector, index);
    }

    // The values the expression is made of: reads of an input or a constant, and the
    // one value that is not - the index. False where something in it cannot be
    // worked out at all.
    private static bool CollectLeaves(HlslTreeNode node, List<RegisterInputNode> reads,
        List<HlslTreeNode> others, HashSet<HlslTreeNode> seen)
    {
        if (!seen.Add(node))
        {
            return true;
        }
        switch (node)
        {
            case ConstantNode constant:
                return IntegerOf(constant) != null;
            case RegisterInputNode read when read.RegisterComponentKey.RegisterKey is D3D10RegisterKey
                { OperandType: OperandType.Input or OperandType.ConstantBuffer }:
                reads.Add(read);
                return true;
            case TempVariableNode or PhiNode or RegisterInputNode:
                others.Add(node);
                return true;
            case BitwiseAndOperation or BitwiseOrOperation or BitwiseXorOperation
                or AddOperation or SubtractOperation or NegateOperation
                or MoveConditionalOperation or ComparisonNode
                or ConvertOperation { TargetType: "int" or "uint" }:
                return node.Inputs.All(input => CollectLeaves(input, reads, others, seen));
            default:
                return false;
        }
    }

    private static bool IsSameValue(HlslTreeNode a, HlslTreeNode b)
    {
        return ReferenceEquals(a, b)
            || (a is TempVariableNode x && b is TempVariableNode y
                && x.DeclarationIndex != null && x.DeclarationIndex == y.DeclarationIndex
                && x.ComponentIndex == y.ComponentIndex)
            || (a is RegisterInputNode r && b is RegisterInputNode s
                && r.RegisterComponentKey.Equals(s.RegisterComponentKey));
    }

    private static int? IntegerOf(ConstantNode constant)
    {
        if (constant.IntegerValue is int integer)
        {
            return integer;
        }
        return constant.Value == (int)constant.Value ? (int)constant.Value : null;
    }

    // The 32 bits the expression leaves, as the integer instructions it came from
    // would: a comparison is a mask, a movc tests its condition for any bit set.
    private static int? Evaluate(HlslTreeNode node, Func<HlslTreeNode, int?> leaf)
    {
        if (leaf(node) is int value)
        {
            return value;
        }
        int? Input(int i) => Evaluate(node.Inputs[i], leaf);
        unchecked
        {
            switch (node)
            {
                case ConstantNode constant:
                    return IntegerOf(constant);
                case BitwiseAndOperation:
                    return Input(0) & Input(1);
                case BitwiseOrOperation:
                    return Input(0) | Input(1);
                case BitwiseXorOperation:
                    return Input(0) ^ Input(1);
                case AddOperation:
                    return Input(0) + Input(1);
                case SubtractOperation:
                    return Input(0) - Input(1);
                case NegateOperation:
                    return -Input(0);
                case ConvertOperation:
                    return Input(0);
                case MoveConditionalOperation:
                    return Input(0) switch
                    {
                        null => null,
                        0 => Input(2),
                        _ => Input(1),
                    };
                case ComparisonNode comparison:
                    {
                        if (Input(0) is not int left || Input(1) is not int right)
                        {
                            return null;
                        }
                        bool? holds = comparison.IsUnsigned
                            ? Compare((uint)left, (uint)right, comparison.Comparison)
                            : Compare(left, right, comparison.Comparison);
                        return holds == null ? null : holds.Value ? -1 : 0;
                    }
                default:
                    return null;
            }
        }
    }

    private static bool? Compare<T>(T left, T right, IfComparison comparison)
        where T : IComparable<T>
    {
        int order = left.CompareTo(right);
        return comparison switch
        {
            IfComparison.GT => order > 0,
            IfComparison.EQ => order == 0,
            IfComparison.GE => order >= 0,
            IfComparison.LT => order < 0,
            IfComparison.NE => order != 0,
            IfComparison.LE => order <= 0,
            _ => null,
        };
    }
}
