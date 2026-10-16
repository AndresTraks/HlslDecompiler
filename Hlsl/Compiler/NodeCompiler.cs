using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using HlslDecompiler.Hlsl.TemplateMatch;
using HlslDecompiler.Util;
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

    // Variables numbered during a rolled-back measurement, with their prior state.
    private List<(TempVariableNode Variable, int ComponentIndex, int? VariableSize)>
        _measuredNumbering;

    public int NextTempVariableIndex
    {
        get => _tempAssignmentindexCounter;
        set => _tempAssignmentindexCounter = value;
    }

    /// <summary>
    /// Variables for a shared subexpression, one per component, sized up front (the
    /// lazy path would size them as a whole register). The shared declaration index
    /// makes the writer name them as one vector.
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

    public System.Func<ResourceDefinition, string> StructuredElementTypeName { get; set; }

    // See HlslAst.DoubleValues.
    private readonly ISet<HlslTreeNode> _doubleValues;

    // A variable is not a parsed value, so it answers from its declared type.
    private bool IsDoubleValued(HlslTreeNode node)
    {
        return node switch
        {
            TempVariableNode variable => variable.IsDouble,
            TempAssignmentNode assignment => assignment.TempVariable.IsDouble,
            _ => _doubleValues.Contains(node),
        };
    }

    // The innermost counted loop's variable, which aL refers to; set by the writer.
    public string LoopVariableName { get; set; }

    public NodeCompiler(RegisterState registers, ISet<HlslTreeNode> doubleValues = null)
    {
        _registers = registers;
        _doubleValues = doubleValues ?? new HashSet<HlslTreeNode>();
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
    /// Set while measuring: every expression compiled, with its nodes. What the text
    /// repeats depends on grouping, not the graph, so the writer compiles then counts.
    /// </summary>
    public List<(HlslTreeNode[] Nodes, string Text)> Recording { get; set; }

    /// <summary>
    /// Set alongside Recording: nodes inside a grouper match (not its operands).
    /// Naming one would break the pattern on the next compile.
    /// </summary>
    public HashSet<HlslTreeNode> Grouped { get; set; }

    /// <summary>
    /// The component sets the groupers matched whole, recorded beside
    /// <see cref="Grouped"/> while a statement is being measured.
    /// </summary>
    public List<HlslTreeNode[]> GroupMatches { get; set; }

    /// <summary>
    /// Records a recovered idiom node as one expression. Nothing is marked grouped,
    /// but the match must be recorded so shared-value naming sees it.
    /// </summary>
    private void MarkMatched(IEnumerable<HlslTreeNode> matched)
    {
        if (Grouped == null)
        {
            return;
        }
        GroupMatches?.Add([.. matched]);
    }

    /// <summary>
    /// A vector times the product of two matrices, `mul(p, mul(world, viewProjection))`,
    /// where an fx_2_0 preshader computed the matrix product.
    /// </summary>
    private string TryCompileVectorByMatrixProduct(List<HlslTreeNode> components)
    {
        List<(HlslTreeNode, HlslTreeNode)>[] terms = [.. components.Select(Terms)];
        int rowCount = terms[0]?.Count ?? 0;
        if (rowCount < 2 || terms.Any(t => t == null || t.Count != rowCount))
        {
            return null;
        }

        // Each term is a vector component (shared by all results) times a row element.
        var pairs = new List<(HlslTreeNode Factor, List<HlslTreeNode> Row)>();
        for (int i = 0; i < rowCount; i++)
        {
            (HlslTreeNode factor1, HlslTreeNode factor2) = terms[0][i];
            HlslTreeNode shared = new[] { factor1, factor2 }.FirstOrDefault(candidate => terms.All(t =>
                NodeGrouper.AreNodesEquivalent(t[i].Item1, candidate)
                || NodeGrouper.AreNodesEquivalent(t[i].Item2, candidate)));
            if (shared == null)
            {
                return null;
            }
            pairs.Add((shared, [.. terms.Select(t =>
                NodeGrouper.AreNodesEquivalent(t[i].Item1, shared) ? t[i].Item2 : t[i].Item1)]));
        }
        if (pairs.All(p => p.Factor is IHasComponentIndex))
        {
            pairs = [.. pairs.OrderBy(p => ((IHasComponentIndex)p.Factor).ComponentIndex)];
        }
        List<HlslTreeNode> vector = [.. pairs.Select(p => p.Factor)];
        if (_nodeGrouper.GroupComponents(vector).Count != 1)
        {
            return null;
        }

        // Each row a row of one matrix, in order, times the same second matrix.
        MatrixMultiplicationContext first = null;
        string leftMatrix = null;
        for (int i = 0; i < rowCount; i++)
        {
            MatrixMultiplicationContext multiplication =
                _nodeGrouper.MatrixMultiplicationGrouper.TryGetMultiplicationGroup(pairs[i].Row);
            if (multiplication?.MatrixDeclaration == null
                || _registers.TryGetMatrixRow(multiplication.Vector) is not (string matrix, int row)
                || row != i)
            {
                return null;
            }
            if (i == 0)
            {
                (first, leftMatrix) = (multiplication, matrix);
            }
            else if (matrix != leftMatrix
                || !multiplication.MatrixDeclaration.Equals(first.MatrixDeclaration)
                || multiplication.IsMatrixByVector != first.IsMatrixByVector)
            {
                return null;
            }
        }
        if (_registers.ConstantDeclarations.FirstOrDefault(d => d.Name == leftMatrix)?.TypeInfo.Rows != rowCount)
        {
            return null;
        }
        string product = _matrixMultiplicationCompiler.CompileMatrixProduct(first, leftMatrix);
        if (product == null)
        {
            return null;
        }
        MarkGrouped(components, vector);
        return $"mul({Compile(vector)}, {product})";
    }

    // The products a sum adds up, or null if it adds anything else.
    private static List<(HlslTreeNode, HlslTreeNode)> Terms(HlslTreeNode node)
    {
        switch (node)
        {
            case AddOperation add:
                List<(HlslTreeNode, HlslTreeNode)> left = Terms(add.Addend1);
                List<(HlslTreeNode, HlslTreeNode)> right = Terms(add.Addend2);
                return left == null || right == null ? null : [.. left, .. right];
            case MultiplyAddOperation multiplyAdd:
                List<(HlslTreeNode, HlslTreeNode)> rest = Terms(multiplyAdd.Addend);
                return rest == null ? null : [(multiplyAdd.Factor1, multiplyAdd.Factor2), .. rest];
            case MultiplyOperation multiply:
                return [(multiply.Factor1, multiply.Factor2)];
            default:
                return null;
        }
    }

    private void MarkGrouped(IEnumerable<HlslTreeNode> matched, params IEnumerable<HlslTreeNode>[] operands)
    {
        if (Grouped == null)
        {
            return;
        }
        // A whole match is one expression and may be named as one; a constructor
        // of unrelated values may not.
        GroupMatches?.Add([.. matched]);
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
    /// Compiles a value where a float is wanted (an output or return), so bits are
    /// reinterpreted. An output carries no type of its own.
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

    /// <summary>
    /// Compiles these groups for what they reveal, then discards the text and rolls
    /// back any variable numbering, so measuring has no side effects. Nests.
    /// </summary>
    public CompileMeasurement Measure(IEnumerable<IEnumerable<HlslTreeNode>> groups)
    {
        return Measure(groups, keepNumbering: false);
    }

    /// <summary>
    /// The same compile, keeping the numbers it invents. This is how the writer
    /// numbers variables: numbering depends on grouping, settled only while writing.
    /// Assignment order is later sorted by these numbers, so they must persist.
    /// </summary>
    public CompileMeasurement MeasureAndNumber(IEnumerable<IEnumerable<HlslTreeNode>> groups)
    {
        return Measure(groups, keepNumbering: true);
    }

    private CompileMeasurement Measure(
        IEnumerable<IEnumerable<HlslTreeNode>> groups, bool keepNumbering)
    {
        var measurement = new CompileMeasurement();
        var outerRecording = Recording;
        var outerGrouped = Grouped;
        var outerGroupMatches = GroupMatches;
        var outerNumbering = _measuredNumbering;
        int outerCounter = _tempAssignmentindexCounter;
        // A measurement compiles statements of its own, from the top.
        int outerDepth = _compileDepth;
        _compileDepth = 0;
        var numbering = new List<(TempVariableNode, int, int?)>();
        Recording = measurement.Recording;
        // Null when keeping numbers, so an enclosing measurement does not roll them back.
        Grouped = measurement.Grouped;
        GroupMatches = measurement.GroupMatches;
        _measuredNumbering = keepNumbering ? null : numbering;
        try
        {
            foreach (IEnumerable<HlslTreeNode> group in groups)
            {
                Compile(group);
            }
        }
        finally
        {
            Recording = outerRecording;
            Grouped = outerGrouped;
            GroupMatches = outerGroupMatches;
            _measuredNumbering = outerNumbering;
            _compileDepth = outerDepth;
            // Backwards: a variable may be recorded twice; the first entry is original.
            for (int i = numbering.Count - 1; i >= 0; i--)
            {
                (TempVariableNode variable, int component, int? size) = numbering[i];
                variable.DeclarationIndex = null;
                variable.ComponentIndex = component;
                variable.VariableSize = size;
            }
            if (!keepNumbering)
            {
                _tempAssignmentindexCounter = outerCounter;
            }
        }
        return measurement;
    }

    /// <summary>
    /// Compile nesting depth; 1 at a statement's top. A rewrite that emits an
    /// operator where the node is a call must parenthesize unless at the top.
    /// </summary>
    private int _compileDepth;

    // The top depth: 1 for a statement, 2 for an assignment's value.
    private int _topDepth = 1;

    // The variables the current assignment writes, for an accumulation to name first.
    private HashSet<HlslTreeNode> _assignedVariables;

    public string Compile(List<HlslTreeNode> components, int promoteToVectorSize = PromoteToAnyVectorSize)
    {
        _compileDepth++;
        try
        {
            string compiled = CompileUnrecorded(components, promoteToVectorSize);
            Recording?.Add(([.. components], compiled));
            return compiled;
        }
        finally
        {
            _compileDepth--;
        }
    }

    private string CompileUnrecorded(List<HlslTreeNode> components, int promoteToVectorSize)
    {
        if (components.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(components));
        }

        // A float's bits where a float is wanted: reinterpret, don't convert. Only
        // if every component is bits; the inside is compiled as an integer vector.
        if (_readingAsFloat && components.All(c => ValueTypes.IsReinterpretedAsFloat(c)
            || IsIntegerRegisterReadAsFloat(c)
            || (_compileDepth == _floatArithmeticOperandDepth && IsIntegerVariableReadAsFloat(c))))
        {
            bool wasAssigningToInteger = _assigningToInteger;
            _readingAsFloat = false;
            _assigningToInteger = true;
            try
            {
                return $"asfloat({CompileUnrecorded(components, promoteToVectorSize)})";
            }
            finally
            {
                _readingAsFloat = true;
                _assigningToInteger = wasAssigningToInteger;
            }
        }

        // A float input read by iadd and the like: fxc would have converted it
        // with ftoi had the number been meant.
        if (_compileDepth == _integerArithmeticOperandDepth
            && components.All(c => c is RegisterInputNode or RelativeAddressNode && IsFloatValued(c)))
        {
            bool wasReadingAsFloat = _readingAsFloat;
            int wasIntegerArithmeticOperandDepth = _integerArithmeticOperandDepth;
            _integerArithmeticOperandDepth = -1;
            _readingAsFloat = true;
            try
            {
                return $"{(_assigningToUnsigned ? "asuint" : "asint")}({CompileUnrecorded(components, promoteToVectorSize)})";
            }
            finally
            {
                _readingAsFloat = wasReadingAsFloat;
                _integerArithmeticOperandDepth = wasIntegerArithmeticOperandDepth;
            }
        }

        if (components.Count > 1)
        {
            if (TryCompileVectorByMatrixProduct(components) is string product)
            {
                return product;
            }

            IList<IList<HlslTreeNode>> componentGroups = _nodeGrouper.GroupComponents(components);
            if (componentGroups.Count > 1)
            {
                if (TryCompileTransposedDots(components) is string weighted)
                {
                    return weighted;
                }
                // An elementwise op with exactly one ungrouped operand stays one op
                // with a constructor around that operand, which fxc keeps as one
                // instruction; two half-ops in a constructor it keeps apart.
                if (IsElementwiseAcross(components) && CountUngroupedOperands(components) == 1)
                {
                    return CompileOperation((Operation)components[0], components, promoteToVectorSize);
                }
                if (TryCompileProductWithOne(components) is string productWithOne)
                {
                    return productWithOne;
                }
                return CompileVectorConstructor(components, componentGroups);
            }

            var multiplication = _nodeGrouper.MatrixMultiplicationGrouper.TryGetMultiplicationGroup(components);
            if (multiplication != null)
            {
                // A row index is compiled separately, so it bounds the match too.
                MarkGrouped(components, multiplication.Vector,
                    multiplication.ElementIndexNode == null ? [] : [multiplication.ElementIndexNode]);
                return _matrixMultiplicationCompiler.Compile(multiplication);
            }
            // Dots the multiplication grouper did not take whole have no vector form.
            if (components.All(c => c is DotProductOperation)
                && components.Distinct(ReferenceEqualityComparer.Instance).Count() > 1)
            {
                // Rows in another order are still one multiply, read through a swizzle.
                if (TryCompilePermutedMultiplication(components) is string permuted)
                {
                    return permuted;
                }
                if (TryCompileTransposedDots(components) is string weighted)
                {
                    return weighted;
                }
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

            // Fallback for reflects IdiomRecovery did not reach.
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

        // Components the grouper joined through a folded constant factor (or addend,
        // below) are written back as one op: `t1 + float4(1, 0, 3, 4)`.
        if (components.Count > 1
            && components.Any(c => c is MultiplyOperation)
            && components.Any(c => c is not MultiplyOperation)
            && components.All(c => c is not MultiplyOperation multiply
                || multiply.Factor1 is ConstantNode || multiply.Factor2 is ConstantNode))
        {
            // The restored 1 takes the type of the constants beside it; a float 1
            // among integers would turn an imad into a float mad with conversions.
            bool integerFactors = components
                .OfType<MultiplyOperation>()
                .Select(multiply => FactorOfFoldedMultiply(multiply))
                .All(factor => factor is ConstantNode constant && constant.IntegerValue != null)
                && !components.Any(IsFloatValued);
            List<HlslTreeNode> multiplied = [.. components.Select(FactoredOfFoldedMultiply)];
            List<HlslTreeNode> factors = [..
                components.Select(c => FactorOfFoldedMultiply(c, integerFactors))];
            // Bracket a sum: `a + b * f` multiplies only b.
            string multipliedText = Compile(multiplied, promoteToVectorSize);
            if (IsSum(multiplied[0]) || multipliedText.Contains(" + ") && !IsBracketed(multipliedText)
                && !IsOneCall(multipliedText))
            {
                multipliedText = $"({multipliedText})";
            }
            return $"{multipliedText} * {Compile(factors, factors.Count)}";
        }

        if (components.Count > 1
            && components.Any(c => c is AddOperation)
            && components.Any(c => c is not AddOperation)
            && components.All(c => c is not AddOperation add || HasConstantAddend(add)))
        {
            // The restored 0 takes the type of the constants beside it.
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

        if (first is DoubleConstantNode)
        {
            return CompileDoubleConstant(components);
        }

        if (first is BufferCounterNode counter)
        {
            string counted = _registers.GetRegisterName(
                counter.Buffer.RegisterComponentKey.RegisterKey);
            return counter.IsIncrement
                ? $"{counted}.IncrementCounter()"
                : $"{counted}.DecrementCounter()";
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
            // StatementFinalizer should have lowered every phi.
            throw new InvalidOperationException($"Phi node reached compilation without being lowered: {first} with inputs {string.Join(", ", first.Inputs.Select(i => i.GetType().Name + ":" + i))}.");
        }

        throw new NotImplementedException("Unsupported node: " + first.GetType().Name);
    }

    private static bool IsSum(HlslTreeNode node)
    {
        return node is AddOperation or SubtractOperation;
    }

    // A division not written as normalize(x).
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
        // An attribute evaluation must take the input register itself (fxc crashes
        // on an expression), so only one instruction is one call.
        if (first is EvaluateAttributeOperation
            && components.Any(c => !HlslTreeNode.IsSameInstruction(c, first)))
        {
            return false;
        }
        return components.All(c => Operation.IsSameKind(c, first) && c.Inputs.Count == first.Inputs.Count);
    }

    /// <summary>
    /// `float4(a * b.xyz, b.w)` as `float4(a, 1) * b`: one mul, which fxc can fuse
    /// with the add after it, where the constructor costs a mov beside the mul.
    /// Only under an add: elsewhere the constructor is as cheap, and fxc folds a
    /// negation or a constant into it. The first component must be a product, so
    /// the parent brackets it as one.
    /// </summary>
    private string TryCompileProductWithOne(List<HlslTreeNode> components)
    {
        if (components[0] is not MultiplyOperation
            || !components.All(IsFloatValued)
            || !components.All(c => c.Outputs.Any(o => o is AddOperation))
            || components.OfType<RegisterInputNode>().FirstOrDefault() is not RegisterInputNode passed)
        {
            return null;
        }
        RegisterKey vector = passed.RegisterComponentKey.RegisterKey;
        bool OfVector(HlslTreeNode node) =>
            node is RegisterInputNode register && register.RegisterComponentKey.RegisterKey.Equals(vector);
        var factors = new List<HlslTreeNode>();
        var others = new List<HlslTreeNode>();
        foreach (HlslTreeNode component in components)
        {
            if (OfVector(component))
            {
                factors.Add(component);
                others.Add(new ConstantNode(1f));
            }
            else if (component is MultiplyOperation product
                && product.Inputs.FirstOrDefault(OfVector) is HlslTreeNode factor)
            {
                factors.Add(factor);
                others.Add(product.Inputs[product.Inputs[1] == factor ? 0 : 1]);
            }
            else
            {
                return null;
            }
        }
        if (_nodeGrouper.GroupComponents(factors).Count != 1)
        {
            return null;
        }
        return $"{Compile(others, components.Count)} * {Compile(factors, components.Count)}";
    }

    /// <summary>
    /// A structured access at a computed byte offset: an array member indexed by a
    /// variable, the offset being the member's plus the index times the element
    /// size (`e.w[k]`). Null where the offset is an immediate. Throws where the
    /// offset reaches no array member, rather than write the wrong element.
    /// </summary>
    public string CompileDynamicMember(RegisterKey resourceKey, string element,
        HlslTreeNode byteOffset, IList<int> components)
    {
        if (byteOffset == null || byteOffset is ConstantNode)
        {
            return null;
        }
        HlslTreeNode variable = byteOffset;
        int constant = 0;
        if (byteOffset is AddOperation add
            && add.Inputs.FirstOrDefault(i => i is ConstantNode { IntegerValue: not null }) is ConstantNode addend)
        {
            constant = addend.IntegerValue.Value;
            variable = ReferenceEquals(add.Inputs[0], addend) ? add.Inputs[1] : add.Inputs[0];
        }
        if (_registers.FindStructuredArrayMember(resourceKey, constant)
                is not var (path, bytes, index, component, width)
            || components.Any(c => component + c >= width))
        {
            throw new NotImplementedException(
                $"A structured access at a computed byte offset outside an array member ({resourceKey}).");
        }
        string indexText = CompileElementIndex(variable, bytes);
        if (index != 0)
        {
            indexText = $"{indexText} + {index}";
        }
        bool whole = components.Count == width
            && components.Select((c, i) => component + c == i).All(x => x);
        string swizzle = width == 1 || whole
            ? ""
            : "." + string.Concat(components.Select(c => "xyzw"[component + c]));
        return $"{element}.{path}[{indexText}]{swizzle}";
    }

    /// <summary>
    /// A component of dword-addressed groupshared memory: `g0[i][dword]`, the dword
    /// being the byte offset over four plus the component.
    /// </summary>
    public string CompileGroupSharedDword(string name, string address, HlslTreeNode byteOffset,
        int elementByteOffset, int component)
    {
        string dword;
        if (byteOffset == null || byteOffset is ConstantNode)
        {
            dword = (elementByteOffset / 4 + component).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            dword = CompileElementIndex(byteOffset, 4);
            if (component != 0)
            {
                dword = $"{dword} + {component}";
            }
        }
        return $"{name}[{address}][{dword}]";
    }

    // A byte offset as an element index: `k << 2` or `k * 4` over 4-byte elements
    // is k, fxc's bfi for a masked shift is the masked index; else a division.
    private string CompileElementIndex(HlslTreeNode offset, int bytes)
    {
        int shift = System.Numerics.BitOperations.Log2((uint)bytes);
        bool powerOfTwo = 1 << shift == bytes;
        switch (offset)
        {
            case ShiftLeftOperation { Amount: ConstantNode { IntegerValue: int amount } } left
                when powerOfTwo && amount == shift:
                return CompileAsInteger([left.Value]);
            case MultiplyOperation product
                when product.Inputs.FirstOrDefault(i => i is ConstantNode { IntegerValue: int m } && m == bytes)
                    is HlslTreeNode size:
                return CompileAsInteger([ReferenceEquals(product.Inputs[0], size) ? product.Inputs[1] : product.Inputs[0]]);
            case BitFieldInsertOperation
            {
                Width: ConstantNode { IntegerValue: int bits },
                Offset: ConstantNode { IntegerValue: int at },
                Value: ConstantNode { IntegerValue: 0 },
            } insert when powerOfTwo && at == shift:
                return $"({CompileAsInteger([insert.Insert])} & {(1 << bits) - 1})";
            default:
                return $"({CompileAsInteger([offset])}) / {bytes}";
        }
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

    /// <summary>
    /// What a select tests, bracketed where it is a select itself, since `?:` binds
    /// more loosely than anything else.
    /// </summary>
    private string CompileCondition(List<HlslTreeNode> components)
    {
        // A select between masks used as a condition is a select between bools:
        // `count ? count <= 1 : true`, not `: -1`.
        if (components.Count == 1
            && components[0].Inputs[0] is MoveConditionalOperation masks
            && IsMask(masks.Inputs[1]) && IsMask(masks.Inputs[2])
            && (masks.Inputs[1] is ComparisonNode || masks.Inputs[2] is ComparisonNode))
        {
            return $"({CompileCondition([masks])} ? {CompileMask(masks.Inputs[1])} : {CompileMask(masks.Inputs[2])})";
        }
        string condition = Compile(components.Select(g => g.Inputs[0]));
        return components.Any(g => g.Inputs[0] is CompareOperation or MoveConditionalOperation)
            ? $"({condition})"
            : condition;
    }

    // A permutation that sorts both operands' distinct lanes together, or null if
    // they are already in order or no single order works.
    private static int[] InLaneOrder(IList<HlslTreeNode> left, IList<HlslTreeNode> right)
    {
        if (left.Count < 2
            || !left.Concat(right).All(n => n is IHasComponentIndex)
            || left.Select(n => ((IHasComponentIndex)n).ComponentIndex).Distinct().Count() != left.Count
            || right.Select(n => ((IHasComponentIndex)n).ComponentIndex).Distinct().Count() != right.Count)
        {
            return null;
        }
        int[] order = [.. Enumerable.Range(0, right.Count).OrderBy(i => ((IHasComponentIndex)right[i]).ComponentIndex)];
        if (order.SequenceEqual(Enumerable.Range(0, right.Count)))
        {
            return null;
        }
        for (int i = 1; i < order.Length; i++)
        {
            if (((IHasComponentIndex)left[order[i]]).ComponentIndex <= ((IHasComponentIndex)left[order[i - 1]]).ComponentIndex)
            {
                return null;
            }
        }
        return order;
    }

    // A shift left by one constant amount under 31 is written as a multiply.
    private static bool IsWrittenAsMultiply(List<HlslTreeNode> components)
    {
        if (components.Count == 0 || components[0] is not ShiftLeftOperation)
        {
            return false;
        }
        var amount = components.Select(g => g.Inputs[1]).ToList();
        return components.All(c => c is ShiftLeftOperation)
            && amount[0] is ConstantNode shift
            && amount.All(a => a is ConstantNode c && c.Value == shift.Value)
            && shift.Value >= 0 && shift.Value < 31
            && shift.Value == (int)shift.Value;
    }

    // A comparison, or a constant all ones or all zeroes.
    private static bool IsMask(HlslTreeNode node)
    {
        return node is ComparisonNode
            || node is ConstantNode { IntegerValue: -1 or 0 }
            || node is ConstantNode { IntegerValue: null, Value: 0 };
    }

    private string CompileMask(HlslTreeNode mask)
    {
        return mask is ConstantNode constant
            ? (constant.Value == 0 && constant.IntegerValue is null or 0 ? "false" : "true")
            : Compile(mask);
    }

    // Each result component depends only on that component of the operands.
    // Conversions are left out: `(float4)float4(a, b, c, d)` reads worse.
    internal static bool IsElementwise(Operation operation)
    {
        return operation is AddOperation or SubtractOperation or MultiplyOperation
            or BitFieldExtractOperation or BitFieldInsertOperation
            or MultiplyAddOperation or FusedMultiplyAddOperation
            or DivisionOperation or NegateOperation or AbsoluteOperation
            or MinimumOperation or MaximumOperation or SaturateOperation or ClampOperation
            or LinearInterpolateOperation or SmoothStepOperation or StepOperation
            or MoveConditionalOperation
            or FractionalOperation or FloorOperation or CeilingOperation or RoundOperation
            or TruncateOperation or SquareRootOperation or ReciprocalOperation
            or ReciprocalSquareRootOperation or ExponentialOperation or LogOperation
            or NaturalExponentialOperation or NaturalLogarithmOperation
            or PowerOperation or SineOperation or CosineOperation or SignOperation
            or ArcSineOperation or ArcCosineOperation or ArcTangentOperation or ArcTangent2Operation
            or FloatingModuloOperation or EvaluateAttributeOperation
            or IsNotANumberOperation or IsInfiniteOperation or IsFiniteOperation;
    }

    /// <summary>
    /// ubfe and ibfe as shift and mask, with immediates folded to numbers. ibfe
    /// sign-extends by shifting the field to the top of an int and back down.
    /// </summary>
    private string CompileBitFieldExtract(BitFieldExtractOperation extract, List<HlslTreeNode> components)
    {
        List<HlslTreeNode> widths = [.. components.Select(c => c.Inputs[0])];
        List<HlslTreeNode> offsets = [.. components.Select(c => c.Inputs[1])];
        List<HlslTreeNode> values = [.. components.Select(c => c.Inputs[2])];
        string value = CompileIntegerOperand(values);
        string size = components.Count > 1 ? components.Count.ToString() : "";

        if (extract.IsUnsigned)
        {
            if (!IsUnsignedAlready(extract.Value))
            {
                value = $"(uint{size}){value}";
            }
            if (AllIntegers(widths, out int[] width) && AllIntegers(offsets, out int[] offset))
            {
                string shifted = offset.All(o => o == 0)
                    ? value
                    : $"{value} >> {CompileIntegers(offset)}";
                if (width.All(w => w >= 32))
                {
                    return shifted;
                }
                string masked = offset.All(o => o == 0) ? shifted : $"({shifted})";
                return $"{masked} & {CompileIntegers([.. width.Select(w => w >= 32 ? -1 : (1 << w) - 1)])}";
            }
            return $"({value} >> {CompileOperand(offsets)}) & ((1 << {CompileOperand(widths)}) - 1)";
        }

        if (IsUnsignedAlready(extract.Value))
        {
            value = $"(int{size}){value}";
        }
        if (AllIntegers(widths, out int[] signedWidth) && AllIntegers(offsets, out int[] signedOffset))
        {
            int[] up = [.. signedWidth.Zip(signedOffset, (w, o) => 32 - w - o)];
            int[] down = [.. signedWidth.Select(w => 32 - w)];
            string raised = up.All(u => u == 0) ? value : $"({value} << {CompileIntegers(up)})";
            return down.All(d => d == 0) ? raised : $"{raised} >> {CompileIntegers(down)}";
        }
        string upBy = $"32 - {CompileOperand(widths)} - {CompileOperand(offsets)}";
        return $"({value} << ({upBy})) >> (32 - {CompileOperand(widths)})";
    }

    /// <summary>
    /// bfi as masks: `(v &amp; ~65280) | ((b &lt;&lt; 8) &amp; 65280)`.
    /// </summary>
    private string CompileBitFieldInsert(List<HlslTreeNode> components)
    {
        List<HlslTreeNode> widths = [.. components.Select(c => c.Inputs[0])];
        List<HlslTreeNode> offsets = [.. components.Select(c => c.Inputs[1])];
        string insert = CompileIntegerOperand(components.Select(c => c.Inputs[2]));
        string value = CompileIntegerOperand(components.Select(c => c.Inputs[3]));

        // fxc writes `(x & 7) << 2` as a bfi into zero; omit the kept-base half.
        bool insertsIntoNothing = components.All(c => ConstantMatcher.IsZero(c.Inputs[3]));
        if (AllIntegers(widths, out int[] width) && AllIntegers(offsets, out int[] offset))
        {
            int[] mask = [.. width.Zip(offset, (w, o) => (w >= 32 ? -1 : (1 << w) - 1) << o)];
            string maskText = CompileIntegers(mask);
            string shifted = offset.All(o => o == 0) ? insert : $"({insert} << {CompileIntegers(offset)})";
            // Bracketed, since & binds loosely. Kept bits as a positive number where
            // possible: `& 16777215`, but `& ~65280` rather than `& -65281`.
            string keptText = mask.All(m => ~m >= 0)
                ? CompileIntegers([.. mask.Select(m => ~m)])
                : $"~{maskText}";
            return insertsIntoNothing
                ? $"({shifted} & {maskText})"
                : $"({value} & {keptText}) | ({shifted} & {maskText})";
        }
        string maskExpression = $"(((1 << {CompileOperand(widths)}) - 1) << {CompileOperand(offsets)})";
        return insertsIntoNothing
            ? $"(({insert} << {CompileOperand(offsets)}) & {maskExpression})"
            : $"({value} & ~{maskExpression}) | (({insert} << {CompileOperand(offsets)}) & {maskExpression})";
    }

    private static bool AllIntegers(List<HlslTreeNode> nodes, out int[] values)
    {
        values = new int[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ConstantNode constant)
            {
                return false;
            }
            values[i] = constant.IntegerValue ?? (int)constant.Value;
        }
        return true;
    }

    private string CompileIntegers(int[] values)
    {
        return _constantCompiler.Compile([.. values.Select(v => new ConstantNode(v))]);
    }

    // Compiles a sub-expression, parenthesised when its operator binds more loosely.
    private string CompileOperand(IEnumerable<HlslTreeNode> components, int promoteToVectorSize = PromoteToAnyVectorSize)
    {
        List<HlslTreeNode> list = components.ToList();
        string compiled = Compile(list, promoteToVectorSize);
        // A comparison used as a value is its mask: all ones for true, not 1.
        if (list[0] is ComparisonNode)
        {
            return $"({compiled} ? -1 : 0)";
        }
        // By the node, unless written as one call (e.g. cross for three subtracts).
        return AssociativityTester.NeedsParenthesesAsOperand(list[0]) && !IsOneCall(compiled)
                && !IsBracketed(compiled)
            ? $"({compiled})"
            : compiled;
    }

    /// <summary>
    /// The arguments of text that is exactly one vector constructor of the given
    /// scalar type (`float2(a, b)` gives `a, b`), else null.
    /// </summary>
    public static string ConstructorArguments(string text, string scalarType)
    {
        if (!IsConstructor(text) || !System.Text.RegularExpressions.Regex.IsMatch(text, $@"^{scalarType}[234]\("))
        {
            return null;
        }
        int open = text.IndexOf('(');
        return text[(open + 1)..^1];
    }

    // Whether the text is exactly one vector constructor.
    private static bool IsConstructor(string text)
    {
        return IsOneCall(text)
            && System.Text.RegularExpressions.Regex.IsMatch(text, @"^(float|int|uint|bool)[234]\(");
    }

    // Whether the text is a negated product like `-0.5 * t.y`.
    private static bool IsNegatedProduct(string text)
    {
        if (text.Length < 2 || text[0] != '-' || text[1] == '-' || text[1] == ' ')
        {
            return false;
        }
        int depth = 0;
        for (int i = 1; i < text.Length; i++)
        {
            char c = text[i];
            depth += c == '(' || c == '[' ? 1 : c == ')' || c == ']' ? -1 : 0;
            if (depth == 0 && "+-/%?:<>=&|^!~".Contains(c)
                && !(c == '-' && i > 0 && (text[i - 1] == 'e' || text[i - 1] == 'E')))
            {
                return false;
            }
        }
        return true;
    }

    // Whether swapping operands is safe for fxc: plain arithmetic over registers,
    // constants and variables. Resource and call order set fxc's bindings, and
    // product placement decides which side fuses into a mad.
    private static bool CanReorder(IEnumerable<HlslTreeNode> first, IEnumerable<HlslTreeNode> second, bool products)
    {
        if (!products && first.Concat(second).Any(node => node is MultiplyOperation or MultiplyAddOperation
            or DotProductOperation or ShiftLeftOperation or NegateOperation { Value: MultiplyOperation }))
        {
            return false;
        }
        HashSet<HlslTreeNode> seen = HlslTreeNode.NewNodeSet();
        var pending = new Stack<HlslTreeNode>(first.Concat(second));
        while (pending.Count != 0)
        {
            HlslTreeNode node = pending.Pop();
            if (!seen.Add(node))
            {
                continue;
            }
            switch (node)
            {
                case ConstantNode or RegisterInputNode or TempVariableNode:
                    continue;
                case GroupNode or Operation or ComparisonNode:
                    foreach (HlslTreeNode input in node.Inputs)
                    {
                        pending.Push(input);
                    }
                    continue;
                default:
                    return false;
            }
        }
        return true;
    }

    // Canonical order for commutative operands, independent of fxc's scheduling:
    // temp, input, computed, constant buffer, literal; then natural text order.
    private static bool SortsAfter(HlslTreeNode firstNode, string first, HlslTreeNode secondNode, string second)
    {
        int firstRank = OperandRank(firstNode, first);
        int secondRank = OperandRank(secondNode, second);
        if (firstRank != secondRank)
        {
            return firstRank > secondRank;
        }
        return CompareNatural(first, second) > 0;
    }

    private static int OperandRank(HlslTreeNode node, string text)
    {
        if (node is ConstantNode || IsConstantText(text))
        {
            return 4;
        }
        return node switch
        {
            TempVariableNode or TempAssignmentNode => 0,
            RegisterInputNode register => register.RegisterComponentKey.RegisterKey switch
            {
                D3D10RegisterKey { OperandType: OperandType.ConstantBuffer or OperandType.ImmediateConstantBuffer } => 3,
                D3D9RegisterKey { Type: RegisterType.Const or RegisterType.ConstInt or RegisterType.ConstBool } => 3,
                _ => 1,
            },
            _ => 2,
        };
    }

    // Text order with each run of digits compared as the number it is.
    private static int CompareNatural(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int si = i, sj = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;
                string na = a[si..i].TrimStart('0'), nb = b[sj..j].TrimStart('0');
                int byLength = na.Length.CompareTo(nb.Length);
                if (byLength != 0) return byLength;
                int byDigits = string.CompareOrdinal(na, nb);
                if (byDigits != 0) return byDigits;
                continue;
            }
            int byChar = a[i].CompareTo(b[j]);
            if (byChar != 0) return byChar;
            i++;
            j++;
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }

    private static bool IsConstantText(string text)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(text,
            @"^(-?[0-9][0-9.e+-]*|(float|int|uint|bool)[234]?\([-0-9.e+, ]*\)|true|false)$");
    }

    // Whether the text is in one pair of brackets from end to end.
    private static bool IsBracketed(string text)
    {
        if (text.Length < 2 || text[0] != '(' || text[^1] != ')')
        {
            return false;
        }
        int depth = 0;
        for (int i = 0; i < text.Length; i++)
        {
            depth += text[i] == '(' ? 1 : text[i] == ')' ? -1 : 0;
            if (depth == 0 && i != text.Length - 1)
            {
                return false;
            }
        }
        return depth == 0;
    }

    // Whether the text is exactly one function call.
    private static bool IsOneCall(string text)
    {
        int open = text.IndexOf('(');
        if (open <= 0 || text[^1] != ')'
            || !text.Take(open).All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.'))
        {
            return false;
        }
        int depth = 0;
        for (int i = open; i < text.Length; i++)
        {
            depth += text[i] == '(' ? 1 : text[i] == ')' ? -1 : 0;
            if (depth == 0 && i != text.Length - 1)
            {
                return false;
            }
        }
        return depth == 0;
    }

    private bool IsFloatRegister(RegisterInputNode register)
    {
        return _registers.GetDeclaredType(register.RegisterComponentKey) == DeclaredType.Float;
    }

    // The depth of the operands of the float arithmetic being compiled, or -1.
    // A conversion's operand or an array index deeper down is still an integer.
    private int _floatArithmeticOperandDepth = -1;

    // Likewise for integer arithmetic, where a float input read directly is its bits.
    private int _integerArithmeticOperandDepth = -1;

    // An integer variable read directly by float arithmetic holds a float's bits
    // (fxc reused the register), so it is read back with asfloat.
    private static bool IsIntegerVariableReadAsFloat(HlslTreeNode value)
    {
        return value is TempVariableNode { IsInteger: true, IsBool: false, IsBits: false, IsDouble: false };
    }

    /// <summary>
    /// An SM4 integer uniform or input read by float arithmetic with no conversion:
    /// fxc would have written utof/itof had the number been meant, so these are bits.
    /// Only where every reader that says anything says float.
    /// </summary>
    private bool IsIntegerRegisterReadAsFloat(HlslTreeNode value)
    {
        if (value is not RegisterInputNode register
            || register.RegisterComponentKey.RegisterKey is not D3D10RegisterKey
                { OperandType: OperandType.ConstantBuffer or OperandType.Input
                    or OperandType.InputPatchConstant or OperandType.InputControlPoint
                    or OperandType.OutputControlPoint })
        {
            return false;
        }
        return _registers.GetDeclaredType(register.RegisterComponentKey) is DeclaredType.Int or DeclaredType.Uint
            && ValueTypes.ReadAsInteger(register) == false;
    }

    // An operand of a bitwise op or shift: a float is reinterpreted with asint,
    // since its bits are wanted and HLSL rejects these ops on floats (X3082).
    private string CompileIntegerOperand(IEnumerable<HlslTreeNode> components)
    {
        List<HlslTreeNode> list = components.ToList();
        return IsFloatValued(list[0])
            ? $"asint({Compile(list)})"
            : CompileOperand(list);
    }

    /// <summary>
    /// An integer argument of an interlocked operation; a float there is its bits.
    /// </summary>
    public string CompileIntegerArgument(HlslTreeNode node, bool unsigned = false)
    {
        string compiled = Compile(node);
        if (!IsFloatValued(node))
        {
            return compiled;
        }
        // asint into a uint argument draws fxc warning X3203.
        return unsigned ? $"asuint({compiled})" : $"asint({compiled})";
    }

    // Whether a value is a float, from the value itself: its readers here are
    // integer instructions by construction.
    public bool IsFloatValued(HlslTreeNode node)
    {
        return node switch
        {
            TempVariableNode temp => !temp.IsInteger,
            RegisterInputNode register => IsFloatRegister(register),
            RelativeAddressNode relative => DeclaredTypeOfVertexRead(relative) == DeclaredType.Float,
            ConvertOperation convert => convert.TargetType is not ("int" or "uint"),
            // These read one type and make the other.
            FloatToHalfOperation => false,
            HalfToFloatOperation => true,
            BitsToDoubleOperation => true,
            ComparisonNode => false,
            // Recorded facts, which templates hand on to the nodes they build.
            Operation operation => ValueTypes.ComputesInIntegers(operation) == false,
            _ => false,
        };
    }

    /// <summary>
    /// Whether a value is an integer, a register read with no recorded kind by
    /// its declared type.
    /// </summary>
    public bool IsIntegerValued(HlslTreeNode node)
    {
        // An SM4 register holds what it is declared, however it is read after.
        DeclaredType declared = node switch
        {
            RegisterInputNode { RegisterComponentKey.RegisterKey: D3D10RegisterKey } register
                => _registers.GetDeclaredType(register.RegisterComponentKey),
            RelativeAddressNode relative => DeclaredTypeOfVertexRead(relative),
            _ => DeclaredType.Unknown,
        };
        if (declared is DeclaredType.Int or DeclaredType.Uint)
        {
            return true;
        }
        return ValueTypes.IsIntegerValue(node) == true;
    }

    // A stage input read through a dynamic vertex index (`patch[i].id`).
    private DeclaredType DeclaredTypeOfVertexRead(RelativeAddressNode relative)
    {
        return relative.RegisterComponentKey.RegisterKey is D3D10RegisterKey
            {
                OperandType: OperandType.Input or OperandType.InputControlPoint or OperandType.OutputControlPoint
            }
            ? _registers.GetDeclaredType(relative.RegisterComponentKey)
            : DeclaredType.Unknown;
    }

    /// <summary>
    /// Set while compiling a value assigned to an integer variable, so a constructor
    /// is not written `int2 t0 = float2(a, b)`.
    /// </summary>
    private bool _assigningToInteger;

    /// <summary>
    /// Like _assigningToInteger, but cleared on entering an operation.
    /// </summary>
    private bool _assignedDirectlyToInteger;

    private static HlslTreeNode FactoredOfFoldedMultiply(HlslTreeNode node)
    {
        return node is not MultiplyOperation multiply
            ? node
            : multiply.Factor1 is ConstantNode ? multiply.Factor2 : multiply.Factor1;
    }

    // The constant factor, or a fresh 1 (not a graph node, so nothing reads it).
    private static HlslTreeNode FactorOfFoldedMultiply(HlslTreeNode node, bool integer = false)
    {
        return node is not MultiplyOperation multiply
            ? (integer ? new ConstantNode(1) : new ConstantNode(1f))
            : multiply.Factor1 is ConstantNode ? multiply.Factor1 : multiply.Factor2;
    }

    private static bool HasConstantAddend(AddOperation add)
    {
        return add.Addend1 is ConstantNode || add.Addend2 is ConstantNode;
    }

    private static HlslTreeNode BaseOfFoldedAdd(HlslTreeNode node)
    {
        return node is not AddOperation add
            ? node
            : add.Addend1 is ConstantNode ? add.Addend2 : add.Addend1;
    }

    // The constant addend, or a fresh 0 (not a graph node, so nothing reads it).
    private static HlslTreeNode AddendOfFoldedAdd(HlslTreeNode node, bool integer = false)
    {
        return node is not AddOperation add
            ? (integer ? new ConstantNode(0) : new ConstantNode(0f))
            : add.Addend1 is ConstantNode ? add.Addend1 : add.Addend2;
    }

    /// <summary>
    /// Dots against one weight vector over transposed values, written as a weighted
    /// sum: `float3(dot(float3(t1.x, t6.x, t0.x), k), ...)` is
    /// `t1 * k.x + t6 * k.y + t0 * k.z`. Taken only if the sum measures without
    /// vector constructors.
    /// </summary>
    private string TryCompileTransposedDots(List<HlslTreeNode> components)
    {
        if (!components.All(c => c is DotProductOperation))
        {
            return null;
        }
        List<DotProductOperation> dots = [.. components.Cast<DotProductOperation>()];
        for (int shared = 0; shared < 2; shared++)
        {
            if (dots[0].Inputs[shared] is not GroupNode weights || weights.Length < 2
                || !dots.All(dot => dot.Inputs[shared] is GroupNode other
                    && other.Length == weights.Length
                    && Enumerable.Range(0, weights.Length).All(j => SameValue(other[j], weights[j])))
                || !dots.All(dot => dot.Inputs[1 - shared] is GroupNode values
                    && values.Length == weights.Length)
                // A broadcast dot is one value, not a transpose (asked by value).
                || !Enumerable.Range(0, weights.Length).All(j => ColumnDiffers(dots, 1 - shared, j)))
            {
                continue;
            }
            // Each component as its sum, for the compiler to group across components.
            var built = new List<HlslTreeNode>();
            var sums = new List<HlslTreeNode>();
            foreach (DotProductOperation dot in dots)
            {
                var values = (GroupNode)dot.Inputs[1 - shared];
                HlslTreeNode sum = null;
                for (int j = 0; j < weights.Length; j++)
                {
                    HlslTreeNode product = new MultiplyOperation(values[j], weights[j]) { ConsumesInteger = false };
                    built.Add(product);
                    if (sum != null)
                    {
                        sum = new AddOperation(sum, product) { ConsumesInteger = false };
                        built.Add(sum);
                    }
                    else
                    {
                        sum = product;
                    }
                }
                sums.Add(sum);
            }
            try
            {
                string measured = Measure([sums]).Recording[^1].Text;
                if (System.Text.RegularExpressions.Regex.IsMatch(measured, @"\b(float|int|uint|half|double|bool)[234]\("))
                {
                    continue;
                }
                // Bracketed when nested: the node is a dot, which callers don't bracket.
                string weighted = Compile(sums);
                return _compileDepth > _topDepth ? $"({weighted})" : weighted;
            }
            finally
            {
                // Detach the temporary nodes from the graph's readers.
                foreach (HlslTreeNode node in built)
                {
                    foreach (HlslTreeNode input in node.Inputs)
                    {
                        input.Outputs.Remove(node);
                    }
                }
                // And from the recording, or naming would see them twice.
                HashSet<HlslTreeNode> temporary = HlslTreeNode.NewNodeSet();
                temporary.UnionWith(built);
                Recording?.RemoveAll(record => record.Nodes.Any(temporary.Contains));
                GroupMatches?.RemoveAll(match => match.Any(temporary.Contains));
                Grouped?.ExceptWith(temporary);
            }
        }
        return null;
    }

    // Whether no two of the dots read the same value at this place of this side.
    private static bool ColumnDiffers(List<DotProductOperation> dots, int side, int place)
    {
        var column = dots.Select(dot => ((GroupNode)dot.Inputs[side])[place]).ToList();
        for (int a = 0; a < column.Count; a++)
        {
            for (int b = a + 1; b < column.Count; b++)
            {
                if (SameValue(column[a], column[b]))
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Dots that are a matrix multiply's rows out of order, as the multiply read
    /// through a swizzle; null where no order is the whole matrix.
    /// </summary>
    private string TryCompilePermutedMultiplication(List<HlslTreeNode> components)
    {
        foreach (int[] order in Orders(components.Count).Skip(1))
        {
            List<HlslTreeNode> rows = [.. order.Select(i => components[i])];
            MatrixMultiplicationContext multiplication =
                _nodeGrouper.MatrixMultiplicationGrouper.TryGetMultiplicationGroup(rows);
            if (multiplication == null || multiplication.MatrixRowCount != rows.Count)
            {
                continue;
            }
            MarkGrouped(rows, multiplication.Vector,
                multiplication.ElementIndexNode == null ? [] : [multiplication.ElementIndexNode]);
            string swizzle = string.Concat(Enumerable.Range(0, components.Count)
                .Select(i => "xyzw"[Array.IndexOf(order, i)]));
            return $"{_matrixMultiplicationCompiler.Compile(multiplication)}.{swizzle}";
        }
        return null;
    }

    // All permutations of 0..count-1, identity first.
    private static IEnumerable<int[]> Orders(int count)
    {
        static IEnumerable<int[]> Permute(int[] prefix, int[] rest)
        {
            if (rest.Length == 0)
            {
                yield return prefix;
                yield break;
            }
            for (int i = 0; i < rest.Length; i++)
            {
                foreach (int[] order in Permute([.. prefix, rest[i]], [.. rest.Where((_, j) => j != i)]))
                {
                    yield return order;
                }
            }
        }
        return Permute([], [.. Enumerable.Range(0, count)]);
    }

    private string CompileVectorConstructor(List<HlslTreeNode> components, IList<IList<HlslTreeNode>> componentGroups)
    {
        UngroupConstantGroups(componentGroups);

        // An integer assignment makes an int constructor only if no component is a
        // float; doubles always make a double vector.
        string integerType = _assigningToUnsigned ? "uint" : "int";
        // Mixed components assigned straight to an integer variable: it holds bits,
        // so the float components are reinterpreted.
        bool reinterpretFloats = _assignedDirectlyToInteger
            && components.Any(IsFloatValued) && !components.All(IsFloatValued);
        string type = components.All(IsDoubleValued)
            ? "double"
            : reinterpretFloats || (_assigningToInteger && !components.Any(IsFloatValued))
                ? integerType
                : "float";
        var parts = new List<string>();
        for (int g = 0; g < componentGroups.Count; g++)
        {
            IList<HlslTreeNode> group = componentGroups[g];
            // A run of transposed dots beside other components is offered on its
            // own and stands as one argument.
            if (!reinterpretFloats && group.Count == 1 && group[0] is DotProductOperation)
            {
                int end = g;
                while (end + 1 < componentGroups.Count && componentGroups[end + 1].Count == 1
                    && componentGroups[end + 1][0] is DotProductOperation)
                {
                    end++;
                }
                if (end > g && end - g + 1 < components.Count
                    && TryCompileTransposedDots([.. componentGroups.Skip(g).Take(end - g + 1).Select(r => r[0])]) is string run)
                {
                    parts.Add(run);
                    g = end;
                    continue;
                }
            }
            string compiled = Compile(group, group.Count);
            if (reinterpretFloats && IsFloatValued(group[0]))
            {
                compiled = $"as{integerType}({compiled})";
            }
            // A float constructor read as floats: an integer variable in it holds
            // bits, as one read directly by float arithmetic does.
            else if (type == "float" && _readingAsFloat && group.All(IsIntegerVariableReadAsFloat))
            {
                compiled = $"asfloat({compiled})";
            }
            // A broadcast whose text stays scalar is repeated once per component;
            // a constructor does not pad scalars.
            int copies = group.Count > 1 && BroadcastsScalar(group) ? group.Count : 1;
            parts.AddRange(Enumerable.Repeat(compiled, copies));
        }
        return $"{type}{components.Count}({string.Join(", ", parts)})";
    }

    /// <summary>Whether the group is one value whose text is a scalar.</summary>
    private bool BroadcastsScalar(IList<HlslTreeNode> group)
    {
        HlslTreeNode head = group[0];
        return group.Skip(1).All(c => SameValue(c, head))
            && BroadcastWidth(head, group.Count) == 1;
    }

    private static bool SameValue(HlslTreeNode a, HlslTreeNode b)
    {
        return ReferenceEquals(a, b)
            || (a is DoubleConstantNode doubleA && b is DoubleConstantNode doubleB
                && doubleA.Value == doubleB.Value)
            || NodeGrouper.AreNodesEquivalent(a, b);
    }

    /// <summary>
    /// How wide a broadcast's text comes out when compiled with `requested`
    /// components. Must mirror what each compile case does with the width.
    /// </summary>
    private int BroadcastWidth(HlslTreeNode node, int requested)
    {
        switch (node)
        {
            case ConstantNode or DoubleConstantNode
                or DotProductOperation or LengthOperation or LoadStructuredNode
                or VectorComponentNode:
                return 1;
            case GroupNode vector:
                // Distinct components set their own width; a repeated one is as asked.
                return vector.Inputs.Skip(1).All(c => SameValue(c, vector.Inputs[0]))
                    ? requested
                    : vector.Inputs.Count;
            case MultiplyOperation:
                return MaxBroadcastWidth(node.Inputs, requested);
            // A cast names a vector type: `(float2)`.
            case ConvertOperation:
                return requested;
            case MoveConditionalOperation or CompareOperation:
                // The branches carry the width, not the condition.
                return MaxBroadcastWidth(node.Inputs.Skip(1), requested);
            case ComparisonNode comparison
                when comparison.IsUnsigned && !IsUnsignedAlready(comparison.Left)
                    && !IsUnsignedAlready(comparison.Right):
                // The (uintN) cast is as wide as the comparison.
                return requested;
            case TextureLoadOutputNode lod
                when lod.Controls.HasFlag(TextureLoadControls.CalculateLod):
                return 1;
            case RenderTargetSampleCountNode:
                return 1;
            case TempVariableNode:
                return 1;
            // Other component reads spread into a swizzle.
            case IHasComponentIndex:
                return requested;
            default:
                return MaxBroadcastWidth(node.Inputs, 1);
        }
    }

    private int MaxBroadcastWidth(IEnumerable<HlslTreeNode> nodes, int requested)
    {
        int width = 1;
        foreach (HlslTreeNode node in nodes)
        {
            width = Math.Max(width, BroadcastWidth(node, requested));
        }
        return width;
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

    // Double literals: one number where all agree, else a double constructor.
    private string CompileDoubleConstant(List<HlslTreeNode> components)
    {
        string[] values = [.. components.Select(c =>
            ConstantFormatter.Format(((DoubleConstantNode)c).Value))];
        if (values.All(v => v == values[0]))
        {
            return values[0];
        }
        return $"double{values.Length}({string.Join(", ", values)})";
    }

    private string CompileConstant(List<HlslTreeNode> components, int promoteToVectorSize)
    {
        var constantComponents = components.Cast<ConstantNode>().ToArray();
        return _constantCompiler.Compile(constantComponents, _assigningToUnsigned);
    }

    /// <summary>
    /// Set while compiling a float operation's operands, where bits get an asfloat.
    /// </summary>
    private bool _readingAsFloat;

    private string CompileOperation(Operation operation, List<HlslTreeNode> components, int promoteToVectorSize)
    {
        bool wasReadingAsFloat = _readingAsFloat;
        // From what the operation computes in, else the value it makes. Bitwise ops
        // and moves are listed explicitly: they take whatever they are given.
        bool wasReadingAsBits = _readingAsBits;
        bool wasAssignedDirectly = _assignedDirectlyToInteger;
        int wasFloatArithmeticOperandDepth = _floatArithmeticOperandDepth;
        int wasIntegerArithmeticOperandDepth = _integerArithmeticOperandDepth;
        _assignedDirectlyToInteger = false;
        _readingAsBits = operation is BitwiseAndOperation or BitwiseOrOperation or BitwiseXorOperation
            or BitwiseNotOperation or ShiftLeftOperation or ShiftRightOperation
            or BitFieldExtractOperation or BitFieldInsertOperation;
        _readingAsFloat = operation switch
        {
            BitwiseAndOperation or BitwiseOrOperation or BitwiseXorOperation
                or BitwiseNotOperation or ShiftLeftOperation or ShiftRightOperation
                or BitFieldExtractOperation or BitFieldInsertOperation
                or MoveOperation or MoveConditionalOperation => false,
            _ => (ValueTypes.ComputesInIntegers(operation) ?? ValueTypes.IsIntegerValue(operation)) != true,
        };
        _floatArithmeticOperandDepth = _readingAsFloat
            && operation is AddOperation or SubtractOperation or MultiplyOperation
                or DivisionOperation or MultiplyAddOperation
            ? _compileDepth + 1
            : -1;
        _integerArithmeticOperandDepth = !_readingAsFloat
            && ValueTypes.ComputesInIntegers(operation) == true
            && operation is AddOperation or SubtractOperation or MultiplyOperation
                or DivisionOperation or MultiplyAddOperation
            ? _compileDepth + 1
            : -1;
        try
        {
            return CompileOperationOperands(operation, components, promoteToVectorSize);
        }
        finally
        {
            _readingAsFloat = wasReadingAsFloat;
            _readingAsBits = wasReadingAsBits;
            _assignedDirectlyToInteger = wasAssignedDirectly;
            _floatArithmeticOperandDepth = wasFloatArithmeticOperandDepth;
            _integerArithmeticOperandDepth = wasIntegerArithmeticOperandDepth;
        }
    }

    /// <summary>
    /// Set while compiling a bitwise op's or shift's operands, so an integer texel
    /// is not wrapped in asfloat (`asfloat(mask.Load(p)) &amp; 4` is X3082).
    /// </summary>
    private bool _readingAsBits;

    // Set beside _assigningToInteger for an unsigned variable, so constants say uint.
    private bool _assigningToUnsigned;

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

            // Before ConsumerOperation. HLSL picks firstbit_hi or firstbit_shi by
            // operand type, so the unsigned one casts where needed.
            case FirstBitHighOperation high:
                {
                    string value = Compile(components.Select(g => g.Inputs[0]));
                    if (high.IsUnsigned && !IsUnsignedAlready(high.Value))
                    {
                        string size = components.Count > 1 ? components.Count.ToString() : "";
                        // A cast binds tightly: `(uint)a + b` casts only a.
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

            case BitsToDoubleOperation _:
                {
                    // asdouble takes uints; grouped so a double2 is one call.
                    bool wasAssigningToUnsigned = _assigningToUnsigned;
                    _assigningToUnsigned = true;
                    try
                    {
                        string low = CompileAsInteger(components.Select(c => c.Inputs[0]));
                        string high = CompileAsInteger(components.Select(c => c.Inputs[1]));
                        return $"asdouble({low}, {high})";
                    }
                    finally
                    {
                        _assigningToUnsigned = wasAssigningToUnsigned;
                    }
                }

            case SignGreaterOrEqualOperation _:
            case SignLessOperation _:
                {
                    // sge and slt yield 1 or 0; there is no HLSL function for them.
                    string comparison = operation is SignLessOperation ? "<" : ">=";
                    string value1 = CompileOperand(components.Select(g => g.Inputs[0]));
                    string value2 = CompileOperand(components.Select(g => g.Inputs[1]));
                    return $"({value1} {comparison} {value2}) ? 1 : 0";
                }

            case ShiftLeftOperation _:
                {
                    // fxc emits ishl for a multiply by a power of two; writing it back
                    // as a multiply also avoids `<<` on an untyped float temp.
                    var amount = components.Select(g => g.Inputs[1]).ToList();
                    if (IsWrittenAsMultiply(components) && amount[0] is ConstantNode shift)
                    {
                        // Bracket a sum: `(a + b) * 65536`.
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
                    // HLSL picks the >> kind from the operand type, so ushr casts to
                    // uintN (a bare (uint) over a vector is X3014).
                    if (shiftRight.IsUnsigned && !IsUnsignedAlready(shiftRight.Inputs[0]))
                    {
                        string size = components.Count > 1 ? components.Count.ToString() : "";
                        value = $"(uint{size}){value}";
                    }
                    return string.Format("{0} >> {1}", value,
                        CompileOperand(components.Select(g => g.Inputs[1])));
                }

            case BitFieldExtractOperation extract:
                return CompileBitFieldExtract(extract, components);

            case BitFieldInsertOperation _:
                return CompileBitFieldInsert(components);

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
                    var operand1 = components.Select(g => g.Inputs[0]);
                    var operand2 = components.Select(g => g.Inputs[1]);
                    string text1 = CompileIntegerOperand(operand1);
                    string text2 = CompileIntegerOperand(operand2);
                    // An accumulation names its variable first, `t1 = t1 | bit`; other
                    // operands go in canonical order, since fxc's schedule varies.
                    bool accumulates = _compileDepth == _topDepth && _assignedVariables != null;
                    if (accumulates && operand2.All(_assignedVariables.Contains)
                        && !operand1.Any(_assignedVariables.Contains))
                    {
                        (text1, text2) = (text2, text1);
                    }
                    else if (!(accumulates && operand1.Concat(operand2).Any(_assignedVariables.Contains))
                        && CanReorder(operand1, operand2, products: true)
                        && SortsAfter(operand1.First(), text1, operand2.First(), text2))
                    {
                        (text1, text2) = (text2, text1);
                    }
                    return $"{text1} {bitwise} {text2}";
                }

            case AddOperation _:
                {
                    var addend1 = components.Select(g => g.Inputs[0]);
                    var addend2 = components.Select(g => g.Inputs[1]);
                    bool decided = true;
                    // The sum goes first: `b + c + a` keeps the order `a + (b + c)`
                    // computed, unlike `a + b + c`.
                    if (IsSum(addend2.First()) && !IsSum(addend1.First()))
                    {
                        (addend1, addend2) = (addend2, addend1);
                    }
                    // An accumulation names what it adds to first: `t0 = t0 + x`.
                    else if (_compileDepth == _topDepth && _assignedVariables != null
                        && addend2.All(_assignedVariables.Contains)
                        && !addend1.Any(_assignedVariables.Contains))
                    {
                        (addend1, addend2) = (addend2, addend1);
                    }
                    // A select goes second: `x + (c ? a : b)`.
                    else if (addend1.First() is MoveConditionalOperation or CompareOperation
                        && addend2.First() is not (MoveConditionalOperation or CompareOperation)
                        && !IsSum(addend2.First())
                        && !(_assignedVariables != null && addend1.Any(_assignedVariables.Contains)))
                    {
                        (addend1, addend2) = (addend2, addend1);
                    }
                    else
                    {
                        decided = IsSum(addend1.First()) || IsSum(addend2.First())
                            || (_assignedVariables != null
                                && addend1.Concat(addend2).Any(_assignedVariables.Contains)
                                && _compileDepth == _topDepth);
                    }
                    string right = CompileOperand(addend2);
                    string leftText = CompileOperand(addend1);
                    // Otherwise canonical order, for integer adds only (a float add's
                    // order changes fxc's output) or two named values at the top.
                    bool namedPair = _compileDepth == _topDepth
                        && addend1.All(a => a is TempVariableNode) && addend2.All(a => a is TempVariableNode);
                    if (!decided && (namedPair || (!IsFloatValued(addend1.First()) && !IsFloatValued(addend2.First())))
                        && CanReorder(addend1, addend2, products: false)
                        && SortsAfter(addend1.First(), leftText, addend2.First(), right))
                    {
                        (addend1, addend2) = (addend2, addend1);
                        (leftText, right) = (right, leftText);
                    }
                    // A shift written as a multiply binds as one: `x * 2 + y`, `y + x * 2`.
                    if (IsWrittenAsMultiply(addend1.ToList()) && IsBracketed(leftText))
                    {
                        leftText = leftText[1..^1];
                    }
                    if (IsWrittenAsMultiply(addend2.ToList()) && IsBracketed(right))
                    {
                        right = right[1..^1];
                    }
                    // `0.5 - 0.5 * y`, not `0.5 + -0.5 * y`; negation is exact.
                    if (!IsSum(addend2.First()) && IsNegatedProduct(right))
                    {
                        return $"{leftText} - {right[1..]}";
                    }
                    return string.Format("{0} + {1}",
                        leftText,
                        IsSum(addend2.First()) ? $"({right})" : right);
                }

            case SubtractOperation _:
                {
                    var subtrahend = components.Select(g => g.Inputs[1]);
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

                    // Keep `a * (b / c)`: `a * b / c` rounds differently.
                    bool firstIsAssociative = AssociativityTester.TestForMultiplication(multiplicand1.First());
                    bool secondIsAssociative = AssociativityTester.TestForMultiplication(multiplicand2.First())
                        && !IsQuotient(multiplicand2);
                    string first = Compile(multiplicand1, promoteToVectorSize);
                    string second = Compile(multiplicand2, promoteToVectorSize);
                    // Two named values at the top go in canonical order.
                    if (_compileDepth == _topDepth
                        && multiplicand1.All(m => m is TempVariableNode) && multiplicand2.All(m => m is TempVariableNode)
                        && SortsAfter(multiplicand1.First(), first, multiplicand2.First(), second))
                    {
                        (first, second) = (second, first);
                    }
                    return (firstIsAssociative || IsOneCall(first) ? first : $"({first})")
                        + " * "
                        + (secondIsAssociative || IsOneCall(second) ? second : $"({second})");
                }

            case ModuloOperation _:
                {
                    var dividend = components.Select(g => g.Inputs[0]);
                    var divisor = components.Select(g => g.Inputs[1]);
                    string left = CompileOperand(dividend);
                    string right = CompileOperand(divisor);
                    // % binds like /: bracket a sum, and a divisor that is a product
                    // or quotient.
                    bool bracketDividend = IsSum(dividend.First());
                    bool bracketDivisor = IsSum(divisor.First())
                        || divisor.First() is MultiplyOperation or ModuloOperation
                        || IsQuotient(divisor);
                    return string.Format("{0} % {1}",
                        bracketDividend ? $"({left})" : left,
                        bracketDivisor ? $"({right})" : right);
                }

            case DivisionOperation _:
                {
                    var dividend = components.Select(g => g.Inputs[0]);
                    var divisor = components.Select(g => g.Inputs[1]);

                    // rsq is parsed as 1 / sqrt for the templates; one no template
                    // took is written back as rsqrt.
                    if (ConstantMatcher.IsOne(dividend.First())
                        && divisor.First() is SquareRootOperation)
                    {
                        return $"rsqrt({Compile(divisor.Select(d => d.Inputs[0]))})";
                    }

                    // A divisor that is a product or quotient needs brackets too.
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
            case ArcTangent2Operation _:
                {
                    var value1 = Compile(components.Select(g => g.Inputs[0]));
                    var value2 = Compile(components.Select(g => g.Inputs[1]));

                    // min/max pick the overload from the operands: one unsigned operand
                    // makes the call unsigned.
                    bool isUnsigned = operation is MinimumOperation { IsUnsigned: true }
                        or MaximumOperation { IsUnsigned: true };
                    if (isUnsigned && !IsUnsignedAlready(operation.Inputs[0]) && !IsUnsignedAlready(operation.Inputs[1]))
                    {
                        string size = components.Count > 1 ? components.Count.ToString() : "";
                        value1 = $"(uint{size}){CompileOperand(components.Select(g => g.Inputs[0]))}";
                    }
                    // So imin/imax cast unsigned operands to int.
                    else if (!isUnsigned && operation is MinimumOperation or MaximumOperation
                        && ValueTypes.ComputesInIntegers(operation) == true)
                    {
                        string size = components.Count > 1 ? components.Count.ToString() : "";
                        if (IsUnsignedAlready(operation.Inputs[0]))
                        {
                            value1 = $"(int{size}){CompileOperand(components.Select(g => g.Inputs[0]))}";
                        }
                        if (IsUnsignedAlready(operation.Inputs[1]))
                        {
                            value2 = $"(int{size}){CompileOperand(components.Select(g => g.Inputs[1]))}";
                        }
                    }

                    var name = operation.HlslFunction;

                    return $"{name}({value1}, {value2})";
                }

            case ConvertOperation convert:
                {
                    // A cast binds tightly: `(int)a + b` converts only a.
                    var value = components.Select(g => g.Inputs[0]).ToList();
                    string compiledValue;
                    // Lanes of one utof/itof over unlike values: an integer constructor,
                    // `(float2)uint2(a & 1, a >> 1)`.
                    if (convert.SourceUnsigned != null && convert.TargetType is "float" or "double"
                        && value.Count > 1 && _nodeGrouper.GroupComponents(value).Count > 1)
                    {
                        bool wasAssigningToUnsigned = _assigningToUnsigned;
                        _assigningToUnsigned = convert.SourceUnsigned == true;
                        try
                        {
                            compiledValue = CompileAsInteger(value);
                        }
                        finally
                        {
                            _assigningToUnsigned = wasAssigningToUnsigned;
                        }
                    }
                    else
                    {
                        compiledValue = Compile(value);
                    }
                    bool isSingleTerm = value[0] is RegisterInputNode
                        || value[0] is ConstantNode
                        || value[0] is TempVariableNode
                        || value[0] is ConvertOperation
                        // Calls (and negation) need no extra brackets.
                        || value[0] is ConsumerOperation
                        || value[0] is MaximumOperation or MinimumOperation
                            or PowerOperation or ArcTangent2Operation or ClampOperation or SmoothStepOperation
                            or LinearInterpolateOperation or FusedMultiplyAddOperation
                            or DotProductOperation or LengthOperation
                            or FirstBitHighOperation
                        || value[0] is VectorComponentNode;
                    // As wide as the value: `(float2)`, not `(float)`.
                    string castType = components.Count > 1
                        ? convert.TargetType + components.Count
                        : convert.TargetType;
                    // utof of an int: HLSL would convert it as signed (itof), so it
                    // goes through uint first.
                    if (convert.SourceUnsigned == true && convert.TargetType is "float" or "double"
                        && value.Any(IsSignedAlready))
                    {
                        string uintType = components.Count > 1 ? $"uint{components.Count}" : "uint";
                        compiledValue = isSingleTerm || IsBracketed(compiledValue)
                            ? $"({uintType}){compiledValue}"
                            : $"({uintType})({compiledValue})";
                        isSingleTerm = true;
                    }
                    return isSingleTerm || IsBracketed(compiledValue) || IsConstructor(compiledValue)
                        ? $"({castType}){compiledValue}"
                        : $"({castType})({compiledValue})";
                }

            case LinearInterpolateOperation _:
                {
                    // `lrp dst, s, y, x` is lerp(x, y, s).
                    var amount = Compile(components.Select(g => g.Inputs[0]));
                    var to = Compile(components.Select(g => g.Inputs[1]));
                    var from = Compile(components.Select(g => g.Inputs[2]));

                    return $"lerp({from}, {to}, {amount})";
                }

            case CompareOperation _:
                {
                    var value1 = CompileCondition(components);
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
            case VectorComponentNode select:
                {
                    // The whole vector: narrowing it would shift what the index names.
                    string vector = Compile([select.Vector], select.Vector.Length);
                    return $"{vector}[{CompileAsInteger([select.Index])}]";
                }
            case DotProductOperation _:
                {
                    // Keep the full width: dot(r0.ww, r1.xx) is a dp2add, dot(r0.w, r1.x)
                    // a multiply.
                    int vectorSize = components[0].Inputs[0] is GroupNode vector
                        ? vector.Inputs.Count
                        : PromoteToAnyVectorSize;
                    // dot(a.yzx, b.yzx) is dot(a, b).
                    if (components.Count == 1
                        && components[0].Inputs[0] is GroupNode left
                        && components[0].Inputs[1] is GroupNode right
                        && left.Inputs.Count == right.Inputs.Count
                        && InLaneOrder(left.Inputs, right.Inputs) is int[] order)
                    {
                        return $"dot({Compile([.. order.Select(i => left.Inputs[i])], vectorSize)}, "
                            + $"{Compile([.. order.Select(i => right.Inputs[i])], vectorSize)})";
                    }
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
                    // The address is an integer index; a broadcast reads one element,
                    // so a repeated address stays scalar.
                    var addressInputs = components.Select(g => g.Inputs[0]).ToList();
                    var address = CompileAsInteger(
                        addressInputs.Skip(1).All(a => SameValue(a, addressInputs[0]))
                            ? [addressInputs[0]]
                            : addressInputs);
                    var resource = (RegisterInputNode)components[0].Inputs[2];
                    RegisterKey resourceKey = resource.RegisterComponentKey.RegisterKey;
                    if (_registers.IsDwordAddressedThreadGroupSharedMemory(resourceKey))
                    {
                        string shared = _registers.GetRegisterName(resourceKey);
                        List<string> dwords = [.. components.Select(g => CompileGroupSharedDword(shared, address,
                            ((LoadStructuredNode)g).ByteOffset, ((LoadStructuredNode)g).ElementByteOffset,
                            ((IHasComponentIndex)g.Inputs[2]).ComponentIndex))];
                        string sharedType = _registers.ThreadGroupSharedTypes.TryGetValue(
                            resourceKey.Number, out string declared) ? declared : "float";
                        return dwords.Count == 1 ? dwords[0]
                            : $"{sharedType}{dwords.Count}({string.Join(", ", dwords)})";
                    }
                    if (load.IsGroupShared)
                    {
                        // Raw groupshared memory is declared as an array (or a scalar).
                        return _registers.GroupSharedElementReference(
                            resourceKey, _registers.GetRegisterName(resourceKey), address);
                    }
                    if (load.IsRaw)
                    {
                        // LoadN up to the highest component, then a swizzle.
                        int width = components.Max(g => ((IHasComponentIndex)g.Inputs[2]).ComponentIndex) + 1;
                        string rawSwizzle = GetAstSourceSwizzleName(
                            components.Select(g => (IHasComponentIndex)g.Inputs[2]), width);
                        string method = width == 1 ? "Load" : $"Load{width}";
                        return $"{_registers.GetRegisterName(resourceKey)}.{method}({address}){rawSwizzle}";
                    }
                    // The swizzle goes after the subscript; components come from the
                    // resource operand. The byte offset picks a matrix row or a member.
                    string element = $"{_registers.GetRegisterName(resourceKey)}[{address}]";
                    if (CompileDynamicMember(resourceKey, element, load.ByteOffset,
                        [.. components.Select(g => ((IHasComponentIndex)g.Inputs[2]).ComponentIndex)])
                        is string dynamicMember)
                    {
                        return dynamicMember;
                    }
                    string members = _registers.NameStructuredMembers(resourceKey, element,
                        load.ElementByteOffset,
                        [.. components.Select(g => ((IHasComponentIndex)g.Inputs[2]).ComponentIndex)]);
                    if (members != null)
                    {
                        return members;
                    }
                    // Wide groupshared memory is a struct of synthesized members named
                    // by start offset (no reflection data).
                    if (_registers.ThreadGroupSharedMember(resourceKey, load.ElementByteOffset)
                        is var (memberName, memberComponents, memberBase))
                    {
                        // A load can start in one member and end in the next (.w of
                        // m0 and the float m4): then each component names its own.
                        var lanes = components.Select(g => _registers.ThreadGroupSharedMember(resourceKey,
                            load.ElementByteOffset + ((IHasComponentIndex)g.Inputs[2]).ComponentIndex * 4)).ToList();
                        if (lanes.Any(lane => lane?.Name != memberName))
                        {
                            List<string> parts = [.. lanes.Select(lane => lane.Value.Components == 1
                                ? $"{element}.{lane.Value.Name}"
                                : $"{element}.{lane.Value.Name}.{"xyzw"[lane.Value.ComponentBase]}")];
                            return parts.Count == 1 ? parts[0]
                                : $"{(IsFloatValued(components[0]) ? "float" : "int")}{parts.Count}({string.Join(", ", parts)})";
                        }
                        return $"{element}.{memberName}" + GetAstSourceSwizzleName(
                            components.Select(g => (IHasComponentIndex)g.Inputs[2]),
                            memberComponents,
                            componentBase: -memberBase);
                    }
                    string row = _registers.ApplyStructuredElementRow(resourceKey, element,
                        load.ElementByteOffset);
                    // For a scalar or vector element the offset selects a component
                    // (.w of a uint4 is offset 12), so it is the swizzle's base.
                    int componentBase = ReferenceEquals(row, element)
                        ? -load.ElementByteOffset / 4
                        : 0;
                    // A double takes two components; the swizzle names values.
                    int loadPerElement = _registers.GetStructuredComponentsPerElement(resourceKey);
                    return row + GetAstSourceSwizzleName(
                        components.Select(g => (IHasComponentIndex)g.Inputs[2]),
                        _registers.GetRegisterMaskedLength(resourceKey) / loadPerElement,
                        componentBase: componentBase,
                        componentsPerElement: loadPerElement);
                }
            case LogicalAndOperation _:
            case LogicalOrOperation _:
                {
                    if (components.All(c => ReferenceEquals(c, operation)) && TryCompileAnyAll(operation, out string anyAll))
                    {
                        return anyAll;
                    }
                    string op = operation is LogicalAndOperation ? "&&" : "||";
                    // An or under an and keeps its brackets: && binds tighter.
                    string Operand(int index)
                    {
                        string text = Compile(components.Select(g => g.Inputs[index]));
                        return operation is LogicalAndOperation
                            && components[0].Inputs[index] is LogicalOrOperation
                            && !text.StartsWith("any(")
                            ? $"({text})"
                            : text;
                    }
                    return $"{Operand(0)} {op} {Operand(1)}";
                }

            case ClampOperation _:
                return string.Format("clamp({0}, {1}, {2})",
                    Compile(components.Select(g => g.Inputs[0])),
                    Compile(components.Select(g => g.Inputs[1])),
                    Compile(components.Select(g => g.Inputs[2])));

            case FusedMultiplyAddOperation _:
                return string.Format("fma({0}, {1}, {2})",
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

            case EvaluateAttributeOperation evaluate:
                {
                    // The sample index or offset is one integer for all components.
                    string attribute = Compile(components.Select(g => g.Inputs[0]));
                    if (evaluate.Place == EvaluateAttributeAt.Centroid)
                    {
                        return $"{evaluate.HlslName}({attribute})";
                    }
                    string at = CompileAsInteger(
                        evaluate.Inputs.Skip(1).ToList());
                    return $"{evaluate.HlslName}({attribute}, {at})";
                }

            case FloatingModuloOperation _:
                return string.Format("fmod({0}, {1})",
                    Compile(components.Select(g => g.Inputs[0])),
                    Compile(components.Select(g => g.Inputs[1])));

            case MoveConditionalOperation _:
                {
                    var value1 = CompileCondition(components);

                    // A movc moves bits, but HLSL's mixed select converts. So a branch
                    // of the other kind is reinterpreted to the kind the select is
                    // read as - compiled in its own kind, so a constructor of
                    // integers under asfloat is an int vector, not a float one that
                    // converted them. Constants don't say their kind.
                    bool BranchIs(int input, bool floats) => components.All(c =>
                        c.Inputs[input] is not ConstantNode
                        && (floats ? IsFloatValued(c.Inputs[input]) : IsIntegerValued(c.Inputs[input])));
                    string CompileBranch(int branch)
                    {
                        int other = 3 - branch;
                        IEnumerable<HlslTreeNode> nodes = components.Select(g => g.Inputs[branch]);
                        bool intoInteger = _assigningToInteger;
                        if (intoInteger && components.Count > 1
                            && components.Any(c => c.Inputs[branch] is not ConstantNode && IsFloatValued(c.Inputs[branch]))
                            && components.Any(c => c.Inputs[branch] is not ConstantNode && IsIntegerValued(c.Inputs[branch])))
                        {
                            // Mixed, so each float component is reinterpreted by itself.
                            string reinterpret = _assigningToUnsigned ? "asuint" : "asint";
                            List<string> parts = [.. nodes.Select(n => n is not ConstantNode && IsFloatValued(n)
                                ? $"{reinterpret}({CompileAsFloat([n])})"
                                : CompileAsInteger([n]))];
                            return $"{(_assigningToUnsigned ? "uint" : "int")}{parts.Count}({string.Join(", ", parts)})";
                        }
                        // Into an integer, a float branch is bits even beside constants.
                        bool otherIsConstant = components.All(c => c.Inputs[other] is ConstantNode);
                        if (!(intoInteger ? BranchIs(branch, true) && (BranchIs(other, false) || otherIsConstant)
                            : BranchIs(branch, false) && BranchIs(other, true)))
                        {
                            return Compile(nodes, components.Count);
                        }
                        string call = intoInteger ? (_assigningToUnsigned ? "asuint" : "asint") : "asfloat";
                        bool wasAssigningToInteger = _assigningToInteger;
                        bool wasReadingAsFloat = _readingAsFloat;
                        _assigningToInteger = !intoInteger;
                        _readingAsFloat = intoInteger;
                        try
                        {
                            return $"{call}({Compile(nodes, components.Count)})";
                        }
                        finally
                        {
                            _assigningToInteger = wasAssigningToInteger;
                            _readingAsFloat = wasReadingAsFloat;
                        }
                    }
                    var value2 = CompileBranch(1);
                    var value3 = CompileBranch(2);

                    // `cond ? 1 : 0` is int to HLSL; a float constant branch gets a
                    // decimal point (the other is promoted by it).
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

                    // Conditions that don't form one vector make separate selects, not
                    // `float2(a, b) ? 1.0 : 0`.
                    if (components.Count > 1 && IsConstructor(value1))
                    {
                        string selects = string.Join(", ", components.Select(c =>
                            $"{Compile([c.Inputs[0]])} ? {Branch(c.Inputs[1])} : {Branch(c.Inputs[2])}"));
                        return $"float{components.Count}({selects})";

                        string Branch(HlslTreeNode branch)
                        {
                            string text = Compile([branch]);
                            return branch is ConstantNode { IntegerValue: null } ? WithDecimalPoint(text) : text;
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

    private static bool AreFloatConstants(List<HlslTreeNode> components, int inputIndex)
    {
        return components.All(c => c.Inputs[inputIndex] is ConstantNode { IntegerValue: null });
    }

    // Adds ".0" to a literal of only digits and minus signs.
    private static string WithDecimalPoint(string value)
    {
        return value.Any(c => !char.IsAsciiDigit(c) && c != (char)45) ? value : value + ".0";
    }

    // A register index into an array of matrices is element * rows; undo a visible
    // multiply rather than emitting a division.
    public string CompileRegisterIndexAsElement(RelativeAddressNode address, int rows)
    {
        return address.IndexCountsElements
            ? CompileAsInteger([address.Index])
            : CompileRegisterIndexAsElement(address.Index, rows);
    }

    public string CompileRegisterIndexAsElement(HlslTreeNode index, int rows)
    {
        // DXBC shifts where D3D9 multiplies.
        if (index is ShiftLeftOperation shift
            && shift.Inputs[1] is ConstantNode shiftAmount
            && rows == 1 << (int)shiftAmount.Value)
        {
            return CompileAsInteger([shift.Inputs[0]]);
        }
        if (index is MultiplyOperation multiply)
        {
            for (int i = 0; i < 2; i++)
            {
                if (multiply.Inputs[i] is ConstantNode constant && constant.Value == rows)
                {
                    return CompileAsInteger([multiply.Inputs[1 - i]]);
                }
            }
        }
        // fxc masks and scales in one bfi: `m[(i + 1) & 7]` is
        // `bfi r0.x, l(3), l(2), i + 1, l(0)`. Dropping the shift recovers the mask.
        if (index is BitFieldInsertOperation insert
            && insert.Inputs[0] is ConstantNode { IntegerValue: int width }
            && insert.Inputs[1] is ConstantNode { IntegerValue: int offset }
            && ConstantMatcher.IsZero(insert.Inputs[3])
            && width < 32 && rows == 1 << offset)
        {
            // Both brackets are needed: & binds more loosely than + and more
            // tightly than | and ^.
            string masked = CompileAsInteger([insert.Inputs[2]]);
            if (insert.Inputs[2] is not (RegisterInputNode or ConstantNode
                or TempVariableNode or TempAssignmentNode))
            {
                masked = $"({masked})";
            }
            return $"({masked} & {(1 << width) - 1})";
        }
        return $"{CompileAsInteger([index])} / {rows}";
    }

    /// <summary>
    /// A Load address that is a texel plus a constant, written as
    /// `int3(t2, 0) + int3(1, 0, 0)` rather than `int3(t2 + int2(1, 0), 0)`, which
    /// fxc compiles to one more instruction. Only with a constant mip level.
    /// </summary>
    private string CompileOffsetAddress(List<HlslTreeNode> address)
    {
        if (address.Count < 2 || address[^1] is not ConstantNode mip
            || (mip.IntegerValue is null && mip.Value != MathF.Floor(mip.Value)))
        {
            return null;
        }
        var bases = new List<HlslTreeNode>();
        var offsets = new List<HlslTreeNode>();
        bool offset = false;
        foreach (HlslTreeNode lane in address.Take(address.Count - 1))
        {
            if (lane is AddOperation add
                && add.Inputs.Count(input => input is ConstantNode { IntegerValue: not null }) == 1
                && add.Inputs.Single(input => input is not ConstantNode { IntegerValue: not null }) is HlslTreeNode laneBase
                && laneBase is not ConstantNode)
            {
                bases.Add(laneBase);
                offsets.Add(add.Inputs.Single(input => input is ConstantNode { IntegerValue: not null }));
                offset = true;
            }
            else if (lane is ConstantNode)
            {
                return null;
            }
            else
            {
                bases.Add(lane);
                offsets.Add(new ConstantNode(0));
            }
        }
        if (!offset)
        {
            return null;
        }
        bases.Add(mip);
        offsets.Add(new ConstantNode(0));
        return $"{Compile(bases, bases.Count)} + {Compile(offsets, offsets.Count)}";
    }

    // Texel offsets change which texels are read, so they belong in the call.
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

    // An array subscript is an integer.
    public string CompileIndexableTempIndex(HlslTreeNode index)
    {
        bool wasAssigningToInteger = _assigningToInteger;
        bool wasReadingAsFloat = _readingAsFloat;
        _assigningToInteger = true;
        _readingAsFloat = false;
        try
        {
            return Compile(index);
        }
        finally
        {
            _assigningToInteger = wasAssigningToInteger;
            _readingAsFloat = wasReadingAsFloat;
        }
    }

    /// <summary>
    /// The assigned value, without a half cast where the variable is declared half.
    /// </summary>
    private static HlslTreeNode AssignedValue(TempAssignmentNode assignment)
    {
        return assignment.TempVariable.IsHalf
            && assignment.Value is ConvertOperation { TargetType: "half" } cast
            ? cast.Value
            : assignment.Value;
    }

    /// <summary>
    /// An msad4 accumulator: always a uint4, with 0 in lanes nothing reads.
    /// </summary>
    private string CompileMsadAccumulator(Msad4Node[] lanes)
    {
        if (lanes.Length == 4 && lanes.Select((l, i) => l.ComponentIndex == i).All(b => b))
        {
            return CompileAsInteger(lanes.Select(l => l.Accumulator));
        }
        var parts = new List<string>();
        for (int lane = 0; lane < 4; lane++)
        {
            Msad4Node read = lanes.FirstOrDefault(l => l.ComponentIndex == lane);
            parts.Add(read == null ? "0" : CompileAsInteger([read.Accumulator]));
        }
        return $"uint4({string.Join(", ", parts)})";
    }

    /// <summary>
    /// Compiles a value where an integer is wanted, such as an index or an element
    /// of an integer buffer: constructors are integer, and nothing gets asfloat.
    /// </summary>
    public string CompileAsInteger(IEnumerable<HlslTreeNode> group)
    {
        bool wasAssigningToInteger = _assigningToInteger;
        bool wasReadingAsFloat = _readingAsFloat;
        _assigningToInteger = true;
        _readingAsFloat = false;
        try
        {
            return Compile(group);
        }
        finally
        {
            _assigningToInteger = wasAssigningToInteger;
            _readingAsFloat = wasReadingAsFloat;
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
            string index = CompileAsInteger([relativeAddress.Index]);

            // A def-defined array is named after its first register.
            if (_registers.FindConstantArray(arrayKey.RegisterKey) is ConstantArray literals)
            {
                int literalOffset = arrayKey.RegisterKey.Number - literals.BaseRegisterIndex;
                if (literalOffset != 0)
                {
                    index += $" + {literalOffset}";
                }
                return $"{literals.Name}[{index}]{swizzle}";
            }
            // The immediate constant buffer packs several arrays; the immediate in
            // `icb[r0.x + 5]` picks which, and names it.
            if (arrayKey.RegisterKey is D3D10RegisterKey immediateKey
                && immediateKey.OperandType == OperandType.ImmediateConstantBuffer)
            {
                return $"{_registers.ImmediateConstantBufferName(immediateKey.Number)}"
                    + $"[{index}]{swizzle}";
            }

            // Named from the declaration; the base register need not be the first:
            // `floats[i + 2]` reads c2[a0.x] when floats starts at c0.
            string arrayName = _registers.GetRegisterName(arrayKey);
            if (arrayKey.RegisterKey is D3D10RegisterKey vertexKey
                && vertexKey.OperandType is OperandType.Input or OperandType.OutputControlPoint
                && vertexKey.GSVertex.HasValue
                && _registers.FindInputDeclaration(vertexKey, arrayKey.ComponentIndex)
                    is RegisterDeclaration vertex)
            {
                // `vertices[i].semantic`; the member is found by component, since
                // semantics can share a register.
                string memberSwizzle = GetAstSourceSwizzleName(componentsWithIndices,
                    _registers.GetRegisterMaskedLength(arrayKey),
                    promoteToVectorSize,
                    _registers.GetInputComponentBase(arrayKey));
                string vertexArray = vertexKey.OperandType == OperandType.OutputControlPoint
                    ? RegisterState.OutputPatchName
                    : _registers.InputArrayName;
                return $"{vertexArray}[{index}].{vertex.Name}{memberSwizzle}";
            }
            // Input registers made one array by dcl_indexrange.
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
                arrayName = constantBufferArray.Name;
                int elementOffset = _registers.GetConstantBufferElementOffset(
                    d3d10ArrayKey, constantBufferArray);
                if (constantBufferArray.TypeInfo.MemberInfo != null
                    && constantBufferArray.TypeInfo.NumElements > 1)
                {
                    // Array of structs: element = index / stride; the constant
                    // offset's remainder picks the member.
                    int stride = constantBufferArray.RegistersPerElement;
                    string element = CompileRegisterIndexAsElement(relativeAddress, stride);
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
                            swizzle = GetAstSourceSwizzleName(
                                componentsWithIndices, member.MatrixRowWidth, promoteToVectorSize);
                            return $"{member.MatrixRow(row)}{swizzle}";
                        }
                        swizzle = GetAstSourceSwizzleName(
                            componentsWithIndices, member.Width, promoteToVectorSize, member.ComponentBase);
                        return $"{member.Name}{swizzle}";
                    }
                }
                if (constantBufferArray.TypeInfo.Rows > 1)
                {
                    // Array of matrices: element = index / rows, then the row.
                    string matrixElement = CompileRegisterIndexAsElement(
                        relativeAddress, constantBufferArray.RegistersPerElement);
                    return RegisterState.MatrixRegisterComponents(
                        constantBufferArray.TypeInfo, $"{arrayName}[{matrixElement}]",
                        elementOffset, swizzle);
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
                if (array.TypeInfo.MemberInfo != null)
                {
                    // Array of structs: element = index / stride; the remainder
                    // register picks the member.
                    int stride = Math.Max(array.RegistersPerElement, 1);
                    string element = CompileRegisterIndexAsElement(relativeAddress, stride);
                    if (registerOffset / stride != 0)
                    {
                        element += $" + {registerOffset / stride}";
                    }
                    if (RegisterState.TryGetStructMemberAtRegister(
                            array, $"{arrayName}[{element}]", registerOffset % stride,
                            out StructMemberAccess member))
                    {
                        // A matrix comes back whole; the register picks its row.
                        if (member.IsMatrix)
                        {
                            member = RegisterState.MatrixRowOf(member, registerOffset % stride);
                        }
                        string memberSwizzle = member.Width <= 1
                            ? ""
                            : GetAstSourceSwizzleName(
                                componentsWithIndices, member.Width, promoteToVectorSize);
                        return $"{member.Name}{memberSwizzle}";
                    }
                }
                if (array.TypeInfo.Rows > 1)
                {
                    // Array of matrices: element = index / rows, then the row.
                    string element = CompileRegisterIndexAsElement(
                        relativeAddress, array.RegistersPerElement);
                    return RegisterState.MatrixRegisterComponents(
                        array.TypeInfo, $"{arrayName}[{element}]", registerOffset, swizzle);
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
                // At most one of the two bases is non-zero for a register.
                int componentBase = _registers.GetConstantComponentBase(shaderInput.RegisterComponentKey)
                    + _registers.GetInputComponentBase(shaderInput.RegisterComponentKey);
                swizzle = GetAstSourceSwizzleName(componentsWithIndices,
                    _registers.GetRegisterMaskedLength(shaderInput.RegisterComponentKey),
                    promoteToVectorSize,
                    componentBase,
                    _registers.GetConstantComponentsPerElement(shaderInput.RegisterComponentKey));
            }

            // A struct member owns its register; the swizzle is over its components.
            if (_registers.TryGetConstantMember(shaderInput.RegisterComponentKey, out StructMemberAccess member))
            {
                if (member.Width <= 1)
                {
                    return member.Name;
                }
                string memberSwizzle = GetAstSourceSwizzleName(
                    componentsWithIndices, member.Width, promoteToVectorSize);
                return $"{member.Name}{memberSwizzle}";
            }

            // Not a plain swizzle: a matrix column component is a subscripted element.
            return _registers.GetRegisterComponents(shaderInput.RegisterComponentKey, swizzle);
        }

        if (first is ResourceLoadNode resourceLoad)
        {
            string loadSwizzle = GetAstSourceSwizzleName(
                componentsWithIndices, 4, promoteToVectorSize);
            if (TryCompileTextureBufferLoad(resourceLoad, loadSwizzle, out string textureBufferRead))
            {
                return textureBufferRead;
            }
            // A writable view is read by subscript rather than Load.
            bool isWritableView = resourceLoad.Resource.RegisterComponentKey.RegisterKey
                is D3D10RegisterKey { OperandType: OperandType.UnorderedAccessView };
            ResourceDefinition resourceDefinition = _registers.ResourceDefinitions
                .Where(d => d.ShaderInputType == (isWritableView
                    ? D3DShaderInputType.UavRWTyped
                    : D3DShaderInputType.Texture))
                .First(d => d.BindPoint == resourceLoad.Resource.RegisterComponentKey.RegisterKey.Number);
            // Load addresses are integer texels: no float constructor, no asfloat.
            bool wasAssigningToInteger = _assigningToInteger;
            bool wasReadingAsFloat = _readingAsFloat;
            _assigningToInteger = true;
            _readingAsFloat = false;
            string address;
            try
            {
                address = CompileOffsetAddress(resourceLoad.Address.ToList())
                    ?? Compile(resourceLoad.Address, resourceLoad.Address.Count());
            }
            finally
            {
                _assigningToInteger = wasAssigningToInteger;
                _readingAsFloat = wasReadingAsFloat;
            }
            string loadOffsets = CompileSampleOffsets(resourceLoad.SampleOffsets, resourceDefinition);
            string sampleIndex = resourceLoad.HasSampleIndex
                ? $", {CompileAsInteger([resourceLoad.SampleIndex])}"
                : "";
            string loaded = isWritableView
                ? $"{resourceDefinition.Name}[{address}]{loadSwizzle}"
                : $"{resourceDefinition.Name}.Load({address}{sampleIndex}{loadOffsets}){loadSwizzle}";
            // An integer texel read as a float is reinterpreted (e.g. a packed depth).
            if (resourceDefinition.IsIntegerReturnType && !_readingAsBits && !_assigningToInteger
                && components.All(c => ValueTypes.ReadAsInteger(c) != true))
            {
                loaded = $"asfloat({loaded})";
            }
            return loaded;
        }

        if (first is RenderTargetSampleCountNode)
        {
            return "GetRenderTargetSampleCount()";
        }

        if (first is SamplePositionNode samplePosition)
        {
            // The swizzle comes from the resource operand.
            string positionSwizzle = GetAstSourceSwizzleName(
                components.Select(c => (IHasComponentIndex)((SamplePositionNode)c).Resource),
                2, promoteToVectorSize);
            string index = CompileAsInteger([samplePosition.SampleIndex]);
            if (samplePosition.Resource.RegisterComponentKey.RegisterKey
                is D3D10RegisterKey { OperandType: OperandType.Rasterizer })
            {
                return $"GetRenderTargetSamplePosition({index}){positionSwizzle}";
            }
            string sampled = _registers.GetRegisterName(
                samplePosition.Resource.RegisterComponentKey.RegisterKey);
            return $"{sampled}.GetSamplePosition({index}){positionSwizzle}";
        }

        if (first is TextureLoadOutputNode textureLoad)
        {
            // The channel comes from the resource operand's swizzle. Except for lod,
            // whose result is a scalar float that cannot be swizzled.
            string swizzle = textureLoad.Controls.HasFlag(TextureLoadControls.CalculateLod)
                ? ""
                : textureLoad.Texture != null
                    ? GetAstSourceSwizzleName(
                        components.Select(c => (IHasComponentIndex)((TextureLoadOutputNode)c).Texture),
                        4, promoteToVectorSize)
                    : GetAstSourceSwizzleName(componentsWithIndices, 4, promoteToVectorSize);

            var textureDefinition = _registers.ResourceDefinitions
                .Where(d => d.ShaderInputType == D3DShaderInputType.Texture)
                .FirstOrDefault(d => d.BindPoint == textureLoad.Texture.RegisterComponentKey.RegisterKey.Number);
            if (textureDefinition != null)
            {
                var samplerDefinition = _registers.ResourceDefinitions
                    .Where(d => d.ShaderInputType == D3DShaderInputType.Sampler)
                    .FirstOrDefault(d => d.BindPoint == textureLoad.Sampler.RegisterComponentKey.RegisterKey.Number);
                string texcoords = Compile(textureLoad.TextureCoordinateInputs, textureDefinition.GetDimensionSize());
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
                    // The sampler swizzle picks the gathered channel (`s0.y` is green).
                    string channel = textureLoad.Sampler.RegisterComponentKey.ComponentIndex switch
                    {
                        1 => "Green",
                        2 => "Blue",
                        3 => "Alpha",
                        _ => "",
                    };
                    if (textureLoad.Controls.HasFlag(TextureLoadControls.Compare))
                    {
                        method = "GatherCmp" + channel;
                        extraArguments = $", {Compile(new[] { textureLoad.ScalarArgument })}";
                    }
                    else
                    {
                        method = "Gather" + channel;
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
                if (textureLoad.Controls.HasFlag(TextureLoadControls.ProgrammableOffset))
                {
                    extraArguments += $", {CompileAsInteger(textureLoad.Offsets.ToList())}";
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

        if (first is Msad4Node msad)
        {
            // Reference and source are shared by all components; the accumulator is
            // gathered across them. Every msad4 parameter is a uint.
            bool wasAssigningToUnsigned = _assigningToUnsigned;
            _assigningToUnsigned = true;
            string reference;
            string sourceLow;
            string sourceHigh;
            string accumulator;
            try
            {
                reference = CompileAsInteger([msad.Reference]);
                sourceLow = CompileAsInteger([msad.SourceLow]);
                sourceHigh = CompileAsInteger([msad.SourceHigh]);
                accumulator = CompileMsadAccumulator(components.Cast<Msad4Node>().ToArray());
            }
            finally
            {
                _assigningToUnsigned = wasAssigningToUnsigned;
            }
            string msadSwizzle = GetAstSourceSwizzleName(
                componentsWithIndices, 4, promoteToVectorSize);
            return $"msad4({reference}, uint2({sourceLow}, {sourceHigh}), {accumulator})"
                + msadSwizzle;
        }

        if (first is InterfaceCallNode call)
        {
            // One call, swizzled.
            MarkMatched(components);
            LinkageModel.InterfaceInfo iface =
                _registers.Linkage.InterfaceByNumber(call.InterfaceNumber);
            string arguments = string.Join(", ", call.ArgumentComponents
                .Select(argument => Compile(argument)));
            LinkageModel.MethodInfo method = iface.Methods[call.FunctionIndex];
            string callSwizzle = GetAstSourceSwizzleName(
                componentsWithIndices, method.ReturnComponents, promoteToVectorSize);
            return $"{iface.InstanceExpression(call.InstanceIndex)}"
                + $".{method.Name}({arguments}){callSwizzle}";
        }

        if (first is LitOutputNode lit)
        {
            string nDotL = Compile(new[] { lit.NDotL });
            string nDotH = Compile(new[] { lit.NDotH });
            string specularPower = Compile(new[] { lit.SpecularPower });
            string litSwizzle = GetAstSourceSwizzleName(
                componentsWithIndices, 4, promoteToVectorSize);
            return $"lit({nDotL}, {nDotH}, {specularPower}){litSwizzle}";
        }

        if (first is MatrixMultiplyOutputNode matrixComponent)
        {
            // Swizzle against the multiplication's width, not the register's.
            MarkMatched(components);
            string multiplied = _matrixMultiplicationCompiler.Compile(matrixComponent.Matrix);
            string matrixSwizzle = GetAstSourceSwizzleName(componentsWithIndices,
                matrixComponent.RowCount, promoteToVectorSize);
            return $"{multiplied}{matrixSwizzle}";
        }

        if (first is ReflectOutputNode reflectComponent)
        {
            // Swizzle against the vector's width, not the register's.
            MarkMatched(components);
            string incident = Compile(reflectComponent.Incident.Inputs);
            string normal = Compile(reflectComponent.Normal.Inputs);
            string reflectSwizzle = GetAstSourceSwizzleName(componentsWithIndices,
                reflectComponent.Incident.Inputs.Count, promoteToVectorSize);
            return $"reflect({incident}, {normal}){reflectSwizzle}";
        }

        if (first is NormalizeOutputNode)
        {
            // Swizzle against the vector's width, not the register's (nrm writes .xyz).
            MarkMatched(components);
            // All components permuted: `normalize(t1.yzx).zxy` is normalize(t1).
            if (components.Count == first.Inputs.Count && components.Count > 1
                && components.All(c => c is NormalizeOutputNode n && n.Inputs.SequenceEqual(first.Inputs))
                && components.Select(c => ((NormalizeOutputNode)c).ComponentIndex).Distinct().Count() == components.Count)
            {
                return $"normalize({Compile(components.Select(c => first.Inputs[((NormalizeOutputNode)c).ComponentIndex]))})";
            }
            string input = Compile(first.Inputs);
            string swizzle = GetAstSourceSwizzleName(componentsWithIndices, first.Inputs.Count,
                promoteToVectorSize);
            return $"normalize({input}){swizzle}";
        }

        if (first is TempAssignmentNode tempAssignment)
        {
            if (tempAssignment.Value is ResourceInfoNode)
            {
                return CompileResourceInfoCall(components.Cast<TempAssignmentNode>().ToList());
            }
            if (tempAssignment.Value is ConsumeNode)
            {
                return CompileConsumeCall(components.Cast<TempAssignmentNode>().ToList());
            }
            if (tempAssignment.Value is DoubleBitsNode)
            {
                return CompileDoubleBitsCall(components.Cast<TempAssignmentNode>().ToList());
            }

            string variableCompiled = Compile(components.Select(a => (a as TempAssignmentNode).TempVariable));

            string type;
            if (tempAssignment.IsReassignment)
            {
                type = string.Empty;
            }
            else
            {
                type = tempAssignment.TempVariable.TypeName;
                if (tempAssignment.TempVariable.VariableSize > 1)
                {
                    type += tempAssignment.TempVariable.VariableSize;
                }
                type += " ";
                variableCompiled = $"{_registers.TemporaryPrefix}{tempAssignment.TempVariable.DeclarationIndex}";
            }
            bool wasAssigningToInteger = _assigningToInteger;
            bool wasAssigningToUnsigned = _assigningToUnsigned;
            bool wasAssignedDirectly = _assignedDirectlyToInteger;
            _assigningToInteger = tempAssignment.TempVariable.IsInteger;
            _assigningToUnsigned = tempAssignment.TempVariable.IsUnsigned;
            _assignedDirectlyToInteger = tempAssignment.TempVariable.IsInteger;
            int wasTopDepth = _topDepth;
            _topDepth = _compileDepth + 1;
            HashSet<HlslTreeNode> wasAssignedVariables = _assignedVariables;
            _assignedVariables = HlslTreeNode.NewNodeSet();
            foreach (TempAssignmentNode assigned in components.Cast<TempAssignmentNode>())
            {
                _assignedVariables.Add(assigned.TempVariable);
            }
            string compiled;
            try
            {
                compiled = Compile(components.Select(a => AssignedValue((TempAssignmentNode)a)));
            }
            finally
            {
                _assigningToInteger = wasAssigningToInteger;
                _assigningToUnsigned = wasAssigningToUnsigned;
                _assignedDirectlyToInteger = wasAssignedDirectly;
                _topDepth = wasTopDepth;
                _assignedVariables = wasAssignedVariables;
            }
            // A mask constant into a bool is the bool: `t = -1` is `t = true`.
            // A float into an integer variable is its bits (if every component is;
            // mixed vectors reinterpret per component in the constructor).
            if (tempAssignment.TempVariable.IsBool
                && components.Count == 1
                && AssignedValue(tempAssignment) is ConstantNode mask)
            {
                compiled = mask.Value == 0 && mask.IntegerValue is null or 0 ? "false" : "true";
            }
            else if (tempAssignment.TempVariable.IsInteger
                && components.All(a => IsFloatValued(((TempAssignmentNode)a).Value)))
            {
                compiled = $"asint({compiled})";
            }
            // Integer constants into a float variable are fxc's untyped immediates:
            // the float their bits make (`1`, not 1065353216).
            else if (!tempAssignment.TempVariable.IsInteger && !tempAssignment.TempVariable.IsDouble
                && components.All(a => AssignedValue((TempAssignmentNode)a) is ConstantNode { IntegerValue: int bits }
                    && float.IsNormal(BitConverter.Int32BitsToSingle(bits)) || AssignedValue((TempAssignmentNode)a) is ConstantNode { IntegerValue: 0 }))
            {
                List<string> floats = [.. components.Select(a => ConstantFormatter.Format(BitConverter.Int32BitsToSingle(
                    ((ConstantNode)AssignedValue((TempAssignmentNode)a)).IntegerValue.Value)))];
                compiled = floats.Distinct().Count() == 1 ? floats[0] : $"float{floats.Count}({string.Join(", ", floats)})";
            }
            // And integers into a float variable are its bits: raw buffer dwords, or
            // the integer arithmetic fxc left in the register.
            else if (!tempAssignment.TempVariable.IsInteger && !tempAssignment.TempVariable.IsDouble
                && components.All(a => ValueTypes.IsReinterpretedAsFloat(((TempAssignmentNode)a).Value)
                    || (((TempAssignmentNode)a).Value is not ConstantNode
                        && IsIntegerValued(((TempAssignmentNode)a).Value))))
            {
                compiled = $"asfloat({compiled})";
            }
            return $"{type}{variableCompiled} = {compiled};";
        }

        if (first is TempVariableNode tempVariable)
        {
            if (tempVariable.DeclarationIndex == null)
            {
                int index = _tempAssignmentindexCounter;
                _tempAssignmentindexCounter++;
                // Number distinct variables, not positions: a broadcast repeats one.
                List<HlslTreeNode> variables = [.. components
                    .Distinct(ReferenceEqualityComparer.Instance)
                    .Cast<HlslTreeNode>()];
                for (int i = 0; i < variables.Count; i++)
                {
                    var component = variables[i] as TempVariableNode;
                    _measuredNumbering?.Add(
                        (component, component.ComponentIndex, component.VariableSize));
                    component.DeclarationIndex = index;
                    component.ComponentIndex = i;
                    component.VariableSize = variables.Count;
                }
            }

            // No promoteToVectorSize: `t1 * color.xyz`, not `t1.xxx * color.xyz`.
            string variableName = $"{_registers.TemporaryPrefix}{tempVariable.DeclarationIndex}";
            if (tempVariable.StructuredElementOf != null
                && _registers.NameStructuredMembers(tempVariable.StructuredElementOf, variableName, 0,
                    [.. componentsWithIndices.Select(c => c.ComponentIndex)]) is string members)
            {
                return members;
            }
            string swizzle = GetAstSourceSwizzleName(componentsWithIndices, (int)tempVariable.VariableSize);
            return $"{variableName}{swizzle}";
        }

        if (first is ConsumeNode consume)
        {
            if (consume.NamedAs == null)
            {
                throw new NotImplementedException(
                    "A Consume result was compiled before its call was named.");
            }
            return Compile(components.Select(c => (HlslTreeNode)((ConsumeNode)c).NamedAs));
        }

        if (first is DoubleBitsNode doubleBits)
        {
            // As a root it is read from the variable its asuint was hoisted into.
            if (doubleBits.NamedAs == null)
            {
                throw new NotImplementedException(
                    "A word of a double was compiled before its asuint was named.");
            }
            return Compile(components.Select(c => (HlslTreeNode)((DoubleBitsNode)c).NamedAs));
        }

        if (first is ResourceInfoNode resourceInfo)
        {
            // As a root it is read from the variable its call was hoisted into.
            if (resourceInfo.NamedAs == null)
            {
                throw new NotImplementedException(
                    "A resinfo result was compiled before its GetDimensions call was named.");
            }
            return Compile(components.Select(c => (HlslTreeNode)((ResourceInfoNode)c).NamedAs));
        }

        throw new NotImplementedException();
    }

    private const int BytesPerConstantRegister = 4 * sizeof(float);

    /// <summary>
    /// A tbuffer read with ld names the variable at that register, like a cbuffer:
    /// `ld r0, l(1, 1, 1, 1), t0` over `tbuffer P { float4 a; float4 b; }` is b.
    /// </summary>
    private bool TryCompileTextureBufferLoad(
        ResourceLoadNode resourceLoad, string swizzle, out string compiled)
    {
        compiled = null;
        ResourceDefinition buffer = _registers.ResourceDefinitions
            .Where(d => d.ShaderInputType == D3DShaderInputType.TBuffer)
            .FirstOrDefault(d => d.BindPoint
                == resourceLoad.Resource.RegisterComponentKey.RegisterKey.Number);
        if (buffer == null)
        {
            return false;
        }
        if (resourceLoad.Address.FirstOrDefault() is not ConstantNode element)
        {
            // A computed index reads an element of the buffer's array.
            return TryCompileTextureBufferArrayLoad(resourceLoad, buffer, swizzle, out compiled);
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

    /// <summary>
    /// A tbuffer array subscripted by the load's register address, converted to an
    /// element index relative to the array's start.
    /// </summary>
    private bool TryCompileTextureBufferArrayLoad(ResourceLoadNode resourceLoad,
        ResourceDefinition buffer, string swizzle, out string compiled)
    {
        compiled = null;
        D3D10ConstantDeclaration array = _registers.ConstantDeclarations
            .OfType<D3D10ConstantDeclaration>()
            .Where(d => d.IsTextureBuffer && d.BufferName == buffer.Name)
            .FirstOrDefault(d => d.TypeInfo.NumElements > 1);
        if (array == null)
        {
            return false;
        }
        HlslTreeNode address = resourceLoad.Address.First();
        string index = array.RegistersPerElement > 1
            ? CompileRegisterIndexAsElement(address, array.RegistersPerElement)
            : CompileAsInteger([address]);
        int baseRegister = array.VariableOffset / BytesPerConstantRegister;
        if (baseRegister != 0)
        {
            index = $"{index} - {baseRegister / array.RegistersPerElement}";
        }
        compiled = $"{array.Name}[{index}]{swizzle}";
        return true;
    }

    /// <summary>
    /// `float4 t0 = input.Consume();` as one declaring statement.
    /// </summary>
    private string CompileConsumeCall(List<TempAssignmentNode> assignments)
    {
        var consume = (ConsumeNode)assignments[0].Value;
        TempVariableNode variable = assignments[0].TempVariable;
        ResourceDefinition buffer = _registers.ResourceDefinitions
            .First(d => d.BindPoint == consume.Buffer.RegisterComponentKey.RegisterKey.Number
                && d.ShaderInputType == D3DShaderInputType.UavConsumeStructured);
        string width = variable.VariableSize == 1 ? "" : variable.VariableSize.ToString();
        // Declared as the element type: the struct, else the scalar type, else float.
        if (variable.StructuredElementOf != null && StructuredElementTypeName?.Invoke(buffer) is string structName)
        {
            return $"{structName} {_registers.TemporaryPrefix}{variable.DeclarationIndex} = {buffer.Name}.Consume();";
        }
        string scalar = buffer.ElementType is { MemberInfo: null or { Count: 0 } }
            ? buffer.ElementType.ParameterType.ToString().ToLowerInvariant()
            : "float";
        return $"{scalar}{width} {_registers.TemporaryPrefix}{variable.DeclarationIndex} = {buffer.Name}.Consume();";
    }

    /// <summary>
    /// A hoisted asuint over a double: declaration, then the call filling out
    /// parameters. Two statements.
    /// </summary>
    private string CompileDoubleBitsCall(List<TempAssignmentNode> assignments)
    {
        var bits = (DoubleBitsNode)assignments[0].Value;
        TempVariableNode variable = assignments[0].TempVariable;
        string name = $"{_registers.TemporaryPrefix}{variable.DeclarationIndex}";
        return $"uint2 {name};" + "\r\n"
            + $"asuint({Compile(bits.Value)}, {name}.x, {name}.y);";
    }

    /// <summary>
    /// A hoisted resinfo: declaration, then GetDimensions filling out parameters.
    /// Two statements.
    /// </summary>
    private string CompileResourceInfoCall(List<TempAssignmentNode> assignments)
    {
        var info = (ResourceInfoNode)assignments[0].Value;
        TempVariableNode variable = assignments[0].TempVariable;
        RegisterKey resourceKey = info.Resource.RegisterComponentKey.RegisterKey;
        ResourceDefinition resource = info.IsBuffer
            ? _registers.GetBufferDefinition(resourceKey)
            : _registers.GetTextureDefinition(resourceKey);

        string type = info.ReturnType == D3D10ResInfoReturnType.Uint ? "uint" : "float";
        string name = $"{_registers.TemporaryPrefix}{variable.DeclarationIndex}";
        string width = variable.VariableSize == 1 ? "" : variable.VariableSize.ToString();
        string declaration = $"{type}{width} {name};";

        // A structured buffer reports count and stride; a byte address buffer its size.
        if (info.IsBuffer)
        {
            return declaration + "\r\n" + (info.ReportsStride
                ? $"{resource.Name}.GetDimensions({name}.x, {name}.y);"
                : $"{resource.Name}.GetDimensions({name});");
        }
        // From the resource too: a declared sample count is folded to a constant,
        // leaving no sampleinfo to go by. MS textures have no mip-level overload.
        bool isMultisampled = assignments.Any(a => ((ResourceInfoNode)a.Value).IsSampleCount)
            || resource.Dimension is ResourceDimension.Texture2Dms
                or ResourceDimension.Texture2DmsArray;
        if (isMultisampled)
        {
            // Width, height, [elements,] samples.
            return declaration + "\r\n" + (variable.VariableSize == 4
                ? $"{resource.Name}.GetDimensions({name}.x, {name}.y, {name}.z, {name}.w);"
                : $"{resource.Name}.GetDimensions({name}.x, {name}.y, {name}.z);");
        }
        // Mip count is always in w; depth or array size in z. The mip form is used
        // when the level is not constant 0 or a component past the no-mip form's
        // outputs is read.
        ResourceDimension? dimension = resource.Dimension;
        bool hasDepth = dimension is ResourceDimension.Texture2DArray
            or ResourceDimension.TextureCubeArray or ResourceDimension.Texture3D;
        bool is1D = dimension == ResourceDimension.Texture1D;
        int noMipComponents = is1D ? 1 : hasDepth ? 3 : 2;
        bool mipForm = !(info.MipLevel is ConstantNode zero && zero.Value == 0)
            || assignments.Any(a => ((ResourceInfoNode)a.Value).InfoComponent >= noMipComponents);
        string dimensions = !mipForm && is1D ? name
            : !mipForm && hasDepth ? $"{name}.x, {name}.y, {name}.z"
            : !mipForm ? $"{name}.x, {name}.y"
            : is1D ? $"{name}.x, {name}.w"
            : hasDepth ? $"{name}.x, {name}.y, {name}.z, {name}.w"
            : $"{name}.x, {name}.y, {name}.w";
        string call = mipForm
            ? $"{resource.Name}.GetDimensions({Compile(info.MipLevel)}, {dimensions});"
            : $"{resource.Name}.GetDimensions({dimensions});";
        return declaration + "\r\n" + call;
    }

    /// <summary>
    /// Recovers any()/all() from a tree of ors/ands over a vector comparison's lanes.
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
        // Lane order doesn't matter; component order lets the swizzle drop.
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
        // A bool tested against zero is the bool itself: `flag != 0` costs a movc.
        if (components.Count == 1
            && first.Comparison is IfComparison.NE or IfComparison.EQ
            && first.Right is ConstantNode { Value: 0 }
            && (first.Left is TempVariableNode { IsBool: true }
                || (first.Left is RegisterInputNode register
                    && _registers.GetDeclaredType(register.RegisterComponentKey) == DeclaredType.Bool)))
        {
            string flag = Compile(first.Left);
            return first.Comparison == IfComparison.NE ? flag : $"!{flag}";
        }
        // Two masks compared for equality compare as bools: `(a == b) == (c < d)`.
        // Only when both sides are comparisons.
        if (first.Comparison is IfComparison.EQ or IfComparison.NE
            && components.Cast<ComparisonNode>().All(
                c => c.Left is ComparisonNode && c.Right is ComparisonNode))
        {
            string leftBool = Compile(components.Cast<ComparisonNode>().Select(c => c.Left));
            string rightBool = Compile(components.Cast<ComparisonNode>().Select(c => c.Right));
            return $"({leftBool}) {first.Comparison.ToHlslString()} ({rightBool})";
        }
        // CompileOperand brackets bitwise operands: == binds tighter than &.
        // Constants in an unsigned comparison are uint, avoiding a mismatch warning.
        bool wasAssigningToUnsigned = _assigningToUnsigned;
        _assigningToUnsigned = first.IsUnsigned;
        string left;
        string right;
        try
        {
            left = CompileOperand(components.Cast<ComparisonNode>().Select(c => c.Left));
            right = CompileOperand(components.Cast<ComparisonNode>().Select(c => c.Right));
        }
        finally
        {
            _assigningToUnsigned = wasAssigningToUnsigned;
        }
        // HLSL takes signedness from the operands; one unsigned side suffices. The
        // cast goes on a non-constant side (`(uint)0 <= x` draws a warning) and is
        // as wide as the comparison (X3014).
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
        // Equality operands go in canonical order.
        if (first.Comparison is IfComparison.EQ or IfComparison.NE
            && CanReorder(components.Select(c => c.Inputs[0]), components.Select(c => c.Inputs[1]), products: true)
            && SortsAfter(first.Left, left, first.Right, right))
        {
            (left, right) = (right, left);
        }
        return $"{left} {first.Comparison.ToHlslString()} {right}";
    }

    /// <summary>
    /// Whether HLSL already reads a value as unsigned, directly or by promotion.
    /// </summary>
    /// <summary>
    /// Whether a value compiles to an int: an int variable or cast, or arithmetic
    /// over one with no uint operand (int + uint is uint).
    /// </summary>
    private bool IsSignedAlready(HlslTreeNode node)
    {
        return node switch
        {
            TempVariableNode variable => variable.IsInteger && !variable.IsUnsigned && !variable.IsBool && !variable.IsBits,
            TempAssignmentNode assignment => IsSignedAlready(assignment.TempVariable),
            ConvertOperation { TargetType: "int" } => true,
            AddOperation or SubtractOperation or MultiplyOperation
                or BitwiseAndOperation or BitwiseOrOperation or BitwiseXorOperation or ShiftLeftOperation
                => !node.Inputs.Any(IsUnsignedAlready) && node.Inputs.Any(IsSignedAlready),
            _ => false,
        };
    }

    private bool IsUnsignedAlready(HlslTreeNode node)
    {
        switch (node)
        {
            case ConvertOperation { TargetType: "uint" }:
            case ShiftRightOperation { IsUnsigned: true }:
            case MinimumOperation { IsUnsigned: true }:
            case MaximumOperation { IsUnsigned: true }:
            case TempVariableNode { IsUnsigned: true }:
            case TempAssignmentNode { TempVariable.IsUnsigned: true }:
                return true;
            case RegisterInputNode register:
                return IsUnsignedRegister(register);
            // The branches, not the condition.
            case MoveConditionalOperation select:
                return IsUnsignedAlready(select.Inputs[1]) || IsUnsignedAlready(select.Inputs[2]);
            // Operations that promote to unsigned.
            case AddOperation or SubtractOperation or MultiplyOperation
                or ShiftLeftOperation or MinimumOperation or MaximumOperation
                or BitwiseAndOperation or BitwiseOrOperation or BitwiseXorOperation
                or BitwiseNotOperation or MoveOperation:
                return node.Inputs.Any(IsUnsignedAlready);
            default:
                return false;
        }
    }

    private bool IsUnsignedRegister(RegisterInputNode register)
    {
        return _registers.GetDeclaredType(register.RegisterComponentKey) == DeclaredType.Uint;
    }

    /// <param name="componentBase">
    /// The register component the variable starts at: a float3 at .yzw is eyePos.xyz.
    /// </param>
    private static string GetAstSourceSwizzleName(IEnumerable<IHasComponentIndex> inputs,
        int registerSize, 
        int promoteToVectorSize = PromoteToAnyVectorSize,
        int componentBase = 0,
        int componentsPerElement = 1)
    {
        if (registerSize == 1 || registerSize > 4)
        {
            // A scalar can still be spread to the requested width: `sigma.xx`.
            return promoteToVectorSize is > 1 and <= 4 && registerSize == 1
                ? "." + new string('x', promoteToVectorSize)
                : "";
        }

        string swizzleName = "";
        foreach (int swizzle in inputs.Select(
            i => (i.ComponentIndex - componentBase) / componentsPerElement))
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
