using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using HlslDecompiler.Hlsl.TemplateMatch;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// Idioms recovered from the freshly parsed graph: a vector read at a computed
/// component, and an array index that counts registers instead of elements.
/// Must run before the statement finalizer, the type facts, and immediate typing,
/// since the products a subscript replaces stop being readers.
/// </summary>
internal sealed class ParsedIdioms
{
    private readonly RegisterState _registerState;
    private readonly IList<IList<IStatement>> _bodies;

    private ParsedIdioms(RegisterState registerState, IList<IList<IStatement>> bodies)
    {
        _registerState = registerState;
        _bodies = bodies;
    }

    public static void Recover(RegisterState registerState, IList<IList<IStatement>> bodies)
    {
        new ParsedIdioms(registerState, bodies).Recover();
    }

    private void Recover()
    {
        foreach (RelativeAddressNode read in Values().OfType<RelativeAddressNode>().ToList())
        {
            StripElementStride(read);
        }
        foreach (AddOperation sum in Values().OfType<AddOperation>().ToList())
        {
            // Already taken apart as part of an earlier sum.
            if (sum.Outputs.Count == 0 && !IsStatementValue(sum))
            {
                continue;
            }
            List<MultiplyOperation> products = Products(sum);
            if (products == null)
            {
                continue;
            }
            HlslTreeNode[][] operands = [.. products.Select(p => new[] { p.Inputs[0], p.Inputs[1] })];
            HlslTreeNode subscript = TakeVectorComponent(operands) ?? TakeSelectedComponent(operands);
            if (subscript != null)
            {
                subscript.ConsumesInteger = sum.ConsumesInteger ?? false;
                subscript.SourceInstruction = sum.SourceInstruction;
                subscript.SourceComponent = sum.SourceComponent;
                ReplaceEverywhere(sum, subscript);
                TakeApart(sum, Chain(sum));
            }
        }
        foreach (CompareOperation select in Values().OfType<CompareOperation>().ToList())
        {
            // Already taken apart as part of an enclosing chain.
            if (select.Outputs.Count == 0 && !IsStatementValue(select))
            {
                continue;
            }
            HashSet<HlslTreeNode> chain = HlslTreeNode.NewNodeSet();
            if (TakeArrayElement(select, chain) is RelativeAddressNode element)
            {
                element.SourceInstruction = select.SourceInstruction;
                element.SourceComponent = select.SourceComponent;
                ReplaceEverywhere(select, element);
                TakeApart(select, chain);
            }
        }
    }

    /// <summary>
    /// `a[i]` of a constant array where a shader model 3 pixel shader cannot index
    /// constants: fxc selects each element in turn, `cmp r, -abs(i - k), a[k], r`
    /// from k = 0 with 0 as the start, and the last cmp is the read. The chain
    /// must take every element of one array, so `a[i]` compiles to it again.
    /// </summary>
    private RelativeAddressNode TakeArrayElement(CompareOperation select, HashSet<HlslTreeNode> chain)
    {
        var elements = new List<RegisterInputNode>();
        var tests = new List<HlslTreeNode>();
        HlslTreeNode node = select;
        while (node is CompareOperation
            {
                Value: NegateOperation { Value: AbsoluteOperation { Value: HlslTreeNode difference } } negate
            } compare)
        {
            if (Moved(compare.GreaterEqualValue) is not RegisterInputNode element)
            {
                return null;
            }
            chain.Add(compare);
            chain.Add(negate);
            chain.Add(negate.Value);
            chain.Add(difference);
            elements.Add(element);
            tests.Add(difference);
            node = compare.LessValue;
        }
        int count = elements.Count;
        if (count < 2 || !ConstantMatcher.IsZero(Moved(node)))
        {
            return null;
        }
        // Outermost first: element count - 1 down to 0.
        HlslTreeNode index = null;
        for (int i = 0; i < count; i++)
        {
            HlslTreeNode tested = Tested(tests[i], count - 1 - i);
            if (tested == null || (index != null && !IsSameRead(tested, index) && !ReferenceEquals(Moved(tested), Moved(index))))
            {
                        return null;
            }
            index ??= tested;
        }
        // A loop counter or a register read: an index computed into a register
        // is read by the chain's statements, and the subscript would hide that
        // read from them, so the register could be reassigned before it.
        if (Moved(index) is not (PhiNode or RegisterInputNode))
        {
            return null;
        }
        RegisterComponentKey first = elements[count - 1].RegisterComponentKey;
        if (first.RegisterKey is not D3D9RegisterKey { Type: RegisterType.Const } firstRegister
            || _registerState.FindConstantOfComponent(first) is not ConstantDeclaration array
            || array.TypeInfo.NumElements != count)
        {
            return null;
        }
        int stride = System.Math.Max(array.RegistersPerElement, 1);
        int offset = firstRegister.Number - array.RegisterIndex;
        if (offset < 0 || offset >= stride)
        {
            return null;
        }
        for (int i = 0; i < count; i++)
        {
            RegisterComponentKey key = elements[count - 1 - i].RegisterComponentKey;
            if (key.RegisterKey is not D3D9RegisterKey { Type: RegisterType.Const } register
                || register.Number != firstRegister.Number + i * stride
                || key.ComponentIndex != first.ComponentIndex)
            {
                return null;
            }
        }
        return new RelativeAddressNode(first, index) { IndexCountsElements = true };
    }

