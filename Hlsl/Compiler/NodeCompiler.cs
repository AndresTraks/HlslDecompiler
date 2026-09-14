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
    /// A variable standing for one shared subexpression rather than for a register.
    /// It is numbered here because the counter lives here, and it is given its size
    /// and component up front: the lazy path below numbers a whole register's worth of
    /// components at once, and would make this one the fourth of four.
    /// </summary>
    public TempVariableNode CreateScalarTempVariable()
    {
        return CreateTempVariables(1)[0];
    }

    /// <summary>
    /// One variable per component of a subexpression that spans several. They share
    /// a declaration index, which is what makes the writer name them as one vector -
    /// a value graph holds a four wide operation as four separate nodes, and naming
    /// each of them on its own turns one instruction into four statements that can
    /// never be put back together.
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

    public string Compile(List<HlslTreeNode> components, int promoteToVectorSize = PromoteToAnyVectorSize)
    {
        if (components.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(components));
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
                var vector = Compile(normalize);
                return $"normalize({vector})";
            }
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
            or LinearInterpolateOperation or SmoothStepOperation or MoveConditionalOperation
            or FractionalOperation or FloorOperation or CeilingOperation or RoundOperation
            or TruncateOperation or SquareRootOperation or ReciprocalOperation
            or ReciprocalSquareRootOperation or ExponentialOperation or LogOperation
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
    /// Set while compiling the value of an assignment to an integer variable. A
    /// vector constructor has no idea what it is being assigned to, and
    /// `int2 t0 = float2(a, b)` sends both components through a float on the way.
    /// </summary>
    private bool _assigningToInteger;

    private string CompileVectorConstructor(List<HlslTreeNode> components, IList<IList<HlslTreeNode>> componentGroups)
    {
        UngroupConstantGroups(componentGroups);

        string type = _assigningToInteger ? "int" : "float";
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

    private string CompileOperation(Operation operation, List<HlslTreeNode> components, int promoteToVectorSize)
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
                        return string.Format("{0} * {1}",
                            CompileOperand(components.Select(g => g.Inputs[0])),
                            1 << (int)shift.Value);
                    }
                    return string.Format("{0} << {1}",
                        CompileOperand(components.Select(g => g.Inputs[0])),
                        CompileOperand(amount));
                }

            case ShiftRightOperation shiftRight:
                {
                    string value = CompileOperand(components.Select(g => g.Inputs[0]));
                    // HLSL reads >> as arithmetic or logical from the type of what
                    // is shifted, so ushr has to say it there. As wide as the value,
                    // since a bare (uint) over two components is X3014.
                    if (shiftRight.IsUnsigned)
                    {
                        string size = components.Count > 1 ? components.Count.ToString() : "";
                        value = $"(uint{size}){value}";
                    }
                    return string.Format("{0} >> {1}", value,
                        CompileOperand(components.Select(g => g.Inputs[1])));
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
                        CompileOperand(components.Select(g => g.Inputs[0])),
                        CompileOperand(components.Select(g => g.Inputs[1])));
                }

            case AddOperation _:
                {
                    return string.Format("{0} + {1}",
                        CompileOperand(components.Select(g => g.Inputs[0])),
                        CompileOperand(components.Select(g => g.Inputs[1])));
                }

            case SubtractOperation _:
                {
                    return string.Format("{0} - {1}",
                        CompileOperand(components.Select(g => g.Inputs[0])),
                        CompileOperand(components.Select(g => g.Inputs[1])));
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

                    bool firstIsAssociative = AssociativityTester.TestForMultiplication(multiplicand1.First());
                    bool secondIsAssociative = AssociativityTester.TestForMultiplication(multiplicand2.First());
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
                    bool dividendIsAssociative = AssociativityTester.TestForMultiplication(dividend.First());
                    bool divisorIsAssociative = AssociativityTester.TestForMultiplication(divisor.First());
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
                    return $"{_registers.GetRegisterName(resourceKey)}[{address}]{swizzle}";
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

            case FloatingModuloOperation _:
                return string.Format("fmod({0}, {1})",
                    Compile(components.Select(g => g.Inputs[0])),
                    Compile(components.Select(g => g.Inputs[1])));

            case MoveConditionalOperation _:
                {
                    var value1 = Compile(components.Select(g => g.Inputs[0]));
                    var value2 = Compile(components.Select(g => g.Inputs[1]), components.Count);
                    var value3 = Compile(components.Select(g => g.Inputs[2]), components.Count);

                    return $"{value1} ? {value2} : {value3}";
                }
            default:
                throw new NotImplementedException(operation.GetType().Name);
        }
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
            if (arrayKey.RegisterKey is D3D10RegisterKey d3d10ArrayKey
                && _registers.FindConstant(d3d10ArrayKey, arrayKey.ComponentIndex)
                    is ConstantDeclaration constantBufferArray)
            {
                // Named from the declaration, which carries no element index of its
                // own, unlike the register.
                arrayName = constantBufferArray.Name;
                int elementOffset = _registers.GetConstantBufferElementOffset(
                    d3d10ArrayKey, constantBufferArray);
                if (constantBufferArray.TypeInfo.Rows > 1)
                {
                    // An array of matrices takes two subscripts, the same as the D3D9
                    // case below: the register index counts rows across the array, so
                    // the element is that index over the row count and the row is what
                    // is left. Indexing it as though each register were an element
                    // gives dot(float4, float4x4).
                    string matrixElement = CompileRegisterIndexAsElement(
                        relativeAddress.Index, constantBufferArray.RegistersPerElement);
                    string matrixName = _registers.ColumnMajorOrder
                        ? $"transpose({arrayName}[{matrixElement}])"
                        : $"{arrayName}[{matrixElement}]";
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
                    string matrix = _registers.ColumnMajorOrder
                        ? $"transpose({arrayName}[{element}])"
                        : $"{arrayName}[{element}]";
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
            ResourceDefinition resourceDefinition = _registers.ResourceDefinitions
                .Where(d => d.ShaderInputType == D3DShaderInputType.Texture)
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
            return $"{resourceDefinition.Name}.Load({address}{loadOffsets}){loadSwizzle}";
        }

        if (first is TextureLoadOutputNode textureLoad)
        {
            // From the resource operand, not from the load: the load is named after
            // the component it writes, and the resource says which channel that
            // component came from. The same as LoadStructuredNode above.
            string swizzle = textureLoad.Texture != null
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
                if (textureLoad.Controls.HasFlag(TextureLoadControls.Gather))
                {
                    method = "Gather";
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
                type = tempAssignment.TempVariable.IsInteger ? "int" : "float";
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
    private string CompileResourceInfoCall(List<TempAssignmentNode> assignments)
    {
        var info = (ResourceInfoNode)assignments[0].Value;
        TempVariableNode variable = assignments[0].TempVariable;
        ResourceDefinition resource = _registers.ResourceDefinitions
            .Where(d => d.ShaderInputType == D3DShaderInputType.Texture)
            .First(d => d.BindPoint == info.Resource.RegisterComponentKey.RegisterKey.Number);

        string type = info.ReturnType == D3D10ResInfoReturnType.Uint ? "uint" : "float";
        string name = $"t{variable.DeclarationIndex}";
        string declaration = $"{type}{variable.VariableSize} {name};";

        // The two-wide variable is the two-argument overload; otherwise the full
        // form, which for a 2D texture is the mip level in and width, height and
        // mip count out. z is the depth or array size, which a 2D texture has not.
        string call = variable.VariableSize == 2
            ? $"{resource.Name}.GetDimensions({name}.x, {name}.y);"
            : $"{resource.Name}.GetDimensions({Compile(info.MipLevel)}, {name}.x, {name}.y, {name}.w);";
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
        var left = Compile(components.Cast<ComparisonNode>().Select(c => c.Left));
        var right = Compile(components.Cast<ComparisonNode>().Select(c => c.Right));
        return $"{left} {first.Comparison.ToHlslString()} {right}";
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
