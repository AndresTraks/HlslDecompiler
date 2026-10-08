using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using HlslDecompiler.Hlsl.TemplateMatch;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// The idioms recovered from the graph the parser has just built, before anything
/// else reads it: a vector read at a computed component, and an array index that
/// counts registers where the source counted elements. Each is an identity over
/// the values - a sum of products against the rows of an identity matrix is a
/// subscript, whatever instruction made it - so it is asked of the finished graph
/// rather than of the operands of the one instruction being parsed. They used to
/// be recognised in the middle of the parse, by the code building that
/// instruction's nodes, and saw only what it had in hand.
///
/// Before the statement finalizer and the type facts, both of which read the
/// graph as these leave it, and before the parse types its immediates by their
/// readers: the products a subscript replaces are readers of nothing afterwards.
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
            // Taken apart already, as part of a sum recovered before it.
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
    }

    /// <summary>
    /// The products a dot product is the sum of, in the order of the components they
    /// multiply - a dp4 is ((x + y) + z) + w - or null where the sum is not only
    /// products. A dp2add's addend is dropped where it is zero, which it is when fxc
    /// writes a two wide dot that way.
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
    /// The subscript a dot product stands for where one of its operands is a row of
    /// an identity matrix in the immediate constant buffer, which is how fxc reads a
    /// vector at a component it has to work out - `v[i]` is `dp4 dst, v, icb[i]`.
    /// Null for an ordinary dot product. The reads of the identity come off the
    /// buffer's tally once the products are taken apart: an array nothing reads any
    /// more is not declared at all.
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
                // Row c of the identity holds the one in component c, so the operand's
                // swizzle has to be in order for the dot to pick the index's component:
                // `icb[i].yxzw` would answer v[i] for two of the four indices only.
                || rows.Where((row, component) => row == null
                    || !icb.Equals(row.RegisterComponentKey.RegisterKey)
                    || row.ComponentIndex != component
                    || !ReferenceEquals(row.Index, rows[0].Index)).Any()
                || !_registerState.IsImmediateConstantBufferIdentity(icb.Number, width))
            {
                continue;
            }
            // A subscript has to be written on something that can carry one, and the
            // operand is only ever a register read here - a swizzle of one takes a
            // subscript, `float4(a, b, c, d)[i]` is not something fxc will compile,
            // and a temp the writer inlines an expression into could be either.
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

    /// <summary>
    /// Whether the value is read straight out of a constant buffer or an input
    /// register, which is what comes out of the writer as a name with a swizzle on
    /// it rather than as an expression.
    /// </summary>
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
    /// The subscript a dot product stands for where one of its operands is a vector of
    /// tests of an integer against each component's number, which is how a shader
    /// model 3 shader reads a vector at a component it has to work out, having no
    /// immediate constant buffer to hold an identity in: `v[i]` is
    /// `cmp r0, -abs(i - (0, 1, 2, 3)), 1, 0` dotted with v. Read back as that, it
    /// came out as a vector of `i == 0 ? 1.0 : 0.0` and a sum of products.
    ///
    /// Only for an index declared an integer. A float index between two whole numbers
    /// matches none of the tests and dots to zero, where the subscript would truncate
    /// it and read a component.
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

    // What `cmp dst, -abs(i - c), 1, 0` tests for being c: i, where the value is
    // that, and null otherwise. fxc adds -c rather than subtracting, and adds the
    // -0 of the first component as well.
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

    // The offsets are moved into the register before the index is added to them,
    // and an address into the address register before it is read through.
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
    /// An index into an array of matrices counts registers, so fxc multiplies the
    /// element by the rows first - `ishl r0.x, v1.x, l(2)`, an imul by 4, or the
    /// mad a shader model 3 shader hands its mova. Read through here, rather than
    /// divided back at the write, the product is never a value: named where four
    /// rows read it, it came out as `int4 t2 = t1 * 4` and `bones[t2.x / 4]` four
    /// times over.
    /// </summary>
    private void StripElementStride(RelativeAddressNode read)
    {
        if (read.IndexCountsElements)
        {
            return;
        }
        int stride = _registerState.FindConstantOfComponent(read.RegisterComponentKey)?.RegistersPerElement ?? 1;
        if (stride <= 1 || !TryStripElementStride(Moved(read.Index), stride, out HlslTreeNode element))
        {
            return;
        }
        // An input that goes straight into the address register, scaled and
        // nothing else, is an integer: a source that declared it float would carry
        // a floor on the way, and this one has none. Declared float instead, fxc
        // put the floor back - a frc and an add - and matrix_palette cost two
        // instructions for it.
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
        element = null;
        return false;
    }

    /// <summary>
    /// Every value the statements reach, through what each one reads.
    /// </summary>
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
    /// Puts the replacement wherever the node was: in what reads it, which the
    /// graph's own Replace reaches, and in the statements, which hold their values
    /// in maps and slots that are not input edges and which it does not.
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

    // The sums and products a dot product was built of, which go with it.
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
    /// Takes a replaced value out of the graph, and with it the part of the graph it
    /// was made of - the products and sums of a dot product, which nothing else was
    /// built to read. Left attached they count as readers of what is under them, and
    /// the writer went on naming values nothing reads. A row of the identity a
    /// product read comes off the buffer's tally. Anything else it read is only let
    /// go of: another instruction's value is still the value of its register, and the
    /// statement finalizer is what decides that nothing reads it.
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