    /// <summary>
    /// A dot product's products in component order (dp4 is ((x + y) + z) + w), or
    /// null. A zero dp2add addend is dropped.
    /// </summary>
    private static List<MultiplyOperation> Products(AddOperation sum)
    {
        var terms = new List<HlslTreeNode>();
        HlslTreeNode node = sum;
        while (node is AddOperation add)
        {
            terms.Insert(0, add.Addend2);
            node = add.Addend1;
        }
        terms.Insert(0, node);
        if (terms.Count == 3 && ConstantMatcher.IsZero(terms[2]))
        {
            terms.RemoveAt(2);
        }
        if (terms.Count < 2 || terms.Count > 4 || !terms.All(term => term is MultiplyOperation))
        {
            return null;
        }
        return [.. terms.Cast<MultiplyOperation>()];
    }

    /// <summary>
    /// `v[i]` from `dp4 dst, v, icb[i]` where icb is an identity matrix.
    /// </summary>
    private HlslTreeNode TakeVectorComponent(HlslTreeNode[][] operands)
    {
        int width = operands.Length;
        for (int selector = 0; selector < 2; selector++)
        {
            RelativeAddressNode[] rows = [.. operands.Select(
                componentInput => componentInput[selector] as RelativeAddressNode)];
            if (rows[0]?.RegisterComponentKey.RegisterKey
                is not D3D10RegisterKey { OperandType: OperandType.ImmediateConstantBuffer } icb
                // The swizzle must be in order: `icb[i].yxzw` is not v[i].
                || rows.Where((row, component) => row == null
                    || !icb.Equals(row.RegisterComponentKey.RegisterKey)
                    || row.ComponentIndex != component
                    || !ReferenceEquals(row.Index, rows[0].Index)).Any()
                || !_registerState.IsImmediateConstantBufferIdentity(icb.Number, width))
            {
                continue;
            }
            // Only a named register read takes a subscript; fxc rejects
            // `float4(a, b, c, d)[i]`, and a temp may be inlined into either.
            HlslTreeNode[] vector = [.. operands.Select(
                componentInput => componentInput[1 - selector])];
            if (!vector.All(IsNamedVectorRead))
            {
                continue;
            }
            return new VectorComponentNode(new GroupNode(vector), rows[0].Index);
        }
        return null;
    }

    /// <summary>A constant buffer or input read, which the writer emits as a name.</summary>
    private static bool IsNamedVectorRead(HlslTreeNode node)
    {
        RegisterKey registerKey = node switch
        {
            RegisterInputNode read => read.RegisterComponentKey.RegisterKey,
            RelativeAddressNode read => read.RegisterComponentKey.RegisterKey,
            _ => null,
        };
        return registerKey is D3D10RegisterKey
        {
            OperandType: OperandType.ConstantBuffer or OperandType.Input
        };
    }

