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
                return CompileVectorConstructor(components, componentGroups);
            }

            var multiplication = _nodeGrouper.MatrixMultiplicationGrouper.TryGetMultiplicationGroup(components);
            if (multiplication != null)
            {
                return _matrixMultiplicationCompiler.Compile(multiplication);
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
            return Compile(group.Inputs);
        }

        if (first is PhiNode)
        {
            // Phis are an IR construct. StatementFinalizer lowers them to a temp
            // variable plus its assignments; reaching here means that did not happen.
            throw new InvalidOperationException("Phi node reached compilation without being lowered.");
        }

        throw new NotImplementedException("Unsupported node: " + first.GetType().Name);
    }

    // Compiles a sub-expression, parenthesised when its operator binds more loosely
    // than the one it is being nested into.
    private string CompileOperand(IEnumerable<HlslTreeNode> components, int promoteToVectorSize = PromoteToAnyVectorSize)
    {
        List<HlslTreeNode> list = components.ToList();
        string compiled = Compile(list, promoteToVectorSize);
        return AssociativityTester.NeedsParenthesesAsOperand(list[0])
            ? $"({compiled})"
            : compiled;
    }

    private string CompileVectorConstructor(List<HlslTreeNode> components, IList<IList<HlslTreeNode>> componentGroups)
    {
        UngroupConstantGroups(componentGroups);

        IEnumerable<string> compiledConstructorParts = componentGroups.Select(g => Compile(g, g.Count));
        return $"float{components.Count}({string.Join(", ", compiledConstructorParts)})";
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

                    bool divisorIsAssociative = AssociativityTester.TestForMultiplication(divisor.First());
                    string format = divisorIsAssociative
                        ? "{0} / {1}"
                        : "{0} / ({1})";

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

            case LinearInterpolateOperation _:
                {
                    var value1 = Compile(components.Select(g => g.Inputs[0]));
                    var value2 = Compile(components.Select(g => g.Inputs[1]));
                    var value3 = Compile(components.Select(g => g.Inputs[2]));

                    var name = "lerp";

                    return $"{name}({value1}, {value2}, {value3})";
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
                    var x = Compile(components.Select(g => g.Inputs[0]));
                    var y = Compile(components.Select(g => g.Inputs[1]));
                    return $"dot({x}, {y})";
                }
            case LengthOperation _:
                {
                    var value1 = Compile(components.Select(g => g.Inputs[0]));
                    return $"length({value1})";
                }
            case LoadStructuredNode _:
                {
                    // TODO: consider the byte offset in Inputs[1].
                    var address = Compile(components.Select(g => g.Inputs[0]));
                    var resource = (RegisterInputNode)components[0].Inputs[2];
                    RegisterKey resourceKey = resource.RegisterComponentKey.RegisterKey;
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
                    string op = operation is LogicalAndOperation ? "&&" : "||";
                    return string.Format("{0} " + op + " {1}",
                        Compile(components.Select(g => g.Inputs[0])),
                        Compile(components.Select(g => g.Inputs[1])));
                }

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
    private string CompileRegisterIndexAsElement(HlslTreeNode index, int rows)
    {
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

    private string CompileNodesWithComponents(List<HlslTreeNode> components, HlslTreeNode first, int promoteToVectorSize)
    {
        var componentsWithIndices = components.Cast<IHasComponentIndex>();

        if (first is LoopCounterNode)
        {
            return LoopVariableName
                ?? throw new InvalidOperationException("aL used outside a counted loop");
        }

        if (first is RelativeAddressNode relativeAddress)
        {
            RegisterComponentKey arrayKey = relativeAddress.RegisterComponentKey;
            string swizzle = GetAstSourceSwizzleName(componentsWithIndices,
                _registers.GetRegisterMaskedLength(arrayKey.RegisterKey),
                promoteToVectorSize);
            string index = Compile(new[] { relativeAddress.Index });
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
                        relativeAddress.Index, array.TypeInfo.Rows);
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
                swizzle = GetAstSourceSwizzleName(componentsWithIndices,
                    _registers.GetRegisterMaskedLength(shaderInput.RegisterComponentKey),
                    promoteToVectorSize);
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
            string address = Compile(resourceLoad.Address, resourceLoad.Address.Count());
            return $"{resourceDefinition.Name}.Load({address}){loadSwizzle}";
        }

        if (first is TextureLoadOutputNode textureLoad)
        {
            string swizzle = GetAstSourceSwizzleName(componentsWithIndices, 4);

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
                if (textureLoad.Controls.HasFlag(TextureLoadControls.Grad))
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

        if (first is NormalizeOutputNode)
        {
            string input = Compile(first.Inputs);
            string swizzle = GetAstSourceSwizzleName(componentsWithIndices, 4);
            return $"normalize({input}){swizzle}";
        }

        if (first is TempAssignmentNode tempAssignment)
        {
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
            string compiled = Compile(components.Select(a => (a as TempAssignmentNode).Value));
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

        throw new NotImplementedException();
    }

    private string CompileComparison(List<HlslTreeNode> components, ComparisonNode first)
    {
        var left = Compile(components.Cast<ComparisonNode>().Select(c => c.Left));
        var right = Compile(components.Cast<ComparisonNode>().Select(c => c.Right));
        return $"{left} {first.Comparison.ToHlslString()} {right}";
    }

    private static string GetAstSourceSwizzleName(IEnumerable<IHasComponentIndex> inputs,
        int registerSize, 
        int promoteToVectorSize = PromoteToAnyVectorSize)
    {
        if (registerSize == 1 || registerSize > 4)
        {
            return "";
        }

        string swizzleName = "";
        foreach (int swizzle in inputs.Select(i => i.ComponentIndex))
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
