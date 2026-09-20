using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public sealed class NodeCompiler
{
    private readonly RegisterState _registers;
    private readonly NodeGrouper _nodeGrouper;
    private readonly ConstantCompiler _constantCompiler;
    private readonly MatrixMultiplicationCompiler _matrixMultiplicationCompiler;
    private int _tempAssignmentindexCounter = 0;

    /// <summary>
    /// Variables standing for a shared subexpression rather than for a register, one
    /// per component. They are numbered here because the counter lives here, and
    /// given their size and component up front: the lazy path below numbers a whole
    /// register's worth of components at once, and would make a scalar the fourth
    /// of four. The components share a declaration index, which is what makes the
    /// writer name them as one vector - a value graph holds a four wide operation as
    /// four separate nodes, and naming each of them on its own turns one instruction
    /// into four statements that can never be put back together.
    /// </summary>
    public TempVariableNode[] CreateTempVariables(int size)
    {
        int index = _tempAssignmentindexCounter++;
        return [.. Enumerable.Range(0, size).Select(component => new TempVariableNode
        {
            DeclarationIndex = index,
            ComponentIndex = component,
            VariableSize = size,
        })];
    }

    public const int PromoteToAnyVectorSize = -1;

    // The variable of the innermost counted loop, which aL refers to. The writer
    // generates that name from the nesting depth, so it has to be handed in.
    public string LoopVariableName { get; set; }

    public NodeCompiler(RegisterState registers)
    {
        _registers = registers;
        _nodeGrouper = new NodeGrouper(registers);
        _constantCompiler = new ConstantCompiler();
        _matrixMultiplicationCompiler = new MatrixMultiplicationCompiler(this);
    }

    public string Compile(HlslTreeNode node)
    {
        return Compile([node]);
    }

    public string Compile(IEnumerable<HlslTreeNode> group, int promoteToVectorSize = PromoteToAnyVectorSize)
    {
        return Compile(group.ToList(), promoteToVectorSize);
    }

    /// <summary>
    /// Set while the writer measures a statement before writing it: every expression
    /// compiled on the way, with the nodes it was compiled from. Which of them the
    /// text repeats is not a property of the graph - a node read by sixteen
    /// multiplies is written once when the grouper makes a matrix multiply of them -
    /// so the writer compiles first and counts afterwards.
    /// </summary>
    public List<(HlslTreeNode[] Nodes, string Text)> Recording { get; set; }

    /// <summary>
    /// Set alongside Recording: the nodes a grouper took the inside of. A cross
    /// product is recognised from the six multiplies under it, and those multiplies
    /// are never written on their own - naming one puts a variable where the next
    /// compile expects the pattern, and the grouper stops matching. The operands a
    /// grouper hands back are not in here: those are compiled by themselves anyway,
    /// so a name is safe at the boundary of a match and nowhere inside it.
    /// </summary>
    public HashSet<HlslTreeNode> Grouped { get; set; }

    /// <summary>
    /// Records everything under the matched components except what is under the
    /// operands the match handed back.
    /// </summary>
    private void MarkGrouped(IEnumerable<HlslTreeNode> matched, params IEnumerable<HlslTreeNode>[] operands)
    {
        if (Grouped == null)
        {
            return;
        }
        HashSet<HlslTreeNode> boundary = HlslTreeNode.NewNodeSet();
        foreach (IEnumerable<HlslTreeNode> operand in operands)
        {
            foreach (HlslTreeNode node in Reachable(operand))
            {
                boundary.Add(node);
            }
        }
        foreach (HlslTreeNode node in Reachable(matched))
        {
            if (!boundary.Contains(node))
            {
                Grouped.Add(node);
            }
        }
    }

    private static IEnumerable<HlslTreeNode> Reachable(IEnumerable<HlslTreeNode> roots)
    {
        HashSet<HlslTreeNode> seen = HlslTreeNode.NewNodeSet();
        var stack = new Stack<HlslTreeNode>(roots);
        while (stack.Count != 0)
        {
            HlslTreeNode node = stack.Pop();
            if (!seen.Add(node))
            {
                continue;
            }
            yield return node;
            foreach (HlslTreeNode input in HlslTreeNode.TraversableInputs(node))
            {
                stack.Push(input);
            }
        }
    }

    /// <summary>
    /// Compiles a value standing where a float is wanted - an output, or a return.
    /// Bits reaching one of those are reinterpreted, the same as bits reaching the
    /// operand of a float operation; the writer has to say so because an output is
    /// not an operation and carries no type of its own into here.
    /// </summary>
    public string CompileAsFloat(IEnumerable<HlslTreeNode> group)
    {
        bool wasReadingAsFloat = _readingAsFloat;
        _readingAsFloat = true;
        try
        {
            return Compile(group);
        }
        finally
        {
            _readingAsFloat = wasReadingAsFloat;
        }
    }

    public string Compile(List<HlslTreeNode> components, int promoteToVectorSize = PromoteToAnyVectorSize)
    {
        string compiled = CompileUnrecorded(components, promoteToVectorSize);
        Recording?.Add(([.. components], compiled));
        return compiled;
    }

    private string CompileUnrecorded(List<HlslTreeNode> components, int promoteToVectorSize)
    {
        if (components.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(components));
        }

        // An integer that is a float's bits, standing where a float is wanted: the
        // bits are the value, and converting them gives the number they happen to
        // make - a packed pair of half floats came back as three thousand million.
        // Every component, so that a constructor whose components are not all bits
        // is left to reinterpret them one at a time.
        if (_readingAsFloat && components.All(StatementFinalizer.IsBitsValue))
        {
            _readingAsFloat = false;
            try
            {
                return $"asfloat({CompileUnrecorded(components, promoteToVectorSize)})";
            }
            finally
            {
                _readingAsFloat = true;
            }
        }

        if (components.Count > 1)
        {
            IList<IList<HlslTreeNode>> componentGroups = _nodeGrouper.GroupComponents(components);
            if (componentGroups.Count > 1)
            {
                // The grouper splits a vector operation whose operands do not all
                // group - a mad whose addend is ddx in .xy and ddy in .zw. Written
                // as two half-mads inside a constructor, fxc keeps the halves apart;
                // written as one mad with the constructor around the operand that
                // differs, it is the instruction it came from. Only for an operation
                // that works a component at a time - a dot or a length is not one -
                // and only when one operand differs: two constructors, one per
                // operand, read worse than the one around the whole.
                if (IsElementwiseAcross(components) && CountUngroupedOperands(components) == 1)
                {
                    return CompileOperation((Operation)components[0], components, promoteToVectorSize);
                }
                return CompileVectorConstructor(components, componentGroups);
            }

            var multiplication = _nodeGrouper.MatrixMultiplicationGrouper.TryGetMultiplicationGroup(components);
            if (multiplication != null)
            {
                // The index a row is read through is handed back too, and is
                // written on its own - `cascadeTransform[min(t0, 3)]` - so it is a
                // boundary of the match like the vector is.
                MarkGrouped(components, multiplication.Vector,
                    multiplication.ElementIndexNode == null ? [] : [multiplication.ElementIndexNode]);
                return _matrixMultiplicationCompiler.Compile(multiplication);
            }
            // Dot products group only as rows of one matrix multiply. A run of them
            // the multiplication grouper does not take whole - two rows of a four
            // row matrix, say - has no vector form, and is written one by one.
            if (components.All(c => c is DotProductOperation)
                && components.Distinct(ReferenceEqualityComparer.Instance).Count() > 1)
            {
                return CompileVectorConstructor(components,
                    [.. components.Select(c => (IList<HlslTreeNode>)[c])]);
            }

            var normalize = _nodeGrouper.NormalizeGrouper.TryGetContext(components);
            if (normalize != null)
            {
                MarkGrouped(components, normalize);
                var vector = Compile(normalize);
                return $"normalize({vector})";
            }

            var reflect = _nodeGrouper.ReflectGrouper.TryGetContext(components);
            if (reflect != null)
            {
                MarkGrouped(components, reflect.Value.Incident, reflect.Value.Normal);
                return $"reflect({Compile(reflect.Value.Incident)}, {Compile(reflect.Value.Normal)})";
            }

            var cross = _nodeGrouper.CrossProductGrouper.TryGetContext(components);
            if (cross != null)
            {
                MarkGrouped(components, cross.Value.A, cross.Value.B);
                return $"cross({Compile(cross.Value.A)}, {Compile(cross.Value.B)})";
            }
        }

        // The other half of the grouper seeing through a folded zero: the components
        // that kept their constant and the ones that lost it are written back into
        // the one add, `t1 + float4(1, 0, 3, 4)` rather than a constructor of three
        // conditionals. Saying they group and then compiling them apart is what
        // reads Inputs[1] of a node that has none.
        if (components.Count > 1
            && components.Any(c => c is MultiplyOperation)
            && components.Any(c => c is not MultiplyOperation)
            && components.All(c => c is not MultiplyOperation multiply
                || multiply.Factor1 is ConstantNode || multiply.Factor2 is ConstantNode))
        {
            List<HlslTreeNode> multiplied = [.. components.Select(FactoredOfFoldedMultiply)];
            List<HlslTreeNode> factors = [.. components.Select(FactorOfFoldedMultiply)];
            return $"{Compile(multiplied, promoteToVectorSize)} * {Compile(factors, factors.Count)}";
        }

        if (components.Count > 1
            && components.Any(c => c is AddOperation)
            && components.Any(c => c is not AddOperation)
            && components.All(c => c is not AddOperation add || HasConstantAddend(add)))
        {
            // The zero takes the type of the constants beside it. Written as a float
            // among integers it makes the whole vector a float2, and an integer
            // expression that was three instructions becomes six.
            bool integer = components
                .OfType<AddOperation>()
                .Select(add => AddendOfFoldedAdd(add))
                .All(addend => addend is ConstantNode constant && constant.IntegerValue != null);
            List<HlslTreeNode> bases = [.. components.Select(BaseOfFoldedAdd)];
            List<HlslTreeNode> addends = [.. components.Select(
                c => AddendOfFoldedAdd(c, integer))];
            return $"{Compile(bases, promoteToVectorSize)} + {Compile(addends, addends.Count)}";
        }

        var first = components[0];

        if (first is ConstantNode)
        {
            return CompileConstant(components, promoteToVectorSize);
        }

        if (first is Operation operation)
        {
            return CompileOperation(operation, components, promoteToVectorSize);
        }

        if (first is IHasComponentIndex)
        {
            return CompileNodesWithComponents(components, first, promoteToVectorSize);
        }

        if (first is ComparisonNode comparison)
        {
            return CompileComparison(components, comparison);
        }

        if (first is GroupNode group)
        {
            return Compile(group.Inputs, promoteToVectorSize);
        }

        if (first is PhiNode)
        {
            // Phis are an IR construct. StatementFinalizer lowers them to a temp
            // variable plus its assignments; reaching here means that did not happen.
            throw new InvalidOperationException("Phi node reached compilation without being lowered.");
        }

        throw new NotImplementedException("Unsupported node: " + first.GetType().Name);
    }

    private static bool IsSum(HlslTreeNode node)
    {
        return node is AddOperation or SubtractOperation;
    }

    // A division that is written as one - not the `x / length(x)` that is written
    // as normalize(x).
    private bool IsQuotient(IEnumerable<HlslTreeNode> components)
    {
        List<HlslTreeNode> list = [.. components];
        return list[0] is DivisionOperation
            && (list.Count == 1 || _nodeGrouper.NormalizeGrouper.TryGetContext(list) == null);
    }

    private static bool IsElementwiseAcross(List<HlslTreeNode> components)
    {
        if (components[0] is not Operation first || !IsElementwise(first))
        {
            return false;
        }
        return components.All(c => c.GetType() == first.GetType() && c.Inputs.Count == first.Inputs.Count);
    }

    private int CountUngroupedOperands(List<HlslTreeNode> components)
    {
        int ungrouped = 0;
        for (int operand = 0; operand < components[0].Inputs.Count; operand++)
        {
            List<HlslTreeNode> values = [.. components.Select(c => c.Inputs[operand])];
            if (_nodeGrouper.GroupComponents(values).Count > 1)
            {
                ungrouped++;
            }
        }
        return ungrouped;
    }

    // An operation whose every result component depends on that component of its
    // operands alone, so that a vector of them is the same operation on vectors.
    // A conversion is left out: `(float4)float4(a, b, c, d)` says less than four
    // casts do.
    private static bool IsElementwise(Operation operation)
    {
        return operation is AddOperation or SubtractOperation or MultiplyOperation
            or MultiplyAddOperation or DivisionOperation or NegateOperation or AbsoluteOperation
            or MinimumOperation or MaximumOperation or SaturateOperation or ClampOperation
            or LinearInterpolateOperation or SmoothStepOperation or StepOperation
            or MoveConditionalOperation
            or FractionalOperation or FloorOperation or CeilingOperation or RoundOperation
            or TruncateOperation or SquareRootOperation or ReciprocalOperation
            or ReciprocalSquareRootOperation or ExponentialOperation or LogOperation
            or NaturalExponentialOperation or NaturalLogarithmOperation
            or PowerOperation or SineOperation or CosineOperation or SignOperation
            or FloatingModuloOperation;
    }

    // Compiles a sub-expression, parenthesised when its operator binds more loosely
    // than the one it is being nested into.
    private string CompileOperand(IEnumerable<HlslTreeNode> components, int promoteToVectorSize = PromoteToAnyVectorSize)
    {
        List<HlslTreeNode> list = components.ToList();
        string compiled = Compile(list, promoteToVectorSize);
        // A comparison standing where a value is wanted is the mask it wrote, which
        // is all ones for true rather than one. sign expands to the difference of
        // two comparisons, and reading them as HLSL bools gave back the negation of
        // the sign: `(t < 0) - (t > 0)` is 1 where the shader says -1.
        if (list[0] is ComparisonNode)
        {
            return $"({compiled} ? -1 : 0)";
        }
        return AssociativityTester.NeedsParenthesesAsOperand(list[0])
            ? $"({compiled})"
            : compiled;
    }

    /// <summary>
    /// Whether a register holds floats, from the declaration that names it: a
    /// constant buffer variable by its own type, an input by the component type its
    /// signature gives, and a thread id by neither, being a uint.
    /// </summary>
    private bool IsFloatRegister(RegisterInputNode register)
    {
        RegisterComponentKey key = register.RegisterComponentKey;
        ConstantDeclaration constant = _registers.FindConstant(key.RegisterKey);
        if (constant != null)
        {
            return constant.TypeInfo.ParameterType is not (ParameterType.Int
                or ParameterType.Uint or ParameterType.Bool);
        }
        return _registers.RegisterDeclarations.TryGetValue(key.RegisterKey, out RegisterDeclaration declaration)
            && !declaration.TypeName.Contains("int");
    }

    // An operand a bitwise operator or a shift reads. Where the value is a float -
    // a temp declared as one, or an operation that computes one - the integer
    // wanted is its bits and not its number, so it is reinterpreted rather than
    // converted. It has to be reinterpreted somehow: HLSL will not apply either
    // operator to a float at all (X3082).
    private string CompileIntegerOperand(IEnumerable<HlslTreeNode> components)
    {
        List<HlslTreeNode> list = components.ToList();
        return IsFloatValued(list[0])
            ? $"asint({Compile(list)})"
            : CompileOperand(list);
    }

    // Whether a value is a float, from the value itself rather than from what reads
    // it: the readers of one of these are integer instructions by construction.
    private bool IsFloatValued(HlslTreeNode node)
    {
        return node switch
        {
            TempVariableNode temp => !temp.IsInteger,
            RegisterInputNode register => IsFloatRegister(register),
            ConvertOperation convert => convert.TargetType is not ("int" or "uint"),
            // The half conversions read one type and make the other, so the
            // ConsumesInteger test below answers the wrong question for them: an
            // f32tof16 assigned to an integer variable was reinterpreted rather than
            // left alone, and `asint` around half float bits says nothing true.
            FloatToHalfOperation => false,
            HalfToFloatOperation => true,
            ComparisonNode => false,
            Operation operation => operation.ConsumesInteger == false,
            _ => false,
        };
    }

    /// <summary>
    /// Set while compiling the value of an assignment to an integer variable. A
    /// vector constructor has no idea what it is being assigned to, and
    /// `int2 t0 = float2(a, b)` sends both components through a float on the way.
    /// </summary>
    private bool _assigningToInteger;

    // What the multiply was of, or the whole node where the one was folded away.
    private static HlslTreeNode FactoredOfFoldedMultiply(HlslTreeNode node)
    {
        return node is not MultiplyOperation multiply
            ? node
            : multiply.Factor1 is ConstantNode ? multiply.Factor2 : multiply.Factor1;
    }

    // What it was multiplied by, or the one that was folded out.
    private static HlslTreeNode FactorOfFoldedMultiply(HlslTreeNode node)
    {
        return node is not MultiplyOperation multiply
            ? new ConstantNode(1f)
            : multiply.Factor1 is ConstantNode ? multiply.Factor1 : multiply.Factor2;
    }

    private static bool HasConstantAddend(AddOperation add)
    {
        return add.Addend1 is ConstantNode || add.Addend2 is ConstantNode;
    }

    // What the add was of, or the whole node where the add was folded away.
    private static HlslTreeNode BaseOfFoldedAdd(HlslTreeNode node)
    {
        return node is not AddOperation add
            ? node
            : add.Addend1 is ConstantNode ? add.Addend2 : add.Addend1;
    }

    // What was added, or the zero that was folded out. A fresh node rather than one
    // of the graph's: nothing reads it, and the compiler runs over and over.
    private static HlslTreeNode AddendOfFoldedAdd(HlslTreeNode node, bool integer = false)
    {
        return node is not AddOperation add
            ? (integer ? new ConstantNode(0) : new ConstantNode(0f))
            : add.Addend1 is ConstantNode ? add.Addend1 : add.Addend2;
    }

    private string CompileVectorConstructor(List<HlslTreeNode> components, IList<IList<HlslTreeNode>> componentGroups)
    {
        UngroupConstantGroups(componentGroups);

        // Assigning to an integer does not make the components integers. Where one
        // of them computes a float - a constructor inside the float half of an
        // integer assignment, `dot(levels.xyz, float3(...))` under a cast to uint -
        // an int constructor truncates it before the arithmetic that wanted it.
        string type = _assigningToInteger && !components.Any(IsFloatValued)
            ? "int"
            : "float";
        IEnumerable<string> compiledConstructorParts = componentGroups.Select(g => Compile(g, g.Count));
        return $"{type}{components.Count}({string.Join(", ", compiledConstructorParts)})";
    }

    private static void UngroupConstantGroups(IList<IList<HlslTreeNode>> componentGroups)
    {
        int i = 0;
        while (i < componentGroups.Count)
        {
            var componentGroup = componentGroups[i];
            if (componentGroup.All(c => c is ConstantNode))
            {
                componentGroups.RemoveAt(i);
                foreach (var groupComponent in componentGroup)
                {
                    componentGroups.Insert(i, new[] { groupComponent });
                    i++;
                }
            }
            else
            {
                i++;
            }
        }
    }

    private string CompileConstant(List<HlslTreeNode> components, int promoteToVectorSize)
    {
        var constantComponents = components.Cast<ConstantNode>().ToArray();
        return _constantCompiler.Compile(constantComponents);
    }

    /// <summary>
    /// Set while the operands of a float operation are compiled. A bits value read
    /// there is the float those bits are and gets an asfloat; one an integer
    /// operation reads, or a move carries on, is left as it is.
    /// </summary>
    private bool _readingAsFloat;

    private string CompileOperation(Operation operation, List<HlslTreeNode> components, int promoteToVectorSize)
    {
        bool wasReadingAsFloat = _readingAsFloat;
        // From what the operation makes rather than from the flag it was built
        // with: a template that rebuilds an add or a multiply makes a node with no
        // flag on it, and an operation that makes an integer reads integers. The
        // operators that read bits, and the moves that carry them, are named
        // separately because they make an integer out of whatever they are given.
        _readingAsFloat = operation switch
        {
            BitwiseAndOperation or BitwiseOrOperation or BitwiseXorOperation
                or BitwiseNotOperation or ShiftLeftOperation or ShiftRightOperation
                or MoveOperation or MoveConditionalOperation => false,
            _ => StatementFinalizer.IsIntegerValue(operation) != true,
        };
        try
        {
            return CompileOperationOperands(operation, components, promoteToVectorSize);
        }
        finally
        {
            _readingAsFloat = wasReadingAsFloat;
        }
    }

    private string CompileOperationOperands(Operation operation, List<HlslTreeNode> components, int promoteToVectorSize)
    {
        switch (operation)
        {
            case NegateOperation _:
                {
                    string name = operation.Mnemonic;
                    IEnumerable<HlslTreeNode> input = components.Select(g => g.Inputs[0]);
                    bool isAssociative = AssociativityTester.TestForMultiplication(input.First());
                    string value = Compile(input);
                    return isAssociative
                        ? $"-{value}"
                        : $"-({value})";
                }

            // Before ConsumerOperation, which this is one of: firstbithigh names
            // two instructions and HLSL tells them apart by the operand, so the
            // unsigned one says so where the value does not already say it itself.
            // Without the cast, firstbithigh over a temp - which is declared int -
            // compiles back to firstbit_shi, which answers differently for every
            // word with its top bit set.
            case FirstBitHighOperation high:
                {
                    string value = Compile(components.Select(g => g.Inputs[0]));
                    if (high.IsUnsigned && !IsUnsignedAlready(high.Value))
                    {
                        string size = components.Count > 1 ? components.Count.ToString() : "";
                        // A cast binds tighter than the arithmetic it is put in front
                        // of, so anything but a value already standing alone needs
                        // the brackets: `(uint)a + b` casts a and adds b to it.
                        string cast = high.Value is RegisterInputNode or ConstantNode
                            or TempVariableNode or TempAssignmentNode
                            ? value
                            : $"({value})";
                        value = $"(uint{size}){cast}";
                    }
                    return $"firstbithigh({value})";
                }

            case ConsumerOperation _:
                {
                    string name = operation.HlslFunction;
                    string value = Compile(components.Select(g => g.Inputs[0]));
                    return $"{name}({value})";
                }

            case SignGreaterOrEqualOperation _:
            case SignLessOperation _:
                {
                    // sge and slt compare two operands and yield 1 or 0. There is no
                    // HLSL function of that name, and writing one operand as a call
                    // dropped the other silently.
                    string comparison = operation is SignLessOperation ? "<" : ">=";
                    string value1 = CompileOperand(components.Select(g => g.Inputs[0]));
                    string value2 = CompileOperand(components.Select(g => g.Inputs[1]));
                    return $"({value1} {comparison} {value2}) ? 1 : 0";
                }

            case ShiftLeftOperation _:
                {
                    // A shift wants integer operands, and a temp carries no type
                    // here - `t1 << 2` on a float does not compile. fxc emits ishl
                    // for a multiplication by a power of two, so write it back as
                    // one: exact, and it types itself.
                    var amount = components.Select(g => g.Inputs[1]).ToList();
                    if (amount[0] is ConstantNode shift
                        && amount.All(a => a is ConstantNode c && c.Value == shift.Value)
                        && shift.Value >= 0 && shift.Value < 31
                        && shift.Value == (int)shift.Value)
                    {
                        // Parenthesised where a multiplication would not bind the
                        // whole of it: `(a + b) << 16` written as `a + b * 65536`
                        // shifts only the second addend. The sign bit of a packed
                        // half float sat in the first.
                        var shifted = components.Select(g => g.Inputs[0]).ToList();
                        string value = CompileOperand(shifted);
                        return string.Format("{0} * {1}",
                            IsSum(shifted[0]) ? $"({value})" : value,
                            1 << (int)shift.Value);
                    }
                    return string.Format("{0} << {1}",
                        CompileIntegerOperand(components.Select(g => g.Inputs[0])),
                        CompileOperand(amount));
                }

            case ShiftRightOperation shiftRight:
                {
                    string value = CompileIntegerOperand(components.Select(g => g.Inputs[0]));
                    // HLSL reads >> as arithmetic or logical from the type of what
                    // is shifted, so ushr has to say it there. As wide as the value,
                    // since a bare (uint) over two components is X3014. Not over a
                    // value HLSL already reads as unsigned.
                    if (shiftRight.IsUnsigned && !IsUnsignedAlready(shiftRight.Inputs[0]))
                    {
                        string size = components.Count > 1 ? components.Count.ToString() : "";
                        value = $"(uint{size}){value}";
                    }
                    return string.Format("{0} >> {1}", value,
                        CompileOperand(components.Select(g => g.Inputs[1])));
                }

            case BitwiseNotOperation _:
                {
                    IEnumerable<HlslTreeNode> input = components.Select(g => g.Inputs[0]);
                    bool isAssociative = AssociativityTester.TestForMultiplication(input.First());
                    string value = Compile(input);
                    return isAssociative ? $"~{value}" : $"~({value})";
                }

            case BitwiseAndOperation _:
            case BitwiseOrOperation _:
            case BitwiseXorOperation _:
                {
                    string bitwise = operation switch
                    {
                        BitwiseAndOperation _ => "&",
                        BitwiseOrOperation _ => "|",
                        _ => "^",
                    };
                    return string.Format("{0} " + bitwise + " {1}",
                        CompileIntegerOperand(components.Select(g => g.Inputs[0])),
                        CompileIntegerOperand(components.Select(g => g.Inputs[1])));
                }

            case AddOperation _:
                {
                    var addend1 = components.Select(g => g.Inputs[0]);
                    var addend2 = components.Select(g => g.Inputs[1]);
                    // `a + (b + c)` written without the brackets is `(a + b) + c`,
                    // which sums in another order - a different rounding, and a mad
                    // chain fxc no longer sees. Addition commutes, so the sum goes
                    // first: `b + c + a` is the number that was computed.
                    if (IsSum(addend2.First()) && !IsSum(addend1.First()))
                    {
                        (addend1, addend2) = (addend2, addend1);
                    }
                    string right = CompileOperand(addend2);
                    return string.Format("{0} + {1}",
                        CompileOperand(addend1),
                        IsSum(addend2.First()) ? $"({right})" : right);
                }

            case SubtractOperation _:
                {
                    var subtrahend = components.Select(g => g.Inputs[1]);
                    // The subtrahend keeps its brackets: `a - (b + c)` without them
                    // is `a - b + c`, which adds c rather than taking it away.
                    string right = CompileOperand(subtrahend);
                    return string.Format("{0} - {1}",
                        CompileOperand(components.Select(g => g.Inputs[0])),
                        IsSum(subtrahend.First()) ? $"({right})" : right);
                }

            case MultiplyOperation _:
                {
                    var multiplicand1 = components.Select(g => g.Inputs[0]);
                    var multiplicand2 = components.Select(g => g.Inputs[1]);

                    if (!(multiplicand1.First() is ConstantNode) && multiplicand2.First() is ConstantNode)
                    {
                        var temp = multiplicand1;
                        multiplicand1 = multiplicand2;
                        multiplicand2 = temp;
                    }

                    // A quotient on the right is kept as one: `a * (b / c)` is what
                    // was computed, and `a * b / c` rounds differently and shares
                    // nothing with a `b / c` computed elsewhere.
                    bool firstIsAssociative = AssociativityTester.TestForMultiplication(multiplicand1.First());
                    bool secondIsAssociative = AssociativityTester.TestForMultiplication(multiplicand2.First())
                        && !IsQuotient(multiplicand2);
                    string format =
                        (firstIsAssociative ? "{0}" : "({0})") +
                        " * " +
                        (secondIsAssociative ? "{1}" : "({1})");

                    return string.Format(format,
                        Compile(multiplicand1, promoteToVectorSize),
                        Compile(multiplicand2, promoteToVectorSize));
                }

            case ModuloOperation _:
                return string.Format("{0} % {1}",
                    CompileOperand(components.Select(g => g.Inputs[0])),
                    CompileOperand(components.Select(g => g.Inputs[1])));

            case DivisionOperation _:
                {
                    var dividend = components.Select(g => g.Inputs[0]);
                    var divisor = components.Select(g => g.Inputs[1]);

                    // The dividend needs them as much as the divisor does: an add or a
                    // subtract binds more loosely than the division, so `(a - b) / c`
                    // written without them is `a - b / c`, which is a different number.
                    // And a divisor that is itself a product or a quotient needs them
                    // whatever it is made of: `a / (b * c)` without them is `a / b * c`,
                    // which is a times c over b.
                    bool dividendIsAssociative = AssociativityTester.TestForMultiplication(dividend.First());
                    bool divisorIsAssociative = AssociativityTester.TestForMultiplication(divisor.First())
                        && divisor.First() is not MultiplyOperation && !IsQuotient(divisor);
                    string format = (dividendIsAssociative ? "{0}" : "({0})")
                        + " / "
                        + (divisorIsAssociative ? "{1}" : "({1})");

                    return string.Format(format,
                        Compile(dividend),
                        Compile(divisor));
                }

            case MaximumOperation _:
            case MinimumOperation _:
            case PowerOperation _:
                {
                    var value1 = Compile(components.Select(g => g.Inputs[0]));
                    var value2 = Compile(components.Select(g => g.Inputs[1]));

                    var name = operation.HlslFunction;

                    return $"{name}({value1}, {value2})";
                }

            case ConvertOperation convert:
                {
                    // A cast binds tighter than the arithmetic around it, so anything
                    // that is not already a single term needs brackets of its own:
                    // `(int)a + b` would convert only a.
                    var value = components.Select(g => g.Inputs[0]).ToList();
                    string compiledValue = Compile(value);
                    bool isSingleTerm = value[0] is RegisterInputNode
                        || value[0] is ConstantNode
                        || value[0] is TempVariableNode
                        || value[0] is ConvertOperation
                        // A call brings its own brackets. Negation is one of these
                        // and needs none either: a cast over it still applies last.
                        || value[0] is ConsumerOperation;
                    // As wide as what is being converted: `(float)` on a two
                    // component value asks for a constructor with one argument.
                    string castType = components.Count > 1
                        ? convert.TargetType + components.Count
                        : convert.TargetType;
                    return isSingleTerm
                        ? $"({castType}){compiledValue}"
                        : $"({castType})({compiledValue})";
                }

            case LinearInterpolateOperation _:
                {
                    // `lrp dst, s, y, x` is x + s * (y - x), and lerp takes the amount
                    // last: lerp(x, y, s). Passing them straight through made the
                    // amount the first endpoint and the second endpoint the amount.
                    var amount = Compile(components.Select(g => g.Inputs[0]));
                    var to = Compile(components.Select(g => g.Inputs[1]));
                    var from = Compile(components.Select(g => g.Inputs[2]));

                    return $"lerp({from}, {to}, {amount})";
                }

            case CompareOperation _:
                {
                    var value1 = Compile(components.Select(g => g.Inputs[0]));
                    var value2 = Compile(components.Select(g => g.Inputs[1]), components.Count);
                    var value3 = Compile(components.Select(g => g.Inputs[2]), components.Count);

                    return $"{value1} >= 0 ? {value2} : {value3}";
                }
            case GreaterEqualOperation _:
                {
                    var value1 = Compile(components.Select(g => g.Inputs[0]));
                    var value2 = Compile(components.Select(g => g.Inputs[1]));

                    return $"{value1} >= {value2}";
                }
            case DotProductOperation _:
                {
                    // A dot takes its width from the vector, so a repeated component has to
                    // stay written out. dot(r0.ww, r1.xx) is a dp2add; dot(r0.w, r1.x) is
                    // a multiply, and recompiles as one.
                    int vectorSize = components[0].Inputs[0] is GroupNode vector
                        ? vector.Inputs.Count
                        : PromoteToAnyVectorSize;
                    var x = Compile(components.Select(g => g.Inputs[0]), vectorSize);
                    var y = Compile(components.Select(g => g.Inputs[1]), vectorSize);
                    return $"dot({x}, {y})";
                }
            case LengthOperation _:
                {
                    var value1 = Compile(components.Select(g => g.Inputs[0]));
                    return $"length({value1})";
                }
            case LoadStructuredNode load:
                {
                    // TODO: consider the byte offset in Inputs[1].
                    var address = Compile(components.Select(g => g.Inputs[0]));
                    var resource = (RegisterInputNode)components[0].Inputs[2];
                    RegisterKey resourceKey = resource.RegisterComponentKey.RegisterKey;
                    if (load.IsRaw)
                    {
                        // A raw buffer reads as many dwords as the highest component
                        // asked for: Load, Load2, Load3 or Load4 at the byte offset,
                        // and a swizzle after it where the shader took less than all.
                        int width = components.Max(g => ((IHasComponentIndex)g.Inputs[2]).ComponentIndex) + 1;
                        string rawSwizzle = GetAstSourceSwizzleName(
                            components.Select(g => (IHasComponentIndex)g.Inputs[2]), width);
                        string method = width == 1 ? "Load" : $"Load{width}";
                        return $"{_registers.GetRegisterName(resourceKey)}.{method}({address}){rawSwizzle}";
                    }
                    // The subscript picks the element, so a component selection goes
                    // after it. Naming the buffer with a swizzle gave `In.w[i]`.
                    // The load carries no component index of its own; the resource
                    // operand it reads does.
                    string swizzle = GetAstSourceSwizzleName(
                        components.Select(g => (IHasComponentIndex)g.Inputs[2]),
                        _registers.GetRegisterMaskedLength(resourceKey));
                    // The byte offset picks a row where the element is a matrix and
                    // a member where it is a struct.
                    string element = $"{_registers.GetRegisterName(resourceKey)}[{address}]";
                    string members = _registers.NameStructuredMembers(resourceKey, element,
                        load.ElementByteOffset,
                        [.. components.Select(g => ((IHasComponentIndex)g.Inputs[2]).ComponentIndex)]);
                    return members
                        ?? _registers.ApplyStructuredElementRow(resourceKey, element,
                            load.ElementByteOffset) + swizzle;
                }
            case LogicalAndOperation _:
            case LogicalOrOperation _:
                {
                    if (components.All(c => ReferenceEquals(c, operation)) && TryCompileAnyAll(operation, out string anyAll))
                    {
                        return anyAll;
                    }
                    string op = operation is LogicalAndOperation ? "&&" : "||";
                    return string.Format("{0} " + op + " {1}",
                        Compile(components.Select(g => g.Inputs[0])),
                        Compile(components.Select(g => g.Inputs[1])));
                }

            case ClampOperation _:
                return string.Format("clamp({0}, {1}, {2})",
                    Compile(components.Select(g => g.Inputs[0])),
                    Compile(components.Select(g => g.Inputs[1])),
                    Compile(components.Select(g => g.Inputs[2])));

            case SmoothStepOperation _:
                return string.Format("smoothstep({0}, {1}, {2})",
                    Compile(components.Select(g => g.Inputs[0])),
                    Compile(components.Select(g => g.Inputs[1])),
                    Compile(components.Select(g => g.Inputs[2])));

            case StepOperation _:
                return string.Format("step({0}, {1})",
                    Compile(components.Select(g => g.Inputs[0])),
                    Compile(components.Select(g => g.Inputs[1])));

            case FloatingModuloOperation _:
                return string.Format("fmod({0}, {1})",
                    Compile(components.Select(g => g.Inputs[0])),
                    Compile(components.Select(g => g.Inputs[1])));

            case MoveConditionalOperation _:
                {
                    var value1 = Compile(components.Select(g => g.Inputs[0]));
                    var value2 = Compile(components.Select(g => g.Inputs[1]), components.Count);
                    var value3 = Compile(components.Select(g => g.Inputs[2]), components.Count);

                    // Two constant branches are the one place a whole float has
                    // nothing beside it to be typed by: `cond ? 1 : 0` is a pair of
                    // int literals to HLSL, and the arithmetic around it goes
                    // integer with them. It is how a comparison mask anded with the
                    // bits of 1.0f comes out - the set branch is the float, the
                    // clear one the integer zero - and it cost comparison_mask two
                    // instructions. The point goes on the branches that are floats;
                    // a zero beside one is promoted by it.
                    if (AreConstants(components, 1) && AreConstants(components, 2)
                        && (AreFloatConstants(components, 1) || AreFloatConstants(components, 2)))
                    {
                        if (AreFloatConstants(components, 1))
                        {
                            value2 = WithDecimalPoint(value2);
                        }
                        if (AreFloatConstants(components, 2))
                        {
                            value3 = WithDecimalPoint(value3);
                        }
                    }

                    return $"{value1} ? {value2} : {value3}";
                }
            default:
                throw new NotImplementedException(operation.GetType().Name);
        }
    }

    private static bool AreConstants(List<HlslTreeNode> components, int inputIndex)
    {
        return components.All(c => c.Inputs[inputIndex] is ConstantNode);
    }

    // Whether every component of an operand is a constant that stands for a float.
    private static bool AreFloatConstants(List<HlslTreeNode> components, int inputIndex)
    {
        return components.All(c => c.Inputs[inputIndex] is ConstantNode { IntegerValue: null });
    }

    // A float literal HLSL would otherwise read as an integer. Left alone for one
    // that already says what it is - a decimal point, an exponent, or a name like
    // NaN and INF.
    private static string WithDecimalPoint(string value)
    {
        return value.Any(c => !char.IsAsciiDigit(c) && c != (char)45) ? value : value + ".0";
    }

    // The index into an array of matrices counts registers, so it is already the
    // element index times the row count. Undo that multiplication where it is
    // visible rather than emitting a division that only fxc would fold away.
    public string CompileRegisterIndexAsElement(HlslTreeNode index, int rows)
    {
        // DXBC shifts where D3D9 multiplies: `ishl r0.x, v1.x, l(2)` is the element
        // times four.
        if (index is ShiftLeftOperation shift
            && shift.Inputs[1] is ConstantNode shiftAmount
            && rows == 1 << (int)shiftAmount.Value)
        {
            return Compile(new[] { shift.Inputs[0] });
        }
        if (index is MultiplyOperation multiply)
        {
            for (int i = 0; i < 2; i++)
            {
                if (multiply.Inputs[i] is ConstantNode constant && constant.Value == rows)
                {
                    return Compile(new[] { multiply.Inputs[1 - i] });
                }
            }
        }
        return $"{Compile(new[] { index })} / {rows}";
    }

    // An offset shifts the read by whole texels. Leaving it out compiles and
    // reads the wrong ones, so it belongs in the call.
    private static string CompileSampleOffsets(int[] sampleOffsets, ResourceDefinition texture)
    {
        if (sampleOffsets == null)
        {
            return "";
        }

        int dimension = texture.GetDimensionSize();
        string offsets = string.Join(", ", sampleOffsets.Take(dimension)
            .Select(o => o.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return dimension > 1
            ? $", int{dimension}({offsets})"
            : $", {offsets}";
    }

    // An array subscript is an integer, so its literals print as integers and its
    // arithmetic is not sent through a float.
    public string CompileIndexableTempIndex(HlslTreeNode index)
    {
        bool wasAssigningToInteger = _assigningToInteger;
        _assigningToInteger = true;
        try
        {
            return Compile(index);
        }
        finally
        {
            _assigningToInteger = wasAssigningToInteger;
        }
    }

    /// <summary>
    /// Compiles a value standing where an integer is wanted - the element of a
    /// buffer of integers. A vector constructor has no idea what it is being
    /// assigned to, so `countbits(x)` and its neighbours came out inside a float4
    /// on the way into a RWStructuredBuffer&lt;uint4&gt;, which is a conversion each
    /// way and loses everything above what a float holds exactly.
    /// </summary>
    public string CompileAsInteger(IEnumerable<HlslTreeNode> group)
    {
        bool wasAssigningToInteger = _assigningToInteger;
        _assigningToInteger = true;
        try
        {
            return Compile(group);
        }
        finally
        {
            _assigningToInteger = wasAssigningToInteger;
        }
    }

    private string CompileNodesWithComponents(List<HlslTreeNode> components, HlslTreeNode first, int promoteToVectorSize)
    {
        var componentsWithIndices = components.Cast<IHasComponentIndex>();

        if (first is LoopCounterNode)
        {
            return LoopVariableName
                ?? throw new InvalidOperationException("aL used outside a counted loop");
        }

        if (first is IndexableTempLoadNode indexableTempLoad)
        {
            string swizzle = GetAstSourceSwizzleName(componentsWithIndices,
                _registers.IndexableTemps[indexableTempLoad.Register].Components,
                promoteToVectorSize);
            return $"x{indexableTempLoad.Register}[{CompileIndexableTempIndex(indexableTempLoad.Index)}]{swizzle}";
        }

        if (first is RelativeAddressNode relativeAddress)
        {
            RegisterComponentKey arrayKey = relativeAddress.RegisterComponentKey;
            string swizzle = GetAstSourceSwizzleName(componentsWithIndices,
                _registers.GetRegisterMaskedLength(arrayKey.RegisterKey),
                promoteToVectorSize);
            string index = Compile(new[] { relativeAddress.Index });

            // A def-defined array has no declaration to be named from, so it takes
            // the name of the register its run starts at and is declared alongside
            // the uniforms.
            if (_registers.FindConstantArray(arrayKey.RegisterKey) is ConstantArray literals)
            {
                int literalOffset = arrayKey.RegisterKey.Number - literals.BaseRegisterIndex;
                if (literalOffset != 0)
                {
                    index += $" + {literalOffset}";
                }
                return $"{literals.Name}[{index}]{swizzle}";
            }
            // The immediate constant buffer has no declaration to be named from; the
            // disassembly calls it icb and so does the one this writes out.
            if (arrayKey.RegisterKey is D3D10RegisterKey immediateKey
                && immediateKey.OperandType == OperandType.ImmediateConstantBuffer)
            {
                return $"icb[{index}]{swizzle}";
            }

            // Named from the declaration rather than the register, which would carry
            // an element index of its own. The base register need not be the first of
            // the array: `floats[i + 2]` reads c2[a0.x] when floats starts at c0.
            string arrayName = _registers.GetRegisterName(arrayKey);
            if (arrayKey.RegisterKey is D3D10RegisterKey vertexKey
                && vertexKey.OperandType == OperandType.Input
                && vertexKey.GSVertex.HasValue
                && _registers.RegisterDeclarations.TryGetValue(vertexKey, out RegisterDeclaration vertex))
            {
                // The vertex array is the subscript and the semantic the member, the
                // other way round from a constant buffer array.
                return $"i[{index}].{vertex.Name}{swizzle}";
            }
            // A run of input registers declared as one array by dcl_indexrange. Here
            // the semantic is the array and the index its subscript, the way round a
            // constant buffer array has it.
            if (arrayKey.RegisterKey is D3D10RegisterKey inputArrayKey
                && inputArrayKey.OperandType == OperandType.Input
                && !inputArrayKey.GSVertex.HasValue)
            {
                return $"{arrayName}[{index}]{swizzle}";
            }
            if (arrayKey.RegisterKey is D3D10RegisterKey d3d10ArrayKey
                && _registers.FindConstant(d3d10ArrayKey, arrayKey.ComponentIndex)
                    is ConstantDeclaration constantBufferArray)
            {
                // Named from the declaration, which carries no element index of its
                // own, unlike the register.
                arrayName = constantBufferArray.Name;
                int elementOffset = _registers.GetConstantBufferElementOffset(
                    d3d10ArrayKey, constantBufferArray);
                if (constantBufferArray.TypeInfo.MemberInfo != null
                    && constantBufferArray.TypeInfo.NumElements > 1)
                {
                    // An array of structs picked at run time: the index counts
                    // registers across the array, so the element is that over the
                    // registers one takes, and the constant part of the offset says
                    // which register of the element - which member - is read.
                    int stride = constantBufferArray.RegistersPerElement;
                    string element = CompileRegisterIndexAsElement(relativeAddress.Index, stride);
                    if (elementOffset / stride != 0)
                    {
                        element += $" + {elementOffset / stride}";
                    }
                    if (RegisterState.TryGetStructMemberAt(constantBufferArray, $"{arrayName}[{element}]",
                        elementOffset % stride, arrayKey.ComponentIndex, out StructMemberAccess member))
                    {
                        if (member.IsMatrix)
                        {
                            int row = (elementOffset % stride) - member.StartOffset / 4;
                            string matrix = $"transpose({member.Name})";
                            int rowWidth = member.TypeInfo.Rows;
                            swizzle = GetAstSourceSwizzleName(componentsWithIndices, rowWidth, promoteToVectorSize);
                            return $"{matrix}[{row}]{swizzle}";
                        }
                        swizzle = GetAstSourceSwizzleName(
                            componentsWithIndices, member.Width, promoteToVectorSize, member.ComponentBase);
                        return $"{member.Name}{swizzle}";
                    }
                }
                if (constantBufferArray.TypeInfo.Rows > 1)
                {
                    // An array of matrices takes two subscripts, the same as the D3D9
                    // case below: the register index counts rows across the array, so
                    // the element is that index over the row count and the row is what
                    // is left. Indexing it as though each register were an element
                    // gives dot(float4, float4x4).
                    string matrixElement = CompileRegisterIndexAsElement(
                        relativeAddress.Index, constantBufferArray.RegistersPerElement);
                    string matrixName = $"transpose({arrayName}[{matrixElement}])";
                    return $"{matrixName}[{elementOffset}]{swizzle}";
                }
                if (elementOffset != 0)
                {
                    index += $" + {elementOffset}";
                }
                return $"{arrayName}[{index}]{swizzle}";
            }
            if (arrayKey.RegisterKey is D3D9RegisterKey d3d9ArrayKey
                && _registers.FindConstant(d3d9ArrayKey) is ConstantDeclaration array)
            {
                arrayName = array.Name;
                int registerOffset = d3d9ArrayKey.Number - array.RegisterIndex;
                if (array.TypeInfo.Rows > 1)
                {
                    // An array of matrices takes two subscripts. The register index
                    // counts rows across the whole array, so the element is that
                    // index over the row count and the row is the constant left over.
                    string element = CompileRegisterIndexAsElement(
                        relativeAddress.Index, array.RegistersPerElement);
                    string matrix = $"transpose({arrayName}[{element}])";
                    return $"{matrix}[{registerOffset}]{swizzle}";
                }
                if (registerOffset != 0)
                {
                    index += $" + {registerOffset}";
                }
            }
            return $"{arrayName}[{index}]{swizzle}";
        }

        if (first is RegisterInputNode shaderInput)
        {
            var registerKey = shaderInput.RegisterComponentKey.RegisterKey;

            string swizzle = "";
            if (!(registerKey is D3D9RegisterKey d3D9RegisterKey && d3D9RegisterKey.Type == RegisterType.Sampler)
                && !(registerKey is D3D10RegisterKey d3D10RegisterKey && d3D10RegisterKey.OperandType == OperandType.Immediate32))
            {
                // A component base from whichever kind of packing applies - at most one
                // of the two is ever non-zero for a given register.
                int componentBase = _registers.GetConstantComponentBase(shaderInput.RegisterComponentKey)
                    + _registers.GetInputComponentBase(shaderInput.RegisterComponentKey);
                swizzle = GetAstSourceSwizzleName(componentsWithIndices,
                    _registers.GetRegisterMaskedLength(shaderInput.RegisterComponentKey),
                    promoteToVectorSize,
                    componentBase);
            }

            // A named struct member already identifies the component, so it takes no
            // swizzle of its own.
            if (_registers.TryGetConstantMemberName(shaderInput.RegisterComponentKey, out string memberName))
            {
                return memberName;
            }

            string name = _registers.GetRegisterName(shaderInput.RegisterComponentKey);
            return $"{name}{swizzle}";
        }

        if (first is ResourceLoadNode resourceLoad)
        {
            string loadSwizzle = GetAstSourceSwizzleName(componentsWithIndices, 4);
            if (TryCompileTextureBufferLoad(resourceLoad, loadSwizzle, out string textureBufferRead))
            {
                return textureBufferRead;
            }
            // A writable view is read by subscript rather than by Load, which is
            // what the shader said and what a store into it looks like beside it.
            bool isWritableView = resourceLoad.Resource.RegisterComponentKey.RegisterKey
                is D3D10RegisterKey { OperandType: OperandType.UnorderedAccessView };
            ResourceDefinition resourceDefinition = _registers.ResourceDefinitions
                .Where(d => d.ShaderInputType == (isWritableView
                    ? D3DShaderInputType.UavRWTyped
                    : D3DShaderInputType.Texture))
                .First(d => d.BindPoint == resourceLoad.Resource.RegisterComponentKey.RegisterKey.Number);
            // Load addresses in texels, so its coordinate vector is built as ints:
            // as a float3 fxc converts an integer address to float and straight back.
            bool wasAssigningToInteger = _assigningToInteger;
            _assigningToInteger = true;
            string address;
            try
            {
                address = Compile(resourceLoad.Address, resourceLoad.Address.Count());
            }
            finally
            {
                _assigningToInteger = wasAssigningToInteger;
            }
            string loadOffsets = CompileSampleOffsets(resourceLoad.SampleOffsets, resourceDefinition);
            string sampleIndex = resourceLoad.HasSampleIndex
                ? $", {Compile(new[] { resourceLoad.SampleIndex })}"
                : "";
            string loaded = isWritableView
                ? $"{resourceDefinition.Name}[{address}]{loadSwizzle}"
                : $"{resourceDefinition.Name}.Load({address}{sampleIndex}{loadOffsets}){loadSwizzle}";
            // A texel of a texture of uints is an integer, and is named as one
            // where its readers read it as one. Read as anything else it is the
            // bits it holds and not the number they make: a G-buffer packs a depth
            // into such a texture beside a normal, and converting the depth would
            // give whatever number its bits happen to be.
            if (resourceDefinition.IsIntegerReturnType
                && components.All(c => InstructionParser.GetConsumedType(c) != true))
            {
                loaded = $"asfloat({loaded})";
            }
            return loaded;
        }

        if (first is TextureLoadOutputNode textureLoad)
        {
            // From the resource operand, not from the load: the load is named after
            // the component it writes, and the resource says which channel that
            // component came from. The same as LoadStructuredNode above.
            // lod is the exception: its resource swizzle chooses between the two
            // levels rather than naming a channel of a texel, and the level itself
            // is a float. `CalculateLevelOfDetailUnclamped(...).y` is a swizzle of a
            // scalar, which does not compile.
            string swizzle = textureLoad.Controls.HasFlag(TextureLoadControls.CalculateLod)
                ? ""
                : textureLoad.Texture != null
                    ? GetAstSourceSwizzleName(
                        components.Select(c => (IHasComponentIndex)((TextureLoadOutputNode)c).Texture), 4)
                    : GetAstSourceSwizzleName(componentsWithIndices, 4);

            var textureDefinition = _registers.ResourceDefinitions
                .Where(d => d.ShaderInputType == D3DShaderInputType.Texture)
                .FirstOrDefault(d => d.BindPoint == textureLoad.Texture.RegisterComponentKey.RegisterKey.Number);
            if (textureDefinition != null)
            {
                var samplerDefinition = _registers.ResourceDefinitions
                    .Where(d => d.ShaderInputType == D3DShaderInputType.Sampler)
                    .FirstOrDefault(d => d.BindPoint == textureLoad.Sampler.RegisterComponentKey.RegisterKey.Number);
                string texcoords = Compile(textureLoad.TextureCoordinateInputs, textureDefinition.GetDimensionSize());
                // Writing every variant as Sample dropped the level, the gradients or
                // the compared value, and still compiled.
                string method = "Sample";
                string extraArguments = "";
                if (textureLoad.Controls.HasFlag(TextureLoadControls.CalculateLod))
                {
                    method = textureLoad.Controls.HasFlag(TextureLoadControls.Unclamped)
                        ? "CalculateLevelOfDetailUnclamped"
                        : "CalculateLevelOfDetail";
                }
                else if (textureLoad.Controls.HasFlag(TextureLoadControls.Gather))
                {
                    // A comparison gather takes the value to compare against after
                    // the coordinate, the way a comparison sample does.
                    if (textureLoad.Controls.HasFlag(TextureLoadControls.Compare))
                    {
                        method = "GatherCmp";
                        extraArguments = $", {Compile(new[] { textureLoad.ScalarArgument })}";
                    }
                    else
                    {
                        method = "Gather";
                    }
                }
                else if (textureLoad.Controls.HasFlag(TextureLoadControls.Grad))
                {
                    method = "SampleGrad";
                    extraArguments = $", {Compile(textureLoad.DerivativeX)}, {Compile(textureLoad.DerivativeY)}";
                }
                else if (textureLoad.ScalarArgument != null)
                {
                    method = textureLoad.Controls switch
                    {
                        TextureLoadControls.Lod => "SampleLevel",
                        TextureLoadControls.Bias => "SampleBias",
                        TextureLoadControls.Compare => "SampleCmp",
                        _ => "SampleCmpLevelZero",
                    };
                    extraArguments = $", {Compile(new[] { textureLoad.ScalarArgument })}";
                }
                extraArguments += CompileSampleOffsets(textureLoad.SampleOffsets, textureDefinition);
                return $"{textureDefinition.Name}.{method}({samplerDefinition.Name}, {texcoords}{extraArguments}){swizzle}";
            }
            else
            {
                string sampler = Compile(new[] { textureLoad.Sampler });
                string texcoords = Compile(textureLoad.TextureCoordinateInputs);
                var samplerConstant = _registers.FindConstant(RegisterSet.Sampler,
                    textureLoad.Sampler.RegisterComponentKey.RegisterKey.Number);
                string samplerType = samplerConstant.TypeInfo.ParameterType == ParameterType.SamplerCube
                    ? "CUBE"
                    : (samplerConstant.GetSamplerDimension() + "D");
                string bias = textureLoad.Controls.HasFlag(TextureLoadControls.Bias) ? "bias" : "";
                string lod = textureLoad.Controls.HasFlag(TextureLoadControls.Lod) ? "lod" : "";
                string grad = textureLoad.Controls.HasFlag(TextureLoadControls.Grad) ? "grad" : "";
                string gradParams = textureLoad.Controls.HasFlag(TextureLoadControls.Grad)
                    ? (", " + Compile(textureLoad.DerivativeX) + ", " + Compile(textureLoad.DerivativeY))
                    : "";
                string proj = textureLoad.Controls.HasFlag(TextureLoadControls.Project) ? "proj" : "";
                return $"tex{samplerType}{bias}{lod}{grad}{proj}({sampler}, {texcoords}{gradParams}){swizzle}";
            }
        }

        if (first is LitOutputNode lit)
        {
            // Every component reads the same three inputs, so they are compiled from
            // this node rather than gathered across the group.
            string nDotL = Compile(new[] { lit.NDotL });
            string nDotH = Compile(new[] { lit.NDotH });
            string specularPower = Compile(new[] { lit.SpecularPower });
            string litSwizzle = GetAstSourceSwizzleName(componentsWithIndices, 4);
            return $"lit({nDotL}, {nDotH}, {specularPower}){litSwizzle}";
        }

        if (first is NormalizeOutputNode)
        {
            string input = Compile(first.Inputs);
            string swizzle = GetAstSourceSwizzleName(componentsWithIndices, 4);
            return $"normalize({input}){swizzle}";
        }

        if (first is TempAssignmentNode tempAssignment)
        {
            if (tempAssignment.Value is ResourceInfoNode)
            {
                return CompileResourceInfoCall(components.Cast<TempAssignmentNode>().ToList());
            }

            // Compile variable once with all components
            string variableCompiled = Compile(components.Select(a => (a as TempAssignmentNode).TempVariable));

            string type;
            if (tempAssignment.IsReassignment)
            {
                type = string.Empty;
            }
            else
            {
                type = tempAssignment.TempVariable.IsInteger
                    ? tempAssignment.TempVariable.IntegerTypeName
                    : "float";
                if (tempAssignment.TempVariable.VariableSize > 1)
                {
                    type += tempAssignment.TempVariable.VariableSize;
                }
                type += " ";
                variableCompiled = $"t{tempAssignment.TempVariable.DeclarationIndex}";
            }
            bool wasAssigningToInteger = _assigningToInteger;
            _assigningToInteger = tempAssignment.TempVariable.IsInteger;
            string compiled;
            try
            {
                compiled = Compile(components.Select(a => (a as TempAssignmentNode).Value));
            }
            finally
            {
                _assigningToInteger = wasAssigningToInteger;
            }
            // A variable its readers type as an integer, holding a value that is a
            // float: the integer they read is its bits, so the assignment
            // reinterprets rather than converts. Converting rounded a bit pattern
            // to the number nearest it, which is not the same bits at all.
            if (tempAssignment.TempVariable.IsInteger && IsFloatValued(tempAssignment.Value))
            {
                compiled = $"asint({compiled})";
            }
            return $"{type}{variableCompiled} = {compiled};";
        }

        if (first is TempVariableNode tempVariable)
        {
            if (tempVariable.DeclarationIndex == null)
            {
                int index = _tempAssignmentindexCounter;
                _tempAssignmentindexCounter++;
                for (int i = 0; i < components.Count; i++)
                {
                    var component = components[i] as TempVariableNode;
                    component.DeclarationIndex = index;
                    component.ComponentIndex = i;
                    component.VariableSize = components.Count;
                }
            }

            string swizzle = GetAstSourceSwizzleName(componentsWithIndices, (int)tempVariable.VariableSize);
            return $"t{tempVariable.DeclarationIndex}{swizzle}";
        }

        if (first is ResourceInfoNode resourceInfo)
        {
            // A resinfo result standing as an output's own value is read from the
            // variable its call was hoisted into; the hoist rewires every other
            // reader, but a root has none to rewire.
            if (resourceInfo.NamedAs == null)
            {
                throw new NotImplementedException(
                    "A resinfo result was compiled before its GetDimensions call was named.");
            }
            return Compile(components.Select(c => (HlslTreeNode)((ResourceInfoNode)c).NamedAs));
        }

        throw new NotImplementedException();
    }

    /// <summary>
    /// The declaration and the call for a hoisted resinfo: GetDimensions fills out
    /// parameters, so the variable is declared first and the call fills it. Two
    /// statements, which the writer indents line by line.
    /// </summary>
    /// <summary>
    /// A texture buffer is bound to a t register and read with ld, one element at a
    /// time, where a constant buffer is read as cb0[n] - but the block behind it is
    /// the same shape, so the element is a variable of it and not a buffer load.
    /// `ld r0, l(1, 1, 1, 1), t0` over `tbuffer Params { float4 tint; float4
    /// offset; }` is the offset.
    /// </summary>
    private const int BytesPerConstantRegister = 4 * sizeof(float);

    private bool TryCompileTextureBufferLoad(
        ResourceLoadNode resourceLoad, string swizzle, out string compiled)
    {
        compiled = null;
        ResourceDefinition buffer = _registers.ResourceDefinitions
            .Where(d => d.ShaderInputType == D3DShaderInputType.TBuffer)
            .FirstOrDefault(d => d.BindPoint
                == resourceLoad.Resource.RegisterComponentKey.RegisterKey.Number);
        if (buffer == null || resourceLoad.Address.FirstOrDefault() is not ConstantNode element)
        {
            return false;
        }
        int register = element.IntegerValue ?? (int)element.Value;
        D3D10ConstantDeclaration variable = _registers.ConstantDeclarations
            .OfType<D3D10ConstantDeclaration>()
            .Where(d => d.IsTextureBuffer && d.BufferName == buffer.Name)
            .FirstOrDefault(d => d.VariableOffset / BytesPerConstantRegister == register);
        if (variable == null)
        {
            return false;
        }
        compiled = $"{variable.Name}{swizzle}";
        return true;
    }

    private string CompileResourceInfoCall(List<TempAssignmentNode> assignments)
    {
        var info = (ResourceInfoNode)assignments[0].Value;
        TempVariableNode variable = assignments[0].TempVariable;
        RegisterKey resourceKey = info.Resource.RegisterComponentKey.RegisterKey;
        // A buffer is bound as one of several kinds, and which it is decides the
        // overload below as well as where the name comes from.
        ResourceDefinition resource = info.IsBuffer
            ? _registers.ResourceDefinitions.First(d => d.BindPoint == resourceKey.Number
                && d.ShaderInputType is D3DShaderInputType.Structured
                    or D3DShaderInputType.ByteAddress or D3DShaderInputType.UavRWStructured
                    or D3DShaderInputType.UavRWByteAddress)
            : _registers.ResourceDefinitions
                .Where(d => d.ShaderInputType == D3DShaderInputType.Texture)
                .First(d => d.BindPoint == resourceKey.Number);

        string type = info.ReturnType == D3D10ResInfoReturnType.Uint ? "uint" : "float";
        string name = $"t{variable.DeclarationIndex}";
        // A byte address buffer reports one number, and `uint1` is not how a scalar
        // is spelled.
        string width = variable.VariableSize == 1 ? "" : variable.VariableSize.ToString();
        string declaration = $"{type}{width} {name};";

        // The two-wide variable is the two-argument overload; otherwise the full
        // form, which for a 2D texture is the mip level in and width, height and
        // mip count out. z is the depth or array size, which a 2D texture has not.
        // A multisampled texture has no mips and reports how many samples it has in
        // place of the mip count; an array of them reports the element count first,
        // so it takes four and still no mip level.
        // A buffer reports what it holds rather than how big it is: a structured
        // one its element count and its stride, a byte address one its size. The
        // stride is a constant fxc already knows, so the shader reads the count
        // alone and the second out parameter is there to be written into.
        if (info.IsBuffer)
        {
            return declaration + "\r\n" + (info.IsRawBuffer
                ? $"{resource.Name}.GetDimensions({name});"
                : $"{resource.Name}.GetDimensions({name}.x, {name}.y);");
        }
        bool isMultisampled = assignments.Any(a => ((ResourceInfoNode)a.Value).IsSampleCount);
        string call = (isMultisampled, variable.VariableSize) switch
        {
            (true, 4) => $"{resource.Name}.GetDimensions({name}.x, {name}.y, {name}.z, {name}.w);",
            (true, _) => $"{resource.Name}.GetDimensions({name}.x, {name}.y, {name}.z);",
            (false, 2) => $"{resource.Name}.GetDimensions({name}.x, {name}.y);",
            _ => $"{resource.Name}.GetDimensions({Compile(info.MipLevel)}, {name}.x, {name}.y, {name}.w);",
        };
        return declaration + "\r\n" + call;
    }

    /// <summary>
    /// any() and all() over a vector comparison compile to a tree of ors or ands
    /// over its lanes. Written back lane by lane fxc reduces them one at a time;
    /// as the intrinsic over the vector it pairs them up the way it did originally.
    /// </summary>
    private bool TryCompileAnyAll(Operation root, out string compiled)
    {
        compiled = null;
        var leaves = new List<ComparisonNode>();
        var pending = new Stack<HlslTreeNode>();
        pending.Push(root);
        while (pending.Count != 0)
        {
            HlslTreeNode node = pending.Pop();
            if (node.GetType() == root.GetType())
            {
                pending.Push(node.Inputs[0]);
                pending.Push(node.Inputs[1]);
            }
            else if (node is ComparisonNode comparison)
            {
                leaves.Add(comparison);
            }
            else
            {
                return false;
            }
        }
        if (leaves.Count < 2)
        {
            return false;
        }
        // Lane order is immaterial to the intrinsic, and component order is what
        // lets the swizzle drop away.
        leaves.Sort((a, b) => LaneOf(a).CompareTo(LaneOf(b)));
        for (int i = 1; i < leaves.Count; i++)
        {
            if (!_nodeGrouper.CanGroupComponents(leaves[0], leaves[i]))
            {
                return false;
            }
        }
        string intrinsic = root is LogicalAndOperation ? "all" : "any";
        compiled = $"{intrinsic}({Compile(leaves.Cast<HlslTreeNode>().ToList())})";
        return true;
    }

    private static int LaneOf(ComparisonNode comparison)
    {
        return comparison.Left is IHasComponentIndex left ? left.ComponentIndex
            : comparison.Right is IHasComponentIndex right ? right.ComponentIndex
            : 0;
    }

    private string CompileComparison(List<HlslTreeNode> components, ComparisonNode first)
    {
        // A bool constant tested against zero is the bool itself: `if (flag)` is
        // what was written, and `flag != 0` compares an int, which costs fxc a movc
        // to make one of the bool first.
        if (components.Count == 1
            && first.Comparison is IfComparison.NE or IfComparison.EQ
            && first.Right is ConstantNode { Value: 0 }
            && first.Left is RegisterInputNode register
            && _registers.FindConstant(register)?.TypeInfo.ParameterType == ParameterType.Bool)
        {
            string flag = Compile(first.Left);
            return first.Comparison == IfComparison.NE ? flag : $"!{flag}";
        }
        // Through CompileOperand, since a comparison binds tighter than the bitwise
        // operators and the conditional: `(x & 0x7f800000) == 0x7f800000` is a bit
        // test, and written without the brackets it is `x & (a == b)`, which is a
        // different expression and not one HLSL will even accept on a float.
        string left = CompileOperand(components.Cast<ComparisonNode>().Select(c => c.Left));
        string right = CompileOperand(components.Cast<ComparisonNode>().Select(c => c.Right));
        // ult and ilt both read as `a < b`, and HLSL takes the signedness from the
        // operands, so the unsigned form has to say so at one of them - the usual
        // promotion then carries it to the other, which is why one side already
        // being unsigned is enough. Where neither is, the cast goes on the side that
        // is not a constant: `(uint)0 <= x` is a tautology fxc warns about. As wide
        // as the comparison, since a bare (uint) over two components is X3014.
        if (first.IsUnsigned
            && !IsUnsignedAlready(first.Left)
            && !IsUnsignedAlready(first.Right))
        {
            string size = components.Count > 1 ? components.Count.ToString() : "";
            if (first.Left is not ConstantNode)
            {
                left = $"(uint{size}){left}";
            }
            else if (first.Right is not ConstantNode)
            {
                right = $"(uint{size}){right}";
            }
        }
        return $"{left} {first.Comparison.ToHlslString()} {right}";
    }

    /// <summary>
    /// Whether HLSL already reads a value as unsigned, so that it needs no cast of
    /// its own: a variable or a register declared uint, or an expression one of
    /// those reaches. `1664525 * k.x + 1013904223` over a uint4 k is unsigned
    /// arithmetic by promotion, and casting anything in it says nothing new.
    /// </summary>
    private bool IsUnsignedAlready(HlslTreeNode node)
    {
        switch (node)
        {
            case ConvertOperation { TargetType: "uint" }:
            case ShiftRightOperation { IsUnsigned: true }:
            case TempVariableNode { IsUnsigned: true }:
            case TempAssignmentNode { TempVariable.IsUnsigned: true }:
                return true;
            case RegisterInputNode register:
                return IsUnsignedRegister(register);
            // The branches of a select, not the condition: `c ? a : b` takes its
            // type from a and b, and c is a bool by then whatever it was.
            case MoveConditionalOperation select:
                return IsUnsignedAlready(select.Inputs[1]) || IsUnsignedAlready(select.Inputs[2]);
            // Through the operations that promote: an unsigned operand makes the
            // whole of one unsigned, whichever side it is on.
            case AddOperation or SubtractOperation or MultiplyOperation
                or ShiftLeftOperation or MinimumOperation or MaximumOperation
                or BitwiseAndOperation or BitwiseOrOperation or BitwiseXorOperation
                or BitwiseNotOperation or MoveOperation:
                return node.Inputs.Any(IsUnsignedAlready);
            default:
                return false;
        }
    }

    /// <summary>
    /// Whether a register holds an unsigned integer: a constant buffer variable
    /// declared uint, an input whose signature types it one, or a thread id, which
    /// is a uint without being declared anything.
    /// </summary>
    private bool IsUnsignedRegister(RegisterInputNode register)
    {
        RegisterComponentKey key = register.RegisterComponentKey;
        ConstantDeclaration constant = _registers.FindConstant(key.RegisterKey);
        if (constant != null)
        {
            return constant.TypeInfo.ParameterType == ParameterType.Uint;
        }
        return _registers.RegisterDeclarations.TryGetValue(key.RegisterKey, out RegisterDeclaration declaration)
            && declaration.TypeName.Contains("uint");
    }

    /// <param name="componentBase">
    /// Which component of its register the thing being named starts at. A float3
    /// packed after a float sits at .yzw of its register, and naming that eyePos.yzw
    /// asks for components the variable has not got - it is eyePos.xyz.
    /// </param>
    private static string GetAstSourceSwizzleName(IEnumerable<IHasComponentIndex> inputs,
        int registerSize, 
        int promoteToVectorSize = PromoteToAnyVectorSize,
        int componentBase = 0)
    {
        if (registerSize == 1 || registerSize > 4)
        {
            return "";
        }

        string swizzleName = "";
        foreach (int swizzle in inputs.Select(i => i.ComponentIndex - componentBase))
        {
            swizzleName += "xyzw"[swizzle];
        }
        if (promoteToVectorSize != PromoteToAnyVectorSize)
        {
            swizzleName = swizzleName.Substring(0, promoteToVectorSize);
        }

        if (swizzleName.Equals("xyzw".Substring(0, registerSize)))
        {
            return "";
        }

        if (promoteToVectorSize == PromoteToAnyVectorSize && swizzleName.Distinct().Count() == 1)
        {
            return "." + swizzleName.First();
        }

        return "." + swizzleName;
    }
}