    /// <summary>
    /// The SM3 form of `v[i]`: `cmp r0, -abs(i - (0, 1, 2, 3)), 1, 0` dotted with v.
    /// Integer indices only: a fractional float index dots to zero, where a
    /// subscript would truncate it.
    /// </summary>
    private HlslTreeNode TakeSelectedComponent(HlslTreeNode[][] operands)
    {
        for (int selector = 0; selector < 2; selector++)
        {
            HlslTreeNode index = null;
            for (int component = 0; component < operands.Length; component++)
            {
                HlslTreeNode tested = TestedForComponent(operands[component][selector], component);
                if (tested == null || (index != null && !IsSameRead(tested, index)))
                {
                    index = null;
                    break;
                }
                index = tested;
            }
            HlslTreeNode[] vector = [.. operands.Select(componentInput => componentInput[1 - selector])];
            if (index is RegisterInputNode read
                && _registerState.GetDeclaredType(read.RegisterComponentKey) == DeclaredType.Int
                && vector.All(component => component is RegisterInputNode))
            {
                return new VectorComponentNode(new GroupNode(vector), index);
            }
        }
        return null;
    }

    // The i of `cmp dst, -abs(i - c), 1, 0`, or null. fxc writes i + -c, and
    // c == 0 may appear with or without the add.
    private static HlslTreeNode TestedForComponent(HlslTreeNode test, int component)
    {
        if (test is not CompareOperation
            {
                Value: NegateOperation { Value: AbsoluteOperation { Value: HlslTreeNode difference } }
            } compare
            || !ConstantMatcher.IsOne(compare.GreaterEqualValue)
            || !ConstantMatcher.IsZero(compare.LessValue))
        {
            return null;
        }
        if (difference is AddOperation add)
        {
            if (Moved(add.Addend1) is ConstantNode offset1 && offset1.Value == -component)
            {
                return add.Addend2;
            }
            if (Moved(add.Addend2) is ConstantNode offset2 && offset2.Value == -component)
            {
                return add.Addend1;
            }
            return null;
        }
        return component == 0 ? difference : null;
    }

    // The i of `i - k`, which fxc writes i + -k, or just i where k is 0.
    private static HlslTreeNode Tested(HlslTreeNode difference, int k)
    {
        if (difference is AddOperation add)
        {
            for (int i = 0; i < 2; i++)
            {
                if (Moved(add.Inputs[i]) is ConstantNode offset && offset.Value == -k)
                {
                    return add.Inputs[1 - i];
                }
            }
            return null;
        }
        return k == 0 ? difference : null;
    }

    private static HlslTreeNode Moved(HlslTreeNode node)
    {
        while (node is MoveOperation move)
        {
            node = move.Inputs[0];
        }
        return node;
    }

    private static bool IsSameRead(HlslTreeNode a, HlslTreeNode b)
    {
        return ReferenceEquals(a, b)
            || (a is RegisterInputNode readA && b is RegisterInputNode readB
                && readA.RegisterComponentKey.Equals(readB.RegisterComponentKey));
    }

    /// <summary>
    /// An index into a matrix array counts registers (`ishl r0.x, v1.x, l(2)`).
    /// Stripped here rather than divided at the write, so the scaled index never
    /// becomes a named value.
    /// </summary>
    private void StripElementStride(RelativeAddressNode read)
    {
        if (read.IndexCountsElements)
        {
            return;
        }
        int stride = _registerState.FindConstantOfComponent(read.RegisterComponentKey)?.RegistersPerElement ?? 1;
        HlslTreeNode element = Moved(read.Index);
        if (stride > 1 && !TryStripElementStride(element, stride, out element))
        {
            return;
        }
        // fxc floors a float index (`x - frac(x)`) before mova, and floors it again
        // when compiled; written out, the floor is compiled twice.
        HlslTreeNode unfloored = read.RegisterComponentKey.RegisterKey is D3D9RegisterKey
            ? Unfloored(Moved(element))
            : null;
        if (stride <= 1 && unfloored == null)
        {
            return;
        }
        // An input scaled straight into the address register is an int: a float
        // source would need a floor (frc + add) on the way.
        if (element is RegisterInputNode { RegisterComponentKey.RegisterKey: D3D9RegisterKey { Type: RegisterType.Input } } input)
        {
            const int SInt32ComponentType = 2;
            RegisterDeclaration declaration = _registerState.MethodInputRegisters
                .FirstOrDefault(d => d.RegisterKey.Equals(input.RegisterComponentKey.RegisterKey));
            if (declaration != null)
            {
                declaration.ComponentType = SInt32ComponentType;
            }
        }
        element = unfloored ?? element;
        var stripped = new RelativeAddressNode(read.RegisterComponentKey, element)
        {
            IndexCountsElements = true,
            ConsumesInteger = read.ConsumesInteger,
            SourceInstruction = read.SourceInstruction,
            SourceComponent = read.SourceComponent,
        };
        ReplaceEverywhere(read, stripped);
        TakeApart(read, HlslTreeNode.NewNodeSet());
    }

