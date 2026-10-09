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

    // While a measuring compile is running, the variables it had to number and what
    // they looked like before, so that the numbers go back with the text. See
    // Measure.
    private List<(TempVariableNode Variable, int ComponentIndex, int? VariableSize)>
        _measuredNumbering;

    /// <summary>The number the next variable is given. Set back where variables
    /// were merged and their numbers closed up.</summary>
    public int NextTempVariableIndex
    {
        get => _tempAssignmentindexCounter;
        set => _tempAssignmentindexCounter = value;
    }

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

    // The values the parser recorded as doubles; see HlslAst.DoubleValues.
    private readonly ISet<HlslTreeNode> _doubleValues;

    /// <summary>
    /// Whether a value is a double. A variable is not one of the parsed values - it
    /// was made to stand for one - so it answers from its own declared type, which
    /// was settled from the record when the variable was created.
    /// </summary>
    private bool IsDoubleValued(HlslTreeNode node)
    {
        return node switch
        {
            TempVariableNode variable => variable.IsDouble,
            TempAssignmentNode assignment => assignment.TempVariable.IsDouble,
            _ => _doubleValues.Contains(node),
        };
    }

    // The variable of the innermost counted loop, which aL refers to. The writer
    // generates that name from the nesting depth, so it has to be handed in.
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
    /// The component sets the groupers matched whole, recorded beside
    /// <see cref="Grouped"/> while a statement is being measured.
    /// </summary>
    public List<HlslTreeNode[]> GroupMatches { get; set; }

    /// <summary>
    /// Records everything under the matched components except what is under the
    /// operands the match handed back.
    /// </summary>
    /// <summary>
    /// A set of components that is one expression, where the expression is a node
    /// rather than a shape the compiler recognised: an idiom IdiomRecovery put into
    /// the graph. The match is recorded and nothing is marked grouped, because there
    /// is nothing inside the match to keep a name out of - the component is the
    /// match.
    ///
    /// It has to be recorded all the same. The passes that name a value a root
    /// shares with another reader ask GroupMatches whether the components are one
    /// expression, and a recovered idiom that does not answer stops being named:
    /// vs_2_0/fog_lighting wrote `mul(i.position, worldViewProjection)` three times
    /// where it had named it once.
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
    /// A vector times the product of two matrices: `mul(p, mul(world, viewProjection))`.
    /// An fx_2_0 preshader multiplies the matrices before the shader runs, and the
    /// shader multiplies the vector by what it was handed, so the two halves meet
    /// only in the graph - each component is the vector's components times a row of
    /// the product each, added up, and each row is a row of the first matrix times
    /// the second. Recognised a row at a time, it came out as the sum it is:
    /// `p.x * mul(world[0], viewProjection) + p.y * mul(world[1], ...) + ...`.
    /// </summary>
    private string TryCompileVectorByMatrixProduct(List<HlslTreeNode> components)
    {
        List<(HlslTreeNode, HlslTreeNode)>[] terms = [.. components.Select(Terms)];
        int rowCount = terms[0]?.Count ?? 0;
        if (rowCount < 2 || terms.Any(t => t == null || t.Count != rowCount))
        {
            return null;
        }

        // Each term is a component of the vector, the same in every component of
        // the result, times an element of a row of the matrix.
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

    // The products a sum adds up, as their two factors, or null for one that adds
    // anything else.
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
        // What the match is of, as against what it is made of. A set of components
        // one grouper took whole is one expression and can be named as one; four
        // unrelated values that share a register are a constructor, and naming them
        // writes an assignment inside it.
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

    /// <summary>
    /// Compiles these groups, keeps what the compile revealed, and leaves nothing
    /// behind: the text is thrown away and the names it had to invent on the way are
    /// taken back.
    ///
    /// Emission numbers a variable the first time it writes one, and it has to -
    /// what number a variable gets depends on how the writer groups the components
    /// it writes, which is an emission-time decision and cannot be settled before.
    /// So a measuring compile cannot help numbering, and if it keeps its numbers the
    /// corpus comes out named in the order it was measured rather than the order it
    /// is written. Rolling them back is what makes a measurement free, and free is
    /// what lets a decision be taken by measuring both ways.
    ///
    /// Nests: the state is saved and restored, so a measurement inside a measurement
    /// is the inner one's answer and the outer one's rollback.
    /// </summary>
    public CompileMeasurement Measure(IEnumerable<IEnumerable<HlslTreeNode>> groups)
    {
        return Measure(groups, keepNumbering: false);
    }

    /// <summary>
    /// The same compile, keeping the numbers it invents. This is how the writer
    /// numbers its variables, and it is not an accident of where the counter lives:
    /// what number a variable gets depends on how the writer groups the components
    /// it writes, which is settled while writing and not before, so the only thing
    /// that can number the variables in writing order is a write.
    ///
    /// Which makes the writer's first measurement two jobs at once, and the second
    /// one unnamed until now. Rolling its numbers back moves ten fixtures: eight
    /// renumber harmlessly, and cs_4_0/bitpack emits `source.Load(t1.x)` a line
    /// above the declaration of t1, because the order the assignments are sorted
    /// into is read off numbers that no longer exist by then.
    ///
    /// So the measurements that are also the numbering say so, and the ones that
    /// only want to know something use <see cref="Measure(IEnumerable{IEnumerable{HlslTreeNode}})"/>
    /// and leave nothing behind.
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
        // Null while the numbering is being kept, so the lazy path records nothing
        // and an enclosing measurement does not roll these back either: a number the
        // writer is keeping is the writer's now.
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
            // Backwards: one variable can be numbered, rolled back and numbered
            // again within a measurement, and the first entry is the one that says
            // what it looked like to begin with.
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
    /// How many compiles deep this one is: one for what a statement writes, more
    /// for what is written inside it. A rewrite that writes an operator where the
    /// node is one call - a vector of dots written as the sum they are - has to
    /// bracket it inside an expression, which brackets by the node and not by the
    /// text, and need not at the top.
    /// </summary>
    private int _compileDepth;

    // The depth a value is written at the top of: one for a statement's own
    // compile, one more for the value of an assignment, which its group compiles.
    private int _topDepth = 1;

    // The variables the assignment being compiled writes, for an accumulation to
    // name them first.
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

        // An integer that is a float's bits, standing where a float is wanted: the
        // bits are the value, and converting them gives the number they happen to
        // make - a packed pair of half floats came back as three thousand million.
        // Every component, so that a constructor whose components are not all bits
        // is left to reinterpret them one at a time.
        if (_readingAsFloat && components.All(c => ValueTypes.IsReinterpretedAsFloat(c) || IsIntegerRegisterReadAsFloat(c)))
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
            if (TryCompileVectorByMatrixProduct(components) is string product)
            {
                return product;
            }

            IList<IList<HlslTreeNode>> componentGroups = _nodeGrouper.GroupComponents(components);
            if (componentGroups.Count > 1)
            {
                // Dot products the grouper took apart, one per group, which is where
                // they would be written one by one.
                if (TryCompileTransposedDots(components) is string weighted)
                {
                    return weighted;
                }
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
                // The rows in another order are still one multiply, read through a
                // swizzle: fxc puts them into a register as it schedules them, and a
                // projection's x and y in .yx came back as two dots where the compile
                // before had them as mul(position, (float4x2)cascadeTransform).
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

            // Still here, and no longer the way a reflect is usually recognised:
            // IdiomRecovery puts one into the graph before anything is named, and it
            // reaches every reflect in the corpus - all six of them - so this does
            // not fire for any of those any more. It stays for a shape or a path the
            // recovery does not reach, where a reflect recognised late still reads
            // better than the arithmetic it is made of.
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
            // The one takes the type of the constants beside it, the same as the
            // zero below and for the same reason. `uint4(id, id * 7 + 3, id & 255, 1)`
            // is a mov and an imad, and the mov groups with the imad as the `id * 1`
            // it is - but written `id.xx * float2(1, 7)` the mad is a float one, so
            // the round trip converts the id in and the answer back out and pays a
            // utof and an ftoi for it. Written `int2(1, 7)` it is the imad it was,
            // and one instruction shorter than the original besides: fxc folds the
            // mov into the mad's other component.
            bool integerFactors = components
                .OfType<MultiplyOperation>()
                .Select(multiply => FactorOfFoldedMultiply(multiply))
                .All(factor => factor is ConstantNode constant && constant.IntegerValue != null);
            List<HlslTreeNode> multiplied = [.. components.Select(FactoredOfFoldedMultiply)];
            List<HlslTreeNode> factors = [..
                components.Select(c => FactorOfFoldedMultiply(c, integerFactors))];
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

        if (first is DoubleConstantNode)
        {
            return CompileDoubleConstant(components);
        }

        // A counter call is a value of its own rather than a component of anything:
        // the slot it answers with is an index the shader subscripts the buffer by.
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
            // Phis are an IR construct. StatementFinalizer lowers them to a temp
            // variable plus its assignments; reaching here means that did not happen.
            throw new InvalidOperationException($"Phi node reached compilation without being lowered: {first} with inputs {string.Join(", ", first.Inputs.Select(i => i.GetType().Name + ":" + i))}.");
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
        // An attribute evaluation takes the input register itself and nothing else -
        // fxc answers an expression there with an internal compiler error - so two
        // of them are one call only when they are one instruction.
        if (first is EvaluateAttributeOperation
            && components.Any(c => !HlslTreeNode.IsSameInstruction(c, first)))
        {
            return false;
        }
        return components.All(c => Operation.IsSameKind(c, first) && c.Inputs.Count == first.Inputs.Count);
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
    /// What a select tests, bracketed where it is a select itself. A conditional
    /// binds more loosely than anything else, so one written bare where another's
    /// condition goes is taken apart at the wrong `?`: `a >= 0 ? b : c >= 0 ? d : e`
    /// is `a >= 0 ? b : (c >= 0 ? d : e)`, not `(a >= 0 ? b : c) >= 0 ? d : e`. A
    /// preshader tests one compare's result with another as a matter of course.
    /// </summary>
    private string CompileCondition(List<HlslTreeNode> components)
    {
        // A select between masks tested as a condition is a select between bools:
        // fxc flattens `if (count) t = count <= 1; else t = -1;` into a movc, and
        // read back the test was `(count ? count <= 1 : -1)`, a bool beside an int.
        // The -1 is the mask of true, and said so it is `count ? count <= 1 : true`.
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

    // The order that puts both operands' lanes in order, where that is not the
    // order they are in and one order does: each a component of a register or a
    // variable, all different, rising together.
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

    // An operation whose every result component depends on that component of its
    // operands alone, so that a vector of them is the same operation on vectors.
    // A conversion is left out: `(float4)float4(a, b, c, d)` says less than four
    // casts do.
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
    /// ubfe and ibfe as the shift and mask they mean. Where the width and offset are
    /// immediates - which is how fxc writes a field it can see - the mask and the
    /// shifts are worked out here and written as numbers, which is what a shader
    /// author writes and what fxc turns back into the one instruction. ibfe fills
    /// the top with the field's own top bit, which HLSL does by shifting the field
    /// up to the top of a signed value and back down.
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
                // Five bits wide is a mask of 31; a whole word wide is no mask.
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
    /// bfi as the masks it means: the value with the field's bits cleared, or'd
    /// with the inserted bits shifted into place and cut to the field. Written with
    /// the numbers worked out where they are immediates, the way an author packs
    /// a byte into a word - `(v & ~65280) | ((b << 8) & 65280)`.
    /// </summary>
    private string CompileBitFieldInsert(List<HlslTreeNode> components)
    {
        List<HlslTreeNode> widths = [.. components.Select(c => c.Inputs[0])];
        List<HlslTreeNode> offsets = [.. components.Select(c => c.Inputs[1])];
        string insert = CompileIntegerOperand(components.Select(c => c.Inputs[2]));
        string value = CompileIntegerOperand(components.Select(c => c.Inputs[3]));

        // A bfi keeps the bits of its base that the mask does not cover, and a base
        // of nothing has none to keep. fxc writes a masked shift that way - the
        // whole of `(x & 7) << 2` is a bfi over zero - and writing the half that
        // keeps the base out in full turned one instruction into a not, an and and
        // an or of a constant that is nothing at all.
        bool insertsIntoNothing = components.All(c => ConstantMatcher.IsZero(c.Inputs[3]));
        if (AllIntegers(widths, out int[] width) && AllIntegers(offsets, out int[] offset))
        {
            int[] mask = [.. width.Zip(offset, (w, o) => (w >= 32 ? -1 : (1 << w) - 1) << o)];
            string maskText = CompileIntegers(mask);
            string shifted = offset.All(o => o == 0) ? insert : $"({insert} << {CompileIntegers(offset)})";
            // Bracketed: an and binds more loosely than most of what this can sit
            // inside, and `bfi(...) / 4` written bare is anded with the quotient.
            return insertsIntoNothing
                ? $"({shifted} & {maskText})"
                : $"({value} & ~{maskText}) | ({shifted} & {maskText})";
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
        // By the node, unless the nodes were written as one call: three subtracts
        // that are a cross product bind as tightly as any call does, and
        // `(cross(a, b)) * c` says nothing with its brackets.
        return AssociativityTester.NeedsParenthesesAsOperand(list[0]) && !IsOneCall(compiled)
            ? $"({compiled})"
            : compiled;
    }

    // Whether the text is a vector constructor - float2(...), int3(...) - and
    // nothing beside it.
    private static bool IsConstructor(string text)
    {
        return IsOneCall(text)
            && System.Text.RegularExpressions.Regex.IsMatch(text, @"^(float|int|uint|bool)[234]\(");
    }

    // Whether the text is one function call and nothing beside it: a name, and a
    // bracket that opens after it and closes at the end.
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

    /// <summary>
    /// Whether a register holds floats, from the declaration that names it: a
    /// constant buffer variable by its own type, an input by the component type its
    /// signature gives, and a thread id by neither, being a uint.
    /// </summary>
    private bool IsFloatRegister(RegisterInputNode register)
    {
        return _registers.GetDeclaredType(register.RegisterComponentKey) == DeclaredType.Float;
    }

    /// <summary>
    /// A uniform or an attribute declared an integer and read by float arithmetic
    /// with nothing converting it between: in shader model 4 every instruction says
    /// what it reads, and fxc writes a utof or an itof wherever the source meant the
    /// number, so a float instruction reading the register as it stands is reading
    /// a float's bits. `i.position * i.blendweight * packedScale` over a uint weight
    /// and a uint scale handed in as bits converted both instead - a wrong value,
    /// and two utof in the recompile.
    ///
    /// Not in shader model 3, where every register is a float's and an integer
    /// uniform is read as one because nothing else can read it: `address` there is
    /// a number. And only where every reader that says what it reads says float -
    /// a GetDimensions reading a mip level says nothing, an integer add says
    /// integer, and either leaves the value the number it is.
    /// </summary>
    private bool IsIntegerRegisterReadAsFloat(HlslTreeNode value)
    {
        if (value is not RegisterInputNode register
            || register.RegisterComponentKey.RegisterKey is not D3D10RegisterKey
                { OperandType: OperandType.ConstantBuffer or OperandType.Input })
        {
            return false;
        }
        return _registers.GetDeclaredType(register.RegisterComponentKey) is DeclaredType.Int or DeclaredType.Uint
            && ValueTypes.ReadAsInteger(register) == false;
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

    /// <summary>
    /// A value where an operation that works on integers wants one, and reads it
    /// through an argument rather than an operator: the value or the comparand of
    /// an interlocked operation. A float there is its bits and not its number -
    /// a depth written into groupshared memory as asuint is minimised as the
    /// integer those bits make, and converting it would compare the depth rounded
    /// to a whole number instead.
    /// </summary>
    public string CompileIntegerArgument(HlslTreeNode node, bool unsigned = false)
    {
        string compiled = Compile(node);
        if (!IsFloatValued(node))
        {
            return compiled;
        }
        // asuint where the destination is one, so that the reinterpretation and the
        // thing it is handed to agree: asint into a uint compiles, with fxc warning
        // X3203 about the mismatch and assuming unsigned, which is a warning the
        // shader it came from never had.
        return unsigned ? $"asuint({compiled})" : $"asint({compiled})";
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
            // asdouble reads uints and makes a double, so the same warning applies
            // to it: ConsumesInteger answers about what goes in.
            BitsToDoubleOperation => true,
            ComparisonNode => false,
            // What the operation computes in, which a template that rebuilt it has
            // handed on - the flag the parse put on the instruction is not on the
            // nodes a template builds, and they came out as not floats at all.
            Operation operation => ValueTypes.ComputesInIntegers(operation) == false,
            _ => false,
        };
    }

    /// <summary>
    /// Set while compiling the value of an assignment to an integer variable. A
    /// vector constructor has no idea what it is being assigned to, and
    /// `int2 t0 = float2(a, b)` sends both components through a float on the way.
    /// </summary>
    private bool _assigningToInteger;

    /// <summary>
    /// Set while the value an integer variable is assigned is compiled, and only
    /// until an operation is entered: what the variable is handed directly, as
    /// opposed to what some float arithmetic inside it reads.
    /// </summary>
    private bool _assignedDirectlyToInteger;

    // What the multiply was of, or the whole node where the one was folded away.
    private static HlslTreeNode FactoredOfFoldedMultiply(HlslTreeNode node)
    {
        return node is not MultiplyOperation multiply
            ? node
            : multiply.Factor1 is ConstantNode ? multiply.Factor2 : multiply.Factor1;
    }

    // What it was multiplied by, or the one that was folded out.
    // What it was multiplied by, or the one that was folded out. A fresh node
    // rather than one of the graph's: nothing reads it, and the compiler runs over
    // and over.
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

    /// <summary>
    /// A vector of dot products against one vector of weights, over vectors whose
    /// components line up across the dots - the first of every left side from one
    /// value, the second from another - as those values weighted and added up:
    /// `float3(dot(float3(t1.x, t6.x, t0.x), k), dot(float3(t1.y, t6.y, t0.y), k), ...)`
    /// is `t1 * k.x + t6 * k.y + t0 * k.z`. That is a basis applied to a direction,
    /// three vector mads in the shader it came from; as the dots, fxc transposes the
    /// basis into registers of its own and does a dp3 per component, which cost
    /// transposed_basis eight instructions.
    ///
    /// Recognising it means seeing the components together, which only happens here,
    /// and deciding means knowing whether each value comes out as one - `t1`, a
    /// normalize, a cross product - or as a constructor of loose components, which is
    /// no better than the dots. That is asked by measuring, which leaves nothing
    /// behind; the first attempt at this compiled to decide, and moved nine goldens
    /// with no transpose in them by numbering their variables in passing.
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
                // A dot broadcast across the vector is one value, not a transpose:
                // every component the same dot, every column one component repeated -
                // read again for each component, so asked by value, not by node.
                || !Enumerable.Range(0, weights.Length).All(j => ColumnDiffers(dots, 1 - shared, j)))
            {
                continue;
            }
            // Each component as the sum it is, for the compiler to group across the
            // components the way it groups any arithmetic: the values' components into
            // the values, and a weight read by every component into a broadcast.
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
                // Inside another expression, bracketed: the node is a dot product,
                // which the expression around it does not bracket, and this is a sum.
                string weighted = Compile(sums);
                return _compileDepth > _topDepth ? $"({weighted})" : weighted;
            }
            finally
            {
                // Built to be compiled and nothing else: left as readers of what they
                // read, they would count in every question asked of the graph after.
                foreach (HlslTreeNode node in built)
                {
                    foreach (HlslTreeNode input in node.Inputs)
                    {
                        input.Outputs.Remove(node);
                    }
                }
                // And left in what the compile recorded, they are text the naming
                // passes find twice - once as these sums and once as the dots the
                // caller records them as - and name: a normal map's basis came out
                // as `float3 t6 = <sum>; float3 t7 = t6;`.
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
    /// Dot products that are the rows of one matrix multiply in some order other
    /// than their own, as the multiply read through the swizzle that puts each row
    /// back in its component; or null where no order of them is the whole matrix.
    /// The grouper checks each row against the first, so in an order it takes,
    /// position k really is row k.
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
            // Component i of the result is the row it holds: the position of i in
            // the order.
            string swizzle = string.Concat(Enumerable.Range(0, components.Count)
                .Select(i => "xyzw"[Array.IndexOf(order, i)]));
            return $"{_matrixMultiplicationCompiler.Compile(multiplication)}.{swizzle}";
        }
        return null;
    }

    // Every order of the indices below count, the one they are in first.
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

        // Assigning to an integer does not make the components integers. Where one
        // of them computes a float - a constructor inside the float half of an
        // integer assignment, `dot(levels.xyz, float3(...))` under a cast to uint -
        // an int constructor truncates it before the arithmetic that wanted it.
        // A vector of doubles is a double vector, whatever the assignment wants:
        // `float2(a, b)` over two doubles rounds both of them on the way into a
        // buffer that holds neither.
        string integerType = _assigningToUnsigned ? "uint" : "int";
        // Some of each, handed straight to an integer variable: the variable holds
        // bits, and the float components are the bits they are. Built as a float
        // vector, the integers among them were converted to floats on the way in
        // and the floats converted back - `int2 t2 = float2(t1.x, x * s)` over a
        // t1 that was bits already, which is neither value.
        bool reinterpretFloats = _assignedDirectlyToInteger
            && components.Any(IsFloatValued) && !components.All(IsFloatValued);
        string type = components.All(IsDoubleValued)
            ? "double"
            : reinterpretFloats || (_assigningToInteger && !components.Any(IsFloatValued))
                ? integerType
                : "float";
        var parts = new List<string>();
        foreach (IList<HlslTreeNode> group in componentGroups)
        {
            string compiled = Compile(group, group.Count);
            if (reinterpretFloats && IsFloatValued(group[0]))
            {
                compiled = $"as{integerType}({compiled})";
            }
            // A broadcast of a register fills its group with a swizzle, so its
            // text is already as wide as the components it covers. A broadcast
            // whose text stays a scalar - an expression with nothing in it that
            // took the group's width - covers one. A constructor pads only when
            // a vector is among its arguments, so a scalar is written once per
            // component, which is how a run of constants is written one per
            // argument.
            int copies = group.Count > 1 && BroadcastsScalar(group) ? group.Count : 1;
            parts.AddRange(Enumerable.Repeat(compiled, copies));
        }
        return $"{type}{components.Count}({string.Join(", ", parts)})";
    }

    /// <summary>
    /// Whether the group is one value read by every component of it, and its text
    /// is a scalar rather than the width of the group.
    /// </summary>
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
    /// How wide the text of a broadcast comes out when the compiler is asked for
    /// `requested` components of it - which is what the constructor above hands
    /// each group. The answer is what the case for each operation does with the
    /// width it is given: a read asked for its width spreads into a swizzle of
    /// it, an operation is as wide as the operands it passes the request on to,
    /// and a value with no components to spread - a constant, the one number a
    /// dot or a load reads - is one however it is asked. An operation that
    /// drops the request is only as wide as its operands make it on their own,
    /// and every operand of a broadcast is the one node read twice.
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
                // A vector operand is its swizzle: distinct components say their
                // own width whatever was asked, and the same component read
                // twice is as wide as it is asked for.
                return vector.Inputs.Skip(1).All(c => SameValue(c, vector.Inputs[0]))
                    ? requested
                    : vector.Inputs.Count;
            case MultiplyOperation:
                return MaxBroadcastWidth(node.Inputs, requested);
            // A cast spells itself `(float2)` over a group of two: it names a
            // vector type, and HLSL broadcasts its scalar operand into one.
            case ConvertOperation:
                return requested;
            case MoveConditionalOperation or CompareOperation:
                // The first input is the condition and the tested value, read as
                // one by the case that prints it; the branches after it are what
                // carries the width.
                return MaxBroadcastWidth(node.Inputs.Skip(1), requested);
            case ComparisonNode comparison
                when comparison.IsUnsigned && !IsUnsignedAlready(comparison.Left)
                    && !IsUnsignedAlready(comparison.Right):
                // The cast saying so is as wide as the comparison.
                return requested;
            // A sample's level answers with one float, and the render target's
            // sample count is one uint: neither has components to spread.
            case TextureLoadOutputNode lod
                when lod.Controls.HasFlag(TextureLoadControls.CalculateLod):
                return 1;
            case RenderTargetSampleCountNode:
                return 1;
            // A variable stays the name of its value; what it multiplies,
            // compares or fills decides how wide it is read.
            case TempVariableNode:
                return 1;
            // Everything else that names a component names a value that has them,
            // and spreads into a swizzle when asked for its width.
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

    // Double literals, as a vector where the values of one instruction read
    // different doubles out of the one immediate. The components are the whole
    // of it: one number where they all agree, and a constructor wide enough for
    // the rest.
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
    /// Set while the operands of a float operation are compiled. A bits value read
    /// there is the float those bits are and gets an asfloat; one an integer
    /// operation reads, or a move carries on, is left as it is.
    /// </summary>
    private bool _readingAsFloat;

    private string CompileOperation(Operation operation, List<HlslTreeNode> components, int promoteToVectorSize)
    {
        bool wasReadingAsFloat = _readingAsFloat;
        // From what the operation computes in, and where that is not known - a
        // template that rebuilds an add or a multiply makes a node with no flag on
        // it - from the value it makes. The operators that read bits, and the moves
        // that carry them, are named separately because they make an integer out of
        // whatever they are given.
        bool wasReadingAsBits = _readingAsBits;
        bool wasAssignedDirectly = _assignedDirectlyToInteger;
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
        try
        {
            return CompileOperationOperands(operation, components, promoteToVectorSize);
        }
        finally
        {
            _readingAsFloat = wasReadingAsFloat;
            _readingAsBits = wasReadingAsBits;
            _assignedDirectlyToInteger = wasAssignedDirectly;
        }
    }

    /// <summary>
    /// Set while the operands of a bitwise operator or a shift are compiled. What
    /// those read is the bits of whatever they are given, so an integer texel read
    /// there is left as the integer it is - `asfloat(mask.Load(p)) & 4` is X3082.
    /// </summary>
    private bool _readingAsBits;

    // Set beside _assigningToInteger while the value of an assignment to an unsigned
    // variable is compiled, so that a constant vector in it says uint rather than
    // converting from int silently.
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

            case BitsToDoubleOperation _:
                {
                    // Both halves as unsigned integers: asdouble takes uints, and a
                    // half read as a float would come out as the number its bits
                    // spell. Gathered across the group the way an ordinary operand
                    // is, so that the two doubles of a double2 are one call over a
                    // uint2 of low words and a uint2 of high ones.
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
                    // An accumulation names what it adds to first: `t0 = t0 + x`.
                    // Which side fxc's add has it on is its scheduling, and it
                    // came back the other way round from one round to the next.
                    else if (_compileDepth == _topDepth && _assignedVariables != null
                        && addend2.All(_assignedVariables.Contains)
                        && !addend1.Any(_assignedVariables.Contains))
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
                    // By the node, unless it was written as one call, as CompileOperand
                    // asks: a cross product is three subtracts and binds as a call does.
                    string first = Compile(multiplicand1, promoteToVectorSize);
                    string second = Compile(multiplicand2, promoteToVectorSize);
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
                    // The brackets a division needs, for the reason it needs them: %
                    // binds as tightly as / does. `(id - 2) % 3` written without them
                    // is `id - 2 % 3`, which takes two off the index and never wraps
                    // it - and a divisor that is itself a product or a quotient needs
                    // them too, since `a % b * c` multiplies the remainder.
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

                    // An rsq is reduced to one over a square root, which is what lets
                    // the templates see through it - a reciprocal of one is the root
                    // itself, and a length divided by itself is a normalize. Where
                    // none of them took it, the division is still the rsq it was, and
                    // written as one it is one instruction rather than a sqrt and a
                    // div. HLSL spells it rsqrt, the way it spells rcp.
                    if (ConstantMatcher.IsOne(dividend.First())
                        && divisor.First() is SquareRootOperation)
                    {
                        return $"rsqrt({Compile(divisor.Select(d => d.Inputs[0]))})";
                    }

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
            case ArcTangent2Operation _:
                {
                    var value1 = Compile(components.Select(g => g.Inputs[0]));
                    var value2 = Compile(components.Select(g => g.Inputs[1]));

                    // umin and umax pick their overload from the operands, the way
                    // ushr does: one unsigned operand makes the call unsigned, so
                    // where neither says so the first is made to.
                    bool isUnsigned = operation is MinimumOperation { IsUnsigned: true }
                        or MaximumOperation { IsUnsigned: true };
                    if (isUnsigned && !IsUnsignedAlready(operation.Inputs[0]) && !IsUnsignedAlready(operation.Inputs[1]))
                    {
                        string size = components.Count > 1 ? components.Count.ToString() : "";
                        value1 = $"(uint{size}){CompileOperand(components.Select(g => g.Inputs[0]))}";
                    }

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
                        || value[0] is ConsumerOperation
                        // And so do the calls that take more than one argument,
                        // which were bracketed for no reason: `(float)(max(a, b))`.
                        || value[0] is MaximumOperation or MinimumOperation
                            or PowerOperation or ArcTangent2Operation or ClampOperation or SmoothStepOperation
                            or LinearInterpolateOperation or FusedMultiplyAddOperation
                            or DotProductOperation or LengthOperation
                            or FirstBitHighOperation
                        // A subscript carries its own brackets at the end.
                        || value[0] is VectorComponentNode;
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
                    // The vector as wide as it is, not as wide as the one component
                    // asked for: the subscript picks out of the whole of it, and a
                    // swizzle narrowing it first would move the component the index
                    // names.
                    string vector = Compile([select.Vector], select.Vector.Length);
                    return $"{vector}[{CompileAsInteger([select.Index])}]";
                }
            case DotProductOperation _:
                {
                    // A dot takes its width from the vector, so a repeated component has to
                    // stay written out. dot(r0.ww, r1.xx) is a dp2add; dot(r0.w, r1.x) is
                    // a multiply, and recompiles as one.
                    int vectorSize = components[0].Inputs[0] is GroupNode vector
                        ? vector.Inputs.Count
                        : PromoteToAnyVectorSize;
                    // The pairs in the order both operands have their lanes, where one
                    // order does for both: dot(a.yzx, b.yzx) is dot(a, b), and that is
                    // how a dot over a register fxc rotated reads otherwise.
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
                    // TODO: consider the byte offset in Inputs[1].
                    // The element or the byte offset is an index, not a float's bits:
                    // in a float context the address would be reinterpreted, and
                    // `buffer[asfloat(element)]` is an index made from the bits of a
                    // float rather than the element asked for.
                    // One load reads one element, and every component of a broadcast
                    // of it reads that element through the one index: widening the
                    // address asked for the group spelled `buffer[(int2)element]`, an
                    // index of two elements where the components asked for one read
                    // twice. Only loads of different elements fill the subscript into
                    // a vector index.
                    var addressInputs = components.Select(g => g.Inputs[0]).ToList();
                    var address = CompileAsInteger(
                        addressInputs.Skip(1).All(a => SameValue(a, addressInputs[0]))
                            ? [addressInputs[0]]
                            : addressInputs);
                    var resource = (RegisterInputNode)components[0].Inputs[2];
                    RegisterKey resourceKey = resource.RegisterComponentKey.RegisterKey;
                    if (load.IsGroupShared)
                    {
                        // Raw groupshared memory: the element, not a Load, because
                        // the declaration is an array. One load reads one element,
                        // so there is no width or swizzle to take off it.
                        return $"{_registers.GetRegisterName(resourceKey)}[{address}]";
                    }
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
                    // The byte offset picks a row where the element is a matrix and
                    // a member where it is a struct.
                    string element = $"{_registers.GetRegisterName(resourceKey)}[{address}]";
                    string members = _registers.NameStructuredMembers(resourceKey, element,
                        load.ElementByteOffset,
                        [.. components.Select(g => ((IHasComponentIndex)g.Inputs[2]).ComponentIndex)]);
                    if (members != null)
                    {
                        return members;
                    }
                    // Groupshared memory wider than one register is written as a
                    // struct of them, and the byte offset picks which. There is no
                    // reflection entry to name the members from, so they are named for
                    // where they start and the offset is counted in them.
                    if (_registers.ThreadGroupSharedMember(resourceKey, load.ElementByteOffset)
                        is var (memberName, memberComponents, memberBase))
                    {
                        return $"{element}.{memberName}" + GetAstSourceSwizzleName(
                            components.Select(g => (IHasComponentIndex)g.Inputs[2]),
                            memberComponents,
                            componentBase: -memberBase);
                    }
                    string row = _registers.ApplyStructuredElementRow(resourceKey, element,
                        load.ElementByteOffset);
                    // Where the element is neither a matrix (whose byte offset picks a
                    // row) nor a struct (whose offset picks a member), it is one scalar
                    // or a vector, and the offset selects a component of it: reading the
                    // .w of a uint4 is the load at offset twelve. The offset is whole
                    // components past the operand's own, so it is the swizzle's base.
                    int componentBase = ReferenceEquals(row, element)
                        ? -load.ElementByteOffset / 4
                        : 0;
                    // A double element takes two components for each value it holds,
                    // so the element is that many values wide and the swizzle names
                    // them rather than the components: a StructuredBuffer<double>
                    // loads r0.xy for its one number, and named by component that
                    // came out `input[i].x` off a scalar.
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
                    return string.Format("{0} " + op + " {1}",
                        Compile(components.Select(g => g.Inputs[0])),
                        Compile(components.Select(g => g.Inputs[1])));
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
                    // The attribute is grouped over its components the way any
                    // elementwise operation's operand is; where it is evaluated is
                    // one value for all of them, and an integer either way.
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

                    // A select whose conditions do not make one vector - two unrelated
                    // tests packed into a register - is two selects, not a select on
                    // a constructor: `float2(a, b) ? 1.0 : 0` is HLSL, and reads as
                    // nothing anyone wrote.
                    if (components.Count > 1 && IsConstructor(value1))
                    {
                        string selects = string.Join(", ", components.Select(c =>
                            $"{Compile([c.Inputs[0]])} ? {Branch(c.Inputs[1])} : {Branch(c.Inputs[2])}"));
                        return $"float{components.Count}({selects})";

                        // The same point on a float branch as the whole select puts.
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
    // visible rather than emitting a division that only fxc would fold away. An
    // element is a subscript, so it is compiled as an integer.
    public string CompileRegisterIndexAsElement(RelativeAddressNode address, int rows)
    {
        return address.IndexCountsElements
            ? CompileAsInteger([address.Index])
            : CompileRegisterIndexAsElement(address.Index, rows);
    }

    public string CompileRegisterIndexAsElement(HlslTreeNode index, int rows)
    {
        // DXBC shifts where D3D9 multiplies: `ishl r0.x, v1.x, l(2)` is the element
        // times four.
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
        // An index fxc has already masked is masked inside the register number
        // rather than outside it, and in one instruction: `m[(i + 1) & 7]` is
        // `bfi r0.x, l(3), l(2), i + 1, l(0)`, the mask of three bits shifted up by
        // the two that multiply the element index by the registers it takes. Taking
        // the shift back off leaves the mask the shader wrote, where the shift is
        // exactly the one this index is over. Left as a division, the whole bfi
        // survived into the output with a divide after it, and fxc rebuilt that as
        // two instructions rather than the one it had.
        if (index is BitFieldInsertOperation insert
            && insert.Inputs[0] is ConstantNode { IntegerValue: int width }
            && insert.Inputs[1] is ConstantNode { IntegerValue: int offset }
            && ConstantMatcher.IsZero(insert.Inputs[3])
            && width < 32 && rows == 1 << offset)
        {
            // Bracketed twice over, and neither is spare: an and binds more
            // loosely than the constant offset a member of the element adds onto
            // this, and more loosely than an or or an xor the index itself might be
            // made of - `a | b & 7` ands before it ors.
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

    // An offset shifts the read by whole texels. Leaving it out compiles and
    // reads the wrong ones, so it belongs in the call.
    /// <summary>
    /// A Load address that is a texel plus a constant, written as the whole address
    /// plus the constant: `int3(t2, 0) + int3(1, 0, 0)` rather than
    /// `int3(t2 + int2(1, 0), 0)`. The shader kept the address in a register, mip
    /// level and all, and stepped to the neighbouring texel with one iadd over the
    /// register; given the texel's add alone fxc adds the pair and moves the mip
    /// level in beside it again, an instruction more (screen_position). Only where
    /// the mip level is a constant, and some lane adds an integer constant.
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
        bool wasReadingAsFloat = _readingAsFloat;
        _assigningToInteger = true;
        // And the number is what addresses the element: the bits a float value is
        // standing in for are not. See CompileAsInteger.
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
    /// Compiles a value standing where an integer is wanted - the element of a
    /// buffer of integers. A vector constructor has no idea what it is being
    /// assigned to, so `countbits(x)` and its neighbours came out inside a float4
    /// on the way into a RWStructuredBuffer&lt;uint4&gt;, which is a conversion each
    /// way and loses everything above what a float holds exactly.
    /// </summary>
    /// <summary>
    /// The value an assignment writes, without the partial precision cast where the
    /// variable is declared half: the declaration carries the precision then, and
    /// `half3 t0 = (half3)x;` says it twice.
    /// </summary>
    private static HlslTreeNode AssignedValue(TempAssignmentNode assignment)
    {
        return assignment.TempVariable.IsHalf
            && assignment.Value is ConvertOperation { TargetType: "half" } cast
            ? cast.Value
            : assignment.Value;
    }

    public string CompileAsInteger(IEnumerable<HlslTreeNode> group)
    {
        bool wasAssigningToInteger = _assigningToInteger;
        bool wasReadingAsFloat = _readingAsFloat;
        _assigningToInteger = true;
        // Where an integer is wanted, an integer's bits standing in for a float are
        // not what is read: an address compiled with the float context still on came
        // out as `tex[asfloat(coordinate)]`, the bits of a float truncated back into
        // an index of a texel nobody asked for.
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
            // disassembly calls it icb and so does the one this writes out. The
            // immediate beside the index is the row the read starts at - fxc puts one
            // array after another in the one buffer, so `icb[r0.x + 5]` reads the
            // second of them - and dropped, the read was of the wrong rows entirely.
            if (arrayKey.RegisterKey is D3D10RegisterKey immediateKey
                && immediateKey.OperandType == OperandType.ImmediateConstantBuffer)
            {
                // The immediate beside the index is the row the read starts at, which
                // is the array it reads rather than an offset into one: dropped, the
                // read was of the wrong rows entirely.
                return $"{_registers.ImmediateConstantBufferName(immediateKey.Number)}"
                    + $"[{index}]{swizzle}";
            }

            // Named from the declaration rather than the register, which would carry
            // an element index of its own. The base register need not be the first of
            // the array: `floats[i + 2]` reads c2[a0.x] when floats starts at c0.
            string arrayName = _registers.GetRegisterName(arrayKey);
            if (arrayKey.RegisterKey is D3D10RegisterKey vertexKey
                && vertexKey.OperandType == OperandType.Input
                && vertexKey.GSVertex.HasValue
                && _registers.FindInputDeclaration(vertexKey, arrayKey.ComponentIndex)
                    is RegisterDeclaration vertex)
            {
                // The vertex array is the subscript and the semantic the member, the
                // other way round from a constant buffer array. Which member, and how
                // wide it is, is a question about the component read and not about the
                // register: two semantics can share one, and the register's own
                // declaration named the first of them. `patch[id].position.w` was the
                // texture coordinate packed into the position's register, and a float3
                // has no w to compile.
                string memberSwizzle = GetAstSourceSwizzleName(componentsWithIndices,
                    _registers.GetRegisterMaskedLength(arrayKey),
                    promoteToVectorSize,
                    _registers.GetInputComponentBase(arrayKey));
                return $"{_registers.InputArrayName}[{index}].{vertex.Name}{memberSwizzle}";
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
                    // An array of matrices takes two subscripts, the same as the D3D9
                    // case below: the register index counts rows across the array, so
                    // the element is that index over the row count and the row is what
                    // is left. Indexing it as though each register were an element
                    // gives dot(float4, float4x4).
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
                    // An array of structs: the index counts registers, so the element
                    // is that over the registers one takes - and the register left over
                    // within the element names the member, each of which owns one.
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
                    // An array of matrices takes two subscripts. The register index
                    // counts rows across the whole array, so the element is that
                    // index over the row count and the row is the constant left over.
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
                // A component base from whichever kind of packing applies - at most one
                // of the two is ever non-zero for a given register.
                int componentBase = _registers.GetConstantComponentBase(shaderInput.RegisterComponentKey)
                    + _registers.GetInputComponentBase(shaderInput.RegisterComponentKey);
                swizzle = GetAstSourceSwizzleName(componentsWithIndices,
                    _registers.GetRegisterMaskedLength(shaderInput.RegisterComponentKey),
                    promoteToVectorSize,
                    componentBase,
                    _registers.GetConstantComponentsPerElement(shaderInput.RegisterComponentKey));
            }

            // A named struct member identifies its register outright - each member
            // owns one - so the swizzle reads the member's own components. A scalar
            // has none to pick, and broadcasts as it is where a wider value is wanted.
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

            // Components off the register rather than a swizzle after its name: one
            // component of a matrix's column is an element, and HLSL subscripts an
            // element rather than transposing to reach it.
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
            // Read as a float where the load's result is one, the address would be
            // reinterpreted instead: `tex[asfloat(coordinate)]` truncates the bits
            // of a float back to an index, which is not the texel asked for.
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
            // A texel of a texture of uints is an integer, and is named as one
            // where its readers read it as one. Read as anything else it is the
            // bits it holds and not the number they make: a G-buffer packs a depth
            // into such a texture beside a normal, and converting the depth would
            // give whatever number its bits happen to be.
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
            // From the resource operand, the way a sampled texel's channel is: the
            // destination is .xy and the resource swizzle says which of the two.
            string positionSwizzle = GetAstSourceSwizzleName(
                components.Select(c => (IHasComponentIndex)((SamplePositionNode)c).Resource),
                2, promoteToVectorSize);
            string index = CompileAsInteger([samplePosition.SampleIndex]);
            // The render target is asked about by a function of its own rather than
            // by a method on the resource, there being no resource to name.
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
                    // The sampler operand's swizzle says which channel is gathered -
                    // `s0.y` the green one - and only red's method goes without a
                    // suffix. Leaving the channel out gathered the red one and still
                    // compiled: output that computes something else.
                    string channel = textureLoad.Sampler.RegisterComponentKey.ComponentIndex switch
                    {
                        1 => "Green",
                        2 => "Blue",
                        3 => "Alpha",
                        _ => "",
                    };
                    // A comparison gather takes the value to compare against after
                    // the coordinate, the way a comparison sample does.
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
                // An offset the shader computes goes where an immediate one would,
                // and is an integer vector like one.
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
            // The reference and the source are the same for every component; the
            // accumulator is a vector like the result, so it is gathered across the
            // group the way an ordinary operand is. Read as integers throughout: the
            // intrinsic takes uints, and a reference read as a float would be its bit
            // pattern rather than the four bytes it is.
            // And as unsigned integers, since every parameter of msad4 is a uint: a
            // folded source word of 0xF1F2F3F4 is -235736076 as an int, which wraps to
            // the same bits and says the opposite of what the number is.
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
                accumulator = CompileAsInteger(components.Select(c => ((Msad4Node)c).Accumulator));
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
            // One call read at several components: the method runs once, and the
            // swizzle picks the result, the way a sample's does. The arguments
            // are whole registers - the body reads its parameters four
            // components at a time, and what it reads is what the call site had
            // in them.
            MarkMatched(components);
            LinkageModel.InterfaceInfo iface =
                _registers.Linkage.InterfaceByNumber(call.InterfaceNumber);
            string arguments = string.Join(", ", call.Arguments.Chunk(4)
                .Select(argument => Compile([.. argument])));
            string callSwizzle = GetAstSourceSwizzleName(
                componentsWithIndices, 4, promoteToVectorSize);
            return $"{iface.InstanceExpression(call.InstanceIndex)}"
                + $".{iface.Methods[call.FunctionIndex].Name}({arguments}){callSwizzle}";
        }

        if (first is LitOutputNode lit)
        {
            // Every component reads the same three inputs, so they are compiled from
            // this node rather than gathered across the group.
            string nDotL = Compile(new[] { lit.NDotL });
            string nDotH = Compile(new[] { lit.NDotH });
            string specularPower = Compile(new[] { lit.SpecularPower });
            string litSwizzle = GetAstSourceSwizzleName(
                componentsWithIndices, 4, promoteToVectorSize);
            return $"lit({nDotL}, {nDotH}, {specularPower}){litSwizzle}";
        }

        if (first is MatrixMultiplyOutputNode matrixComponent)
        {
            // As wide as the multiplication, not the register: a float4x3 read by
            // three wide dots writes three components of whatever register it lands
            // in, and swizzling against four asks for a component the mul has not
            // got.
            MarkMatched(components);
            string multiplied = _matrixMultiplicationCompiler.Compile(matrixComponent.Matrix);
            string matrixSwizzle = GetAstSourceSwizzleName(componentsWithIndices,
                matrixComponent.RowCount, promoteToVectorSize);
            return $"{multiplied}{matrixSwizzle}";
        }

        if (first is ReflectOutputNode reflectComponent)
        {
            // As wide as the vectors reflected, the same as a normalize and for the
            // same reason: the components of the reflect are what the swizzle picks
            // from, not the register they happen to sit in.
            MarkMatched(components);
            string incident = Compile(reflectComponent.Incident.Inputs);
            string normal = Compile(reflectComponent.Normal.Inputs);
            string reflectSwizzle = GetAstSourceSwizzleName(componentsWithIndices,
                reflectComponent.Incident.Inputs.Count, promoteToVectorSize);
            return $"reflect({incident}, {normal}){reflectSwizzle}";
        }

        if (first is NormalizeOutputNode)
        {
            // As wide as the vector normalized, not the register: nrm writes .xyz
            // of a four wide register, and swizzling against four wrote
            // `normalize(n).xyz` of a float3 - which fxc then spelled out as a
            // dp3, an rsq and a mul instead of the one nrm.
            MarkMatched(components);
            // Every component, in another order, is the normalize of the inputs in
            // that order: `normalize(t1.yzx).zxy` is normalize(t1). A variable
            // laid out in the order of the vector normalized - InWrittenOrder -
            // takes a rotated nrm's components that way.
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

            // Compile variable once with all components
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
            // A variable its readers type as an integer, holding a value that is a
            // float: the integer they read is its bits, so the assignment
            // reinterprets rather than converts. Converting rounded a bit pattern
            // to the number nearest it, which is not the same bits at all. Every
            // component, not the first: a vector of some of each reinterprets its
            // floats one by one, in the constructor.
            // A mask constant into a bool is the bool: `t = -1` is `t = true`.
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
            // And the other way about: a float variable holding what a byte
            // address buffer handed back holds the float those dwords are.
            else if (!tempAssignment.TempVariable.IsInteger
                && components.All(a => ValueTypes.IsReinterpretedAsFloat(((TempAssignmentNode)a).Value)))
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
                // The distinct variables, not the places they are read. One scalar
                // read by every component of a four wide load arrives here four
                // times, and numbering the positions made it an int4 whose .w was
                // the value - `input[t0.w]` where the shader said `input[t0]`.
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

            // No promoteToVectorSize: a variable names its value, and HLSL
            // broadcasts a scalar the width of whatever reads it. Writing
            // `t1.xxx * color.xyz` where `t1 * color.xyz` said the same thing
            // asks the reader to count swizzle letters.
            string swizzle = GetAstSourceSwizzleName(componentsWithIndices, (int)tempVariable.VariableSize);
            return $"{_registers.TemporaryPrefix}{tempVariable.DeclarationIndex}{swizzle}";
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
            // A word of a double standing as an output's own value is read from the
            // variable its asuint was hoisted into; the hoist rewires every other
            // reader, but a root has none to rewire.
            if (doubleBits.NamedAs == null)
            {
                throw new NotImplementedException(
                    "A word of a double was compiled before its asuint was named.");
            }
            return Compile(components.Select(c => (HlslTreeNode)((DoubleBitsNode)c).NamedAs));
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
        if (buffer == null)
        {
            return false;
        }
        if (resourceLoad.Address.FirstOrDefault() is not ConstantNode element)
        {
            // Read at an index the shader works out rather than a constant one, so
            // there is no one variable of the buffer to name: the array it is an
            // element of is named instead, subscripted by the address. Without this
            // the load fell through to the lookup for a texture, which a texture
            // buffer is not one of, and that threw for want of a match.
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
    /// A texture buffer's array, subscripted by the register the load asks for. The
    /// address counts registers within the buffer, so an array is indexed by however
    /// many registers one of its elements takes, and one that does not start at the
    /// buffer's first register is indexed from where it does start.
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
    /// `float4 t0 = input.Consume();` - the declaration and the call, which a
    /// consume buffer offers instead of a subscript. One statement, where a
    /// GetDimensions takes two, because this one returns its element rather than
    /// filling out parameters.
    /// </summary>
    private string CompileConsumeCall(List<TempAssignmentNode> assignments)
    {
        var consume = (ConsumeNode)assignments[0].Value;
        TempVariableNode variable = assignments[0].TempVariable;
        ResourceDefinition buffer = _registers.ResourceDefinitions
            .First(d => d.BindPoint == consume.Buffer.RegisterComponentKey.RegisterKey.Number
                && d.ShaderInputType == D3DShaderInputType.UavConsumeStructured);
        string width = variable.VariableSize == 1 ? "" : variable.VariableSize.ToString();
        // What the buffer holds, the way the resinfo call below asks its instruction
        // rather than assuming. Written float outright, a ConsumeStructuredBuffer of
        // uints handed its element to a float variable - the uint converted going in
        // and converted back coming out, which above 2^24 is a different number, and
        // the shift that doubled it written as an add of floats. An element with
        // members of its own is a struct this does not name, and stays as it was.
        string scalar = buffer.ElementType is { MemberInfo: null or { Count: 0 } }
            ? buffer.ElementType.ParameterType.ToString().ToLowerInvariant()
            : "float";
        return $"{scalar}{width} {_registers.TemporaryPrefix}{variable.DeclarationIndex} = {buffer.Name}.Consume();";
    }

    /// <summary>
    /// The declaration and the call for a hoisted asuint over a double: the two
    /// words come back through out parameters, so the variable is declared first
    /// and the call fills it. Two statements, which the writer indents line by line
    /// - the same shape the resinfo call below takes.
    /// </summary>
    private string CompileDoubleBitsCall(List<TempAssignmentNode> assignments)
    {
        var bits = (DoubleBitsNode)assignments[0].Value;
        TempVariableNode variable = assignments[0].TempVariable;
        string name = $"{_registers.TemporaryPrefix}{variable.DeclarationIndex}";
        return $"uint2 {name};" + "\r\n"
            + $"asuint({Compile(bits.Value)}, {name}.x, {name}.y);";
    }

    private string CompileResourceInfoCall(List<TempAssignmentNode> assignments)
    {
        var info = (ResourceInfoNode)assignments[0].Value;
        TempVariableNode variable = assignments[0].TempVariable;
        RegisterKey resourceKey = info.Resource.RegisterComponentKey.RegisterKey;
        // A buffer is bound as one of several kinds, and which it is decides the
        // overload below as well as where the name comes from.
        ResourceDefinition resource = info.IsBuffer
            ? _registers.GetBufferDefinition(resourceKey)
            : _registers.GetTextureDefinition(resourceKey);

        string type = info.ReturnType == D3D10ResInfoReturnType.Uint ? "uint" : "float";
        string name = $"{_registers.TemporaryPrefix}{variable.DeclarationIndex}";
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
            return declaration + "\r\n" + (info.ReportsStride
                ? $"{resource.Name}.GetDimensions({name}.x, {name}.y);"
                : $"{resource.Name}.GetDimensions({name});");
        }
        // From what the resource is, not from whether the sample count was asked
        // for. A multisampled texture declared with its count - `Texture2DMSArray
        // <float4, 4>` - has that count folded into a constant, so there is no
        // sampleinfo to go by, and the overloads it has are still the ones without
        // a mip level in them. Asked for the mip form, the width went into the
        // level: `GetDimensions(0, ...)` puts a literal where an out parameter goes.
        bool isMultisampled = assignments.Any(a => ((ResourceInfoNode)a.Value).IsSampleCount)
            || resource.Dimension is ResourceDimension.Texture2Dms
                or ResourceDimension.Texture2DmsArray;
        if (isMultisampled)
        {
            // Width, height and the sample count, with the element count between
            // them for an array, and no mip level in either.
            return declaration + "\r\n" + (variable.VariableSize == 4
                ? $"{resource.Name}.GetDimensions({name}.x, {name}.y, {name}.z, {name}.w);"
                : $"{resource.Name}.GetDimensions({name}.x, {name}.y, {name}.z);");
        }
        // The same overload table the variable was named with: a 1D reports its
        // width alone and the mip count in w; its array the element count in y;
        // an array of 2D, a cube array and a 3D the element count or the depth in
        // z. The mip form takes the level in and the count out, and is what was
        // asked for whenever the level was not the constant nought or the shader
        // read a component past the ones the shape's no-mip form reports - the mip
        // count always, the depth of a shape that has none - where a 4-wide
        // variable has the component either way.
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
            && (first.Left is TempVariableNode { IsBool: true }
                || (first.Left is RegisterInputNode register
                    && _registers.GetDeclaredType(register.RegisterComponentKey) == DeclaredType.Bool)))
        {
            string flag = Compile(first.Left);
            return first.Comparison == IfComparison.NE ? flag : $"!{flag}";
        }
        // Two comparisons compared for equality are the bools they came from, not
        // the masks they wrote. A mask is all ones or all zeroes, so one mask
        // equalling another answers exactly what one bool equalling the other does,
        // and `(a == b) == (c < d)` is what the shader said - a bitonic sort's
        // `(a > b) == ascending`, say. Read as the values they are, the masks leak
        // into the text: `((a == b) ? -1 : 0) == ((c < d) ? -1 : 0)` for the one
        // ieq. Bracketed because a comparison binds no tighter than this one does.
        //
        // Both sides, because a mask compared against anything else is the mask: a
        // comparison against zero is the negation of the condition and is written
        // where that is recognised, not here.
        if (first.Comparison is IfComparison.EQ or IfComparison.NE
            && components.Cast<ComparisonNode>().All(
                c => c.Left is ComparisonNode && c.Right is ComparisonNode))
        {
            string leftBool = Compile(components.Cast<ComparisonNode>().Select(c => c.Left));
            string rightBool = Compile(components.Cast<ComparisonNode>().Select(c => c.Right));
            return $"({leftBool}) {first.Comparison.ToHlslString()} ({rightBool})";
        }
        // Through CompileOperand, since a comparison binds tighter than the bitwise
        // operators and the conditional: `(x & 0x7f800000) == 0x7f800000` is a bit
        // test, and written without the brackets it is `x & (a == b)`, which is a
        // different expression and not one HLSL will even accept on a float.
        // A constant vector in an unsigned comparison is an unsigned one. Left as
        // int, `i >= int3(2, 4, 8)` against a uint is the signed/unsigned mismatch
        // fxc resolves by assuming unsigned and warns about; the cast below does not
        // help, because the other side already being unsigned is what makes it think
        // nothing needs saying.
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
            case MinimumOperation { IsUnsigned: true }:
            case MaximumOperation { IsUnsigned: true }:
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
        return _registers.GetDeclaredType(register.RegisterComponentKey) == DeclaredType.Uint;
    }

    /// <param name="componentBase">
    /// Which component of its register the thing being named starts at. A float3
    /// packed after a float sits at .yzw of its register, and naming that eyePos.yzw
    /// asks for components the variable has not got - it is eyePos.xyz.
    /// </param>
    private static string GetAstSourceSwizzleName(IEnumerable<IHasComponentIndex> inputs,
        int registerSize, 
        int promoteToVectorSize = PromoteToAnyVectorSize,
        int componentBase = 0,
        int componentsPerElement = 1)
    {
        if (registerSize == 1 || registerSize > 4)
        {
            // A scalar has no components to pick, but it can still be spread:
            // `dp2 r0.z, sigma.ww, sigma.ww` is 2 * sigma * sigma, and written
            // `dot(sigma, sigma)` it is sigma * sigma, since a dot of two scalars is
            // one product. The width the caller asks for is the width the value has
            // to be, and `sigma.xx` is how a scalar is made two wide.
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
