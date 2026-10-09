using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using HlslDecompiler.Hlsl.TemplateMatch;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public class HlslAstWriter : HlslWriter
{
    private NodeCompiler _compiler;
    private NodeGrouper _grouper;
    private ISet<HlslTreeNode> _doubleValues = new HashSet<HlslTreeNode>();
    private TemplateMatcher _templateMatcher;
    private int _loopDepth;
    private readonly HashSet<HlslTreeNode> _declaredVariables = HlslTreeNode.NewNodeSet();

    // The numbers of the variables declared so far, for the declarations that
    // have to know whether a name is taken whatever nodes stand under it.
    private readonly HashSet<int> _declaredIndices = [];
    // The variables each Consume call was named into, by the slot that identifies
    // it, so that a second statement reading the same call finds them.
    private readonly Dictionary<HlslTreeNode, TempVariableNode[]> _consumeVariables =
        new(ReferenceEqualityComparer.Instance);
    // And the variables each double taken apart was named into, by the double, so
    // that a statement reading one word finds the call the other was named by.
    private readonly Dictionary<HlslTreeNode, TempVariableNode[]> _doubleBitsVariables =
        new(ReferenceEqualityComparer.Instance);

    // Set while a linkage method's body is being written: a return there hands
    // back this register rather than the shader's outputs.
    private RegisterKey _linkageReturnRegister;

    // The statements of the function being written - main, or one linkage
    // method - for the questions that are about a whole function rather than
    // about the shader.
    private IList<IStatement> _functionStatements;
    private VariableLiveness _liveness;

    public HlslAstWriter(ShaderModel shader)
        : base(shader)
    {
    }

    protected override void WriteMethodBody()
    {
        // A hull shader is written as two functions through one writer, and what has
        // been named is a question about the function being written: the patch
        // constant function's t0 is not main's. Left standing, main's first variable
        // was taken for one declared already and printed as a bare assignment, which
        // is a name nothing declares.
        _declaredVariables.Clear();
        _declaredIndices.Clear();
        _everDeclaredVariables.Clear();
        _consumeVariables.Clear();
        _doubleBitsVariables.Clear();
        _loopDepth = 0;

        if (HasOutputStruct)
        {
            // One vertex variable per stream where the shader writes several: the two
            // structs differ, and so do the registers each stream calls its own.
            foreach (int? stream in _registers.HasSeveralStreams
                ? _registers.Streams.Select(s => (int?)s)
                : [null])
            {
                WriteLine($"{(stream == null ? GetOutputStructureName() : StreamStructureName(stream))} "
                    + $"{_registers.StreamVariableName(stream)};");
            }
            WriteLine();
        }

        if (_registers.IndexableTemps.Count != 0)
        {
            WriteIndexableTempDeclarations(CreateIntegerOperandAnalysis());
            WriteLine();
        }

        WriteAst(_ast);
    }

    private IntegerOperandAnalysis CreateIntegerOperandAnalysis()
    {
        return _phaseShader.Instructions.Count != 0 && _phaseShader.Instructions[0] is D3D10Instruction
            ? new IntegerOperandAnalysis(_phaseShader)
            : null;
    }

    private void WriteAst(HlslAst ast)
    {
        WriteFunctionStatements(ast.Statements, GetMethodReturnType() != "void",
            HasOutputStruct, returnRegister: null);
    }

    /// <summary>
    /// One function, written whole: main, or a linkage method's body - the same
    /// lowering, the same folds, and the same order of both, because a body is a
    /// function of its own and not a subroutine main happens to share a file with.
    /// A body hands back <paramref name="returnRegister"/> where main hands back
    /// its outputs.
    /// </summary>
    private void WriteFunctionStatements(IList<IStatement> statements, bool hasReturnValue,
        bool hasOutputStruct, RegisterKey returnRegister)
    {
        _doubleValues = _ast.DoubleValues;
        _functionStatements = statements;
        _liveness = null;
        _declaredVariables.Clear();
        _declaredIndices.Clear();
        _everDeclaredVariables.Clear();
        _consumeVariables.Clear();
        _doubleBitsVariables.Clear();
        _loopDepth = 0;
        _compiler = new NodeCompiler(_registers, _doubleValues);
        _grouper = new NodeGrouper(_registers);
        _templateMatcher = new TemplateMatcher(_grouper);

        // Before the finalizer, which takes readers away - see ValueTypes.Record.
        var values = new List<HlslTreeNode>();
        new StatementVisitor(statements).Visit(statement =>
        {
            values.AddRange(statement.Outputs.Values);
            values.AddRange(statement.Inputs.Values);
            values.AddRange(statement.HeldNodes);
        });
        ValueTypes.Record(values);
        StatementFinalizer.Finalize(statements, hasReturnValue, hasOutputStruct,
            CreateIntegerOperandAnalysis(), _doubleValues,
            liveOut: returnRegister == null ? null : [returnRegister]);
        FindDeclaredVariables(statements);

        // A fold that sees through a variable can empty a statement above the one it
        // fires in, and what that statement named has to be gone from the output
        // before its line is written - so every fold runs, and the names no fold
        // leaves a reader for go, before any of the function is written.
        TempResolver resolver = TempResolver.Build(statements);
        _templateMatcher.TempResolver = resolver;
        ReduceAll(statements);
        resolver.RemoveUnreadAssignments(statements);

        _linkageReturnRegister = returnRegister;
        WriteStatements(statements);
        _linkageReturnRegister = null;
    }

    /// <summary>
    /// A body of the dynamic linkage, written as the method it is: the statements
    /// the parser read off its instructions, lowered and folded the way main's
    /// are, and returning the one register it writes.
    /// </summary>
    protected override void WriteLinkageMethodBody(LinkageModel.FunctionBodyInfo body)
    {
        WriteFunctionStatements(
            _ast.LinkageBodies.First(entry => ReferenceEquals(entry.Body, body)).Statements,
            hasReturnValue: true, hasOutputStruct: false, body.ReturnRegister);
    }

    /// <summary>
    /// Reduce every value the way writing it will, without writing it. The rewrite
    /// is in place and global, so the same pass at write time still finds what it
    /// finds today in the values no fold reaches until its own line is written,
    /// and nothing else to do. Only what writing reduces before it writes it: a
    /// value it groups first and reduces after has to reach that grouping as the
    /// instructions still are, or the statement is written under names no fold
    /// asked for.
    /// </summary>
    private void ReduceAll(IList<IStatement> statements)
    {
        foreach (IStatement statement in statements)
        {
            if (statement is AssignmentStatement or ReturnStatement)
            {
                foreach (HlslTreeNode root in ReducedTemps(statement))
                {
                    Reduce(root);
                }
            }
            foreach (HlslTreeNode held in HeldNodesToReduce(statement))
            {
                Reduce(held);
            }
            switch (statement)
            {
                case IfStatement ifStatement:
                    ReduceAll(ifStatement.TrueBody);
                    if (ifStatement.FalseBody != null)
                    {
                        ReduceAll(ifStatement.FalseBody);
                    }
                    break;
                case LoopStatement loop:
                    ReduceAll(loop.Body);
                    break;
                case SwitchStatement switchStatement:
                    foreach (SwitchCase switchCase in switchStatement.Cases)
                    {
                        ReduceAll(switchCase.Body);
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// The values a statement holds that writing it reduces before it writes them,
    /// which is everything it holds but for a loop's clauses. A loop prints those
    /// only where it prints as a `for`: an uncounted one prints its condition from
    /// the break still in its body, and a counted-by-register one its trip count
    /// from the constant the bytecode named, so reducing them here would fold
    /// values this writer never writes.
    /// </summary>
    private static IEnumerable<HlslTreeNode> HeldNodesToReduce(IStatement statement)
    {
        if (statement is not LoopStatement loop)
        {
            return statement.HeldNodes;
        }
        if (loop.IsCountedLoop)
        {
            return new HlslTreeNode[]
                { loop.Initializer, loop.ContinueCondition, loop.Increment }
                .Where(clause => clause != null);
        }
        return loop.RepeatCount == null && loop.RepeatCountNode != null
            ? [loop.RepeatCountNode]
            : [];
    }

    /// <summary>
    /// The values writing an assignment or a return reduces before it has decided
    /// anything else about them: the temp variables it assigns, and not the ones it
    /// merely carries forward. The output registers it names are the writer's own
    /// work - it groups the components while they are still instructions and
    /// reduces them only after - and folding one before that grouping would have
    /// it group the folded value instead of the instruction, and write the
    /// statement under names the fold never asked for.
    /// </summary>
    private IEnumerable<HlslTreeNode> ReducedTemps(IStatement statement)
    {
        foreach ((RegisterComponentKey key, HlslTreeNode value) in statement.Outputs)
        {
            if (key.RegisterKey.IsTempRegister
                && !(statement.Inputs.TryGetValue(key, out HlslTreeNode carried)
                    && ReferenceEquals(carried, value)))
            {
                yield return value;
            }
        }
    }

    /// <summary>
    /// Every variable some assignment declares, anywhere in the function. An
    /// assignment to a register that held a variable's value is marked as
    /// reassigning that variable - which is how a loop carries a value round - and
    /// the assignment that declared it can be inlined away afterwards, leaving a
    /// name nothing declares: `t5 = index < stride;` in a loop body, undeclared,
    /// because the load the register held before the loop went into the dot
    /// product that read it. A reassignment of a variable nothing declares is its
    /// declaration.
    /// </summary>
    private readonly HashSet<HlslTreeNode> _everDeclaredVariables = HlslTreeNode.NewNodeSet();

    private void FindDeclaredVariables(IList<IStatement> statements)
    {
        new StatementVisitor(statements).Visit(statement =>
        {
            IEnumerable<TempAssignmentNode> assignments = statement.Outputs.Values
                .OfType<TempAssignmentNode>();
            if (statement is LoopStatement { Initializer: not null } loop)
            {
                assignments = assignments.Append(loop.Initializer);
            }
            foreach (TempAssignmentNode assignment in assignments)
            {
                if (!assignment.IsReassignment)
                {
                    _everDeclaredVariables.Add(assignment.TempVariable);
                }
            }
        });
    }

    /// <summary>
    /// Whether a variable holds nothing but partial precision results, which is what
    /// lets it be declared half: `half3 t0 = saturate(x) * k;` says what
    /// `float3 t0 = (half3)(saturate(x) * k);` was saying twice over.
    ///
    /// Asked of every write and of every component, because there is one declaration
    /// for all of them. fxc writes a register at partial precision and at full
    /// precision as it pleases - a mov_sat filling one that a mul_pp then overwrites -
    /// and declaring that half would narrow the write that was not asking for it.
    /// </summary>
    private bool AssignsOnlyPartialPrecision(HlslTreeNode[] group)
    {
        if (group.Length == 0 || !group.All(IsPartialPrecisionAssignment))
        {
            return false;
        }
        List<TempVariableNode> variables =
            [.. group.Cast<TempAssignmentNode>().Select(assignment => assignment.TempVariable)];
        bool onlyPartialPrecision = true;
        new StatementVisitor(_functionStatements).Visit(statement =>
        {
            foreach (TempAssignmentNode assignment in statement.Outputs.Values.OfType<TempAssignmentNode>())
            {
                if (variables.Any(variable => ReferenceEquals(variable, assignment.TempVariable))
                    && !IsPartialPrecisionAssignment(assignment))
                {
                    onlyPartialPrecision = false;
                }
            }
        });
        return onlyPartialPrecision;
    }

    private static bool IsPartialPrecisionAssignment(HlslTreeNode node)
    {
        return node is TempAssignmentNode { Value: ConvertOperation { TargetType: "half" } };
    }

    // Both kinds of variable are asked the same question: the ones a register is
    // lowered into, and the ones the writer hoists a shared subexpression into. A
    // hoisted one is written exactly once, which the walk above finds nothing to
    // contradict.
    private void MarkPartialPrecision(HlslTreeNode[] group)
    {
        if (!AssignsOnlyPartialPrecision(group))
        {
            return;
        }
        foreach (TempAssignmentNode assignment in group.Cast<TempAssignmentNode>())
        {
            assignment.TempVariable.IsHalf = true;
        }
    }

    // Compiles an assignment group, declaring the variable where nothing else does.
    private string CompileAssignment(HlslTreeNode[] group)
    {
        MarkPartialPrecision(group);
        if (group[0] is TempAssignmentNode { IsReassignment: true }
            && group.All(node => node is TempAssignmentNode assignment
                && !_everDeclaredVariables.Contains(assignment.TempVariable)
                && !_declaredVariables.Contains(assignment.TempVariable)))
        {
            foreach (TempAssignmentNode assignment in group.Cast<TempAssignmentNode>())
            {
                assignment.IsReassignment = false;
                _everDeclaredVariables.Add(assignment.TempVariable);
            }
        }
        // Declared above the block it is assigned in, or under this name already,
        // so the assignment is a reassignment however the finalizer marked it.
        if (group[0] is TempAssignmentNode { IsReassignment: false } declaredAbove
            && (_declaredVariables.Contains(declaredAbove.TempVariable)
                || (declaredAbove.TempVariable.DeclarationIndex is int declaredIndex
                    && _declaredIndices.Contains(declaredIndex))))
        {
            foreach (TempAssignmentNode assignment in group.Cast<TempAssignmentNode>())
            {
                assignment.IsReassignment = true;
            }
        }
        // A variable assigned a part at a time - the xy of a register by one
        // instruction and the zw by another, and named as one - is declared once,
        // whole, and each part is then assigned by its mask. Compiled as two
        // declarations it was `int3 t0 = a; int3 t0 = b;`.
        // Not the calls that fill a whole variable through out parameters, which
        // are one assignment however wide.
        if (group[0] is TempAssignmentNode { IsReassignment: false } first
            && first.Value is not ConsumeNode and not ResourceInfoNode and not DoubleBitsNode
            && first.TempVariable.VariableSize is int size && group.Length < size)
        {
            string type = first.TempVariable.TypeName;
            // Compiling the variable is what numbers it.
            _compiler.Compile(group.Select(node => ((TempAssignmentNode)node).TempVariable));
            WriteLine($"{type}{size} {_registers.TemporaryPrefix}{first.TempVariable.DeclarationIndex};");
            _declaredIndices.Add(first.TempVariable.DeclarationIndex.Value);
            foreach (TempAssignmentNode assignment in group.Cast<TempAssignmentNode>())
            {
                assignment.IsReassignment = true;
                _declaredVariables.Add(assignment.TempVariable);
            }
            MarkPartsReassigned(first.TempVariable);
        }
        string compiled = _compiler.Compile(group);
        if (group[0] is TempAssignmentNode { IsReassignment: false, TempVariable.DeclarationIndex: int declared })
        {
            _declaredIndices.Add(declared);
        }
        return compiled;
    }

    // The other parts of a variable declared whole are reassignments now, whatever
    // statement they are in.
    private void MarkPartsReassigned(TempVariableNode declared)
    {
        new StatementVisitor(_functionStatements).Visit(statement =>
        {
            foreach (TempAssignmentNode assignment in statement.Outputs.Values.OfType<TempAssignmentNode>())
            {
                if (assignment.TempVariable.DeclarationIndex == declared.DeclarationIndex
                    && !ReferenceEquals(assignment.TempVariable, declared))
                {
                    assignment.IsReassignment = true;
                    _declaredVariables.Add(assignment.TempVariable);
                }
            }
        });
    }

    private void WriteStatements(IList<IStatement> statements)
    {
        foreach (IStatement statement in statements)
        {
            WriteStatement(statement);
        }
    }

    // The statement being written, for what it does to need to know where it is.
    private IStatement _currentStatement;

    private void WriteStatement(IStatement statement)
    {
        IStatement outerStatement = _currentStatement;
        _currentStatement = statement;
        try
        {
            WriteStatementOfKind(statement);
        }
        finally
        {
            _currentStatement = outerStatement;
        }
    }

    private void WriteStatementOfKind(IStatement statement)
    {
        if (statement is AssignmentStatement assignmentStatement)
        {
            WriteAssignmentStatement(assignmentStatement);
        }
        else if (statement is BufferAppendStatement bufferAppend)
        {
            WriteBufferAppendStatement(bufferAppend);
        }
        else if (statement is StoreTypedStatement storeTyped)
        {
            WriteStoreTypedStatement(storeTyped);
        }
        else if (statement is StoreStructuredStatement storeStructured)
        {
            WriteStoreStructuredStatement(storeStructured);
        }
        else if (statement is IndexableTempStoreStatement indexableTempStore)
        {
            WriteIndexableTempStoreStatement(indexableTempStore);
        }
        else if (statement is AtomicStatement atomic)
        {
            WriteAtomicStatement(atomic);
        }
        else if (statement is ClipStatement clip)
        {
            WriteClipStatement(clip);
        }
        else if (statement is AppendStatement append)
        {
            WriteLine($"{_registers.StreamParameterName(append.Stream)}"
                + $".Append({_registers.StreamVariableName(append.Stream)});");
        }
        else if (statement is RestartStripStatement restartStrip)
        {
            WriteLine($"{_registers.StreamParameterName(restartStrip.Stream)}.RestartStrip();");
        }
        else if (statement is SyncStatement sync)
        {
            WriteLine($"{sync.IntrinsicName}();");
        }
        else if (statement is LoopStatement loop)
        {
            WriteLoopStatement(loop);
        }
        else if (statement is BreakStatement breakStatement)
        {
            WriteBreakStatement(breakStatement);
        }
        else if (statement is DiscardStatement discardStatement)
        {
            WriteJumpStatement(discardStatement.Comparison, "discard");
        }
        else if (statement is ContinueStatement continueStatement)
        {
            WriteJumpStatement(continueStatement.Comparison, "continue");
        }
        else if (statement is IfStatement ifStatement)
        {
            WriteIfStatement(ifStatement);
        }
        else if (statement is SwitchStatement switchStatement)
        {
            WriteSwitchStatement(switchStatement);
        }
        else if (statement is ReturnStatement returnStatement)
        {
            if (_linkageReturnRegister == null)
            {
                WriteReturnStatement(returnStatement);
            }
            else
            {
                WriteLinkageReturn(returnStatement);
            }
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    // Registers the statement assigns, skipping the ones it merely carries forward.
    private void WriteStatementTempAssignments(IStatement statement)
    {
        foreach (var group in GetTempAssignmentGroups(statement))
        {
            WriteLine(CompileAssignment(group));
        }
    }

    private List<HlslTreeNode[]> GetTempAssignmentGroups(IStatement statement)
    {
        IDictionary<RegisterComponentKey, HlslTreeNode> tempComponents = statement.Outputs
                .Where(o => {
                    if (!o.Key.RegisterKey.IsTempRegister)
                    {
                        return false;
                    }
                    if (statement.Inputs.TryGetValue(o.Key, out var inputNode) && o.Value == inputNode)
                    {
                        return false;
                    }
                    // A variable the register merely holds - an interlocked
                    // operation's old value, written by the call - has nothing to assign.
                    if (o.Value is TempVariableNode)
                    {
                        return false;
                    }
                    return true;
                })
                .ToDictionary();
        return GroupAssignments(tempComponents);
    }

    private void WriteAssignmentStatement(AssignmentStatement assignmentStatement)
    {
        List<HlslTreeNode[]> tempGroups = GetTempAssignmentGroups(assignmentStatement);

        // With one output register there is no struct to write into: every return
        // compiles the live value, so writing `o.name = ...` first names something
        // that was never declared. A geometry shader is the exception - it writes the
        // struct and appends it rather than returning.
        if (_shader.Type != ShaderType.Geometry && _registers.MethodOutputRegisters.Count <= 1)
        {
            foreach (var group in tempGroups)
            {
                WriteLine(CompileAssignment(group));
            }
            return;
        }

        // Skip output registers the statement merely carries forward unchanged, the
        // same way temps are filtered above. Without this every statement re-emits
        // every output, which shows up as duplicated writes after a stream append.
        var writtenOutputs = assignmentStatement.Outputs
            .Where(o => o.Key.RegisterKey.IsOutput)
            .Where(o => !(assignmentStatement.Inputs.TryGetValue(o.Key, out var inputNode)
                && o.Value == inputNode))
            .ToList();
        Dictionary<RegisterComponentKey, HlslTreeNode[]> outputs =
            GroupComponents(writtenOutputs)
                .ToDictionary(r => r.Key, r => r.Value.Select(n => Reduce(n)).ToArray());
        Dictionary<RegisterComponentKey, int[]> outputComponents =
            GroupComponentMasks(writtenOutputs);

        // What the outputs share among themselves and with the temps is named
        // here, the way a return names it: a geometry shader writes its vertex
        // through these statements and never through a return, and the corner
        // offset a position and a texture coordinate both read was written out
        // at each.
        List<RegisterComponentKey> outputKeys = [.. outputs.Keys];
        List<HlslTreeNode[]> hoistRoots = [.. tempGroups, .. outputKeys.Select(key => outputs[key])];
        List<(HlslTreeNode[] Nodes, string Text)> hoisted = CompileSharedSubexpressions(hoistRoots);
        for (int i = 0; i < tempGroups.Count; i++)
        {
            tempGroups[i] = hoistRoots[i];
        }
        for (int i = 0; i < outputKeys.Count; i++)
        {
            outputs[outputKeys[i]] = hoistRoots[tempGroups.Count + i];
        }

        // An output that reads a temp's value as this statement computes it prints
        // after that temp's assignment; one that reads what the register held before
        // prints before the reassignment that overwrites it. Which of the two an
        // output is was recorded at lowering, since afterwards both are the same
        // variable name. Ordering temps and outputs together, rather than as two
        // hardcoded passes, is what lets TempAssignmentOrder see either dependency.
        // The named shared subexpressions join that sort too: one is often built
        // from a temp this very statement declares beside it - the trig pair and
        // the dot read out of it - and writing the shared one first would name a
        // variable the later line has not declared yet.
        var writes = new List<(HlslTreeNode[] Nodes, TempAssignmentNode[] Wants, Action Write)>();
        foreach (var (nodes, text) in hoisted)
        {
            writes.Add((nodes, [], () => WriteLine(text)));
        }
        foreach (var group in tempGroups)
        {
            writes.Add((group, [], () => WriteLine(CompileAssignment(group))));
        }
        foreach (var rootGroup in outputs.OrderBy(o => o.Key.RegisterKey.Number).ThenBy(o => o.Key.ComponentIndex))
        {
            RegisterDeclaration outputRegister = _registers.GetOutputDeclaration(rootGroup.Key);
            HlslTreeNode[] nodes = rootGroup.Value;
            TempAssignmentNode[] wants = [.. assignmentStatement.OutputDependsOnNewValueOf
                .Where(o => o.Key.RegisterKey.Equals(rootGroup.Key.RegisterKey))
                .SelectMany(o => o.Value)
                .Distinct()];
            string outputVariable = _registers.StreamVariableName(
                (rootGroup.Key.RegisterKey as D3D10RegisterKey)?.Stream);
            // A statement that writes part of an output says so: assigning a scalar
            // or a short vector to the whole member broadcasts over the components
            // this statement does not write, and the order it writes them in is not
            // the order of the register's components.
            string componentMask = ComponentMask(outputComponents[rootGroup.Key], outputRegister);
            writes.Add((nodes, wants, () => WriteLine($"{outputVariable}.{outputRegister.Name}{componentMask} = {CompileOutput(rootGroup.Key.RegisterKey, nodes)};")));
        }
        foreach (var write in TempAssignmentOrder.Sort(writes, w => w.Nodes, w => w.Wants))
        {
            write.Write();
        }
    }

    /// <summary>
    /// `buffer.Append(value);` - the slot the counter gave and the store into it,
    /// which is all an append buffer can be asked to do.
    /// </summary>
    private void WriteBufferAppendStatement(BufferAppendStatement append)
    {
        string destination = _registers.GetRegisterName(
            ((RegisterInputNode)append.Destination).RegisterComponentKey.RegisterKey);
        HlslTreeNode[] values = [.. append.Values.Select(Reduce)];
        WriteSharedSubexpressions([values]);
        // An append writes the whole element, so the element itself is what says
        // whether what goes in is an integer.
        RegisterKey appendKey = ((RegisterInputNode)append.Destination)
            .RegisterComponentKey.RegisterKey;
        string value = CompileStoredValue(
            values, _registers.IsIntegerStructuredMember(appendKey, 0));
        WriteLine($"{destination}.Append({value});");
    }

    /// <summary>
    /// `destination[coordinate] = value;` - a texel of a writable view, addressed
    /// by as many coordinates as the view has dimensions.
    /// </summary>
    private void WriteStoreTypedStatement(StoreTypedStatement storeTyped)
    {
        // The view is named without a swizzle: the subscript picks the texel, and a
        // component selection would belong after it rather than on the view.
        string destination = _registers.GetRegisterName(
            ((RegisterInputNode)storeTyped.Destination).RegisterComponentKey.RegisterKey);
        // The coordinate is an integer one, and a vector of them says so where a
        // constructor over the components would be typed by nothing.
        HlslTreeNode[] coordinates = [.. storeTyped.Coordinates.Select(Reduce)];
        HlslTreeNode[] values = [.. storeTyped.Values.Select(Reduce)];
        // As above: what a store alone reads is hoisted from here.
        WriteSharedSubexpressions([values, coordinates]);
        string coordinate = _compiler.CompileAsInteger(coordinates);
        RegisterKey typedKey = ((RegisterInputNode)storeTyped.Destination)
            .RegisterComponentKey.RegisterKey;
        string value = CompileStoredValue(
            values, _registers.GetTextureDefinition(typedKey)?.IsIntegerReturnType);
        WriteLine($"{destination}[{coordinate}] = {value};");
    }

    /// <summary>
    /// The value a store writes. Integers go in as themselves; a float goes in as
    /// the bits it is where the destination holds integers, because `buffer[i] = f`
    /// converts f to the integer nearest it and the shader stored asuint(f). A raw
    /// buffer's dwords say that outright, and a typed or structured view says it
    /// through the element type it was declared with - null where there is no
    /// declaration to ask, which leaves the value alone as before.
    /// </summary>
    private string CompileStoredValue(HlslTreeNode[] values, bool? destinationHoldsIntegers)
    {
        if (values.All(v => ValueTypes.IsIntegerValue(v) == true))
        {
            return _compiler.CompileAsInteger(values);
        }
        // Only where the graph says a value is a float. One it says nothing about is
        // left as it was: it is already being written as whatever it is, and asuint
        // around it would assert something the graph never said - a groupshared load
        // of an int array came out `asuint(g1[i])` and msad4, which answers uints
        // already, came out wrapped in it too.
        return destinationHoldsIntegers == true
            && values.Any(v => ValueTypes.IsIntegerValue(v) == false)
            ? CompileRawStoredValue(values)
            : _compiler.Compile(values);
    }

    // The dwords a raw store writes: each integer as it is, each float as its bits,
    // and a mix as a uint constructor of both.
    private string CompileRawStoredValue(HlslTreeNode[] values)
    {
        if (values.All(v => ValueTypes.IsIntegerValue(v) != true))
        {
            return $"asuint({_compiler.Compile(values)})";
        }
        List<string> dwords = [.. values.Select(v => ValueTypes.IsIntegerValue(v) == true
            ? _compiler.CompileAsInteger([v])
            : $"asuint({_compiler.Compile([v])})")];
        return $"uint{values.Length}({string.Join(", ", dwords)})";
    }

    private void WriteStoreStructuredStatement(StoreStructuredStatement storeStructured)
    {
        // The buffer is named without a swizzle - the subscript selects the element,
        // and any component selection belongs after it, not on the buffer.
        string compiledDestination = _registers.GetRegisterName(
            ((RegisterInputNode)storeStructured.Destination).RegisterComponentKey.RegisterKey);
        HlslTreeNode address = Reduce(storeStructured.Address);
        HlslTreeNode[] storedValues = [.. storeStructured.Values.Select(Reduce)];
        // A store computes its value from here rather than through
        // GroupAssignments, so the hoist has to happen here too - the way a return
        // statement's does. Without it a GetDimensions whose result a store alone
        // reads was never named, and the result of one cannot be an expression.
        WriteSharedSubexpressions([storedValues, [address]]);
        string compiledAddress = _compiler.Compile(address);
        // Into a buffer of integers as integers. A vector constructor is typed by
        // what it is being assigned to and there is nothing else here to say so.
        bool storesIntegers = storedValues.All(v => ValueTypes.IsIntegerValue(v) == true);
        string compiledValue = storesIntegers
            ? _compiler.CompileAsInteger(storedValues)
            : _compiler.Compile(storedValues);
        if (storeStructured.IsGroupShared)
        {
            // The element of the array raw groupshared memory is declared as. The
            // value is stored as the bits it is, the way a raw store always does.
            if (!storesIntegers)
            {
                compiledValue = CompileRawStoredValue(storedValues);
            }
            WriteLine($"{compiledDestination}[{compiledAddress}] = {compiledValue};");
            return;
        }
        if (storeStructured.IsRaw)
        {
            // Store, Store2, Store3 or Store4 at the byte offset, by how many dwords
            // are written. The dwords are uints, and a float among them goes in as
            // its bits: Store2(offset, float2(f, n)) converts f to the integer
            // nearest it, where the shader stored asuint(f).
            string method = storeStructured.Values.Length == 1 ? "Store" : $"Store{storeStructured.Values.Length}";
            if (!storesIntegers)
            {
                compiledValue = CompileRawStoredValue(storedValues);
            }
            WriteLine($"{compiledDestination}.{method}({compiledAddress}, {compiledValue});");
            return;
        }
        // A struct element is written a member at a time: one store of sixteen
        // bytes over a struct of a float3 and a float is both of them, and writing
        // it as one assignment kept only the last.
        RegisterKey bufferKey = ((RegisterInputNode)storeStructured.Destination).RegisterComponentKey.RegisterKey;
        // The element the store reaches says whether a float goes in as its bits,
        // the same question the raw store above answers from the buffer itself.
        compiledValue = CompileStoredValue(storedValues,
            _registers.IsIntegerStructuredMember(bufferKey, storeStructured.ElementByteOffset));
        IList<(string Name, int[] Values)> runs = _registers.FindStructuredMemberRuns(
            bufferKey, $"{compiledDestination}[{compiledAddress}]",
            storeStructured.ElementByteOffset, storeStructured.Components);
        if (runs != null)
        {
            foreach ((string name, int[] values) in runs)
            {
                string run = storesIntegers
                    ? _compiler.CompileAsInteger(values.Select(v => storedValues[v]))
                    : _compiler.Compile(values.Select(v => storedValues[v]));
                WriteLine($"{name} = {run};");
            }
            return;
        }
        // Groupshared memory wider than one register is a struct of them, and the
        // byte offset picks which. Written as the element itself, a store of one
        // member assigned to the whole of it - and a store of the second member
        // overwrote the first.
        string sharedMember = ThreadGroupSharedMemberName(bufferKey,
            storeStructured.ElementByteOffset);
        WriteLine($"{compiledDestination}[{compiledAddress}]{sharedMember} = {compiledValue};");
    }

    /// <summary>
    /// The `.mN` a store into groupshared memory writes, or nothing where the element
    /// fits in one register and is the whole of what a store reaches.
    /// </summary>
    private string ThreadGroupSharedMemberName(RegisterKey bufferKey, int byteOffset)
    {
        if (bufferKey is not D3D10RegisterKey { OperandType: OperandType.ThreadGroupSharedMemory }
            || !_registers.ThreadGroupSharedMemory.TryGetValue(bufferKey.Number,
                out (int Stride, int Elements) shared)
            || shared.Stride <= 16)
        {
            return "";
        }
        return RegisterState.ThreadGroupSharedMemberAt(shared.Stride, byteOffset)
            is var (name, _, _)
            ? $".{name}"
            : "";
    }

    private void WriteAtomicStatement(AtomicStatement atomic)
    {
        RegisterKey resourceKey =
            ((RegisterInputNode)atomic.Destination).RegisterComponentKey.RegisterKey;
        string resource = _registers.GetRegisterName(resourceKey);
        // A typed texture is addressed by a coordinate, and a vector of integers says
        // so where a constructor over the components would be typed by nothing - the
        // same as the store into it.
        HlslTreeNode[] coordinates = atomic.Coordinates == null
            ? null
            : [.. atomic.Coordinates.Select(Reduce)];
        HlslTreeNode addressNode = coordinates != null ? null : Reduce(atomic.Address);
        HlslTreeNode valueNode = Reduce(atomic.Value);
        HlslTreeNode compareNode = atomic.Compare == null ? null : Reduce(atomic.Compare);
        // The hoist a store needs, for the same reason it needs it: an atomic
        // computes what it works on from here rather than through GroupAssignments,
        // so a call whose result nothing but this statement reads is named nowhere
        // else. A Consume is one of those - its result comes back through a method
        // and cannot be an expression - and an atomic over a consumed element
        // stopped the writer outright, the name never having been given.
        List<HlslTreeNode[]> held = [[valueNode]];
        if (compareNode != null)
        {
            held.Add([compareNode]);
        }
        held.Add(coordinates ?? [addressNode]);
        WriteSharedSubexpressions(held);
        string address = coordinates != null
            ? _compiler.CompileAsInteger(coordinates)
            : _compiler.Compile(addressNode);
        // An interlocked operation works on integers, so a float reaching it is the
        // bits it holds rather than the number they make - as unsigned where the
        // destination is, which for groupshared memory is what the unsigned minimum
        // and maximum over it made the declaration say.
        bool unsigned = resourceKey is D3D10RegisterKey
            {
                OperandType: OperandType.ThreadGroupSharedMemory
            }
            && CreateIntegerOperandAnalysis()
                .IsUnsignedThreadGroupSharedMemory(resourceKey.Number);
        string value = _compiler.CompileIntegerArgument(valueNode, unsigned);
        string compare = compareNode == null
            ? null
            : _compiler.CompileIntegerArgument(compareNode, unsigned);

        // The variable the old value goes into has to exist before the call takes
        // its address, and compiling it is what numbers it. Declared here rather
        // than by the block rule above, which declares what a block assigns and
        // this is not an assignment.
        string original = null;
        if (atomic.Original != null)
        {
            original = _compiler.Compile(atomic.Original);
            if (_declaredVariables.Add(atomic.Original))
            {
                string type = atomic.Original.TypeName;
                WriteLine($"{type} {original};");
            }
        }
        string arguments = compare == null ? value : $"{compare}, {value}";
        if (original != null)
        {
            arguments += $", {original}";
        }

        // A byte address buffer has the interlocked operations as methods on itself,
        // taking the byte offset. Everything else - a structured buffer, a typed
        // UAV, groupshared memory - has them as free functions over the destination,
        // which is an element or a texel rather than the resource. Asked of the
        // resource rather than of what the address operand carries, because a typed
        // texture carries no byte offset either.
        if (_registers.IsRawResource(resourceKey))
        {
            WriteLine($"{resource}.{atomic.MethodName}({address}, {arguments});");
            return;
        }

        string element = $"{resource}[{address}]";
        IList<(string Name, int[] Values)> runs = _registers.FindStructuredMemberRuns(
            resourceKey, element, GetElementByteOffset(atomic), [0]);
        if (runs != null && runs.Count == 1)
        {
            element = runs[0].Name;
        }
        WriteLine($"{atomic.MethodName}({element}, {arguments});");
    }

    private static int GetElementByteOffset(AtomicStatement atomic)
    {
        // Through the moves: fxc works the offset out into a register - one mov
        // writes the offsets of every member the shader touches - so it arrives as
        // a move of the constant rather than as the constant. Taken as nothing, the
        // atomic named whichever member sits at the top of the element, and every
        // one of them in an element came out as that one.
        HlslTreeNode offset = atomic.ElementByteOffset;
        while (offset is MoveOperation move)
        {
            offset = move.Inputs[0];
        }
        return offset is ConstantNode constant
            ? constant.IntegerValue ?? (int)constant.Value
            : 0;
    }

    private void WriteIndexableTempStoreStatement(IndexableTempStoreStatement store)
    {
        string index = _compiler.CompileIndexableTempIndex(Reduce(store.Index));
        // The write mask is dropped when the whole element is written, the way a
        // register's is.
        int components = _registers.IndexableTemps[store.Register].Components;
        string mask = store.Components.Length == components
            ? ""
            : "." + string.Concat(store.Components.Select(c => "xyzw"[c]));
        string value = _compiler.Compile(store.Values.Select(Reduce).ToList(), store.Values.Length);
        WriteLine($"x{store.Register}[{index}]{mask} = {value};");
    }

    private void WriteClipStatement( ClipStatement clip)
    {
        string compiled = _compiler.Compile(clip.Values.Select(Reduce));
        WriteLine($"clip({compiled});");
    }

    private void WriteLoopStatement(LoopStatement loop)
    {
        if (WritesFlowAttributes || NeedsLoopAttribute(loop))
        {
            WriteLine("[loop]");
        }
        string loopVariableName = null;
        IList<IStatement> body = loop.Body;
        if (loop.IsCountedLoop)
        {
            // The initializer and increment compile as statements; the for header
            // wants them as clauses. Through CompileAssignment, so that the header
            // declares the variable only where nothing above the loop does: a switch
            // carries a counter register out of a case, the block declared it there,
            // and `for (uint t3 = 0; ...)` under that declaration is a second
            // variable of the same name - fxc warns X3078 and reads the outer one
            // afterwards, which the loop never assigned.
            string initializer = CompileAssignment([Reduce(loop.Initializer)]).TrimEnd(';');
            string condition = _compiler.Compile(Reduce(loop.ContinueCondition));
            string increment = _compiler.Compile(Reduce(loop.Increment)).TrimEnd(';');
            WriteLine($"for ({initializer}; {condition}; {increment}) {{");
        }
        else if (loop.RepeatCount is uint || loop.RepeatCountNode != null)
        {
            string variableName = GetLoopVariableName(_loopDepth);
            string count = loop.RepeatCount is uint repeatCount
                ? repeatCount.ToString()
                : _compiler.Compile(Reduce(loop.RepeatCountNode));
            WriteLine($"for (int {variableName} = 0; {variableName} < {count}; {variableName}++) {{");
            // In the `loop aL, iN` form, aL in the body is this variable.
            if (loop.HasLoopCounter)
            {
                loopVariableName = variableName;
            }
        }
        else if (TryGetExitTest(loop, out HlslTreeNode continueCondition, out IStatement exitTest))
        {
            // The bytecode tests and breaks at the top of the body, and written
            // that way fxc compiles it as if_nz, break, endif where the original
            // had one breakc_nz. As the loop's own condition it is that one
            // instruction again, and the loop reads as the loop it is.
            string condition = _compiler.Compile(Reduce(continueCondition));
            WriteLine($"while ({condition}) {{");
            body = [.. loop.Body.Where(statement => !ReferenceEquals(statement, exitTest))];
        }
        else
        {
            WriteLine("while (true) {");
        }
        indent += "\t";
        _loopDepth++;
        string enclosingLoopVariable = _compiler.LoopVariableName;
        if (loopVariableName != null)
        {
            _compiler.LoopVariableName = loopVariableName;
        }
        WriteStatements(body);
        _compiler.LoopVariableName = enclosingLoopVariable;
        _loopDepth--;
        indent = indent.Substring(0, indent.Length - 1);
        WriteLine("}");
    }

    /// <summary>
    /// The condition a loop whose body opens by breaking on a test can carry in its
    /// header instead, and the statement it came from. The phi assignments that
    /// carry values round the loop write nothing and are stepped over; anything
    /// else in front of the break is code that has to run before the test, and the
    /// loop stays as it is.
    /// </summary>
    private static bool TryGetExitTest(
        LoopStatement loop, out HlslTreeNode continueCondition, out IStatement exitTest)
    {
        continueCondition = null;
        exitTest = null;
        foreach (IStatement statement in loop.Body)
        {
            // The phi assignments that carry values round the loop write nothing,
            // and neither does a statement whose every value is read inline by
            // whatever comes after - the comparison an ilt computes for the
            // breakc that follows it. Both are stepped over.
            if (statement is AssignmentStatement assignment
                && assignment.Outputs.All(output =>
                    output.Value is PhiNode
                    || (assignment.Inputs.TryGetValue(output.Key, out HlslTreeNode input) && ReferenceEquals(input, output.Value))
                    || (output.Key.RegisterKey.IsTempRegister && output.Value is not TempAssignmentNode)))
            {
                continue;
            }
            if (statement is not BreakStatement breakStatement
                || breakStatement.Comparison is not ComparisonNode comparison
                || ConstantMatcher.TryEvaluateComparison(comparison) != null
                || comparison.Inverted() is not ComparisonNode opposite)
            {
                return false;
            }
            continueCondition = opposite;
            exitTest = statement;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Whether the loop has to be marked [loop] for fxc to compile it at all. A
    /// sample with implicit derivatives inside a loop fxc cannot count is an error
    /// unless the loop is marked, so the source it came from was marked; anything
    /// else is left to fxc, which unrolls what it can count and loops the rest,
    /// and the attribute stays out of every loop that does not need it.
    /// </summary>
    private static bool NeedsLoopAttribute(LoopStatement loop)
    {
        if (loop.RepeatCount is uint)
        {
            return false;
        }
        if (loop.IsCountedLoop
            && loop.ContinueCondition is ComparisonNode comparison
            && (comparison.Left is ConstantNode || comparison.Right is ConstantNode))
        {
            return false;
        }
        var roots = new List<HlslTreeNode>();
        new StatementVisitor(loop.Body).Visit(statement =>
        {
            roots.AddRange(statement.Outputs.Values);
            roots.AddRange(statement.HeldNodes);
        });
        return Reachable(roots.Where(r => r != null)).Any(IsGradientOperation);
    }

    // A sample that takes its mip level from the derivatives of its coordinates,
    // or a derivative outright.
    private static bool IsGradientOperation(HlslTreeNode node)
    {
        return node is PartialDerivativeXOperation or PartialDerivativeYOperation
            || (node is TextureLoadOutputNode sample
                && (sample.Controls & (TextureLoadControls.Lod | TextureLoadControls.Grad
                    | TextureLoadControls.LevelZero | TextureLoadControls.Gather)) == 0);
    }

    // Nested loops must not shadow the enclosing loop's counter, and the first one
    // must not shadow the input struct, which is also called i. The counter is what
    // moves: renaming the struct would change every shader that reads it.
    private string GetLoopVariableName(int depth)
    {
        // Only where those names are in scope: with a single input register the
        // parameter is named after its semantic and there is no struct called i.
        bool inputInScope = _registers.MethodInputRegisters.Count > 1
            || _shader.Type == ShaderType.Geometry;
        bool outputInScope = HasOutputStruct;

        string name = depth < 3 ? new string((char)('i' + depth), 1) : $"i{depth}";
        while ((inputInScope && name == _registers.InputVariableName)
            || (outputInScope && name == _registers.OutputVariableName))
        {
            name += "_";
        }
        return name;
    }

    private void WriteSwitchStatement(SwitchStatement switchStatement)
    {
        WriteBlockTempVariables(switchStatement.Outputs, switchStatement.Inputs);

        string selector = _compiler.Compile(Reduce(switchStatement.Selector));
        WriteLine($"switch ({selector}) {{");
        indent += "	";
        foreach (SwitchCase switchCase in switchStatement.Cases)
        {
            WriteLine(switchCase.IsDefault
                ? "default:"
                : $"case {_compiler.Compile(Reduce(switchCase.Label))}:");
            indent += "	";
            WriteStatements(switchCase.Body);
            indent = indent.Substring(0, indent.Length - 1);
        }
        indent = indent.Substring(0, indent.Length - 1);
        WriteLine("}");
    }

    private void WriteBreakStatement(BreakStatement breakStatement)
    {
        WriteJumpStatement(breakStatement.Comparison, "break");
    }

    /// <summary>
    /// Writes a <c>break</c> or <c>continue</c>, guarded by its condition unless that
    /// condition is absent or always true.
    /// </summary>
    private void WriteJumpStatement(HlslTreeNode comparisonNode, string keyword)
    {
        if (comparisonNode == null)
        {
            WriteLine($"{keyword};");
            return;
        }

        bool? constantComparison = ConstantMatcher.TryEvaluateComparison(comparisonNode);
        if (constantComparison.HasValue && constantComparison.Value)
        {
            WriteLine($"{keyword};");
        }
        else
        {
            string comparison = _compiler.Compile(Reduce(comparisonNode));
            WriteLine($"if ({comparison}) {{");
            indent += "\t";
            WriteLine($"{keyword};");
            indent = indent.Substring(0, indent.Length - 1);
            WriteLine("}");
        }
    }

    private void WriteIfStatement(IfStatement ifStatement)
    {
        WriteIfStatementTempVariables(ifStatement);

        HlslTreeNode[] comparison = ifStatement.Comparison;
        IList<IStatement> trueBody = ifStatement.TrueBody;
        IList<IStatement> falseBody = ifStatement.FalseBody;
        // An if whose if side is empty and whose else side is the whole body -
        // `if_lt` over a condition fxc wrote the other way round - says the same
        // thing inverted, with one branch instead of two and nothing in it.
        if (trueBody.Count == 0 && falseBody != null && Inverted(comparison) is HlslTreeNode[] opposite)
        {
            comparison = opposite;
            trueBody = falseBody;
            falseBody = null;
        }

        // A D3D9 `if_<rel>` hands one comparison per component, but its operand is
        // often a single component read through a swizzle - `if_ne r0.z, -r0.z`
        // tests only .z, and all four components carry the one test. HLSL wants a
        // scalar condition, and four copies compiled side by side is a vector; one
        // of them says the same thing. A partial-precision operand makes this worse
        // than an ugly condition: the cast the half-ness puts on each component is
        // sized to the group, so four components come out `(half4)`, and comparing
        // those is X3019 rather than merely wide.
        comparison = [.. comparison.Select(c => ReadAssignedFlag(ifStatement, c))];
        var tested = comparison.Select(Reduce).ToList();
        if (tested.Count > 1
            && tested.All(c => NodeGrouper.AreNodesEquivalent(c, tested[0])))
        {
            tested = [tested[0]];
        }
        if (WritesFlowAttributes)
        {
            WriteLine("[branch]");
        }
        WriteLine($"if ({_compiler.Compile(tested)}) {{");
        indent += "\t";
        WriteStatements(trueBody);
        indent = indent.Substring(0, indent.Length - 1);
        if (falseBody != null)
        {
            WriteLine("} else {");
            indent += "\t";
            WriteStatements(falseBody);
            indent = indent.Substring(0, indent.Length - 1);
        }
        WriteLine("}");
    }

    /// <summary>
    /// A test of a flag the statement before has just put in a variable, as a test of
    /// the variable. A predicate is written out again rather than named (see
    /// IStatement.NamedHeldNodes): named for the if alone, a mask is converted to
    /// the 0 or 1 an int holds on the way in, which is an instruction the shader
    /// did not have - and so here too where nothing reads the variable after the
    /// if: tile_luminance and groupshared_scan assign theirs only for fxc to drop
    /// the assignment, and read, it costs one. But where the variable is there
    /// anyway - decal_blend keeps `decalCount <= 1` for a later test - testing the
    /// comparison again is the extra instruction: `t1 = decalCount <= 1;
    /// if (decalCount > 1)` where the shader read the register it had just
    /// written. Only the statement right before the if, so that nothing can have
    /// assigned the variable in between.
    /// </summary>
    private HlslTreeNode ReadAssignedFlag(IfStatement ifStatement, HlslTreeNode test)
    {
        if (test is not ComparisonNode comparison
            || FindPlace(_functionStatements, ifStatement) is not (IList<IStatement> body, int index)
            || index == 0)
        {
            return test;
        }
        HlslTreeNode opposite = comparison.Inverted();
        foreach (TempAssignmentNode assignment in body[index - 1].Outputs.Values.OfType<TempAssignmentNode>())
        {
            if (assignment.Value is not ComparisonNode flag)
            {
                continue;
            }
            _liveness ??= VariableLiveness.Analyze(_functionStatements);
            if (!_liveness.MayBeLiveAfter(assignment.TempVariable, ifStatement))
            {
                continue;
            }
            bool same = IsSameComparison(flag, comparison);
            if (same || (opposite is ComparisonNode inverse && IsSameComparison(flag, inverse)))
            {
                return new ComparisonNode(assignment.TempVariable, new ConstantNode(0),
                    same ? IfComparison.NE : IfComparison.EQ, isInteger: true);
            }
        }
        return test;
    }

    // Either way round: decal_blend assigns `decalCount <= 1` and tests `1 < decalCount`,
    // whose opposite is `1 >= decalCount`.
    private static bool IsSameComparison(ComparisonNode a, ComparisonNode b)
    {
        if (a.IsInteger != b.IsInteger || a.IsUnsigned != b.IsUnsigned)
        {
            return false;
        }
        if (a.Comparison == b.Comparison
            && NodeGrouper.AreNodesEquivalent(a.Inputs[0], b.Inputs[0])
            && NodeGrouper.AreNodesEquivalent(a.Inputs[1], b.Inputs[1]))
        {
            return true;
        }
        IfComparison mirrored = b.Comparison switch
        {
            IfComparison.LT => IfComparison.GT,
            IfComparison.GT => IfComparison.LT,
            IfComparison.LE => IfComparison.GE,
            IfComparison.GE => IfComparison.LE,
            _ => b.Comparison,
        };
        return a.Comparison == mirrored
            && NodeGrouper.AreNodesEquivalent(a.Inputs[0], b.Inputs[1])
            && NodeGrouper.AreNodesEquivalent(a.Inputs[1], b.Inputs[0]);
    }

    /// <summary>
    /// The comparison with the opposite outcome, component for component, or null
    /// where any of them has none - a test of something that is not a comparison at
    /// all, or one whose inverse the enum cannot name.
    /// </summary>
    private static HlslTreeNode[] Inverted(HlslTreeNode[] comparison)
    {
        var inverted = new HlslTreeNode[comparison.Length];
        for (int i = 0; i < comparison.Length; i++)
        {
            if (comparison[i] is not ComparisonNode node || node.Inverted() is not ComparisonNode opposite)
            {
                return null;
            }
            inverted[i] = opposite;
        }
        return inverted;
    }

    private void WriteIfStatementTempVariables(IfStatement ifStatement)
    {
        WriteBlockTempVariables(ifStatement.Outputs, ifStatement.Inputs);
    }

    /// <summary>
    /// Declares the variables a block assigns, above the block. A variable declared
    /// inside a branch or a case would go out of scope at its closing brace.
    /// </summary>
    private void WriteBlockTempVariables(
        IDictionary<RegisterComponentKey, HlslTreeNode> outputs,
        IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
    {
        // Not the variables the block was handed - but a register the block was
        // handed holding something else, and assigns a fresh variable to, is the
        // block's to declare: a mask register reused for a flag inside an if/else
        // had the flag declared inside one branch and read after the join.
        var newAssignments = outputs
            .Where(o => o.Value is TempVariableNode)
            .Where(o => !(inputs.TryGetValue(o.Key, out HlslTreeNode input) && HandsVariable(input, o.Value)))
            .ToDictionary();
        if (newAssignments.Count > 0)
        {
            foreach (var group in GroupAssignments(newAssignments))
            {
                // Compile variable with all components
                _compiler.Compile(group);

                var variable = group.First() as TempVariableNode;
                // An enclosing block may already declare it, when a nested if merged
                // into the same variable. Declaring it again would shadow it. By
                // number as well as by node: the xy of a register assigned before
                // the block and the xyz the block assigns are two sets of nodes
                // under one name, and the block redeclared it.
                if (!_declaredVariables.Add(variable) || !_declaredIndices.Add(variable.DeclarationIndex.Value))
                {
                    continue;
                }
                string size = variable.VariableSize != 1 ? variable.VariableSize.ToString() : "";
                string type = variable.TypeName;
                WriteLine($"{type}{size} {_registers.TemporaryPrefix}{variable.DeclarationIndex};");
            }
        }
    }

    // Whether the value a block is handed for a register is the variable the
    // block assigns: the variable itself, its assignment, or a join that merges it
    // - the if before this one assigning the same register hands a phi over it.
    private static bool HandsVariable(HlslTreeNode input, HlslTreeNode variable)
    {
        return ReferenceEquals(input, variable)
            || (input is TempAssignmentNode assignment && ReferenceEquals(assignment.TempVariable, variable))
            || (input is PhiNode phi && phi.Inputs.Any(i => HandsVariable(i, variable)));
    }

    /// <summary>
    /// A linkage method's return: the one register the body writes, taken from
    /// what the return statement holds live for it - the expression itself where
    /// the body computed it right here, the variable where one carried it. A
    /// method has no output struct and no semantic: what it computes is what it
    /// hands back.
    /// </summary>
    private void WriteLinkageReturn(ReturnStatement returnStatement)
    {
        // The variables this statement assigns are written first - except the
        // result's own register, which the return itself is about to name, and
        // which is often still an expression rather than a variable.
        var temps = returnStatement.Outputs
            .Where(o => o.Key.RegisterKey.IsTempRegister
                && !o.Key.RegisterKey.Equals(_linkageReturnRegister))
            .Where(o => !(returnStatement.Inputs.TryGetValue(o.Key, out var carried)
                && ReferenceEquals(carried, o.Value)))
            .ToDictionary();
        foreach (var group in GroupAssignments(temps))
        {
            WriteLine(CompileAssignment(group));
        }

        HlslTreeNode[] result = [.. Enumerable.Range(0, 4).Select(component =>
            ResultOf(returnStatement, new RegisterComponentKey(_linkageReturnRegister, component)))];
        List<HlslTreeNode[]> roots = [result];
        WriteSharedSubexpressions(roots);
        string condition = returnStatement.Comparison == null
            ? null
            : _compiler.Compile(Reduce(returnStatement.Comparison));
        string compiled = _compiler.CompileAsFloat(roots[0]);
        WriteLine(condition == null
            ? $"return {compiled};"
            : $"if ({condition}) return {compiled};");
    }

    // The value the method hands back: a variable's assignment returns as the
    // variable, whose line the statements above wrote; an expression is the
    // value itself, and the return is where it is written.
    private HlslTreeNode ResultOf(IStatement statement, RegisterComponentKey key)
    {
        if (!statement.Outputs.TryGetValue(key, out HlslTreeNode value))
        {
            throw new NotImplementedException(
                "a linkage body that returns a register it does not write");
        }
        return Reduce(value is TempAssignmentNode assignment
            ? assignment.TempVariable
            : value);
    }

    private void WriteReturnStatement(ReturnStatement returnStatement)
    {
        // A return can replace an assignment statement, and the returned expression
        // reads what that statement assigned.
        WriteStatementTempAssignments(returnStatement);

        // With an output struct, a return writes only what changed since the struct
        // was last written: an if whose both branches return would otherwise repeat
        // the position computed above it in each of them. A single output has no
        // struct and is returned as the expression, wherever it was computed.
        bool hasOutputStruct = HasOutputStruct;
        var returnedOutputs = returnStatement.Outputs
            .Where(o => o.Key.RegisterKey.IsOutput)
            .Where(o => !(hasOutputStruct
                && returnStatement.Inputs.TryGetValue(o.Key, out var inputNode)
                && o.Value == inputNode))
            .ToList();
        Dictionary<RegisterComponentKey, HlslTreeNode[]> outputs =
            GroupComponents(returnedOutputs)
                .ToDictionary(r => r.Key, r => r.Value.Select(n => Reduce(n)).ToArray());
        Dictionary<RegisterComponentKey, int[]> outputComponents =
            GroupComponentMasks(returnedOutputs);

        // The returned expression is compiled straight from here rather than through
        // GroupAssignments, so the hoist has to happen here too - and what it names
        // has to come back, since a whole output's value may now be a variable and
        // the dictionary holds the group it was written from.
        List<RegisterComponentKey> outputKeys = [.. outputs.Keys];
        List<HlslTreeNode[]> outputGroups = [.. outputKeys.Select(key => outputs[key])];
        WriteSharedSubexpressions(outputGroups);
        for (int i = 0; i < outputKeys.Count; i++)
        {
            outputs[outputKeys[i]] = outputGroups[i];
        }

        string condition = returnStatement.Comparison == null
            ? null
            : _compiler.Compile(Reduce(returnStatement.Comparison));

        // A compute or geometry shader returns nothing, so an early ret leaves with
        // no value at all. Asking for the one output there threw, and a `return;`
        // guarding the rest of a compute shader is how every one of them starts.
        // A geometry shader has outputs - the vertices it appends - and returns
        // none of them: `return o;` from it is X3079.
        if (_registers.MethodOutputRegisters.Count == 0 || _shader.Type == ShaderType.Geometry)
        {
            WriteLine(condition == null ? "return;" : $"if ({condition}) return;");
        }
        else if (!hasOutputStruct)
        {
            var single = outputs.Single();
            string compiled = CompileOutput(single.Key.RegisterKey, single.Value);
            WriteLine(condition == null
                ? $"return {compiled};"
                : $"if ({condition}) return {compiled};");
        }
        else if (condition != null)
        {
            // The outputs were written by the statements before this one; a
            // conditional return only chooses whether to leave with them.
            WriteLine($"if ({condition}) return {_registers.OutputVariableName};");
        }
        else
        {
            foreach (var rootGroup in TempAssignmentOrder.Sort(
                outputs.OrderBy(o => o.Key.RegisterKey.Number).ThenBy(o => o.Key.ComponentIndex),
                o => o.Value))
            {
                RegisterDeclaration outputRegister = _registers.GetOutputDeclaration(rootGroup.Key);
                string componentMask = ComponentMask(outputComponents[rootGroup.Key], outputRegister);
                string compiled = CompileOutput(rootGroup.Key.RegisterKey, rootGroup.Value);
                WriteLine($"{_registers.OutputVariableName}.{outputRegister.Name}{componentMask} = {compiled};");
            }
            // A blank line whether or not anything was written just above: the return
            // reads as the end of the method either way, and after the closing brace
            // of an if and an else that filled the struct between them it needs the
            // separation more, not less.
            WriteLine();
            WriteLine($"return {_registers.OutputVariableName};");
        }
    }

    // An output the signature types as a float takes a float, so bits reaching one
    // are reinterpreted. One it types as an integer takes the integer as it is - and
    // as an integer, so that a whole register returned at once builds an intN and not
    // a floatN: the int-to-float-to-int the float constructor does each way is a
    // lossy round trip, not the identity it looks like once the value leaves the
    // range a float holds exactly.
    private string CompileOutput(RegisterKey outputKey, IEnumerable<HlslTreeNode> nodes)
    {
        RegisterDeclaration declaration = _registers.RegisterDeclarations[outputKey];
        // An output the signature declares at partial precision is a half already, so a
        // half cast on the way into it says the same thing twice - and fxc reads the
        // declaration rather than the cast, which is what makes the cast alone worth
        // nothing.
        if (declaration.ResultModifier.HasFlag(ResultModifier.PartialPrecision))
        {
            nodes = nodes.Select(node =>
                node is ConvertOperation { TargetType: "half" } cast ? cast.Value : node);
        }
        return declaration.IsInteger
            ? _compiler.CompileAsInteger(nodes)
            : _compiler.CompileAsFloat(nodes);
    }

    private void WriteSharedSubexpressions(IList<HlslTreeNode[]> roots)
    {
        foreach ((_, string text) in CompileSharedSubexpressions(roots))
        {
            WriteLine(text);
        }
    }

    /// <summary>
    /// Names the subexpressions the roots share and compiles each named one into
    /// its own line of HLSL. The caller that goes on to order more lines - an
    /// assignment statement writes its temps and outputs through one sort - keeps
    /// the nodes so that sort can see a line that reads a variable one of the
    /// other lines declares; a caller that writes everything it has straight away
    /// just wants the text.
    /// </summary>
    private List<(HlslTreeNode[] Nodes, string Text)> CompileSharedSubexpressions(
        IList<HlslTreeNode[]> roots)
    {
        var compiled = new List<(HlslTreeNode[] Nodes, string Text)>();
        List<HlslTreeNode[]> assignments = TempAssignmentOrder.Sort(
            HoistSharedSubexpressions(roots));
        foreach (HlslTreeNode[] assignment in assignments)
        {
            MarkPartialPrecision(assignment);
            compiled.Add((assignment, _compiler.Compile(assignment)));
        }
        return compiled;
    }

    /// <summary>
    /// The swizzle naming the components an output write touches, when it does not
    /// touch all of them. The member is as wide as the register is written across
    /// the whole shader - the write masks have all been seen by the time this
    /// writes - and an unmasked assignment to it takes the value's width as the
    /// write's, broadcasting it over the components this statement leaves alone and
    /// packing the ones it wrote into the register's first slots.
    /// </summary>
    private static string ComponentMask(int[] components, RegisterDeclaration declaration)
    {
        if (components.Length >= declaration.MaskedLength)
        {
            return "";
        }
        return "." + string.Concat(components.Select(c => "xyzw"[c]));
    }

    private HlslTreeNode Reduce(HlslTreeNode node)
    {
        node = _templateMatcher.Reduce(node);
        NodeFinalizer.Finalize([node]);
        return node;
    }

    private static bool IsPhiRead(HlslTreeNode value)
    {
        return value is TempAssignmentNode assignment
            && assignment.TempVariable.Outputs.Any(reader => reader is PhiNode);
    }

    private List<HlslTreeNode[]> GroupAssignments(IDictionary<RegisterComponentKey, HlslTreeNode> outputs)
    {
        var nodeGrouper = new NodeGrouper(_registers);

        var groups = new List<HlslTreeNode[]>();
        // By register, except that a component whose variable a phi goes on to
        // read - a loop counter or accumulator being given its starting value - is
        // kept apart from the register's other components. Its variable is shared
        // with every reassignment of it, and grouping it into a float3 with two
        // loop invariants that happen to sit beside it makes the counter t0.z for
        // the rest of the shader, which no loop is recovered from.
        var registerGroups = outputs
            .Where(o => o.Key.RegisterKey.IsTempRegister || o.Key.RegisterKey.IsOutput)
            .OrderBy(o => o.Key.ComponentIndex)
            .GroupBy(o => (o.Key.RegisterKey, IsPhiRead(o.Value)))
            .Select(o => o.Select(c => Reduce(c.Value)).ToArray())
            .ToList();
        registerGroups = TempAssignmentOrder.Sort(registerGroups);

        WidenWithSiblingRows(registerGroups);

        // After reducing, not before: naming a subexpression hides it from the
        // templates, and a node feeding four components would be named rather than
        // broadcast.
        groups.AddRange(HoistSharedSubexpressions(registerGroups));

        foreach (var registerGroup in registerGroups)
        {
            // In dependency order before grouping, not after. GroupComponents merges
            // consecutive runs, so two assignments that need a third between them must
            // not be handed to it as neighbours. The sort is stable, so components
            // that do not depend on each other stay in component order.
            var registerNodes = TempAssignmentOrder.SortNodes(registerGroup);
            _compiler.Compile(registerNodes);
            List<HlslTreeNode[]> componentGroups =
                [.. nodeGrouper.GroupComponents(registerNodes).Select(g => g.ToArray())];
            // Before the groups themselves, and before anything overwrites what they
            // read.
            groups.AddRange(HoistStaleReads(componentGroups));
            groups.AddRange(componentGroups);
        }
        List<HlslTreeNode[]> ordered = TempAssignmentOrder.Sort(groups);
        List<HlslTreeNode[]> cyclic = HoistCyclicReads(ordered);
        return cyclic.Count == 0 ? ordered : [.. cyclic, .. ordered];
    }

    /// <summary>
    /// Names the reads that no order of these assignments can make right. Components
    /// that rotate - `mul r0.xyz, r0.yzxy, l(1.5)` taking x from y, y from z and z
    /// from x - and two variables that swap depend on each other in a circle:
    /// whichever is written first destroys what the next one reads. The ordering
    /// pass takes the first of them and moves on, having no order to find, and the
    /// circle was broken silently - output that compiled and computed something the
    /// shader did not. Read out before any of them run, the parallel assignment the
    /// one instruction made is kept.
    /// </summary>
    private List<HlslTreeNode[]> HoistCyclicReads(IList<HlslTreeNode[]> ordered)
    {
        var assignments = new List<HlslTreeNode[]>();
        for (int i = 0; i < ordered.Count; i++)
        {
            HashSet<HlslTreeNode> stale = HlslTreeNode.NewNodeSet();
            for (int j = 0; j < i; j++)
            {
                if (!TempAssignmentOrder.ReadsStale(ordered[i], ordered[j]))
                {
                    continue;
                }
                foreach (TempAssignmentNode written in ordered[j].OfType<TempAssignmentNode>())
                {
                    stale.Add(written.TempVariable);
                }
            }
            if (stale.Count == 0)
            {
                continue;
            }
            HashSet<HlslTreeNode> visited = HlslTreeNode.NewNodeSet();
            var stack = new Stack<HlslTreeNode>(ordered[i]);
            while (stack.Count != 0)
            {
                HlslTreeNode node = stack.Pop();
                if (!visited.Add(node))
                {
                    continue;
                }
                if (node is Operation && ReadsAnyOf(node, stale))
                {
                    // Named, so nothing inside it is read stale either.
                    assignments.Add([NameSubexpression(node, CreateTempVariables([node])[0])]);
                    continue;
                }
                foreach (HlslTreeNode input in HlslTreeNode.TraversableInputs(node))
                {
                    stack.Push(input);
                }
            }
        }
        return assignments;
    }

    /// <summary>
    /// Names the expressions that one instruction computes once and several
    /// assignments then read.
    ///
    /// One instruction writing several components becomes several assignments, and
    /// those run one after another where the instruction did not. `cmp r1, r2.x, r3,
    /// r1` decided every component from one condition, computed before any of them
    /// changed; written as `t1.y = 3 - t1.y >= 0 ? ...` followed by `t1.xzw =
    /// 3 - t1.y >= 0 ? ...`, the second tests a t1.y the first has already
    /// overwritten. Naming the condition first puts back the value the instruction
    /// saw. Unlike the hoisting below, this is not a matter of how large the
    /// expression is - it is wrong at any size.
    /// </summary>
    private List<HlslTreeNode[]> HoistStaleReads(IList<HlslTreeNode[]> componentGroups)
    {
        if (componentGroups.Count < 2)
        {
            return [];
        }

        HashSet<HlslTreeNode> overwritten = HlslTreeNode.NewNodeSet();
        foreach (HlslTreeNode root in componentGroups.SelectMany(g => g))
        {
            if (root is TempAssignmentNode assignment)
            {
                overwritten.Add(assignment.TempVariable);
            }
        }
        if (overwritten.Count == 0)
        {
            return [];
        }

        // Which of the assignments each node is read by. Anything read by more than
        // one of them is read after the first has already run.
        var readers = new Dictionary<HlslTreeNode, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < componentGroups.Count; i++)
        {
            foreach (HlslTreeNode node in Reachable(componentGroups[i]))
            {
                readers.TryGetValue(node, out int count);
                readers[node] = count + 1;
            }
        }

        var assignments = new List<HlslTreeNode[]>();
        HashSet<HlslTreeNode> visited = HlslTreeNode.NewNodeSet();
        var stack = new Stack<HlslTreeNode>(componentGroups.SelectMany(g => g));
        while (stack.Count != 0)
        {
            HlslTreeNode node = stack.Pop();
            if (!visited.Add(node))
            {
                continue;
            }

            if (node is Operation
                && readers.TryGetValue(node, out int count) && count > 1
                && ReadsAnyOf(node, overwritten))
            {
                // Named, so nothing inside it is read stale either.
                assignments.Add([NameSubexpression(node, CreateTempVariables([node])[0])]);
                continue;
            }

            foreach (HlslTreeNode input in HlslTreeNode.TraversableInputs(node))
            {
                stack.Push(input);
            }
        }
        return assignments;
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

    private static bool ReadsAnyOf(HlslTreeNode node, HashSet<HlslTreeNode> variables)
    {
        return Reachable([node]).Any(variables.Contains);
    }

    /// <summary>
    /// An expression read in more than one place is written out at each of them, so a
    /// value built on top of a value built on top of a value doubles the output at
    /// every level. Ten instructions of that reach sixty thousand characters.
    ///
    /// Naming one costs a line, so only the ones big enough to be worth it are named
    /// here; the smaller ones are left to the text pass below.
    /// </summary>
    private const int SharedSubexpressionThreshold = 8;

    private List<HlslTreeNode[]> HoistSharedSubexpressions(
        IList<HlslTreeNode[]> registerGroups)
    {
        // Here because every caller arrives with its roots reduced and nothing named
        // yet, which is the one point an idiom can be put into the graph rather than
        // recognised out of it later. See IdiomRecovery.
        IdiomRecovery.Recover(registerGroups, _grouper.MatrixMultiplicationGrouper);

        var roots = HlslTreeNode.NewNodeSet();
        foreach (HlslTreeNode[] group in registerGroups)
        {
            foreach (HlslTreeNode root in group)
            {
                roots.Add(root);
            }
        }

        var consumers = new Dictionary<HlslTreeNode, int>(ReferenceEqualityComparer.Instance);
        var order = new List<HlslTreeNode>();
        var seen = HlslTreeNode.NewNodeSet();
        var stack = new Stack<HlslTreeNode>(roots);
        while (stack.Count != 0)
        {
            HlslTreeNode node = stack.Pop();
            if (!seen.Add(node))
            {
                continue;
            }
            order.Add(node);
            foreach (HlslTreeNode input in HlslTreeNode.TraversableInputs(node))
            {
                consumers.TryGetValue(input, out int count);
                consumers[input] = count + 1;
                stack.Push(input);
            }
        }

        // GetDimensions has no expression form - it hands its results back through
        // out parameters - so a resinfo result is always named, whatever it costs.
        List<HlslTreeNode[]> resourceInfo = NameResourceInfo(order);
        resourceInfo.AddRange(NameConsumes(order));
        resourceInfo.AddRange(NameDoubleBits(order));

        // Sharing alone is not a reason to name something - almost every expression
        // shares a register read. What is worth naming is a subexpression of some
        // size that the compiler writes more than once, and then whatever text
        // still repeats after those have been taken out.
        //
        // This used to run only on statements whose inlined size passed a budget,
        // on the reading that a short statement has nothing worth cutting. The
        // budget turned itself off exactly where it was wanted: recovering an idiom
        // makes a statement shorter, so a lerp or a normalize put colour_grade
        // under the line and took its two names away with it, and the sample they
        // held came back per component. Twenty-one fixtures name more now, their
        // longest lines a good deal shorter for it, and not one costs an
        // instruction more.

        // What the compiler writes, rather than what the graph shares. A length
        // inside a normalize has a consumer per component and is written none of
        // those times - the grouper takes the divisions whole and the length with
        // them - so naming it by consumer count put a variable where the grouper
        // looks for a LengthOperation, and cost the normalize. The same reading the
        // text pass below is built on: what a node costs in a statement cannot be
        // had off the graph, only by compiling it and looking.
        Dictionary<HlslTreeNode, int> written = MeasureAndNumber(registerGroups).WrittenCounts();

        // Deepest first, so that a shared node inside another one is named before
        // the node containing it stops being reachable from here.
        var candidates = new List<HlslTreeNode>();
        for (int i = order.Count - 1; i >= 0; i--)
        {
            HlslTreeNode node = order[i];
            // An operation, or a normalize, which is not one and is written as a
            // call all the same - IsNameable says so, and this is the same question.
            // The two used to disagree, and it showed as soon as a normalize became a
            // node: recovered into the graph it is nameable to NameRepeatedText and
            // invisible here, so tangent_lighting stopped naming one that two
            // expressions read and wrote it out twice. The rest of what IsNameable
            // allows - a texture load, a lit, a constant - this path has never
            // considered, and whether it should is a question of its own.
            if (roots.Contains(node)
                || node is not (Operation or NormalizeOutputNode or ReflectOutputNode
                    or MatrixMultiplyOutputNode))
            {
                continue;
            }
            if (!consumers.TryGetValue(node, out int count) || count < 2)
            {
                continue;
            }
            if (!written.TryGetValue(node, out int writtenCount) || writtenCount < 2)
            {
                continue;
            }
            if (CountReachable(node) <= SharedSubexpressionThreshold)
            {
                continue;
            }
            candidates.Add(node);
        }
        HashSet<HlslTreeNode> reachable = HlslTreeNode.NewNodeSet();
        foreach (HlslTreeNode node in order)
        {
            reachable.Add(node);
        }
        resourceInfo.AddRange(NameCandidates(candidates, reachable, roots));
        resourceInfo.AddRange(NameRepeatedText(registerGroups, resourceInfo, roots));
        if (MergeVectorReads(resourceInfo, _lastRecording))
        {
            CloseUpNumbering(resourceInfo);
        }
        return Renumber(resourceInfo);
    }

    /// <summary>
    /// Scalars named apart and only ever read together, as one vector. Four dot
    /// products are four instructions and four names, and where the shader goes on
    /// to multiply the vector they make by a matrix, `float4(t13, t9, t5, t2)` was
    /// written out at every row. Those become one variable, read whole, when every
    /// read of each of them is a read of all of them in the one order, and there is
    /// more than one such read. Asked of the compiled text rather than the graph: a
    /// dot product is four multiplies on the graph and reads its vector nowhere, and
    /// the graph's reader lists carry nodes that templates have long since replaced.
    /// </summary>
    private TempVariableNode[] InMatrixRowOrder(
        TempVariableNode[] variables, Dictionary<TempVariableNode, HlslTreeNode[]> scalars)
    {
        MatrixMultiplicationGrouper matrices = _grouper.MatrixMultiplicationGrouper;
        var rows = new List<(TempVariableNode Variable, int Register)>();
        foreach (TempVariableNode variable in variables)
        {
            if (((TempAssignmentNode)scalars[variable][0]).Value is not DotProductOperation dot
                || !variables.All(other => ReferenceEquals(other, variable)
                    || (((TempAssignmentNode)scalars[other][0]).Value is DotProductOperation otherDot
                        && matrices.AreRowsOfOneMatrix(dot, otherDot)))
                || matrices.MatrixRowRegister(dot) is not int register)
            {
                return variables;
            }
            rows.Add((variable, register));
        }
        return [.. rows.OrderBy(r => r.Register).Select(r => r.Variable)];
    }

    private bool MergeVectorReads(
        List<HlslTreeNode[]> assignments, List<(HlslTreeNode[] Nodes, string Text)> recording)
    {
        if (recording == null)
        {
            return false;
        }
        var scalars = new Dictionary<TempVariableNode, HlslTreeNode[]>(ReferenceEqualityComparer.Instance);
        foreach (HlslTreeNode[] group in assignments)
        {
            if (group.Length == 1 && group[0] is TempAssignmentNode assignment
                && assignment.TempVariable.VariableSize == 1)
            {
                scalars[assignment.TempVariable] = group;
            }
        }
        if (scalars.Count < 2)
        {
            return false;
        }

        // Every read of a scalar, and every run of two to four of them read as a
        // vector, counted.
        var reads = new Dictionary<TempVariableNode, List<NodeList>>(ReferenceEqualityComparer.Instance);
        var runs = new Dictionary<NodeList, int>();
        foreach ((HlslTreeNode[] nodes, _) in recording)
        {
            if (!nodes.Any(n => n is TempVariableNode v && scalars.ContainsKey(v)))
            {
                continue;
            }
            var list = new NodeList(nodes);
            foreach (TempVariableNode variable in nodes.OfType<TempVariableNode>().Where(scalars.ContainsKey))
            {
                if (!reads.TryGetValue(variable, out List<NodeList> variableReads))
                {
                    reads[variable] = variableReads = [];
                }
                variableReads.Add(list);
            }
            if (nodes.Length is >= 2 and <= 4
                && nodes.All(n => n is TempVariableNode v && scalars.ContainsKey(v))
                && nodes.Distinct(ReferenceEqualityComparer.Instance).Count() == nodes.Length)
            {
                runs[list] = runs.GetValueOrDefault(list) + 1;
            }
        }

        var merged = HlslTreeNode.NewNodeSet();
        foreach ((NodeList run, int count) in runs
            .Where(r => r.Value > 1)
            .OrderByDescending(r => r.Value * r.Key.Nodes.Length))
        {
            TempVariableNode[] variables = [.. run.Nodes.Cast<TempVariableNode>()];
            // Read as this run and nowhere else, none of them merged already, and
            // declared alike, since they are to share a declaration. Compiling the
            // constructor compiles each component on its own and records that too,
            // so a scalar is read alone exactly once per read of the run.
            if (variables.Any(merged.Contains)
                || variables.Any(v => reads[v].Any(read => !read.Equals(run) && read.Nodes.Length != 1)
                    || reads[v].Count(read => read.Nodes.Length == 1) != count)
                || variables.Any(v => v.IsInteger != variables[0].IsInteger
                    || v.IsBits != variables[0].IsBits || v.IsUnsigned != variables[0].IsUnsigned))
            {
                continue;
            }
            // Rows of one matrix in the order the matrix has them, so that the
            // four dots read as the one mul they are; anything else in the order
            // it was read.
            variables = InMatrixRowOrder(variables, scalars);
            var group = new HlslTreeNode[variables.Length];
            for (int i = 0; i < variables.Length; i++)
            {
                group[i] = scalars[variables[i]][0];
                assignments.Remove(scalars[variables[i]]);
                variables[i].DeclarationIndex = variables[0].DeclarationIndex;
                variables[i].ComponentIndex = i;
                variables[i].VariableSize = variables.Length;
                merged.Add(variables[i]);
            }
            assignments.Add(group);
        }
        return merged.Count != 0;
    }

    // Merging leaves the numbers the merged variables had unused, so the ones in
    // use are closed up and the counter set back to follow them.
    private void CloseUpNumbering(List<HlslTreeNode[]> assignments)
    {
        List<TempVariableNode> variables = [.. assignments
            .SelectMany(group => group)
            .OfType<TempAssignmentNode>()
            .Select(assignment => assignment.TempVariable)];
        List<int> used = [.. variables.Select(v => v.DeclarationIndex.Value).Distinct().OrderBy(i => i)];
        if (used.Count == 0)
        {
            return;
        }
        int first = used[0];
        var renumbered = used.Select((index, i) => (index, i)).ToDictionary(p => p.index, p => first + p.i);
        foreach (TempVariableNode variable in variables)
        {
            variable.DeclarationIndex = renumbered[variable.DeclarationIndex.Value];
        }
        _compiler.NextTempVariableIndex = first + used.Count;
    }

    /// <summary>
    /// What compiling these groups and throwing the text away reveals about them.
    ///
    /// The values, not the assignments: compiling an assignment writes a whole
    /// statement - the declaration, the name and the semicolon - and what is being
    /// counted here is the text of expressions. (It used to say numbering as the
    /// reason, and that reason is gone: a measurement takes its numbers back now.
    /// This one stands on its own.)
    /// </summary>
    /// <summary>
    /// And this one numbers as it measures, because these two measurements are where
    /// the writer's variables get their numbers - see
    /// <see cref="NodeCompiler.MeasureAndNumber"/>.
    /// </summary>
    private CompileMeasurement MeasureAndNumber(IEnumerable<HlslTreeNode[]> groups)
    {
        return _compiler.MeasureAndNumber(Values(groups));
    }

    private static IEnumerable<IEnumerable<HlslTreeNode>> Values(
        IEnumerable<HlslTreeNode[]> groups)
    {
        return groups.Select(group => group.Select(root =>
            root is TempAssignmentNode assignment ? assignment.Value : root));
    }

    /// <summary>
    /// How much of a statement's text has to be a repeat of one expression before
    /// that expression is named: about what the declaration line costs.
    /// </summary>
    private const int RepeatedTextBudget = 24;

    /// <summary>
    /// Names the expressions the statement's text writes out more than once. Which
    /// those are cannot be read off the graph: four dot products read by sixteen
    /// multiplies are one `mul(t0, viewProj)` once the grouper has been at them. So
    /// the statement is compiled and thrown away, the text of every subexpression
    /// counted, the one with the most repeated text named, and the statement compiled
    /// again: the expressions inside the named one are repeated less now, or not at
    /// all, and are measured afresh.
    /// </summary>
    // The text of the statement as it stood when nothing was left to name, for the
    // merge that reads it afterwards.
    private List<(HlslTreeNode[] Nodes, string Text)> _lastRecording;

    private List<HlslTreeNode[]> NameRepeatedText(
        IList<HlslTreeNode[]> registerGroups, List<HlslTreeNode[]> named, HashSet<HlslTreeNode> roots)
    {
        var assignments = new List<HlslTreeNode[]>();
        while (true)
        {
            IEnumerable<HlslTreeNode[]> groups = registerGroups.Concat(named).Concat(assignments);
            CompileMeasurement measurement = MeasureAndNumber(groups);
            List<(HlslTreeNode[] Nodes, string Text)> recording = measurement.Recording;
            HashSet<HlslTreeNode> grouped = measurement.Grouped;
            List<HlslTreeNode[]> groupMatches = measurement.GroupMatches;

            // A repeat is the same nodes compiled again - or the same but for a
            // constant, which may be a node of its own at each: a template that
            // builds one builds one per match, and the 1 in the w of four dot
            // products is four nodes.
            HlslTreeNode[] candidate = recording
                .GroupBy(r => new NodeList(Broadcast(r.Nodes)))
                .Select(g => (g.Key.Nodes, Repeated: (g.Count() - 1) * g.First().Text.Length))
                .Where(r => r.Repeated >= RepeatedTextBudget)
                .Where(r => r.Nodes.All(n => IsNameable(n) && !roots.Contains(n) && !grouped.Contains(n)))
                .Where(r => r.Nodes.Any(n => n is not ConstantNode))
                .Where(r => r.Nodes.Distinct(ReferenceEqualityComparer.Instance).Count() == r.Nodes.Length)
                .OrderByDescending(r => r.Repeated)
                .Select(r => r.Nodes)
                .FirstOrDefault();
            // Only this statement's readers are given the variable. The graph is
            // shared with every other statement that reads the value, and one of
            // those may be outside the block this one is in.
            HashSet<HlslTreeNode> readers = HlslTreeNode.NewNodeSet();
            foreach (HlslTreeNode node in Reachable(groups.SelectMany(g => g)))
            {
                readers.Add(node);
            }

            // The two below run only once nothing is repeated by the nodes. A value
            // the bytecode computes twice is two subtrees, which read alike because
            // they are alike, and neither is a repeat to the grouping above; an
            // instruction read by two expressions is one subtree and not a repeat
            // either. Both are left to last so that they take nothing away from it -
            // naming what is inside a nest before the nest leaves the expression
            // around it written out at every use, and the counts the text pass
            // merges are exactly the ones that would.
            List<HlslTreeNode[]> occurrences = candidate != null
                ? [candidate]
                : TextRepeats(recording, grouped, roots)
                    ?? RootOfSeveralRegisters(registerGroups, recording)
                    ?? RootReadAgain(registerGroups, recording)
                    ?? SplitRead(readers, grouped, roots, measurement)
                    ?? SharedRoots(registerGroups, readers, recording, groupMatches)
                    ?? SharedInstruction(readers, recording, roots)
                    ?? ScatteredInstruction(readers, recording, roots);
            if (occurrences == null)
            {
                _lastRecording = recording;
                return assignments;
            }
            candidate = occurrences[0];

            // The components beside a subtree are its own, so taking them leaves the
            // others behind: the extension is for the one occurrence there is.
            if (occurrences.Count == 1)
            {
                candidate = WithSiblingComponents(candidate, readers, roots);
                occurrences = [candidate];
            }
            occurrences = InWrittenOrder(occurrences);
            candidate = occurrences[0];
            TempVariableNode[] variables = CreateTempVariables(candidate);
            foreach (HlslTreeNode[] occurrence in occurrences)
            {
                for (int i = 0; i < candidate.Length; i++)
                {
                    if (occurrence[i] is not ConstantNode)
                    {
                        Rewire(occurrence[i], variables[i], readers);
                    }
                }
            }
            // A constant's twins: an equal constant read alongside the variable's
            // other components is that component - not the one node compiled, and
            // not only the ones compiled, since a matrix multiply compiles one of
            // its four dots' vectors and the others are never seen.
            int[] valuePositions = [.. Enumerable.Range(0, candidate.Length).Where(i => candidate[i] is not ConstantNode)];
            for (int i = 0; i < candidate.Length; i++)
            {
                if (candidate[i] is not ConstantNode constant)
                {
                    continue;
                }
                foreach (ConstantNode twin in readers.OfType<ConstantNode>().Where(c => SameValue(c, constant)).ToList())
                {
                    HashSet<HlslTreeNode> alongside = HlslTreeNode.NewNodeSet();
                    foreach (HlslTreeNode reader in twin.Outputs)
                    {
                        if (readers.Contains(reader)
                            && valuePositions.All(j => reader.Inputs.Any(input => ReferenceEquals(input, variables[j]))))
                        {
                            alongside.Add(reader);
                        }
                    }
                    Rewire(twin, variables[i], alongside);
                }
            }
            // Where what was named is a register's whole value, the register reads
            // the variable from now on. Rewire moves the readers in the graph and an
            // output has none - it is not a node that reads the value, it is the
            // register whose value that is - so the group it was written from is
            // repointed instead.
            for (int i = 0; i < registerGroups.Count; i++)
            {
                if (registerGroups[i].Length == candidate.Length
                    && registerGroups[i].Zip(candidate).All(pair =>
                        ReferenceEquals(pair.First, pair.Second)))
                {
                    registerGroups[i] = [.. variables];
                }
            }
            assignments.Add([.. candidate.Select((node, i) => (HlslTreeNode)new TempAssignmentNode(variables[i], node))]);
        }
    }

    /// <summary>
    /// The other components of the same instruction, where the candidate is one of
    /// them. One texture load is four nodes, and a component that repeats often
    /// enough on its own is named on its own: a terrain shader reading three
    /// channels of a blend map named each of them and wrote the sample out three
    /// times, once per declaration. Named together they are one read and one
    /// variable.
    ///
    /// Only the components this statement reads, and only the ones the grouper would
    /// write as one swizzle anyway. That is the test rather than input identity: the
    /// components of one sample do not share their coordinate nodes - each is read
    /// into its own - so what says they are one instruction is that they group.
    /// </summary>
    private HlslTreeNode[] WithSiblingComponents(
        HlslTreeNode[] candidate, HashSet<HlslTreeNode> readers, HashSet<HlslTreeNode> roots)
    {
        if (candidate.Length == 0 || candidate[0] is ConstantNode)
        {
            return candidate;
        }
        if (WithSiblingRows(candidate, readers, roots) is HlslTreeNode[] rows)
        {
            return rows;
        }
        if (candidate[0] is Operation)
        {
            return WithSiblingOperations(candidate, readers, roots);
        }
        if (candidate[0] is not IHasComponentIndex
            || candidate[0].Inputs.Count == 0)
        {
            return candidate;
        }

        var components = new List<HlslTreeNode>(candidate);
        var indices = new HashSet<int>(candidate.OfType<IHasComponentIndex>().Select(c => c.ComponentIndex));
        if (indices.Count != candidate.Length)
        {
            return candidate;
        }
        foreach (HlslTreeNode sibling in candidate[0].Inputs[0].Outputs)
        {
            if (sibling is not IHasComponentIndex component
                || sibling.GetType() != candidate[0].GetType()
                || indices.Contains(component.ComponentIndex)
                || !readers.Contains(sibling)
                || roots.Contains(sibling)
                || !_templateMatcher.CanGroupComponents(sibling, candidate[0], false))
            {
                continue;
            }
            indices.Add(component.ComponentIndex);
            components.Add(sibling);
        }
        return components.Count == candidate.Length
            ? candidate
            : [.. components.OrderBy(c => ((IHasComponentIndex)c).ComponentIndex)];
    }

    /// <summary>
    /// The other components of the same instruction, where the instruction is an
    /// operation. A texture load's components are one node apiece over the one
    /// input and differ by ComponentIndex; `ddx(texcoord.x)` and `ddx(texcoord.y)`
    /// are two nodes over two inputs, and what says they are one instruction is
    /// that they group - the same test the other search ends on. Ordered by the
    /// component their input reads, so the variable's components come out in the
    /// order a swizzle of it wants them.
    /// </summary>
    /// <summary>
    /// A register assigned rows of a matrix multiply, given the multiply's other rows
    /// as lanes of its own. A decal's position is three rows of its transform, and
    /// the box test compares all three with one lt - but fxc overwrites the third in
    /// its register with the test's answer within the same instructions, so only
    /// two reached a variable, the third was written as a dot of its own, and the
    /// test as `abs(dot(...)) < 0.5 && all(abs(t3) < 0.5)`: three instructions
    /// dearer for each decal. With the third row a lane of the same variable the
    /// test is all(abs(t3) < 0.5), and the position one float3.
    ///
    /// The variable is the register's, keyed by the register's components, and the
    /// row it is given has no component of that register to be. So it is given here,
    /// where the statement's assignments are gathered, as one more assignment in the
    /// group - and only where every reader of the row is this statement or one after
    /// it in the same block, inside which the variable is declared, and the variable
    /// is assigned nowhere else, so that its declaration is the one this writes.
    /// </summary>
    private void WidenWithSiblingRows(List<HlslTreeNode[]> registerGroups)
    {
        if (_currentStatement == null
            || FindPlace(_functionStatements, _currentStatement) is not (IList<IStatement> body, int index))
        {
            return;
        }
        HashSet<HlslTreeNode> inScope = ValuesOf(body.Skip(index));
        for (int g = 0; g < registerGroups.Count; g++)
        {
            HlslTreeNode[] group = registerGroups[g];
            if (group.Length < 2
                || !group.All(root => root is TempAssignmentNode { IsReassignment: false, Value: DotProductOperation }))
            {
                continue;
            }
            TempAssignmentNode[] assignments = [.. group.Cast<TempAssignmentNode>()];
            HlslTreeNode[] rows = [.. assignments.Select(assignment => assignment.Value)];
            HashSet<HlslTreeNode> candidates = HlslTreeNode.NewNodeSet();
            foreach (HlslTreeNode node in inScope)
            {
                if (node is DotProductOperation && !rows.Contains(node, ReferenceEqualityComparer.Instance)
                    && !node.Outputs.Any(reader => reader is TempAssignmentNode)
                    && node.Outputs.All(inScope.Contains))
                {
                    candidates.Add(node);
                }
            }
            if (candidates.Count == 0
                || WithSiblingRows(rows, candidates, HlslTreeNode.NewNodeSet()) is not HlslTreeNode[] run
                || IsAssignedElsewhere(assignments))
            {
                continue;
            }
            TempVariableNode first = assignments[0].TempVariable;
            var widened = new List<HlslTreeNode>();
            foreach (HlslTreeNode row in run)
            {
                int existing = Array.FindIndex(rows, r => ReferenceEquals(r, row));
                if (existing >= 0)
                {
                    widened.Add(assignments[existing]);
                    continue;
                }
                var lane = new TempVariableNode
                {
                    IsInteger = first.IsInteger,
                    IsUnsigned = first.IsUnsigned,
                    IsBits = first.IsBits,
                    IsDouble = first.IsDouble,
                    IsHalf = first.IsHalf,
                };
                Rewire(row, lane);
                widened.Add(new TempAssignmentNode(lane, row));
            }
            registerGroups[g] = [.. widened];
        }
    }

    // Where a statement sits: the body holding it, and its place in that body.
    private static (IList<IStatement> Body, int Index)? FindPlace(IList<IStatement> body, IStatement statement)
    {
        for (int i = 0; i < body.Count; i++)
        {
            if (ReferenceEquals(body[i], statement))
            {
                return (body, i);
            }
            IEnumerable<IList<IStatement>> inner = body[i] switch
            {
                IfStatement ifStatement => [ifStatement.TrueBody, ifStatement.FalseBody ?? []],
                LoopStatement loop => [loop.Body],
                SwitchStatement switchStatement => switchStatement.Cases.Select(c => c.Body),
                _ => [],
            };
            foreach (IList<IStatement> nested in inner)
            {
                if (FindPlace(nested, statement) is (IList<IStatement>, int) found)
                {
                    return found;
                }
            }
        }
        return null;
    }

    // Every value the statements reach, and those inside them.
    private static HashSet<HlslTreeNode> ValuesOf(IEnumerable<IStatement> statements)
    {
        var roots = new List<HlslTreeNode>();
        new StatementVisitor([.. statements]).Visit(statement =>
        {
            roots.AddRange(statement.Outputs.Values);
            roots.AddRange(statement.Inputs.Values);
            roots.AddRange(statement.HeldNodes);
        });
        HashSet<HlslTreeNode> values = HlslTreeNode.NewNodeSet();
        foreach (HlslTreeNode node in Reachable(roots.Where(root => root != null)))
        {
            values.Add(node);
        }
        return values;
    }

    // Whether any of these variables is assigned by anything but these assignments.
    private bool IsAssignedElsewhere(TempAssignmentNode[] assignments)
    {
        HashSet<HlslTreeNode> variables = HlslTreeNode.NewNodeSet();
        HashSet<HlslTreeNode> own = HlslTreeNode.NewNodeSet();
        foreach (TempAssignmentNode assignment in assignments)
        {
            variables.Add(assignment.TempVariable);
            own.Add(assignment);
        }
        bool elsewhere = false;
        new StatementVisitor(_functionStatements).Visit(statement =>
        {
            foreach (HlslTreeNode value in statement.Outputs.Values.Concat(statement.HeldNodes))
            {
                if (value is TempAssignmentNode other && !own.Contains(other)
                    && variables.Contains(other.TempVariable))
                {
                    elsewhere = true;
                }
            }
        });
        return elsewhere;
    }

    /// <summary>
    /// The other rows of a matrix multiply, where the candidate is some of them. A
    /// decal's position is three rows of one transform, and only the first two were
    /// read twice - as the coordinate it samples with, and by the box test - so the
    /// two were named as `mul(t2, (float4x2)decalMatrix)` and the third written on
    /// its own as `dot(transpose(decalMatrix)[2], t2)`: the multiply taken apart,
    /// two instructions dearer. Named with the rows beside it the statement reads,
    /// the multiply is one again. Only a run of rows with no gap, so that what is
    /// named is a multiply the grouper writes whole.
    /// </summary>
    private HlslTreeNode[] WithSiblingRows(
        HlslTreeNode[] candidate, HashSet<HlslTreeNode> readers, HashSet<HlslTreeNode> roots)
    {
        MatrixMultiplicationGrouper matrices = _grouper.MatrixMultiplicationGrouper;
        // Not one row on its own: a projection's w is named as the divisor, and
        // widened into the whole multiply its divide came apart into one per use -
        // `t2.xy / t2.w` and `t2.z / t2.w` - an instruction dearer apiece.
        if (candidate.Length < 2
            || !candidate.All(node => node is DotProductOperation dot && matrices.MatrixRowRegister(dot) != null)
            || !candidate.Skip(1).All(node =>
                matrices.AreRowsOfOneMatrix((DotProductOperation)candidate[0], (DotProductOperation)node)))
        {
            return null;
        }
        var byRow = new SortedDictionary<int, HlslTreeNode>();
        foreach (DotProductOperation dot in candidate.Cast<DotProductOperation>())
        {
            if (!byRow.TryAdd(matrices.MatrixRowRegister(dot).Value, dot))
            {
                return null;
            }
        }
        int first = byRow.Keys.First();
        int last = byRow.Keys.Last();
        if (last - first + 1 != byRow.Count)
        {
            return null;
        }
        foreach (HlslTreeNode node in readers)
        {
            if (node is not DotProductOperation sibling
                || roots.Contains(sibling)
                || candidate.Contains(sibling, ReferenceEqualityComparer.Instance)
                || matrices.MatrixRowRegister(sibling) is not int row
                || byRow.ContainsKey(row)
                || !matrices.AreRowsOfOneMatrix((DotProductOperation)candidate[0], sibling))
            {
                continue;
            }
            byRow[row] = sibling;
        }
        // The run the candidate is in, and nothing past a gap on either side.
        var run = new List<HlslTreeNode>();
        for (int row = first; byRow.ContainsKey(row - 1); row--)
        {
            first = row - 1;
        }
        for (int row = first; byRow.TryGetValue(row, out HlslTreeNode dot); row++)
        {
            run.Add(dot);
        }
        return run.Count > candidate.Length ? [.. run] : null;
    }

    private HlslTreeNode[] WithSiblingOperations(
        HlslTreeNode[] candidate, HashSet<HlslTreeNode> readers, HashSet<HlslTreeNode> roots)
    {
        if (candidate.Length != 1 || ComponentOrder(candidate[0]) is not int order)
        {
            return candidate;
        }
        var byOrder = new SortedDictionary<int, HlslTreeNode> { [order] = candidate[0] };
        foreach (HlslTreeNode sibling in readers)
        {
            if (ReferenceEquals(sibling, candidate[0])
                || roots.Contains(sibling)
                || !_templateMatcher.CanGroupComponents(sibling, candidate[0], false)
                || ComponentOrder(sibling) is not int siblingOrder
                || byOrder.ContainsKey(siblingOrder))
            {
                continue;
            }
            byOrder[siblingOrder] = sibling;
        }
        return byOrder.Count == 1 ? candidate : [.. byOrder.Values];
    }

    /// <summary>
    /// Which component of its register an expression reads, for ordering the
    /// components of one instruction against each other. The first one found: an
    /// operation over one register's component answers with that component.
    /// </summary>
    private static int? ComponentOrder(HlslTreeNode node)
    {
        return ComponentOrder(node, HlslTreeNode.NewNodeSet());
    }

    private static int? ComponentOrder(HlslTreeNode node, HashSet<HlslTreeNode> visited)
    {
        if (!visited.Add(node))
        {
            return null;
        }
        if (node is IHasComponentIndex indexed)
        {
            return indexed.ComponentIndex;
        }
        foreach (HlslTreeNode input in node.Inputs)
        {
            if (ComponentOrder(input, visited) is int order)
            {
                return order;
            }
        }
        return null;
    }

    /// <summary>
    /// One instruction read by more than one expression. `ddx(texcoord)` inside an
    /// fwidth and again as a component of a constructor is written out at each,
    /// and fxc takes the derivative twice; named once, both read the variable.
    ///
    /// The readers are the test, not the widths the recording holds. A node inside
    /// a constructor is recorded both as its own component and as part of the
    /// group, so being written at two widths is the ordinary case and says
    /// nothing. Two readers is a value computed once and written twice.
    ///
    /// Only the nodes that cost an instruction to compute again. A few characters
    /// of arithmetic written out twice cost nothing, and there are hundreds of
    /// those: naming them would be a declaration apiece for no instruction saved.
    /// </summary>
    /// <summary>
    /// A register's whole value, where something other than the register itself
    /// reads it. Such a value cannot be named a component at a time: the four dot
    /// products of `mul(v, m)` are one match, and naming the one the fog reads
    /// would leave the output reading a variable for its w and the dots for the
    /// rest, which is no multiply at all. Named whole it keeps its shape, since the
    /// assignment compiles the same components and the grouper matches them again.
    ///
    /// Judged by what saying it again costs in text rather than by whether it costs
    /// an instruction - fxc recognises the common subexpression either way, so this
    /// is the text pass's question and takes the text pass's budget.
    /// </summary>
    /// <summary>
    /// What a register written a second time from one value has to cost before the
    /// value is named: about what the declaration line costs, the way
    /// RepeatedTextBudget is, and smaller than it because what a name saves here is
    /// a whole write rather than a repeat inside one expression. Twelve catches the
    /// four hull shaders in the corpus that write a tessellation factor to every
    /// edge, where twenty-four caught two of them, and neither costs an instruction
    /// anywhere.
    /// </summary>
    private const int SharedRootBudget = 12;

    /// <summary>
    /// One value that several registers are written from, named once so that each of
    /// them is an assignment of the name.
    ///
    /// Nothing else names this. SharedRoots below wants a group of more than one
    /// component, because that is the case where the components have to move
    /// together and a grouper match is what says they may; SplitRead drops a root
    /// outright, on the reading that a root is being written where it stands and
    /// needs no name. Which is true of a root written once. A hull shader's patch
    /// constant function clamps its tessellation factor and writes it to four edges
    /// and two insides, and those are six roots that are three values: the clamp
    /// came out four times and the half of it twice, three instructions more than
    /// the shader it was read from.
    /// </summary>
    private List<HlslTreeNode[]> RootOfSeveralRegisters(
        IList<HlslTreeNode[]> registerGroups,
        List<(HlslTreeNode[] Nodes, string Text)> recording)
    {
        // By the nodes they are written from, so that two registers written from one
        // value are one candidate however many components they have.
        foreach (IGrouping<NodeList, HlslTreeNode[]> written in registerGroups
            .GroupBy(group => new NodeList(group))
            .Where(g => g.Count() > 1)
            .OrderByDescending(g => g.Count()))
        {
            HlslTreeNode[] group = written.Key.Nodes;
            if (!group.All(IsNameable)
                || group.Any(node => node.Outputs.Any(reader => reader is TempAssignmentNode)))
            {
                continue;
            }
            // What writing it again costs, measured the way every other name here is:
            // the text the compiler wrote for it, once for each register past the
            // first.
            int text = recording
                .Where(r => r.Nodes.Length == group.Length
                    && r.Nodes.Zip(group).All(pair => ReferenceEquals(pair.First, pair.Second)))
                .Select(r => r.Text.Length)
                .DefaultIfEmpty(0)
                .Max();
            if ((written.Count() - 1) * text >= SharedRootBudget)
            {
                return [group];
            }
        }
        return null;
    }

    /// <summary>
    /// A computed value a register is written from that the statement also writes
    /// inside another expression. quad_tess_factors writes the patch's centre,
    /// `0.25 * (p0 + p1 + p2 + p3)`, to an output and measures the eye's distance
    /// from it, and the join phase that measures it read the fork phases' output
    /// back; written out twice, fxc computes it twice, three instructions with the
    /// scheduling around it. Short enough that no text budget catches it, which is
    /// why it is its own rule: what a second write of a root costs is an
    /// instruction, not text. carried_constants, the same shape over three
    /// control points, comes back to the instructions it was read from as well.
    ///
    /// Hull shaders only, since the cost is the phases': fxc splits the patch
    /// constant function into fork and join phases and computes a value again in
    /// each that writes it out. Elsewhere it is one function and fxc reuses what
    /// it computed - particle_draw's `t1 * t2` written to the texcoord and again
    /// in a mad costs nothing, and named, the mad it folds back into is the same
    /// text again one round on. And not a vector of one value read twice, which
    /// is a broadcast rather than a value the register holds.
    /// </summary>
    private List<HlslTreeNode[]> RootReadAgain(
        IList<HlslTreeNode[]> registerGroups,
        List<(HlslTreeNode[] Nodes, string Text)> recording)
    {
        if (_shader.Type != ShaderType.Hull)
        {
            return null;
        }
        foreach (HlslTreeNode[] group in registerGroups)
        {
            if (!group.All(node => node is Operation && IsNameable(node))
                || group.Distinct(ReferenceEqualityComparer.Instance).Count() != group.Length
                || group.Any(node => node.Outputs.Any(reader => reader is TempAssignmentNode)))
            {
                continue;
            }
            int written = recording.Count(r => r.Nodes.Length == group.Length
                && r.Nodes.Zip(group).All(pair => ReferenceEquals(pair.First, pair.Second)));
            if (written > 1)
            {
                return [group];
            }
        }
        return null;
    }

    private List<HlslTreeNode[]> SharedRoots(
        IList<HlslTreeNode[]> registerGroups,
        HashSet<HlslTreeNode> readers,
        List<(HlslTreeNode[] Nodes, string Text)> recording,
        List<HlslTreeNode[]> groupMatches)
    {
        foreach (HlslTreeNode[] group in registerGroups)
        {
            // One component is what SplitRead above is for; this is for the ones
            // that have to move together - and only where a grouper took them
            // together, since that is what makes them one expression. Four
            // unrelated values sharing a register are a constructor, and an
            // assignment cannot be written inside one.
            if (group.Length < 2
                || !group.All(IsNameable)
                || group.Any(node => node.Outputs.Any(reader => reader is TempAssignmentNode))
                || !groupMatches.Any(match => match.Length == group.Length
                    && match.Zip(group).All(pair => ReferenceEquals(pair.First, pair.Second))))
            {
                continue;
            }
            HashSet<HlslTreeNode> inside = HlslTreeNode.NewNodeSet();
            foreach (HlslTreeNode node in Reachable(group))
            {
                inside.Add(node);
            }
            int repeated = 0;
            foreach (HlslTreeNode node in group)
            {
                if (!node.Outputs.Any(reader =>
                    readers.Contains(reader) && !inside.Contains(reader)))
                {
                    continue;
                }
                repeated += recording
                    .Where(r => r.Nodes.Length == 1 && ReferenceEquals(r.Nodes[0], node))
                    .Sum(r => r.Text.Length);
            }
            if (repeated >= RepeatedTextBudget)
            {
                return [group];
            }
        }
        return null;
    }

    /// <summary>
    /// The components one instruction wrote, where the output writes them out in
    /// more than one place. A four wide mad whose components are read as xz here, y
    /// there and x and zw in the return is one instruction and four lerps in the
    /// text; named, it is the one instruction the bytecode has and the readers are
    /// swizzles of it.
    ///
    /// The node says which instruction made it, so this asks that and then the text
    /// pass's question: whether writing it out at each use costs more than the line
    /// a name takes.
    /// </summary>
    private List<HlslTreeNode[]> SharedInstruction(
        HashSet<HlslTreeNode> readers,
        List<(HlslTreeNode[] Nodes, string Text)> recording,
        HashSet<HlslTreeNode> roots)
    {
        foreach (IGrouping<int, HlslTreeNode> instruction in readers
            .Where(node => node.SourceInstruction != 0 && IsNameable(node) && !roots.Contains(node))
            .GroupBy(node => node.SourceInstruction)
            .Where(group => group.Count() > 1)
            .OrderByDescending(group => group.Count()))
        {
            // In the order the instruction wrote them, so the variable's components
            // are the register's and the readers are the swizzles they were.
            HlslTreeNode[] group = [.. instruction.OrderBy(node => node.SourceComponent)];
            if (group.Any(node => node.Outputs.Any(reader => reader is TempAssignmentNode)))
            {
                continue;
            }
            // Every place the components are written out. What a name saves is all
            // of them but the one it keeps.
            List<(HlslTreeNode[] Nodes, string Text)> written = [.. recording
                .Where(r => r.Nodes.All(group.Contains))];
            // And only where something reads them as a vector. A register whose
            // components are read one at a time by unrelated expressions is four
            // values that share an instruction, and gathering them into a variable
            // makes fxc build the vector that the shader was doing without.
            if (written.Count < 2 || !written.Any(r => r.Nodes.Length > 1))
            {
                continue;
            }
            int saved = written.Sum(r => r.Text.Length) - written.Max(r => r.Text.Length);
            if (saved >= RepeatedTextBudget)
            {
                return [group];
            }
        }
        return null;
    }

    /// <summary>
    /// One instruction's components computed apart. A decal's box test compares all
    /// three components of the position at once - one ge and one and over .xyz -
    /// and multiplies the three answers together, and read back a component at a
    /// time it said step(abs(t3.x), 0.5) * step(abs(t3.y), 0.5) * step(abs(t3.z), 0.5):
    /// three computations, which fxc compiled as a pair and a single, two
    /// instructions dearer. Named as the vector the instruction made - float3 t6 =
    /// step(abs(t3), 0.5) - it is the one instruction again, and the product reads
    /// t6.x, t6.y and t6.z.
    ///
    /// Only where each component is a computation and is written on its own, never
    /// with the others - SharedInstruction is the rule for components something
    /// reads together - and where the components written together are one vector
    /// expression rather than a constructor of them, which would be the same work
    /// three times with a declaration besides. Asked by measuring, which leaves
    /// nothing behind.
    /// </summary>
    private List<HlslTreeNode[]> ScatteredInstruction(
        HashSet<HlslTreeNode> readers,
        List<(HlslTreeNode[] Nodes, string Text)> recording,
        HashSet<HlslTreeNode> roots)
    {
        foreach (IGrouping<int, HlslTreeNode> instruction in readers
            .Where(node => node.SourceInstruction != 0 && node is Operation
                && IsNameable(node) && !roots.Contains(node))
            .GroupBy(node => node.SourceInstruction)
            .Where(group => group.Count() > 1)
            .OrderByDescending(group => group.Count()))
        {
            HlslTreeNode[] group = [.. instruction.OrderBy(node => node.SourceComponent)];
            if (group.Any(node => node.Outputs.Any(reader => reader is TempAssignmentNode))
                || group.Select(node => node.SourceComponent).Distinct().Count() != group.Length)
            {
                continue;
            }
            List<(HlslTreeNode[] Nodes, string Text)> written = [.. recording
                .Where(r => r.Nodes.Any(group.Contains))];
            if (written.Count < group.Length || written.Any(r => r.Nodes.Length > 1))
            {
                continue;
            }
            string together = _compiler.Measure([group]).Recording[^1].Text;
            if (System.Text.RegularExpressions.Regex.IsMatch(together, @"\b(float|int|uint|half|double|bool)[234]\(")
                || !ReadsWholeVectors(together))
            {
                continue;
            }
            return [group];
        }
        return null;
    }

    /// <summary>
    /// Whether every operand the text reads is a vector from its start - a name on
    /// its own, or .xy, .xyz, a single component broadcast - rather than components
    /// picked out of order. fxc packs unrelated scalar arithmetic into one
    /// instruction where the lanes are free, and gathered into a vector those come
    /// back as `texcoord.xz * texcoord.yw` and `abs(a.xw) + 1`: one instruction, as
    /// the bytecode had, but values the shader never had, in place of the two
    /// readable ones it did.
    /// </summary>
    private static bool ReadsWholeVectors(string text)
    {
        foreach (System.Text.RegularExpressions.Match swizzle in
            System.Text.RegularExpressions.Regex.Matches(text, @"\.([xyzw]{2,4})\b"))
        {
            if (swizzle.Groups[1].Value is not ("xy" or "xyz" or "xyzw"))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// About what a declaration line costs, the way RepeatedTextBudget is - but for
    /// one write rather than a repeat, so it is the length at which a read is better
    /// read off a name than in the line it would sit in.
    /// </summary>
    private const int SingleWriteBudget = 28;

    /// <summary>
    /// How many writes this node is worth naming for: what the compiler wrote it,
    /// and one more where it wrote it only once but wrote a great deal of text.
    /// </summary>
    private static int WorthNaming(
        HlslTreeNode node,
        Dictionary<HlslTreeNode, int> writes,
        Dictionary<HlslTreeNode, int> text)
    {
        int written = writes.GetValueOrDefault(node);
        return written == 1 && text.GetValueOrDefault(node) >= SingleWriteBudget
            ? 2
            : written;
    }

    private List<HlslTreeNode[]> SplitRead(
        HashSet<HlslTreeNode> readers,
        HashSet<HlslTreeNode> grouped,
        HashSet<HlslTreeNode> roots,
        CompileMeasurement measurement)
    {
        // How many times the compiler wrote the node. This used to be a count of the
        // expressions that read it, worked out by walking every reader of every
        // component and dropping the ones CanGroupComponents said could be
        // components of one value - which is not the same question. What decides a
        // name is how many times the text will be written, and `sample.y + sample.x`
        // is one expression reading two components and writes the sample twice.
        //
        // The old count was loose in the other direction as well. It answered per
        // instruction, keyed on a node's first input, so every sample through one
        // sampler answered with the same inflated number and got named even where
        // one expression read it; narrowing it to the instruction that made each node
        // was tried on 2026-09-27 and cost twenty fixtures those names. The walk was
        // also quadratic in how many components answered together - forty samples of
        // one texture are a hundred and sixty of them, each walking all the others -
        // and needed a cache per instruction to be affordable.
        //
        // The measurement has had the true count all along: one entry per Compile
        // call, so a value two expressions write appears twice. Measured over the
        // corpus, swapping the one for the other costs no instruction anywhere.
        Dictionary<HlslTreeNode, int> writes = measurement.WrittenCounts();

        // And the third thing: something to go on naming an
        // expensive read that one expression reads. The text the compiler wrote for
        // it, which the measurement also has. A sample written once is still worth a
        // line when the line it would otherwise sit in is long - that is what keeps
        // the fixtures a handful of short statements - and a normalize written once
        // is not, because it is short enough to read where it stands.
        var text = new Dictionary<HlslTreeNode, int>(ReferenceEqualityComparer.Instance);
        foreach ((HlslTreeNode[] nodes, string written) in measurement.Recording)
        {
            foreach (HlslTreeNode node in nodes)
            {
                text[node] = Math.Max(text.GetValueOrDefault(node), written.Length);
            }
        }
        HlslTreeNode chosen = readers
            .Where(node => CostsAnInstruction(node)
                && !roots.Contains(node)
                && !grouped.Contains(node))
            // Not one that has been named already. Counting over the instruction
            // rather than the node means naming a component does not bring its own
            // count down - every component still answers for all of them - so
            // without this the pass names the same sample round after round.
            .Where(node => !node.Outputs.Any(reader => reader is TempAssignmentNode))
            .Select(node => (Node: node, Read: WorthNaming(node, writes, text)))
            .Where(node => node.Read > 1)
            .OrderByDescending(node => node.Read)
            .Select(node => node.Node)
            .FirstOrDefault();
        return chosen == null ? null : [[chosen]];
    }

    /// <summary>
    /// Whether computing this node again is an instruction rather than free. A
    /// load, a texture read and a derivative are; an add of two registers fxc
    /// folds away, and naming one would be a line for nothing.
    /// </summary>
    private static bool CostsAnInstruction(HlslTreeNode node)
    {
        // A normalize is a nrm, or a dp3, an rsq and a mul; written twice it is
        // computed twice - fxc folded `normalize(t)` read by a mad and a cross
        // into three instructions rather than the one nrm it came from.
        return node is LoadStructuredNode
            or TextureLoadOutputNode
            or SamplePositionNode
            or NormalizeOutputNode
            or ReflectOutputNode
            or MatrixMultiplyOutputNode
            or PartialDerivativeXOperation
            or PartialDerivativeYOperation;
    }

    /// <summary>
    /// Whether a value can be given a variable of its own. An operation can, and so
    /// can the multi output nodes - a texture load, a normalize, a lit - which are
    /// not operations but are written as one call all the same. A register read and a
    /// variable already have names; a group, a phi and an assignment are not values.
    /// </summary>
    private static bool IsNameable(HlslTreeNode node)
    {
        return node is Operation
            or TextureLoadOutputNode
            or NormalizeOutputNode
            or ReflectOutputNode
            or MatrixMultiplyOutputNode
            or LitOutputNode
            or ConstantNode;
    }

    /// <summary>
    /// The subtrees one expression's text was written from, where the text repeats
    /// past the budget and the nodes do not: every occurrence of the one expression,
    /// to be named together, or null where there is none.
    /// </summary>
    private List<HlslTreeNode[]> TextRepeats(
        List<(HlslTreeNode[] Nodes, string Text)> recording,
        HashSet<HlslTreeNode> grouped,
        HashSet<HlslTreeNode> roots)
    {
        return recording
            .GroupBy(r => r.Text)
            .Select(g => (Occurrences: g.Select(r => new NodeList(Broadcast(r.Nodes)))
                    .Distinct().Select(n => n.Nodes).ToList(),
                Repeated: (g.Count() - 1) * g.Key.Length))
            .Where(r => r.Occurrences.Count > 1)
            // All of one width. The occurrences are named together, against the
            // components of the first, so a narrower one has nothing at the
            // positions the others are rewired at. Two can differ: Broadcast takes
            // an occurrence whose components are all the one value down to a single
            // node, and a scalar promoted to a vector writes the same text as the
            // vector does.
            .Where(r => r.Occurrences.All(nodes => nodes.Length == r.Occurrences[0].Length))
            .Where(r => r.Repeated >= RepeatedTextBudget)
            .Where(r => r.Occurrences.All(nodes =>
                nodes.All(n => IsNameable(n) && !roots.Contains(n) && !grouped.Contains(n))))
            .Where(r => r.Occurrences[0].Any(n => n is not ConstantNode))
            .Where(r => r.Occurrences.All(nodes =>
                nodes.Distinct(ReferenceEqualityComparer.Instance).Count() == nodes.Length))
            .OrderByDescending(r => r.Repeated)
            .Select(r => r.Occurrences)
            .FirstOrDefault();
    }

    /// <summary>
    /// The components of a value in the order the value has them, rather than in
    /// the order the first reader happened to read them. A repeat is found by its
    /// text, and the text is whatever swizzle that reader used - so the normalize
    /// under a cross product, read .yzx first, was named as `normalize(t.zyx)` and
    /// every reader of it became a permutation of the one it was. Components of one
    /// multi output node have an index apiece, and those one instruction wrote have
    /// the component it wrote them to; either says what order to put them in.
    /// </summary>
    private List<HlslTreeNode[]> InWrittenOrder(List<HlslTreeNode[]> occurrences)
    {
        HlslTreeNode[] first = occurrences[0];
        if (first.Length < 2)
        {
            return occurrences;
        }
        int[] order = null;
        // Rows of one matrix against one vector in the order the matrix has
        // them, so that the four dots read as the one mul they are. Found in
        // reader order they were `float4(row1, row0, row3, row2)`.
        int?[] rows = [.. first.Select(n => n is DotProductOperation dot
            && first.All(other => ReferenceEquals(other, n)
                || (other is DotProductOperation otherDot && _grouper.MatrixMultiplicationGrouper.AreRowsOfOneMatrix(dot, otherDot)))
            ? _grouper.MatrixMultiplicationGrouper.MatrixRowRegister(dot)
            : null)];
        if (rows.All(r => r != null) && rows.Distinct().Count() == first.Length)
        {
            order = [.. Enumerable.Range(0, first.Length).OrderBy(i => rows[i])];
        }
        else if (first.All(n => n is IHasComponentIndex)
            && first.Select(n => ((IHasComponentIndex)n).ComponentIndex).Distinct().Count() == first.Length
            && first.Select(n => n.GetType()).Distinct().Count() == 1)
        {
            order = [.. Enumerable.Range(0, first.Length)
                .OrderBy(i => ((IHasComponentIndex)first[i]).ComponentIndex)];
        }
        else if (first[0].SourceInstruction != 0
            && first.All(n => HlslTreeNode.IsSameInstruction(n, first[0]))
            && first.Select(n => n.SourceComponent).Distinct().Count() == first.Length)
        {
            order = [.. Enumerable.Range(0, first.Length).OrderBy(i => first[i].SourceComponent)];
        }
        if (order == null || order.SequenceEqual(Enumerable.Range(0, first.Length)))
        {
            return occurrences;
        }
        return [.. occurrences.Select(occurrence => order.Select(i => occurrence[i]).ToArray())];
    }

    /// <summary>
    /// Numbers the hoisted variables in the order their assignments are written.
    /// The outermost repeat is named first, and the ones inside it after, so the
    /// order they were made in is the reverse of the order they are read in.
    /// </summary>
    private static List<HlslTreeNode[]> Renumber(List<HlslTreeNode[]> assignments)
    {
        List<HlslTreeNode[]> sorted = TempAssignmentOrder.Sort(assignments);
        List<int> indices = [.. sorted
            .Select(group => ((TempAssignmentNode)group[0]).TempVariable.DeclarationIndex.Value)
            .OrderBy(index => index)];
        for (int i = 0; i < sorted.Count; i++)
        {
            foreach (TempAssignmentNode assignment in sorted[i].Cast<TempAssignmentNode>())
            {
                assignment.TempVariable.DeclarationIndex = indices[i];
            }
        }
        return sorted;
    }

    // One node read as every component of an operand - a scalar divisor under a
    // vector - is written once, and counts as the one node.
    private static HlslTreeNode[] Broadcast(HlslTreeNode[] nodes)
    {
        return nodes.All(n => SameValue(n, nodes[0])) ? [nodes[0]] : nodes;
    }

    // The same node, or two constants of the same value.
    private static bool SameValue(HlslTreeNode a, HlslTreeNode b)
    {
        return ReferenceEquals(a, b)
            || (a is ConstantNode ca && b is ConstantNode cb && ca.Value == cb.Value);
    }

    // The nodes an expression was compiled from, as a grouping key.
    private readonly struct NodeList(HlslTreeNode[] nodes) : IEquatable<NodeList>
    {
        public HlslTreeNode[] Nodes { get; } = nodes;

        public bool Equals(NodeList other)
        {
            return Nodes.Length == other.Nodes.Length
                && Nodes.Zip(other.Nodes).All(pair => SameValue(pair.First, pair.Second));
        }

        public override bool Equals(object obj)
        {
            return obj is NodeList other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (HlslTreeNode node in Nodes)
            {
                hash.Add(node is ConstantNode constant
                    ? constant.Value.GetHashCode()
                    : ReferenceEqualityComparer.Instance.GetHashCode(node));
            }
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// A variable for the nodes, typed as an integer where every one of them is,
    /// the way the finalizer types a register's: by the readers where they agree,
    /// by the operation otherwise. A vector with a float component is a float
    /// vector, whatever the 1 in its last component was.
    /// </summary>
    private TempVariableNode[] CreateTempVariables(IList<HlslTreeNode> nodes)
    {
        TempVariableNode[] variables = _compiler.CreateTempVariables(nodes.Count);
        ApplyValueTypes(variables, nodes);
        return variables;
    }

    /// <summary>
    /// Types variables from the values they will hold. Separate from creating them
    /// because a call's variables are created by how many components its element
    /// has rather than by how many nodes read it - see NameConsumes - and those
    /// need typing just the same.
    /// </summary>
    private void ApplyValueTypes(TempVariableNode[] variables, IList<HlslTreeNode> nodes)
    {
        bool isInteger = nodes.All(node => ValueTypes.IsIntegerValue(node) == true);
        bool isBits = nodes.All(node => ValueTypes.IsBitsVariable(node, isInteger));
        // A double where every component is one, the way an integer is. The value is
        // not asked - a multiply of two doubles is the same node either way - so what
        // answers is the record the parser kept of what the instructions wrote.
        bool isDouble = !isInteger && nodes.All(_doubleValues.Contains);
        // Unsigned only where every component is, and not for bits: those are a
        // float's, and calling them uint says something about them that is not so.
        bool isUnsigned = isInteger && !isBits
            && nodes.All(node => ValueTypes.IsUnsignedValue(node) == true);
        foreach (TempVariableNode variable in variables)
        {
            variable.IsInteger = isInteger;
            variable.IsBits = isBits;
            variable.IsUnsigned = isUnsigned;
            variable.IsDouble = isDouble;
        }
    }

    /// <summary>
    /// Names every resinfo result reachable from here, the components of one call
    /// together, as one variable per call: GetDimensions writes a whole set of out
    /// parameters, and its components are read out of the variable afterwards.
    /// </summary>
    private List<HlslTreeNode[]> NameResourceInfo(IList<HlslTreeNode> order)
    {
        var assignments = new List<HlslTreeNode[]>();
        var named = HlslTreeNode.NewNodeSet();
        foreach (ResourceInfoNode info in order.OfType<ResourceInfoNode>())
        {
            if (named.Contains(info) || info.NamedAs != null)
            {
                continue;
            }
            List<ResourceInfoNode> call = [.. order.OfType<ResourceInfoNode>()
                .Where(other => other.NamedAs == null && IsSameResourceInfoCall(info, other))
                .OrderBy(other => other.InfoComponent)];
            foreach (ResourceInfoNode component in call)
            {
                named.Add(component);
            }

            // Which GetDimensions overload the call was, which decides how wide the
            // variable is that its out parameters are written into. The no-mip form
            // reports as many components as the shape has: a 1D its width alone, a
            // 2D and a cube width and height, and an array of 2D, a cube array and a
            // 3D the element count or the depth after those. Anything the shader
            // read past them - the mip count always, the depth of a shape that has
            // none - has no no-mip spelling, and is what the mip form is for, where
            // a 4-wide variable has the component either way.
            ResourceDimension? dimension = _registers
                .GetTextureDefinition(info.Resource.RegisterComponentKey.RegisterKey)?.Dimension;
            bool hasDepth = dimension is ResourceDimension.Texture2DArray
                or ResourceDimension.TextureCubeArray or ResourceDimension.Texture3D;
            bool is1D = dimension == ResourceDimension.Texture1D;
            int noMipComponents = is1D ? 1 : hasDepth ? 3 : 2;
            bool mipForm = !IsConstantZero(info.MipLevel)
                || call.Any(c => c.InfoComponent >= noMipComponents);
            // A multisampled texture's overload is three wide - width, height and
            // the sample count - and has no mip level to ask about.
            ResourceInfoNode sampleCount = call.FirstOrDefault(c => c.IsSampleCount);
            TempVariableNode[] variables = _compiler.CreateTempVariables(
                info.IsBuffer ? (info.ReportsStride ? 2 : 1)
                : sampleCount != null ? sampleCount.SampleCountComponent + 1
                // A multisampled texture whose count is a constant asks for no
                // sample count and takes the overload with one all the same: width,
                // height and the count, with the element count between them for an
                // array.
                : dimension == ResourceDimension.Texture2DmsArray ? 4
                : dimension == ResourceDimension.Texture2Dms ? 3
                : mipForm ? 4
                : noMipComponents);
            foreach (TempVariableNode variable in variables)
            {
                variable.IsInteger = info.ReturnType == D3D10ResInfoReturnType.Uint;
            }
            assignments.Add([.. call.Select(component =>
            {
                TempVariableNode variable = variables[component.InfoComponent];
                component.NamedAs = variable;
                return (HlslTreeNode)NameSubexpression(component, variable);
            })]);
        }
        return assignments;
    }

    /// <summary>
    /// Names every Consume reachable from here, the components of one call
    /// together. A consume buffer has the one method and it reads the whole
    /// element, so the call is named and its components read out of the variable -
    /// the same shape a GetDimensions takes, for the same reason.
    /// </summary>
    private List<HlslTreeNode[]> NameConsumes(IList<HlslTreeNode> order)
    {
        var assignments = new List<HlslTreeNode[]>();
        var named = HlslTreeNode.NewNodeSet();
        // In the order the buffer was consumed, which is the order the instructions
        // did it in and nothing else. Two calls are independent in the graph - neither
        // reads what the other left - so the walk reaches them in whatever order it
        // reaches them, and naming them in that order wrote the second call's variable
        // first. Both calls say `queue.Consume()`, so the text does not show it: what
        // shows is that the components come out of the wrong one, and each call takes a
        // different element off the counter.
        foreach (ConsumeNode consume in order.OfType<ConsumeNode>()
            .OrderBy(candidate => candidate.Slot.SourceInstruction))
        {
            if (named.Contains(consume) || consume.NamedAs != null)
            {
                continue;
            }
            // One call is one slot: the loads that read what a single
            // imm_atomic_consume took.
            List<ConsumeNode> call = [.. order.OfType<ConsumeNode>()
                .Where(other => other.NamedAs == null
                    && ReferenceEquals(other.Slot, consume.Slot))
                .OrderBy(other => other.ComponentIndex)];
            foreach (ConsumeNode component in call)
            {
                named.Add(component);
            }
            // The whole element, not the components this statement happens to read:
            // the hoist runs once per statement, and a call whose components are
            // read by two of them would otherwise be named twice and consumed twice.
            // The variables are remembered by the slot so the second statement finds
            // the first one's.
            if (!_consumeVariables.TryGetValue(consume.Slot, out TempVariableNode[] variables))
            {
                variables = _compiler.CreateTempVariables(
                    _registers.GetStructuredBufferComponents(
                        consume.Buffer.RegisterComponentKey.RegisterKey));
                // Typed from the components the call reads. Created by how many the
                // element has and nothing else, they were typed by nothing at all, so
                // a uint element whose bits are a float's came out of a variable that
                // said neither - and every float reader of it converted the bits into
                // the number they spell instead of reinterpreting them.
                ApplyValueTypes(variables, [.. call]);
                _consumeVariables[consume.Slot] = variables;
                // Only the statement that names it first writes the call.
                assignments.Add([.. call.Select(component =>
                {
                    TempVariableNode variable = variables[component.ComponentIndex];
                    component.NamedAs = variable;
                    return (HlslTreeNode)NameSubexpression(component, variable);
                })]);
                continue;
            }
            foreach (ConsumeNode component in call)
            {
                component.NamedAs = variables[component.ComponentIndex];
            }
        }
        return assignments;
    }

    /// <summary>
    /// Names every double taken apart into its two words, the two words of one
    /// double together. `asuint` over a double hands them back through out
    /// parameters - there is no expression for one word of it - so the call is named
    /// and the words are read out of the variable, the same shape a GetDimensions
    /// takes and for the same reason.
    /// </summary>
    private List<HlslTreeNode[]> NameDoubleBits(IList<HlslTreeNode> order)
    {
        var assignments = new List<HlslTreeNode[]>();
        var named = HlslTreeNode.NewNodeSet();
        foreach (DoubleBitsNode bits in order.OfType<DoubleBitsNode>())
        {
            if (named.Contains(bits) || bits.NamedAs != null)
            {
                continue;
            }
            // One call is one double: both words come out of the same asuint.
            List<DoubleBitsNode> call = [.. order.OfType<DoubleBitsNode>()
                .Where(other => other.NamedAs == null
                    && ReferenceEquals(other.Value, bits.Value))
                .OrderBy(other => other.ComponentIndex)];
            foreach (DoubleBitsNode component in call)
            {
                named.Add(component);
            }
            // Both words, not the ones this statement happens to read: the call
            // fills a pair of out parameters whether or not the shader stored both,
            // and a one wide variable has nowhere to put the other. The variables
            // are remembered by the double so a second statement finds the first
            // one's rather than calling again.
            if (!_doubleBitsVariables.TryGetValue(bits.Value, out TempVariableNode[] variables))
            {
                variables = _compiler.CreateTempVariables(2);
                foreach (TempVariableNode variable in variables)
                {
                    // asuint's out parameters are uints, and these are a double's
                    // bits rather than a number - which is what a float reader of
                    // one has to be told, so that it reinterprets.
                    variable.IsInteger = true;
                    variable.IsUnsigned = true;
                    variable.IsBits = true;
                }
                _doubleBitsVariables[bits.Value] = variables;
                assignments.Add([.. call.Select(component =>
                {
                    TempVariableNode variable = variables[component.ComponentIndex];
                    component.NamedAs = variable;
                    return (HlslTreeNode)NameSubexpression(component, variable);
                })]);
                continue;
            }
            foreach (DoubleBitsNode component in call)
            {
                component.NamedAs = variables[component.ComponentIndex];
            }
        }
        return assignments;
    }

    private static bool IsSameResourceInfoCall(ResourceInfoNode a, ResourceInfoNode b)
    {
        // One GetDimensions on a multisampled texture becomes a resinfo and a
        // sampleinfo, and fxc picks their return types separately - it took the
        // size as uints and the count as a float in the same call. So the types
        // have to agree only between measurements of the same kind.
        return (a.ReturnType == b.ReturnType || a.IsSampleCount != b.IsSampleCount)
            && a.Resource.RegisterComponentKey.RegisterKey.Equals(b.Resource.RegisterComponentKey.RegisterKey)
            && (ReferenceEquals(a.MipLevel, b.MipLevel)
                || (a.MipLevel is ConstantNode ca && b.MipLevel is ConstantNode cb && ca.Value == cb.Value));
    }

    private static bool IsConstantZero(HlslTreeNode node)
    {
        return node is ConstantNode constant && constant.Value == 0;
    }

    /// <summary>
    /// Names the candidates, the components of one operation together.
    ///
    /// A value graph holds a four wide instruction as four separate nodes and puts
    /// them back together when an assignment is compiled. Naming each of them on its
    /// own happens before that and defeats it: four scalars that were one mad, and
    /// nothing downstream can tell they belong together any more.
    /// </summary>
    private List<HlslTreeNode[]> NameCandidates(List<HlslTreeNode> candidates,
        HashSet<HlslTreeNode> reachable, HashSet<HlslTreeNode> roots)
    {
        var nodeGrouper = new NodeGrouper(_registers);
        var assignments = new List<HlslTreeNode[]>();
        var named = HlslTreeNode.NewNodeSet();
        foreach (HlslTreeNode candidate in candidates)
        {
            if (!named.Add(candidate))
            {
                continue;
            }

            List<HlslTreeNode> group = [candidate];
            foreach (HlslTreeNode other in candidates)
            {
                if (group.Count == 4 || named.Contains(other))
                {
                    continue;
                }
                // Against the first, which is how GroupComponents reads a run too -
                // or where one instruction made both, which is the same question
                // answered by the bytecode rather than by the shape of the two
                // values. A mad over two components of a register is one
                // instruction however differently its operands read, and hoisting
                // used to lose that: the two halves of gbuffer_decode's normal are
                // scaled by one mad and were named apart, which left everything
                // downstream of them scalar.
                if (nodeGrouper.CanGroupComponents(candidate, other)
                    || HlslTreeNode.IsSameInstruction(candidate, other))
                {
                    group.Add(other);
                    named.Add(other);
                }
            }

            // And the instruction's components that are not candidates themselves.
            // skinned_terrain blends the fourth bone into all four components of
            // the position with one mad, and the height is added to its y: x, z
            // and w were read twice and named, the y once, inside the height's
            // expression, so it was blended there again and fxc blended twice.
            // A lane of the same variable, the height reads t2.y. Only where the
            // components are one vector expression, as ScatteredInstruction asks:
            // fxc packs unrelated scalars into the free lanes of one instruction,
            // and gathered, microfacet_lighting's `8 * t6` and `0.5 * t7` were
            // `float2(8, 0.5) * float2(t6, t7)`.
            if (group.All(node => node is Operation && HlslTreeNode.IsSameInstruction(node, candidate))
                && group.Select(node => node.SourceComponent).Distinct().Count() == group.Count)
            {
                List<HlslTreeNode> widened = [.. group];
                foreach (HlslTreeNode other in reachable)
                {
                    if (widened.Count < 4
                        && other is Operation
                        && HlslTreeNode.IsSameInstruction(other, candidate)
                        && !named.Contains(other)
                        && !roots.Contains(other)
                        && !widened.Any(node => node.SourceComponent == other.SourceComponent))
                    {
                        widened.Add(other);
                    }
                }
                if (widened.Count > group.Count)
                {
                    HlslTreeNode[] ordered = [.. widened.OrderBy(node => node.SourceComponent)];
                    string together = _compiler.Measure([ordered]).Recording[^1].Text;
                    if (!System.Text.RegularExpressions.Regex.IsMatch(together, @"\b(float|int|uint|half|double|bool)[234]\(")
                        && ReadsWholeVectors(together))
                    {
                        foreach (HlslTreeNode other in widened.Skip(group.Count))
                        {
                            named.Add(other);
                        }
                        group = widened;
                    }
                }
            }

            // In the order the value has them, not the order they were found in:
            // the candidates are collected from the graph, which is walked from the
            // last component back, and a variable named backwards is read by every
            // swizzle reversed.
            group = [.. InWrittenOrder([[.. group]])[0]];
            // Rows of a matrix multiply take the multiply's other rows with them, as
            // the text pass's candidates do - see WithSiblingRows.
            if (WithSiblingRows([.. group], reachable, roots) is HlslTreeNode[] rows
                && rows.All(row => !named.Contains(row) || group.Contains(row, ReferenceEqualityComparer.Instance)))
            {
                group = [.. rows];
                foreach (HlslTreeNode row in rows)
                {
                    named.Add(row);
                }
            }
            TempVariableNode[] variables = CreateTempVariables(group);
            assignments.Add([.. group.Select((node, i) => (HlslTreeNode)NameSubexpression(node, variables[i]))]);
        }
        return assignments;
    }

    // The node count of the expression as it would be written out, where a node read
    // twice counts twice. Memoised over the shared graph, so measuring the explosion
    // does not take exponential time itself.
    private static int CountReachable(HlslTreeNode node)
    {
        var seen = HlslTreeNode.NewNodeSet();
        var stack = new Stack<HlslTreeNode>();
        stack.Push(node);
        int count = 0;
        while (stack.Count != 0)
        {
            HlslTreeNode current = stack.Pop();
            if (!seen.Add(current))
            {
                continue;
            }
            count++;
            foreach (HlslTreeNode input in HlslTreeNode.TraversableInputs(current))
            {
                stack.Push(input);
            }
        }
        return count;
    }

    private static TempAssignmentNode NameSubexpression(HlslTreeNode node, TempVariableNode variable)
    {
        Rewire(node, variable);
        return new TempAssignmentNode(variable, node);
    }

    // Reads the variable where the node was read - by every reader, or by the
    // readers among the given ones.
    private static void Rewire(HlslTreeNode node, TempVariableNode variable, HashSet<HlslTreeNode> among = null)
    {
        HlslTreeNode[] readers = node.Outputs.ToArray();
        foreach (HlslTreeNode reader in readers)
        {
            if (among != null && !among.Contains(reader))
            {
                continue;
            }
            for (int i = 0; i < reader.Inputs.Count; i++)
            {
                if (ReferenceEquals(reader.Inputs[i], node))
                {
                    reader.Inputs[i] = variable;
                    variable.Outputs.Add(reader);
                }
            }
            node.Outputs.Remove(reader);
        }
    }

    /// <summary>
    /// The components of each output, keyed by the first of them. By the
    /// declaration rather than by the register: fxc packs two outputs into one -
    /// TEXCOORD0 at o1.xy and TEXCOORD1 at o1.z - and one assignment covering both
    /// writes the second into the first and leaves the second unwritten.
    /// </summary>
    private Dictionary<RegisterComponentKey, HlslTreeNode[]> GroupComponents(
        IEnumerable<KeyValuePair<RegisterComponentKey, HlslTreeNode>> outputsByComponent)
    {
        return GroupComponentsWithComponents(outputsByComponent)
            .ToDictionary(o => o.Key, o => o.Value.Nodes);
    }

    private Dictionary<RegisterComponentKey, int[]> GroupComponentMasks(
        IEnumerable<KeyValuePair<RegisterComponentKey, HlslTreeNode>> outputsByComponent)
    {
        return GroupComponentsWithComponents(outputsByComponent)
            .ToDictionary(o => o.Key, o => o.Value.Components);
    }

    private Dictionary<RegisterComponentKey, (int[] Components, HlslTreeNode[] Nodes)>
        GroupComponentsWithComponents(
            IEnumerable<KeyValuePair<RegisterComponentKey, HlslTreeNode>> outputsByComponent)
    {
        return outputsByComponent
            .GroupBy(o => (o.Key.RegisterKey, _registers.GetOutputDeclaration(o.Key).Semantic))
            .ToDictionary(
                o => o.OrderBy(c => c.Key.ComponentIndex).First().Key,
                o => (
                    o.OrderBy(c => c.Key.ComponentIndex).Select(c => c.Key.ComponentIndex).ToArray(),
                    o.OrderBy(c => c.Key.ComponentIndex).Select(c => c.Value).ToArray()));
    }
}