    // x of `x - frac(y)`, or null, where x and y differ by a whole number (fxc
    // takes `2 * y + 1 - frac(y + y)`): frac ignores it.
    private static HlslTreeNode Unfloored(HlslTreeNode value)
    {
        if (value is SubtractOperation subtract
            && Moved(subtract.Subtrahend) is FractionalOperation fraction
            && DifferByWholeNumber(subtract.Minuend, fraction.Inputs[0]))
        {
            return subtract.Minuend;
        }
        if (value is AddOperation add)
        {
            for (int i = 0; i < 2; i++)
            {
                if (Moved(add.Inputs[i]) is NegateOperation { Inputs: [var negated] }
                    && Moved(negated) is FractionalOperation negatedFraction
                    && DifferByWholeNumber(add.Inputs[1 - i], negatedFraction.Inputs[0]))
                {
                    return add.Inputs[1 - i];
                }
            }
        }
        return null;
    }

    // Also for the templates: frac(a) is frac(b) where a and b differ by a whole number.
    internal static bool DifferByWholeNumber(HlslTreeNode a, HlslTreeNode b)
    {
        (HlslTreeNode baseA, float scaleA, float offsetA) = ScaledOffset(Moved(a));
        (HlslTreeNode baseB, float scaleB, float offsetB) = ScaledOffset(Moved(b));
        float difference = offsetA - offsetB;
        return IsSameRead(baseA, baseB) && scaleA == scaleB && difference == System.MathF.Floor(difference);
    }

    // value as base * scale + offset, with constant scale and offset.
    private static (HlslTreeNode Base, float Scale, float Offset) ScaledOffset(HlslTreeNode value)
    {
        HlslTreeNode[] inputs = [.. value.Inputs.Select(Moved)];
        switch (value)
        {
            case AddOperation when inputs[1] is ConstantNode constant:
                return Offset(ScaledOffset(inputs[0]), constant.Value);
            case AddOperation when inputs[0] is ConstantNode constant:
                return Offset(ScaledOffset(inputs[1]), constant.Value);
            case SubtractOperation when inputs[1] is ConstantNode constant:
                return Offset(ScaledOffset(inputs[0]), -constant.Value);
            case AddOperation when IsSameRead(inputs[0], inputs[1]):
                return (inputs[0], 2, 0);
            case MultiplyOperation when inputs[1] is ConstantNode scale:
                return (inputs[0], scale.Value, 0);
            case MultiplyOperation when inputs[0] is ConstantNode scale:
                return (inputs[1], scale.Value, 0);
            case MultiplyAddOperation when inputs[2] is ConstantNode addend && inputs[1] is ConstantNode scale:
                return (inputs[0], scale.Value, addend.Value);
            case MultiplyAddOperation when inputs[2] is ConstantNode addend && inputs[0] is ConstantNode scale:
                return (inputs[1], scale.Value, addend.Value);
            default:
                return (value, 1, 0);
        }

        static (HlslTreeNode, float, float) Offset((HlslTreeNode Base, float Scale, float Offset) form, float by)
        {
            return (form.Base, form.Scale, form.Offset + by);
        }
    }

