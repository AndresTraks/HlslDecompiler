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
            // Shape is checked against the first node; dependencies against every
            // member, since one statement's lanes all read the old values.
            if (CanGroupComponents(node1, node2) == false
                || ReadsWithinGroup(nodes, groupStart, nodeIndex))
            {
                groups.Add(nodes.GetRange(groupStart, nodeIndex - groupStart));
                groupStart = nodeIndex;
            }
        }
        groups.Add(nodes.GetRange(groupStart, nodeIndex - groupStart));
        return groups;
    }

    // Whether the assignment at nodeIndex and any group member (the first included)
    // need each other's new value, which one statement cannot give. Reading another
    // lane's old value is fine: `t1.xywz = float4(t1.xyz, t4)`.
    private static bool ReadsWithinGroup(List<HlslTreeNode> nodes, int groupStart, int nodeIndex)
    {
        if (nodes[nodeIndex] is not TempAssignmentNode added)
        {
            return false;
        }
        for (int i = groupStart; i < nodeIndex; i++)
        {
            if (nodes[i] is TempAssignmentNode member
                && (added.DependsOnNewValueOf.Contains(member) || member.DependsOnNewValueOf.Contains(added)))
            {
                return true;
            }
        }
        return false;
    }

    // Whether children differ at most by component index, so `a.x + b.x` and
    // `a.y + b.y` combine into `a.xy + b.xy`.
    public bool CanGroupComponents(HlslTreeNode node1, HlslTreeNode node2, bool allowMatrixColumn = false)
    {
        // The same node is a broadcast; answered before checks that reject by kind.
        if (ReferenceEquals(node1, node2))
        {
            return true;
        }

        // A component whose constant 0 addend or 1 factor was folded away is still
        // a component of the same instruction.
        if (GroupsWithFoldedAddend(node1, node2) || GroupsWithFoldedAddend(node2, node1)
            || GroupsWithFoldedFactor(node1, node2) || GroupsWithFoldedFactor(node2, node1))
        {
            return true;
        }

        if (!Operation.IsSameKind(node1, node2))
        {
            return false;
        }

        // An attribute evaluation must take the input register itself (fxc crashes
        // on an expression), so two group only when they are one instruction.
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
                // The whole key: cb0[4] and cb0[5] share a register number.
                if (input1.RegisterComponentKey.RegisterKey.Equals(
                    input2.RegisterComponentKey.RegisterKey))
                {
                    // fxc packs several variables into one register.
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
            // The same index node: c0[a0.x] and c0[a0.y] are two array elements.
            return relative1.RegisterComponentKey.RegisterKey.Equals(
                    relative2.RegisterComponentKey.RegisterKey)
                && ReferenceEquals(relative1.Index, relative2.Index)
                && ReferenceEquals(relative1.FieldIndex, relative2.FieldIndex);
        }

        // Lanes of one element of one local array: x0[i] and x1[i] are two values.
        if (node1 is IndexableTempLoadNode array1 && node2 is IndexableTempLoadNode array2)
        {
            return array1.Register == array2.Register && AreNodesEquivalent(array1.Index, array2.Index);
        }

        if (node1 is DotProductOperation)
        {
            // Only two rows of one matrix against one vector. Whether they make a
            // whole matrix is decided by the multiplication grouper later.
            return node2 is DotProductOperation dot2
                && MatrixMultiplicationGrouper.AreRowsOfOneMatrix((DotProductOperation)node1, dot2);
        }

        if (node1 is TempAssignmentNode assignment1 && node2 is TempAssignmentNode assignment2)
        {
            // One reading what the other writes needs two statements, unless one
            // instruction wrote both (it read every operand before writing).
            if ((assignment1.TempVariable.IsInputOf(assignment2.Value) || assignment2.TempVariable.IsInputOf(assignment1.Value))
                && !HlslTreeNode.IsSameInstruction(assignment1.Value, assignment2.Value))
            {
                return false;
            }
            // Unnumbered variables compare equal, so differing types must be checked.
            if (assignment1.TempVariable.IsInteger != assignment2.TempVariable.IsInteger
                || assignment1.TempVariable.IsBits != assignment2.TempVariable.IsBits
                || assignment1.TempVariable.IsBool != assignment2.TempVariable.IsBool)
            {
                return false;
            }
            return CanGroupComponents(assignment1.TempVariable, assignment2.TempVariable);
        }

        if (node1 is TempVariableNode tempVariable1 && node2 is TempVariableNode tempVariable2)
        {
            // Unnumbered variables compare equal too, so a declaration made from
            // this group must not join an integer to a float (int and uint may share).
            return tempVariable1.DeclarationIndex == tempVariable2.DeclarationIndex
                && (tempVariable1.DeclarationIndex != null
                    || (tempVariable1.IsInteger == tempVariable2.IsInteger
                        && tempVariable1.IsDouble == tempVariable2.IsDouble
                        && tempVariable1.IsBool == tempVariable2.IsBool));
        }

        if (node1 is ComparisonNode comparison1 && node2 is ComparisonNode comparison2)
        {
            return CanGroupComponents(comparison1.Left, comparison2.Left)
                && CanGroupComponents(comparison1.Right, comparison2.Right)
                && comparison1.Comparison == comparison2.Comparison;
        }

        // Components of one sample have equivalent operands, not merely groupable
        // ones: uv and uv * 2 group, but are two samples.
        if (node1 is TextureLoadOutputNode textureload1 && node2 is TextureLoadOutputNode textureload2)
        {
            return textureload1.Controls == textureload2.Controls
                && SameSampleOffsets(textureload1.SampleOffsets, textureload2.SampleOffsets)
                && ReadsSameTexel(textureload1, textureload2,
                    textureload1.Texture, textureload2.Texture);
        }

        if (node1 is ResourceLoadNode resourceLoad1 && node2 is ResourceLoadNode resourceLoad2)
        {
            return SameSampleOffsets(resourceLoad1.SampleOffsets, resourceLoad2.SampleOffsets)
                && ReadsSameTexel(resourceLoad1, resourceLoad2,
                    resourceLoad1.Resource, resourceLoad2.Resource);
        }

        // Constants always group as components, so scalar operands that are not
        // components (sample index, subscript, offset, address) need equality here.
        if (node1 is SamplePositionNode position1 && node2 is SamplePositionNode position2
            && !AreNodesEquivalent(position1.SampleIndex, position2.SampleIndex))
        {
            return false;
        }

        if (node1 is VectorComponentNode select1 && node2 is VectorComponentNode select2
            && !ReferenceEquals(select1.Index, select2.Index))
        {
            return false;
        }

        if (node1 is LoadStructuredNode load1 && node2 is LoadStructuredNode load2)
        {
            if (load1.ElementByteOffset != load2.ElementByteOffset)
            {
                return false;
            }
            if (!ReferenceEquals(load1.Address, load2.Address))
            {
                return false;
            }
        }

        // Components of one call share operands by reference; the generic rule
        // would merge `mul(p.xy, M)` with `mul(p.yx, M)`.
        if (node1 is NormalizeOutputNode or ReflectOutputNode or MatrixMultiplyOutputNode)
        {
            return node1.Inputs.Count == node2.Inputs.Count
                && node1.Inputs
                    .Zip(node2.Inputs)
                    .All(operands => ReferenceEquals(operands.First, operands.Second));
        }

        // Components of one interface call: same interface, instance, method, instruction.
        if (node1 is InterfaceCallNode call1 && node2 is InterfaceCallNode call2)
        {
            return HlslTreeNode.IsSameInstruction(node1, node2)
                && call1.InterfaceNumber == call2.InterfaceNumber
                && call1.InstanceIndex == call2.InstanceIndex
                && call1.FunctionIndex == call2.FunctionIndex;
        }

        // Lanes of one conversion are one cast, whatever they convert: fxc keeps
        // `(float2)uint2(a & 1, a >> 1)` one utof, and the arithmetic after it one op.
        if (node1 is ConvertOperation convert1 && node2 is ConvertOperation convert2
            && HlslTreeNode.IsSameInstruction(node1, node2)
            && convert1.TargetType == convert2.TargetType
            && convert1.SourceUnsigned == convert2.SourceUnsigned)
        {
            return true;
        }

        // Lanes of one movc on one condition are one select, whatever the arms are.
        if (node1 is MoveConditionalOperation movc1 && node2 is MoveConditionalOperation movc2
            && HlslTreeNode.IsSameInstruction(node1, node2)
            && (ReferenceEquals(movc1.Condition, movc2.Condition)
                || AreNodesEquivalent(movc1.Condition, movc2.Condition)))
        {
            return true;
        }

        // Dots scaled by components of one vector: `float3(dot(a, n), ...) * c.xyz`.
        if (node1 is MultiplyOperation product1 && node2 is MultiplyOperation product2)
        {
            for (int dotSide = 0; dotSide < 2; dotSide++)
            {
                if (product1.Inputs[dotSide] is DotProductOperation
                    && product2.Inputs[dotSide] is DotProductOperation
                    && !ReferenceEquals(product1.Inputs[dotSide], product2.Inputs[dotSide])
                    && product1.Inputs[1 - dotSide] is not DotProductOperation
                    && CanGroupComponents(product1.Inputs[1 - dotSide], product2.Inputs[1 - dotSide]))
                {
                    return true;
                }
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
        // Only a plain read (`add r, c, v`); for an expression the zero costs an
        // instruction.
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
    /// Whether two sample output components read the one texel. The texture is
    /// compared by register only: its component is the channel read, which differs.
    /// </summary>
    private static bool ReadsSameTexel(
        HlslTreeNode load1, HlslTreeNode load2,
        HlslTreeNode resource1, HlslTreeNode resource2)
    {
        if (load1.Inputs.Count != load2.Inputs.Count)
        {
            return false;
        }
        for (int i = 0; i < load1.Inputs.Count; i++)
        {
            HlslTreeNode operand1 = load1.Inputs[i];
            HlslTreeNode operand2 = load2.Inputs[i];
            if (ReferenceEquals(operand1, resource1) || ReferenceEquals(operand2, resource2))
            {
                if (operand1 is not RegisterInputNode texture1
                    || operand2 is not RegisterInputNode texture2
                    || !texture1.RegisterComponentKey.RegisterKey.Equals(
                        texture2.RegisterComponentKey.RegisterKey))
                {
                    return false;
                }
                continue;
            }
            // Reference check first: AreNodesEquivalent is not reflexive (e.g. a phi).
            if (!ReferenceEquals(operand1, operand2)
                && !AreNodesEquivalent(operand1, operand2))
            {
                return false;
            }
        }
        return true;
    }

    private static bool SameSampleOffsets(int[] offsets1, int[] offsets2)
    {
        if (offsets1 == null || offsets2 == null)
        {
            return offsets1 == null && offsets2 == null;
        }
        return offsets1.SequenceEqual(offsets2);
    }

    /// <summary>
    /// The same for a multiply whose constant factor of 1 was folded away.
    /// </summary>
    private bool GroupsWithFoldedFactor(HlslTreeNode node1, HlslTreeNode node2)
    {
        // Not a constant (a component of nothing) or a sum: a product beside a sum
        // would merge into one wrong expression.
        if (node1 is not MultiplyOperation multiply
            || node2 is MultiplyOperation or ConstantNode or AddOperation or SubtractOperation
                or MultiplyAddOperation)
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

        // Register type first: t1 must not match a matrix in c0-c1 by number.
        if (!input1.RegisterComponentKey.RegisterKey.TypeEquals(input2.RegisterComponentKey.RegisterKey))
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
            // A struct's matrix member: both must be in the same member and element.
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

    // Whether two components of one register belong to one declared variable, for
    // constant buffer and input registers where fxc packs several.
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

        // The sample count is one value in every component, despite its index.
        if (node1 is RenderTargetSampleCountNode && node2 is RenderTargetSampleCountNode)
        {
            return true;
        }

        // Components of one multi-output node share inputs but differ:
        // normalize(n).x is not normalize(n).z.
        if (node1 is IHasComponentIndex indexed1 && node2 is IHasComponentIndex indexed2
            && indexed1.ComponentIndex != indexed2.ComponentIndex)
        {
            return false;
        }

        // Base register too: cb0[idx + 0] and cb0[idx + 1] share their index.
        if (node1 is RelativeAddressNode relative1 && node2 is RelativeAddressNode relative2)
        {
            return relative1.RegisterComponentKey.Equals(relative2.RegisterComponentKey)
                && AreNodesEquivalent(relative1.Index, relative2.Index)
                && (relative1.FieldIndex == null || relative2.FieldIndex == null
                    ? relative1.FieldIndex == relative2.FieldIndex
                    : AreNodesEquivalent(relative1.FieldIndex, relative2.FieldIndex));
        }

        if (node1 is IndexableTempLoadNode array1 && node2 is IndexableTempLoadNode array2)
        {
            return array1.Register == array2.Register && array1.ComponentIndex == array2.ComponentIndex
                && AreNodesEquivalent(array1.Index, array2.Index);
        }

        // A variable has no inputs, so the generic rule would call any two equal.
        if (node1 is TempVariableNode variable1 && node2 is TempVariableNode variable2)
        {
            return ReferenceEquals(variable1, variable2)
                || (variable1.DeclarationIndex != null
                    && variable1.DeclarationIndex == variable2.DeclarationIndex);
        }

        if (node1 is ComparisonNode comparison1 && node2 is ComparisonNode comparison2)
        {
            return comparison1.Comparison == comparison2.Comparison
                && comparison1.IsInteger == comparison2.IsInteger
                && comparison1.IsUnsigned == comparison2.IsUnsigned
                && comparison1.IsBitsTest == comparison2.IsBitsTest
                && AreNodesEquivalent(comparison1.Left, comparison2.Left)
                && AreNodesEquivalent(comparison1.Right, comparison2.Right);
        }

        if (node1 is InterfaceCallNode call1 && node2 is InterfaceCallNode call2)
        {
            return call1.InterfaceNumber == call2.InterfaceNumber
                && call1.InstanceIndex == call2.InstanceIndex
                && call1.FunctionIndex == call2.FunctionIndex
                && call1.HasIndex == call2.HasIndex
                && (!call1.HasIndex || AreNodesEquivalent(call1.Index, call2.Index))
                && AreNodesEquivalent([.. call1.Arguments], [.. call2.Arguments]);
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
