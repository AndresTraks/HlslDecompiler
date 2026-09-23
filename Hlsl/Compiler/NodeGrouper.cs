using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public class NodeGrouper
{
    private readonly RegisterState _registers;

    public NodeGrouper(RegisterState registers)
    {
        MatrixMultiplicationGrouper = new MatrixMultiplicationGrouper(registers);
        NormalizeGrouper = new NormalizeGrouper();
        CrossProductGrouper = new CrossProductGrouper();
        ReflectGrouper = new ReflectGrouper();
        _registers = registers;
    }

    public MatrixMultiplicationGrouper MatrixMultiplicationGrouper { get; }
    public NormalizeGrouper NormalizeGrouper { get; }
    public CrossProductGrouper CrossProductGrouper { get; }
    public ReflectGrouper ReflectGrouper { get; }

    public IList<IList<HlslTreeNode>> GroupComponents(List<HlslTreeNode> nodes)
    {
        switch (nodes.Count)
        {
            case 0:
            case 1:
                return [nodes];
        }

        List<IList<HlslTreeNode>> groups;

        var multiplicationGroup = MatrixMultiplicationGrouper.TryGetMultiplicationGroup(nodes);
        if (multiplicationGroup != null)
        {
            int dimension = multiplicationGroup.MatrixRowCount;
            groups = [nodes.Take(dimension).ToList()];
            if (dimension < nodes.Count)
            {
                List<HlslTreeNode> rest = nodes.Skip(dimension).ToList();
                groups.AddRange(GroupComponents(rest));
            }
            return groups;
        }

        var normalizeGroup = NormalizeGrouper.TryGetContext(nodes);
        if (normalizeGroup != null)
        {
            int dimension = normalizeGroup.Length;
            groups = [nodes.Take(dimension).ToList()];
            if (dimension < nodes.Count)
            {
                List<HlslTreeNode> rest = nodes.Skip(dimension).ToList();
                groups.AddRange(GroupComponents(rest));
            }
            return groups;
        }

        if (nodes.Count >= 3 && CrossProductGrouper.TryGetContext(nodes.Take(3).ToList()) != null)
        {
            groups = [nodes.Take(3).ToList()];
            if (nodes.Count > 3)
            {
                groups.AddRange(GroupComponents(nodes.Skip(3).ToList()));
            }
            return groups;
        }

        groups = [];

        int groupStart = 0;
        int nodeIndex;
        for (nodeIndex = 1; nodeIndex < nodes.Count; nodeIndex++)
        {
            HlslTreeNode node1 = nodes[groupStart];
            HlslTreeNode node2 = nodes[nodeIndex];
            if (CanGroupComponents(node1, node2) == false)
            {
                groups.Add(nodes.GetRange(groupStart, nodeIndex - groupStart));
                groupStart = nodeIndex;
            }
        }
        groups.Add(nodes.GetRange(groupStart, nodeIndex - groupStart));
        return groups;
    }

    // Returns true if children differ at most by component index, meaning they can be combined, for example:
    // n1 = a.x + b.x
    // n2 = a.y + b.y
    // =>
    // n.xy = a.xy + b.xy
    // n = a + b
    public bool CanGroupComponents(HlslTreeNode node1, HlslTreeNode node2, bool allowMatrixColumn = false)
    {
        // Two components reading the one node hold the one value, so they combine
        // into a broadcast whatever the node is. Worth answering before the checks
        // below, which reject whole node kinds rather than compare them.
        if (ReferenceEquals(node1, node2))
        {
            return true;
        }

        // A component whose constant addend was folded away is still a component of
        // the same instruction. `def c, 1, 0, 3, 4` added to a register writes four
        // components with one add; the zero is arithmetic to AddZeroTemplate and
        // nothing in a ConstantNode says it was holding the four together, so the
        // sibling that lost it is recognised here instead.
        if (GroupsWithFoldedAddend(node1, node2) || GroupsWithFoldedAddend(node2, node1)
            || GroupsWithFoldedFactor(node1, node2) || GroupsWithFoldedFactor(node2, node1))
        {
            return true;
        }

        if (!Operation.IsSameKind(node1, node2))
        {
            return false;
        }

        // An attribute evaluation takes the input register itself and nothing else -
        // fxc rejects an expression there, with an internal compiler error rather
        // than a message - so two of them only group when they are one instruction.
        // Two eval_centroid over different attributes would otherwise have become
        // one call over a float4 constructed from both.
        if (node1 is EvaluateAttributeOperation
            && !HlslTreeNode.IsSameInstruction(node1, node2))
        {
            return false;
        }

        if (node1 is ConstantNode)
        {
            return true;
        }

        if (node1 is RegisterInputNode input1 &&
            node2 is RegisterInputNode input2)
        {
            if (input1.RegisterComponentKey.RegisterKey.TypeEquals(input2.RegisterComponentKey.RegisterKey))
            {
                // The whole key, not just the number: for a constant buffer the number
                // is the buffer and the element is the offset, so cb0[4] and cb0[5]
                // share a number. Two different constants then grouped as one register
                // and `right.x * c.x + up.x * c.y` came out as dot(right.xx, c).
                if (input1.RegisterComponentKey.RegisterKey.Equals(
                    input2.RegisterComponentKey.RegisterKey))
                {
                    // And within a register, the same variable: fxc packs
                    // `float roughness; float metallic;` into cb0[1].xy, and a mul
                    // over the pair grouped as one read, which was named after the
                    // first - `t3.yz * roughness`, where the y of it was metallic.
                    return IsSameVariable(input1, input2);
                }

                if (allowMatrixColumn)
                {
                    return SharesMatrixColumnOrRow(input1, input2);
                }
            }
            return false;
        }

        if (node1 is RelativeAddressNode relative1 && node2 is RelativeAddressNode relative2)
        {
            // The index has to be the same node and not merely one that groups:
            // c0[a0.x] and c0[a0.y] are two elements of the array rather than two
            // components of one element. Left to the general rule below, the two
            // index expressions group as components and three bone lookups come out
            // as one.
            return relative1.RegisterComponentKey.RegisterKey.Equals(
                    relative2.RegisterComponentKey.RegisterKey)
                && ReferenceEquals(relative1.Index, relative2.Index);
        }

        if (node1 is DotProductOperation)
        {
            // Two dot products are components of one thing only when they are two
            // rows of one matrix against one vector - a matrix multiply. Anything
            // looser groups unrelated rows, and two bone matrices merged as though
            // they were rows of one. Recognising the pair here is what lets a per
            // bone blend - `mul(p, bones[0]) * w.x + mul(p, bones[1]) * w.y` - group
            // at all. Whether the rows then make a whole matrix in order is for the
            // multiplication grouper at compile time; a run it does not take is
            // written dot by dot.
            return node2 is DotProductOperation dot2
                && MatrixMultiplicationGrouper.AreRowsOfOneMatrix((DotProductOperation)node1, dot2);
        }

        if (node1 is TempAssignmentNode assignment1 && node2 is TempAssignmentNode assignment2)
        {
            if (assignment1.TempVariable.IsInputOf(assignment2.Value) || assignment2.TempVariable.IsInputOf(assignment1.Value))
            {
                return false;
            }
            // Variables that disagree about their type do not share a declaration.
            // Before they are numbered two temp variables compare equal, so without
            // this a running sum and a count of the samples it took are declared as
            // one float2 and the count is carried as a float.
            if (assignment1.TempVariable.IsInteger != assignment2.TempVariable.IsInteger
                || assignment1.TempVariable.IsBits != assignment2.TempVariable.IsBits)
            {
                return false;
            }
            return CanGroupComponents(assignment1.TempVariable, assignment2.TempVariable);
        }

        if (node1 is TempVariableNode tempVariable1 && node2 is TempVariableNode tempVariable2)
        {
            return tempVariable1.DeclarationIndex == tempVariable2.DeclarationIndex;
        }

        if (node1 is ComparisonNode comparison1 && node2 is ComparisonNode comparison2)
        {
            return CanGroupComponents(comparison1.Left, comparison2.Left)
                && CanGroupComponents(comparison1.Right, comparison2.Right)
                && comparison1.Comparison == comparison2.Comparison;
        }

        if (node1 is TextureLoadOutputNode textureload1 && node2 is TextureLoadOutputNode textureload2)
        {
            if (textureload1.Controls != textureload2.Controls)
            {
                return false;
            }
        }

        if (node1 is IHasComponentIndex ||
            node1 is GroupNode ||
            node1 is Operation)
        {
            if (node1.Inputs.Count == node2.Inputs.Count)
            {
                for (int i = 0; i < node1.Inputs.Count; i++)
                {
                    if (!CanGroupComponents(node1.Inputs[i], node2.Inputs[i]))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether an add of a constant is the sibling of a bare node that the same add
    /// would have covered, had its constant not been zero.
    /// </summary>
    private bool GroupsWithFoldedAddend(HlslTreeNode node1, HlslTreeNode node2)
    {
        if (node1 is not AddOperation add || node2 is AddOperation)
        {
            return false;
        }
        // Only where the sibling is a plain read. The shape being recovered is a
        // constant added to a whole register - `add r, c, v` - and there the
        // component that lost its zero is a register or a variable. Where it is an
        // expression, grouping buys nothing and the zero costs an instruction:
        // screen_position's `(int)x + 1` beside `(int)y` went from 14 to 15 as a
        // two wide add of int2(1, 0).
        if (node2 is not (TempVariableNode or RegisterInputNode))
        {
            return false;
        }
        if (add.Addend2 is ConstantNode)
        {
            return CanGroupComponents(add.Addend1, node2);
        }
        return add.Addend1 is ConstantNode && CanGroupComponents(add.Addend2, node2);
    }

    /// <summary>
    /// The same for a multiply, where the one that MultiplyOneTemplate folded away
    /// is what held the components together. `mul r, c, v` with a 1 in one
    /// component of c writes the whole register in one instruction.
    /// </summary>
    private bool GroupsWithFoldedFactor(HlslTreeNode node1, HlslTreeNode node2)
    {
        if (node1 is not MultiplyOperation multiply
            || node2 is MultiplyOperation
            || node2 is not (TempVariableNode or RegisterInputNode))
        {
            return false;
        }
        if (multiply.Factor2 is ConstantNode)
        {
            return CanGroupComponents(multiply.Factor1, node2);
        }
        return multiply.Factor1 is ConstantNode && CanGroupComponents(multiply.Factor2, node2);
    }

    public bool SharesMatrixColumnOrRow(RegisterInputNode input1, RegisterInputNode input2)
    {
        return SharesMatrixColumn(input1, input2)
            || SharesMatrixRow(input1, input2);
    }

    public bool SharesMatrixColumn(RegisterInputNode input1, RegisterInputNode input2)
    {
        if (input1.RegisterComponentKey.ComponentIndex !=
            input2.RegisterComponentKey.ComponentIndex)
        {
            return false;
        }

        ConstantDeclaration constant = _registers.FindConstant(input1);
        if (constant == null)
        {
            return false;
        }
        if (!IsMatrixConstantRegister(constant))
        {
            // A matrix member of a struct: the declaration says Struct for every
            // member it has, so what is at the register decides - and the two have
            // to be in the one member of the one element, not merely in the one
            // struct, or two matrices side by side gather as though they were one.
            return _registers.TryGetStructMatrixAt(
                    input1.RegisterComponentKey, constant, out StructMemberAccess member1, out int element1)
                && constant == _registers.FindConstant(input2)
                && _registers.TryGetStructMatrixAt(
                    input2.RegisterComponentKey, constant, out StructMemberAccess member2, out int element2)
                && member1.Name == member2.Name
                && element1 == element2;
        }
        if (constant is D3D9ConstantDeclaration d3d9Constant)
        {
            int constIndex2 = input2.RegisterComponentKey.Number;
            return d3d9Constant.ContainsIndex(constIndex2);
        }
        return constant == _registers.FindConstant(input2);
    }

    public bool SharesMatrixRow(RegisterInputNode input1, RegisterInputNode input2)
    {
        if (input1.RegisterComponentKey.RegisterKey !=
            input2.RegisterComponentKey.RegisterKey)
        {
            return false;
        }

        var constantRegister = _registers.FindConstant(input1);
        return constantRegister != null
            && IsMatrixConstantRegister(constantRegister);
    }

    // Whether two components of one register belong to one declared variable, where
    // fxc packs several into it: a constant buffer's `roughness` and `metallic`, or
    // an input register's TEXCOORD0 and TEXCOORD1. Any other register is one value.
    private bool IsSameVariable(RegisterInputNode input1, RegisterInputNode input2)
    {
        if (input1.RegisterComponentKey.RegisterKey is not D3D10RegisterKey key)
        {
            return true;
        }
        if (key.OperandType == OperandType.ConstantBuffer)
        {
            ConstantDeclaration first = _registers.FindConstant(key, input1.RegisterComponentKey.ComponentIndex);
            ConstantDeclaration second = _registers.FindConstant(key, input2.RegisterComponentKey.ComponentIndex);
            return first == null || second == null || ReferenceEquals(first, second);
        }
        // An input register packs two variables the way a constant does - TEXCOORD0 at
        // v0.xy and TEXCOORD1 at v0.zw - and grouped as one read they were named after
        // the first and rebased onto it. Where a later read component belongs to the
        // earlier-packed variable, that rebase runs below its base: `float3(uv2, uv1.x)`
        // over v0.zwx tried to name texcoord1's component minus two, off the end.
        if (key.OperandType == OperandType.Input)
        {
            return _registers.IsSameInputVariable(key,
                input1.RegisterComponentKey.ComponentIndex,
                input2.RegisterComponentKey.ComponentIndex);
        }
        return true;
    }

    private static bool IsMatrixConstantRegister(ConstantDeclaration constantRegister)
    {
        return constantRegister.TypeInfo.ParameterClass == ParameterClass.MatrixColumns
            || constantRegister.TypeInfo.ParameterClass == ParameterClass.MatrixRows;
    }

    public static bool AreNodesEquivalent(HlslTreeNode node1, HlslTreeNode node2)
    {
        if (!Operation.IsSameKind(node1, node2))
        {
            return false;
        }

        if (node1 is ConstantNode constant1 &&
            node2 is ConstantNode constant2)
        {
            return constant1.Value == constant2.Value;
        }

        if (node1 is RegisterInputNode input1 &&
            node2 is RegisterInputNode input2)
        {
            return input1.RegisterComponentKey.Equals(input2.RegisterComponentKey);
        }

        if (node1 is Operation operation1 &&
            node2 is Operation operation2)
        {
            if (operation1 is AddOperation add1 &&
                operation2 is AddOperation add2)
            {
                return (AreNodesEquivalent(add1.Addend1, add2.Addend1) && AreNodesEquivalent(add1.Addend2, add2.Addend2))
                    || (AreNodesEquivalent(add1.Addend1, add2.Addend2) && AreNodesEquivalent(add1.Addend2, add2.Addend1));
            }
            else if (operation1 is MultiplyOperation multiply1 &&
                     operation2 is MultiplyOperation multiply2)
            {
                return (AreNodesEquivalent(multiply1.Factor1, multiply2.Factor1) && AreNodesEquivalent(multiply1.Factor2, multiply2.Factor2))
                    || (AreNodesEquivalent(multiply1.Factor1, multiply2.Factor2) && AreNodesEquivalent(multiply1.Factor2, multiply2.Factor1));
            }
        }

        // Two components of one multi output node share every input and are not
        // the same value: normalize(n).x is not normalize(n).z. Comparing inputs
        // alone said they were, and the cross product grouper - which finds the
        // vectors by asking which factor of a product is which - took the first
        // pairing that passed, and wrote cross(float3(t.x, n.yz), t.zyx).
        if (node1 is IHasComponentIndex indexed1 && node2 is IHasComponentIndex indexed2
            && indexed1.ComponentIndex != indexed2.ComponentIndex)
        {
            return false;
        }

        // A variable has no inputs to compare, so two of them compared equal
        // whatever they were - t0.x was t3.x. Each component of a variable is one
        // node that every reader points at, so the node itself says which it is.
        if (node1 is TempVariableNode variable1 && node2 is TempVariableNode variable2)
        {
            return ReferenceEquals(variable1, variable2)
                || (variable1.DeclarationIndex != null
                    && variable1.DeclarationIndex == variable2.DeclarationIndex);
        }

        if ((node1 is IHasComponentIndex) ||
            (node1 is GroupNode) ||
            (node1 is Operation))
        {
            if (node1.Inputs.Count == node2.Inputs.Count)
            {
                for (int i = 0; i < node1.Inputs.Count; i++)
                {
                    if (!AreNodesEquivalent(node1.Inputs[i], node2.Inputs[i]))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        return false;
    }

    public static bool AreNodesEquivalent(ICollection<HlslTreeNode> nodes1, ICollection<HlslTreeNode> nodes2)
    {
        if (nodes1.Count != nodes2.Count)
        {
            return false;
        }
        for (int i = 0; i < nodes1.Count; i++)
        {
            if (!AreNodesEquivalent(nodes1.ElementAt(i), nodes2.ElementAt(i)))
            {
                return false;
            }
        }
        return true;
    }
}