    private static bool TryStripElementStride(HlslTreeNode index, int stride, out HlslTreeNode element)
    {
        if (index is ShiftLeftOperation shift
            && shift.Inputs[1] is ConstantNode amount
            && stride == 1 << (int)amount.Value)
        {
            element = shift.Inputs[0];
            return true;
        }
        if (index is MultiplyOperation multiply)
        {
            for (int i = 0; i < 2; i++)
            {
                if (multiply.Inputs[i] is ConstantNode constant && constant.Value == stride)
                {
                    element = multiply.Inputs[1 - i];
                    return true;
                }
            }
        }
        // Whole elements further in fold into the same imad: `s[i + 1]` of a
        // three-register struct is `imad r0.x, i, l(3), l(3)`.
        if (index is MultiplyAddOperation { Addend: ConstantNode { IntegerValue: int addend } } multiplyAdd
            && addend % stride == 0)
        {
            for (int i = 0; i < 2; i++)
            {
                if (multiplyAdd.Inputs[i] is ConstantNode { IntegerValue: int factor } && factor == stride)
                {
                    element = new AddOperation(multiplyAdd.Inputs[1 - i], new ConstantNode(addend / stride));
                    return true;
                }
            }
        }
        element = null;
        return false;
    }

    private IEnumerable<HlslTreeNode> Values()
    {
        var seen = HlslTreeNode.NewNodeSet();
        var pending = new Stack<HlslTreeNode>();
        foreach (IList<IStatement> body in _bodies)
        {
            new StatementVisitor(body).Visit(statement =>
            {
                foreach (HlslTreeNode value in statement.Outputs.Values
                    .Concat(statement.Inputs.Values).Concat(statement.HeldNodes))
                {
                    pending.Push(value);
                }
            });
        }
        while (pending.Count != 0)
        {
            HlslTreeNode node = pending.Pop();
            if (node == null || !seen.Add(node))
            {
                continue;
            }
            yield return node;
            foreach (HlslTreeNode input in node.Inputs)
            {
                pending.Push(input);
            }
        }
    }

    private bool IsStatementValue(HlslTreeNode node)
    {
        bool found = false;
        foreach (IList<IStatement> body in _bodies)
        {
            new StatementVisitor(body).Visit(statement =>
            {
                found |= statement.Outputs.Values.Contains(node)
                    || statement.Inputs.Values.Contains(node)
                    || statement.HeldNodes.Contains(node);
            });
        }
        return found;
    }

    /// <summary>
    /// Replace also in statements' maps and held slots, which are not input edges.
    /// </summary>
    private void ReplaceEverywhere(HlslTreeNode node, HlslTreeNode replacement)
    {
        node.Replace(replacement);
        node.Outputs.Clear();
        foreach (IList<IStatement> body in _bodies)
        {
            new StatementVisitor(body).Visit(statement =>
            {
                foreach (IDictionary<RegisterComponentKey, HlslTreeNode> map in new[] { statement.Outputs, statement.Inputs })
                {
                    foreach (RegisterComponentKey key in map.Where(e => ReferenceEquals(e.Value, node)).Select(e => e.Key).ToList())
                    {
                        map[key] = replacement;
                    }
                }
                statement.ReplaceHeldNode(node, replacement);
            });
        }
    }

    private static HashSet<HlslTreeNode> Chain(AddOperation sum)
    {
        HashSet<HlslTreeNode> chain = HlslTreeNode.NewNodeSet();
        HlslTreeNode node = sum;
        while (node is AddOperation add)
        {
            chain.Add(add);
            chain.Add(add.Addend2);
            node = add.Addend1;
        }
        chain.Add(node);
        return chain;
    }

    /// <summary>
    /// Detaches a replaced value and its dot-product chain, which would otherwise
    /// count as readers. Identity rows come off the icb tally; other inputs are only
    /// unlinked, leaving deadness to the statement finalizer.
    /// </summary>
    private void TakeApart(HlslTreeNode node, HashSet<HlslTreeNode> chain)
    {
        foreach (HlslTreeNode input in node.Inputs.ToList())
        {
            input.Outputs.Remove(node);
            if (input.Outputs.Count != 0)
            {
                continue;
            }
            if (chain.Contains(input))
            {
                TakeApart(input, chain);
            }
            else if (chain.Contains(node) && input is RelativeAddressNode
                {
                    RegisterComponentKey.RegisterKey: D3D10RegisterKey { OperandType: OperandType.ImmediateConstantBuffer } icb
                } row)
            {
                row.Remove();
                _registerState.UndeclareImmediateConstantBufferRead(icb.Number);
            }
        }
    }
}
