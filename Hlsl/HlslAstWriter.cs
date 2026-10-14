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

    // Declaration numbers taken so far, whatever nodes stand under them.
    private readonly HashSet<int> _declaredIndices = [];
    // Variables each Consume call was named into, keyed by its slot.
    private readonly Dictionary<HlslTreeNode, TempVariableNode[]> _consumeVariables =
        new(ReferenceEqualityComparer.Instance);
    // Variables each split double was named into, so both words share one call.
    private readonly Dictionary<HlslTreeNode, TempVariableNode[]> _doubleBitsVariables =
        new(ReferenceEqualityComparer.Instance);

    // Set while writing a linkage body: a return hands back this register.
    private RegisterKey _linkageReturnRegister;

    // Statements of the function being written (main or one linkage method).
    private IList<IStatement> _functionStatements;
    private VariableLiveness _liveness;

    public HlslAstWriter(ShaderModel shader)
        : base(shader)
    {
    }

    protected override void WriteMethodBody()
    {
        // A hull shader writes two functions through one writer; names are per function.
        _declaredVariables.Clear();
        _declaredIndices.Clear();
        _everDeclaredVariables.Clear();
        _consumeVariables.Clear();
        _doubleBitsVariables.Clear();
        _loopDepth = 0;

        if (HasOutputStruct)
        {
            // One vertex variable per stream: each stream has its own struct.
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
    /// Writes one function (main or a linkage body) with the same lowering and folds.
    /// A body returns <paramref name="returnRegister"/> instead of the outputs.
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
        _compiler = new NodeCompiler(_registers, _doubleValues)
        {
            StructuredElementTypeName = resource => resource.ElementType?.MemberInfo is { Count: > 0 }
                ? GetStructuredElementTypeName(resource)
                : null,
        };
        _grouper = new NodeGrouper(_registers);
        _templateMatcher = new TemplateMatcher(_grouper) { DoubleValues = _doubleValues };

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

        // A fold can empty a statement above the one it fires in, so all folds run
        // and unread names go before any line is written.
        TempResolver resolver = TempResolver.Build(statements);
        _templateMatcher.TempResolver = resolver;
        ReduceAll(statements);
        resolver.RemoveUnreadAssignments(statements);

        _linkageReturnRegister = returnRegister;
        WriteStatements(statements);
        _linkageReturnRegister = null;
    }

    /// <summary>
    /// Writes a dynamic linkage body, returning the one register it writes.
    /// </summary>
    protected override void WriteLinkageMethodBody(LinkageModel.FunctionBodyInfo body)
    {
        WriteFunctionStatements(
            _ast.LinkageBodies.First(entry => ReferenceEquals(entry.Body, body)).Statements,
            hasReturnValue: true, hasOutputStruct: false, body.ReturnRegister);
    }

    /// <summary>
    /// Reduces every value the way writing will, in place, ahead of writing. Only
    /// values writing reduces before grouping: the rest must reach grouping unfolded.
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
    /// Held values writing reduces. A loop's clauses only count where it prints
    /// as a `for`; otherwise they are never written.
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
    /// Temps an assignment or return assigns (not ones carried forward). Output
    /// registers are excluded: they are grouped as instructions and reduced after.
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
    /// Variables some assignment declares anywhere in the function. The declaring
    /// assignment can be inlined away, so a reassignment of a variable nothing
    /// declares becomes its declaration.
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
    /// Whether every write of the variable is partial precision, so it can be
    /// declared half. All writes count: fxc mixes _pp and full writes to a register.
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
        // Declared above this block or under this name already: a reassignment.
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
        // A variable assigned a part at a time is declared once, whole, then each
        // part assigned by mask. Not out-parameter calls, which fill it at once.
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

    // Other parts of a variable declared whole become reassignments everywhere.
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
                    // Also outputs the finalizer gave a variable (assigned in if branches).
                    if (!o.Key.RegisterKey.IsTempRegister
                        && !(o.Key.RegisterKey.IsOutput && o.Value is TempAssignmentNode))
                    {
                        return false;
                    }
                    if (statement.Inputs.TryGetValue(o.Key, out var inputNode) && o.Value == inputNode)
                    {
                        return false;
                    }
                    // A variable the register merely holds (e.g. an atomic's old value).
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

        // With one output register there is no struct: the return writes the value.
        // A geometry shader still writes its struct, which it appends.
        if (_shader.Type != ShaderType.Geometry && _registers.MethodOutputRegisters.Count <= 1)
        {
            foreach (var group in tempGroups)
            {
                WriteLine(CompileAssignment(group));
            }
            return;
        }

        // Skip outputs carried forward unchanged, as for temps.
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

        // Subexpressions shared by outputs and temps are named here, as a return
        // does: a geometry shader writes its vertex here, never through a return.
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

        // Temps, outputs and hoisted subexpressions are sorted together: an output
        // reading a temp's new value goes after it, one reading the old value
        // before (recorded at lowering), and a hoist may read a temp declared here.
        var writes = new List<(HlslTreeNode[] Nodes, TempAssignmentNode[] Wants, Action Write)>();
        foreach (var (nodes, text) in hoisted)
        {
            writes.Add((nodes, [], () => WriteLine(text)));
        }
        foreach (var group in tempGroups)
        {
            writes.Add((group, [], () => WriteLine(CompileAssignment(group))));
        }
        foreach (var rootGroup in outputs.OrderBy(o => OutputOrder(o.Key)).ThenBy(o => o.Key.ComponentIndex))
        {
            RegisterDeclaration outputRegister = _registers.GetOutputDeclaration(rootGroup.Key);
            HlslTreeNode[] nodes = rootGroup.Value;
            TempAssignmentNode[] wants = [.. assignmentStatement.OutputDependsOnNewValueOf
                .Where(o => o.Key.RegisterKey.Equals(rootGroup.Key.RegisterKey))
                .SelectMany(o => o.Value)
                .Distinct()];
            string outputVariable = _registers.StreamVariableName(
                (rootGroup.Key.RegisterKey as D3D10RegisterKey)?.Stream);
            // A partial write needs its mask: a scalar assigned to the member broadcasts.
            string componentMask = ComponentMask(outputComponents[rootGroup.Key], outputRegister);
            writes.Add((nodes, wants, () => WriteLine($"{outputVariable}.{outputRegister.Name}{componentMask} = {CompileOutput(rootGroup.Key.RegisterKey, nodes)};")));
        }
        foreach (var write in TempAssignmentOrder.Sort(writes, w => w.Nodes, w => w.Wants))
        {
            write.Write();
        }
    }

    private void WriteBufferAppendStatement(BufferAppendStatement append)
    {
        string destination = _registers.GetRegisterName(
            ((RegisterInputNode)append.Destination).RegisterComponentKey.RegisterKey);
        HlslTreeNode[] values = [.. append.Values.Select(Reduce)];
        WriteSharedSubexpressions([values]);
        RegisterKey appendKey = ((RegisterInputNode)append.Destination)
            .RegisterComponentKey.RegisterKey;
        // A struct element is built member by member, then appended: a float4 is
        // not the struct (X3017).
        ResourceDefinition appended = _registers.ResourceDefinitions?.FirstOrDefault(d =>
            d.ShaderInputType == D3DShaderInputType.UavAppendStructured && d.BindPoint == appendKey.Number);
        if (appended?.ElementType?.MemberInfo is { Count: > 0 })
        {
            TempVariableNode element = _compiler.CreateTempVariables(1)[0];
            string elementName = $"{_registers.TemporaryPrefix}{element.DeclarationIndex}";
            IList<(string Name, int[] Values)> runs = _registers.FindStructuredMemberRuns(
                appendKey, elementName, 0, [.. Enumerable.Range(0, values.Length)]);
            if (runs != null)
            {
                WriteLine($"{GetStructuredElementTypeName(appended)} {elementName};");
                foreach ((string name, int[] members) in runs)
                {
                    string memberValue = CompileStoredValue(
                        [.. members.Select(i => values[i])],
                        _registers.IsIntegerStructuredMember(appendKey, members[0]));
                    WriteLine($"{name} = {memberValue};");
                }
                WriteLine($"{destination}.Append({elementName});");
                return;
            }
        }
        string value = CompileStoredValue(
            values, _registers.IsIntegerStructuredMember(appendKey, 0));
        WriteLine($"{destination}.Append({value});");
    }

    // `destination[coordinate] = value;`
    private void WriteStoreTypedStatement(StoreTypedStatement storeTyped)
    {
        string destination = _registers.GetRegisterName(
            ((RegisterInputNode)storeTyped.Destination).RegisterComponentKey.RegisterKey);
        HlslTreeNode[] coordinates = [.. storeTyped.Coordinates.Select(Reduce)];
        HlslTreeNode[] values = [.. storeTyped.Values.Select(Reduce)];
        WriteSharedSubexpressions([values, coordinates]);
        string coordinate = _compiler.CompileAsInteger(coordinates);
        RegisterKey typedKey = ((RegisterInputNode)storeTyped.Destination)
            .RegisterComponentKey.RegisterKey;
        string value = CompileStoredValue(
            values, _registers.GetTextureDefinition(typedKey)?.IsIntegerReturnType);
        WriteLine($"{destination}[{coordinate}] = {value};");
    }

    /// <summary>
    /// The value a store writes. A store copies bits, so a float into integers
    /// goes in as asuint(f), not converted. Null means the destination type is unknown.
    /// </summary>
    private string CompileStoredValue(HlslTreeNode[] values, bool? destinationHoldsIntegers)
    {
        if (values.All(v => ValueTypes.IsIntegerValue(v) == true))
        {
            // Into floats, an integer variable or raw-load bits go in as asfloat.
            return destinationHoldsIntegers == false
                && values.All(v => ValueTypes.IsReinterpretedAsFloat(v)
                    || v is TempVariableNode { IsInteger: true, IsBool: false, IsBits: false })
                ? $"asfloat({_compiler.CompileAsInteger(values)})"
                : _compiler.CompileAsInteger(values);
        }
        // asuint only where the graph says float; an unknown value is left as is.
        // Into floats the store is a float context, so bare bits are reinterpreted.
        // Into integers, an untyped select is compiled as an integer select.
        return destinationHoldsIntegers == true
            && values.Any(v => ValueTypes.IsIntegerValue(v) == false)
            ? CompileRawStoredValue(values)
            : destinationHoldsIntegers == false
                ? _compiler.CompileAsFloat(values)
                : destinationHoldsIntegers == true && values.Any(v => v is MoveConditionalOperation)
                    ? _compiler.CompileAsInteger(values)
                    : _compiler.Compile(values);
    }

    // Raw store dwords: integers as is, floats as their bits.
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
        RegisterKey destinationKey =
            ((RegisterInputNode)storeStructured.Destination).RegisterComponentKey.RegisterKey;
        string compiledDestination = _registers.GetRegisterName(destinationKey);
        HlslTreeNode address = Reduce(storeStructured.Address);
        HlslTreeNode[] storedValues = [.. storeStructured.Values.Select(Reduce)];
        // Stores bypass GroupAssignments, so they hoist here (e.g. a GetDimensions
        // result, which cannot be an expression).
        WriteSharedSubexpressions([storedValues, [address]]);
        string compiledAddress = _compiler.Compile(address);
        bool storesIntegers = storedValues.All(v => ValueTypes.IsIntegerValue(v) == true);
        string compiledValue = storesIntegers
            ? _compiler.CompileAsInteger(storedValues)
            : _compiler.Compile(storedValues);
        if (storeStructured.IsGroupShared)
        {
            // Raw groupshared memory stores bits, like any raw store.
            if (!storesIntegers)
            {
                compiledValue = CompileRawStoredValue(storedValues);
            }
            WriteLine($"{_registers.GroupSharedElementReference(destinationKey, compiledDestination, compiledAddress)} = {compiledValue};");
            return;
        }
        if (storeStructured.IsRaw)
        {
            // StoreN by dword count; floats go in as asuint, not converted.
            string method = storeStructured.Values.Length == 1 ? "Store" : $"Store{storeStructured.Values.Length}";
            if (!storesIntegers)
            {
                compiledValue = CompileRawStoredValue(storedValues);
            }
            WriteLine($"{compiledDestination}.{method}({compiledAddress}, {compiledValue});");
            return;
        }
        // A struct element is written a member at a time: one store can span members.
        RegisterKey bufferKey = ((RegisterInputNode)storeStructured.Destination).RegisterComponentKey.RegisterKey;
        // Groupshared memory has no reflection; the integer analysis decides it.
        bool? holdsIntegers = bufferKey is D3D10RegisterKey { OperandType: OperandType.ThreadGroupSharedMemory }
            ? CreateIntegerOperandAnalysis().IsIntegerThreadGroupSharedMemory(bufferKey.Number)
            : _registers.IsIntegerStructuredMember(bufferKey, storeStructured.ElementByteOffset);
        compiledValue = CompileStoredValue(storedValues, holdsIntegers);
        IList<(string Name, int[] Values)> runs = _registers.FindStructuredMemberRuns(
            bufferKey, $"{compiledDestination}[{compiledAddress}]",
            storeStructured.ElementByteOffset, storeStructured.Components);
        if (runs != null)
        {
            foreach ((string name, int[] values) in runs)
            {
                string run = storesIntegers
                    ? _compiler.CompileAsInteger(values.Select(v => storedValues[v]))
                    : holdsIntegers == false
                        ? _compiler.CompileAsFloat(values.Select(v => storedValues[v]))
                        : _compiler.Compile(values.Select(v => storedValues[v]));
                WriteLine($"{name} = {run};");
            }
            return;
        }
        // Groupshared memory wider than a register is a struct; the offset picks the member.
        string sharedMember = ThreadGroupSharedMemberName(bufferKey,
            storeStructured.ElementByteOffset);
        WriteLine($"{compiledDestination}[{compiledAddress}]{sharedMember} = {compiledValue};");
    }

    /// <summary>
    /// The `.mN` of a groupshared store, or "" where the element fits one register.
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
        HlslTreeNode[] coordinates = atomic.Coordinates == null
            ? null
            : [.. atomic.Coordinates.Select(Reduce)];
        HlslTreeNode addressNode = coordinates != null ? null : Reduce(atomic.Address);
        HlslTreeNode valueNode = Reduce(atomic.Value);
        HlslTreeNode compareNode = atomic.Compare == null ? null : Reduce(atomic.Compare);
        // Hoisted here like a store's: e.g. a Consume result cannot be an expression.
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
        // Interlocked operations take a float's bits, unsigned where the destination is.
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

        // The old-value variable is declared before the call; it is not an assignment.
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

        // Byte address buffers have Interlocked* methods taking the byte offset; all
        // else uses free functions over the element or texel.
        if (_registers.IsRawResource(resourceKey))
        {
            WriteLine($"{resource}.{atomic.MethodName}({address}, {arguments});");
            return;
        }

        string element = _registers.GroupSharedElementReference(resourceKey, resource, address);
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
        // fxc movs the offset into a register first, so look through moves.
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
            // Through CompileAssignment so the header declares the counter only if
            // nothing above does; a second declaration shadows it (X3078).
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
            // A break at the top as the while condition recompiles to one breakc,
            // not if/break/endif.
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
    /// Finds a break opening the loop body that can become the loop's condition.
    /// Any real code before the break must run first, so it rules this out.
    /// </summary>
    private static bool TryGetExitTest(
        LoopStatement loop, out HlslTreeNode continueCondition, out IStatement exitTest)
    {
        continueCondition = null;
        exitTest = null;
        foreach (IStatement statement in loop.Body)
        {
            // Skip statements that write nothing: phis and values read inline later.
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
    /// Whether fxc needs [loop] to compile it: a gradient operation in a loop it
    /// cannot count is an error otherwise.
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

    // A derivative, or a sample that takes its mip level from derivatives.
    private static bool IsGradientOperation(HlslTreeNode node)
    {
        return node is PartialDerivativeXOperation or PartialDerivativeYOperation
            || (node is TextureLoadOutputNode sample
                && (sample.Controls & (TextureLoadControls.Lod | TextureLoadControls.Grad
                    | TextureLoadControls.LevelZero | TextureLoadControls.Gather)) == 0);
    }

    // Counters must not shadow an enclosing counter or the input struct `i`;
    // the counter is renamed, not the struct.
    private string GetLoopVariableName(int depth)
    {
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
        // An empty if side with an else is written as the inverted if.
        if (trueBody.Count == 0 && falseBody != null && Inverted(comparison) is HlslTreeNode[] opposite)
        {
            comparison = opposite;
            trueBody = falseBody;
            falseBody = null;
        }

        // A D3D9 `if_<rel>` repeats one test per component; collapse identical ones
        // to a scalar (a `(half4)` comparison would be X3019).
        comparison = [.. comparison.Select(c => ReadAssignedFlag(ifStatement, c))];
        var tested = comparison.Select(Reduce).ToList();
        if (tested.Count > 1
            && tested.All(c => NodeGrouper.AreNodesEquivalent(c, tested[0])))
        {
            tested = [tested[0]];
        }
        // Name the condition's repeated operands above the if. The predicate itself
        // stays unnamed (see IStatement.NamedHeldNodes); its operands cost nothing.
        WriteSharedSubexpressions([[.. tested]]);
        if (WritesFlowAttributes)
        {
            WriteLine("[branch]");
        }
        string condition = _compiler.Compile(tested);
        // An if that only returns is written `if (c) return a;`. The body goes to a
        // buffer first to see if it is one line, so it is compiled once.
        if (falseBody == null && trueBody.Count == 1 && trueBody[0] is ReturnStatement { Comparison: null })
        {
            System.IO.TextWriter outer = internalWriter;
            string outerIndent = indent;
            var buffer = new System.IO.StringWriter();
            internalWriter = buffer;
            indent = "";
            try
            {
                WriteStatements(trueBody);
            }
            finally
            {
                internalWriter = outer;
                indent = outerIndent;
            }
            string[] lines = buffer.ToString().Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
            if (lines.Length == 1 && lines[0].StartsWith("return"))
            {
                WriteLine($"if ({condition}) {lines[0]}");
                return;
            }
            WriteLine($"if ({condition}) {{");
            foreach (string line in lines)
            {
                if (line.Length == 0)
                {
                    WriteLine();
                }
                else
                {
                    WriteLine("\t" + line);
                }
            }
            WriteLine("}");
            return;
        }
        WriteLine($"if ({condition}) {{");
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
    /// Tests a flag the previous statement just assigned through its variable, but
    /// only if the variable is live after the if; otherwise re-testing is cheaper.
    /// Only the statement right before, so nothing reassigns it in between.
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

    // Either way round: `a <= b` is the same as `b >= a`.
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
    /// The per-component inverse, or null where any component has none.
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
    /// Declares the variables a block assigns above it, so they outlive its braces.
    /// </summary>
    private void WriteBlockTempVariables(
        IDictionary<RegisterComponentKey, HlslTreeNode> outputs,
        IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
    {
        // Not variables the block was handed; a register reused for a fresh one is new.
        var newAssignments = outputs
            .Where(o => o.Value is TempVariableNode)
            .Where(o => !(inputs.TryGetValue(o.Key, out HlslTreeNode input) && HandsVariable(input, o.Value)))
            .ToDictionary();
        if (newAssignments.Count > 0)
        {
            foreach (var group in GroupAssignments(newAssignments))
            {
                _compiler.Compile(group);

                var variable = group.First() as TempVariableNode;
                // An enclosing block may declare it already; check by number too, as
                // different nodes can share one name.
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

    // Whether the input is the variable itself, its assignment, or a phi over it.
    private static bool HandsVariable(HlslTreeNode input, HlslTreeNode variable)
    {
        return ReferenceEquals(input, variable)
            || (input is TempAssignmentNode assignment && ReferenceEquals(assignment.TempVariable, variable))
            || (input is PhiNode phi && phi.Inputs.Any(i => HandsVariable(i, variable)));
    }

    /// <summary>
    /// A linkage method's return of the one register the body writes.
    /// </summary>
    private void WriteLinkageReturn(ReturnStatement returnStatement)
    {
        // Temps first, except the result register, which the return names itself.
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

    // An assignment returns as its variable; an expression is returned inline.
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
        // A return can stand in for an assignment statement; write its temps first.
        WriteStatementTempAssignments(returnStatement);

        // With an output struct, write only outputs changed since the last write.
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

        // Returns bypass GroupAssignments, so hoist here and take the groups back.
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

        // Compute and geometry shaders return void (`return o;` in a GS is X3079).
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
            // Earlier statements wrote the outputs; this only decides to leave.
            WriteLine($"if ({condition}) return {_registers.OutputVariableName};");
        }
        else
        {
            foreach (var rootGroup in TempAssignmentOrder.Sort(
                outputs.OrderBy(o => OutputOrder(o.Key)).ThenBy(o => o.Key.ComponentIndex),
                o => o.Value))
            {
                RegisterDeclaration outputRegister = _registers.GetOutputDeclaration(rootGroup.Key);
                string componentMask = ComponentMask(outputComponents[rootGroup.Key], outputRegister);
                string compiled = CompileOutput(rootGroup.Key.RegisterKey, rootGroup.Value);
                WriteLine($"{_registers.OutputVariableName}.{outputRegister.Name}{componentMask} = {compiled};");
            }
            WriteLine();
            WriteLine($"return {_registers.OutputVariableName};");
        }
    }

    // Compiled in the output's signature type. An integer output must build an intN,
    // not a floatN: int-to-float-to-int is lossy for large values.
    private string CompileOutput(RegisterKey outputKey, IEnumerable<HlslTreeNode> nodes)
    {
        RegisterDeclaration declaration = _registers.RegisterDeclarations[outputKey];
        // A half output needs no half cast on the way in.
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
    /// Names the subexpressions the roots share, one compiled line each. The nodes
    /// are returned too so a caller can sort the lines with its own.
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
    /// The swizzle of a partial output write, or "" where it writes the whole member.
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
        // By register, but a component a phi reads (a loop counter's start) is kept
        // apart, or the counter becomes one lane of an unrelated vector.
        var registerGroups = outputs
            .Where(o => o.Key.RegisterKey.IsTempRegister || o.Key.RegisterKey.IsOutput)
            .OrderBy(o => o.Key.ComponentIndex)
            .GroupBy(o => (o.Key.RegisterKey, IsPhiRead(o.Value)))
            .Select(o => o.Select(c => Reduce(c.Value)).ToArray())
            .ToList();
        registerGroups = TempAssignmentOrder.Sort(registerGroups);

        WidenWithSiblingRows(registerGroups);

        // After reducing: naming hides a subexpression from the templates.
        groups.AddRange(HoistSharedSubexpressions(registerGroups));

        foreach (var registerGroup in registerGroups)
        {
            // Sort before grouping: GroupComponents merges consecutive runs.
            var registerNodes = TempAssignmentOrder.SortNodes(registerGroup);
            _compiler.Compile(registerNodes);
            List<HlslTreeNode[]> componentGroups =
                [.. nodeGrouper.GroupComponents(registerNodes).Select(g => g.ToArray())];
            // Lanes of one instruction keep register order, not dependency order.
            for (int i = 0; i < componentGroups.Count; i++)
            {
                HlslTreeNode[] lanes = componentGroups[i];
                if (lanes.Length > 1
                    && lanes.All(lane => lane is TempAssignmentNode assignment
                        && HlslTreeNode.IsSameInstruction(assignment.Value, ((TempAssignmentNode)lanes[0]).Value)))
                {
                    componentGroups[i] = [.. lanes.OrderBy(lane => Array.IndexOf(registerGroup, lane))];
                }
            }
            groups.AddRange(HoistStaleReads(componentGroups));
            groups.AddRange(componentGroups);
        }
        List<HlslTreeNode[]> ordered = TempAssignmentOrder.Sort(groups);
        List<HlslTreeNode[]> cyclic = HoistCyclicReads(ordered);
        return cyclic.Count == 0 ? ordered : [.. cyclic, .. ordered];
    }

    /// <summary>
    /// Names reads no ordering can satisfy, such as rotated components
    /// (`mul r0.xyz, r0.yzxy, ...`) or a swap, so the parallel assignment is kept.
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
    /// Names expressions several component groups of one instruction read, when
    /// an earlier group overwrites what they read. Needed for correctness, at any size.
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

        // A node read by more than one group is read after the first has run.
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

    /// <summary>
    /// Turns `x - c` lanes back into `x + -c` where another lane of the same
    /// instruction is an add, so the lanes group into one vector expression.
    /// </summary>
    private static void RejoinSignedLanes(IList<HlslTreeNode[]> registerGroups)
    {
        List<HlslTreeNode> nodes = [.. Reachable(registerGroups.SelectMany(g => g))
            .Where(n => n.SourceInstruction != 0 && n is AddOperation or SubtractOperation)];
        foreach (IGrouping<int, HlslTreeNode> instruction in nodes.GroupBy(n => n.SourceInstruction))
        {
            if (!instruction.Any(n => n is AddOperation))
            {
                continue;
            }
            foreach (SubtractOperation subtract in instruction.OfType<SubtractOperation>().ToList())
            {
                if (subtract.Subtrahend is not ConstantNode constant)
                {
                    continue;
                }
                var add = new AddOperation(subtract.Minuend, constant.Negated())
                {
                    SourceInstruction = subtract.SourceInstruction,
                    SourceComponent = subtract.SourceComponent,
                };
                subtract.Replace(add);
                for (int g = 0; g < registerGroups.Count; g++)
                {
                    for (int i = 0; i < registerGroups[g].Length; i++)
                    {
                        if (ReferenceEquals(registerGroups[g][i], subtract))
                        {
                            registerGroups[g][i] = add;
                        }
                    }
                }
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

    private static bool ReadsAnyOf(HlslTreeNode node, HashSet<HlslTreeNode> variables)
    {
        return Reachable([node]).Any(variables.Contains);
    }

    /// <summary>
    /// Minimum size of a shared subexpression worth naming; smaller repeats are
    /// left to the text pass. Unnamed, shared values grow the output exponentially.
    /// </summary>
    private const int SharedSubexpressionThreshold = 8;

    private List<HlslTreeNode[]> HoistSharedSubexpressions(
        IList<HlslTreeNode[]> registerGroups)
    {
        // Roots are reduced and nothing is named yet: the point to recover idioms.
        RejoinSignedLanes(registerGroups);
        IdiomRecovery.Recover(registerGroups, _grouper.MatrixMultiplicationGrouper);
        ReadEarlierNames(registerGroups);

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

        // Out-parameter calls (GetDimensions etc.) have no expression form: always named.
        List<HlslTreeNode[]> resourceInfo = NameResourceInfo(order);
        resourceInfo.AddRange(NameConsumes(order));
        resourceInfo.AddRange(NameDoubleBits(order));

        // Named: sizeable subexpressions written more than once, then repeated text.
        // Counted from compiled text, not graph consumers: a length inside a
        // normalize has many consumers but is never written out.
        Dictionary<HlslTreeNode, int> written = MeasureAndNumber(registerGroups).WrittenCounts();

        // Deepest first, so an inner shared node is named before its container.
        var candidates = new List<HlslTreeNode>();
        for (int i = order.Count - 1; i >= 0; i--)
        {
            HlslTreeNode node = order[i];
            // Operations and the call-like output nodes; keep in step with IsNameable.
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
        List<HlslTreeNode[]> named = Renumber(resourceInfo);
        RecordNames(named);
        return named;
    }

    // The loads statements have named, with the statement that named them.
    private readonly Dictionary<HlslTreeNode, (TempVariableNode Variable, IStatement Statement)> _namedLoads =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Reads a load an earlier statement named through that name, where in scope.
    /// Reloading costs an instruction, and after a store it reads the stored value.
    /// </summary>
    private void ReadEarlierNames(IList<HlslTreeNode[]> registerGroups)
    {
        if (_namedLoads.Count == 0 || _currentStatement == null)
        {
            return;
        }
        HashSet<HlslTreeNode> reachable = HlslTreeNode.NewNodeSet();
        foreach (HlslTreeNode node in Reachable(registerGroups.SelectMany(g => g)))
        {
            reachable.Add(node);
        }
        foreach (HlslTreeNode node in reachable.ToList())
        {
            if (!_namedLoads.TryGetValue(node, out var named)
                || ReferenceEquals(named.Statement, _currentStatement)
                || !IsInScopeAfter(named.Statement, _currentStatement))
            {
                continue;
            }
            Rewire(node, named.Variable, reachable);
            for (int g = 0; g < registerGroups.Count; g++)
            {
                for (int i = 0; i < registerGroups[g].Length; i++)
                {
                    if (ReferenceEquals(registerGroups[g][i], node))
                    {
                        registerGroups[g][i] = named.Variable;
                    }
                }
            }
        }
    }

    private void RecordNames(List<HlslTreeNode[]> named)
    {
        if (_currentStatement == null)
        {
            return;
        }
        foreach (TempAssignmentNode assignment in named.SelectMany(g => g).OfType<TempAssignmentNode>())
        {
            if (assignment.Value is LoadStructuredNode or TextureLoadOutputNode or ResourceLoadNode
                && !_namedLoads.ContainsKey(assignment.Value))
            {
                _namedLoads[assignment.Value] = (assignment.TempVariable, _currentStatement);
            }
        }
    }

    // Whether `later` follows `earlier` in its body (at any depth), so its names are in scope.
    private bool IsInScopeAfter(IStatement earlier, IStatement later)
    {
        if (FindPlace(_functionStatements, earlier) is not (IList<IStatement> body, int index))
        {
            return false;
        }
        for (int k = index + 1; k < body.Count; k++)
        {
            bool found = false;
            new StatementVisitor([body[k]]).Visit(statement => found |= ReferenceEquals(statement, later));
            if (found)
            {
                return true;
            }
        }
        return false;
    }

    // Dot products that are rows of one matrix, in row register order.
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

    /// <summary>
    /// Merges scalars only ever read together, in one order and more than once, into
    /// one vector variable. Decided from the compiled text, not the graph.
    /// </summary>
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

        // Count every read of each scalar, and every 2-4 scalar run read as a vector.
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
            // Read only as this run, unmerged, and typed alike to share a declaration.
            // Each run read also records one lone read per component.
            if (variables.Any(merged.Contains)
                || variables.Any(v => reads[v].Any(read => !read.Equals(run) && read.Nodes.Length != 1)
                    || reads[v].Count(read => read.Nodes.Length == 1) != count)
                || variables.Any(v => v.IsInteger != variables[0].IsInteger
                    || v.IsBits != variables[0].IsBits || v.IsUnsigned != variables[0].IsUnsigned))
            {
                continue;
            }
            // Matrix rows in matrix order, so the dots regroup into one mul.
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

    // Closes the numbering gaps merging leaves, and resets the counter.
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
    /// Compiles the groups' values (not assignments) and discards the text, keeping
    /// the measurement. Also numbers the variables; see
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

    // Repeated text needed before naming: about the cost of a declaration line.
    private const int RepeatedTextBudget = 24;

    // The final recording of NameRepeatedText, for MergeVectorReads.
    private List<(HlslTreeNode[] Nodes, string Text)> _lastRecording;

    /// <summary>
    /// Names expressions the statement's text repeats, one at a time, recompiling
    /// after each. Measured on text because grouping changes what is written.
    /// </summary>
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

            // A repeat is the same nodes, up to equal constants (templates build one per match).
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
            // Only this statement's readers get the variable; the graph is shared.
            HashSet<HlslTreeNode> readers = HlslTreeNode.NewNodeSet();
            foreach (HlslTreeNode node in Reachable(groups.SelectMany(g => g)))
            {
                readers.Add(node);
            }

            // The fallbacks run only when no node-identical repeat is left, so naming
            // an inner node never preempts naming the nest around it.
            List<HlslTreeNode[]> occurrences = candidate != null
                ? [candidate]
                : TextRepeats(recording, grouped, roots)
                    ?? RootOfSeveralRegisters(registerGroups, recording)
                    ?? RootReadAgain(registerGroups, recording, named.Concat(assignments).ToList())
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

            // Widen to sibling components only for a single occurrence.
            if (occurrences.Count == 1)
            {
                candidate = WithSiblingComponents(candidate, readers, roots);
                occurrences = [candidate];
            }
            occurrences = InWrittenOrder(occurrences);
            candidate = occurrences[0];
            // Rewiring a shared reader would leak the name into a later statement
            // outside its scope (e.g. the other branch of an if); stop instead.
            if (ReadersOutOfScope(occurrences))
            {
                _lastRecording = recording;
                return assignments;
            }
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
            // Equal constants read alongside the variable's other components are
            // rewired too, including ones never compiled (other dots of a mul).
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
            // A named whole register value is repointed in its group; Rewire only
            // moves graph readers, and a register root has none.
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
    /// Whether a later-written statement outside the name's scope (e.g. a sibling
    /// branch) holds one of the nodes about to be named, or a reader of one.
    /// </summary>
    private bool ReadersOutOfScope(List<HlslTreeNode[]> occurrences)
    {
        if (_currentStatement == null)
        {
            return false;
        }
        var watched = HlslTreeNode.NewNodeSet();
        var stack = new Stack<HlslTreeNode>();
        foreach (HlslTreeNode[] occurrence in occurrences)
        {
            foreach (HlslTreeNode node in occurrence)
            {
                stack.Push(node);
            }
        }
        while (stack.Count != 0)
        {
            HlslTreeNode node = stack.Pop();
            if (!watched.Add(node))
            {
                continue;
            }
            foreach (HlslTreeNode reader in node.Outputs)
            {
                stack.Push(reader);
            }
        }
        // Statements in writing order, each before what it nests.
        var order = new List<IStatement>();
        void Walk(IList<IStatement> body)
        {
            foreach (IStatement statement in body)
            {
                order.Add(statement);
                if (statement is IfStatement i)
                {
                    Walk(i.TrueBody);
                    if (i.FalseBody != null)
                    {
                        Walk(i.FalseBody);
                    }
                }
                else if (statement is LoopStatement l)
                {
                    Walk(l.Body);
                }
                else if (statement is SwitchStatement s)
                {
                    foreach (SwitchCase c in s.Cases)
                    {
                        Walk(c.Body);
                    }
                }
            }
        }
        Walk(_functionStatements);
        int here = order.FindIndex(s => ReferenceEquals(s, _currentStatement));
        HashSet<IStatement> inside = new(ReferenceEqualityComparer.Instance);
        new StatementVisitor([_currentStatement]).Visit(statement => inside.Add(statement));
        bool outOfScope = false;
        for (int k = here + 1; k < order.Count && !outOfScope; k++)
        {
            IStatement later = order[k];
            if (inside.Contains(later) || IsInScopeAfter(_currentStatement, later))
            {
                continue;
            }
            foreach (HlslTreeNode held in later.Inputs.Values.Concat(later.Outputs.Values))
            {
                if (watched.Contains(held))
                {
                    outOfScope = true;
                    break;
                }
            }
        }
        return outOfScope;
    }

    /// <summary>
    /// Widens a candidate with the other components of its instruction that this
    /// statement reads and that group with it, so e.g. a sample is named once.
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
        // No component may be built on, or underneath, the candidate.
        HashSet<HlslTreeNode> within = HlslTreeNode.NewNodeSet();
        foreach (HlslTreeNode root in candidate)
        {
            foreach (HlslTreeNode node in Reachable([root]))
            {
                within.Add(node);
            }
        }
        foreach (HlslTreeNode sibling in candidate[0].Inputs[0].Outputs)
        {
            if (sibling is not IHasComponentIndex component
                || sibling.GetType() != candidate[0].GetType()
                || indices.Contains(component.ComponentIndex)
                || !readers.Contains(sibling)
                || roots.Contains(sibling)
                || within.Contains(sibling)
                || candidate.Any(c => Reachable([sibling]).Contains(c))
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
    /// Adds the other rows of a matrix multiply as lanes of a register assigned some
    /// of them, so a test over all rows reads one vector. Only where every reader of
    /// the added row is in this block from here on and the variable is assigned
    /// nowhere else.
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

    // Output write order: struct order, else register number. Number alone is
    // ambiguous in D3D9, where several outputs are register 0.
    private int OutputOrder(RegisterComponentKey key)
    {
        int place = OutputsInStructOrder().ToList().FindIndex(d => d.RegisterKey.Equals(key.RegisterKey));
        return place >= 0 ? place : 1000 + key.RegisterKey.Number;
    }

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
    /// Widens matrix rows to the gap-free run of rows of that matrix the statement
    /// reads, so the multiply is named whole rather than taken apart.
    /// </summary>
    private HlslTreeNode[] WithSiblingRows(
        HlslTreeNode[] candidate, HashSet<HlslTreeNode> readers, HashSet<HlslTreeNode> roots)
    {
        MatrixMultiplicationGrouper matrices = _grouper.MatrixMultiplicationGrouper;
        // Not a single row: widening a projection's w splits its divides apart.
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

    /// <summary>
    /// Widens an operation candidate with operations that group with it (e.g.
    /// `ddx(uv.x)` and `ddx(uv.y)`), ordered by the component each reads.
    /// </summary>
    private HlslTreeNode[] WithSiblingOperations(
        HlslTreeNode[] candidate, HashSet<HlslTreeNode> readers, HashSet<HlslTreeNode> roots)
    {
        if (candidate.Length != 1 || SiblingOrder(candidate[0], candidate[0]) is not int order)
        {
            return candidate;
        }
        var byOrder = new SortedDictionary<int, HlslTreeNode> { [order] = candidate[0] };
        // A sibling never feeds or reads the candidate: one lane's initializer would
        // read the other lane of the same variable.
        HashSet<HlslTreeNode> withinCandidate = HlslTreeNode.NewNodeSet();
        foreach (HlslTreeNode node in Reachable([candidate[0]]))
        {
            withinCandidate.Add(node);
        }
        foreach (HlslTreeNode sibling in readers)
        {
            if (ReferenceEquals(sibling, candidate[0])
                || roots.Contains(sibling)
                || !_templateMatcher.CanGroupComponents(sibling, candidate[0], false)
                // Same type only: a uint lane in a float vector loses its bits.
                || ValueTypes.IsIntegerValue(sibling) != ValueTypes.IsIntegerValue(candidate[0])
                // A load's siblings are only lanes of the same load (`x * 1` would
                // otherwise group with it).
                || ((sibling is LoadStructuredNode || candidate[0] is LoadStructuredNode)
                    && !(sibling is LoadStructuredNode && candidate[0] is LoadStructuredNode
                        && HlslTreeNode.IsSameInstruction(sibling, candidate[0])))
                || withinCandidate.Contains(sibling)
                || Reachable([sibling]).Contains(candidate[0])
                || SiblingOrder(sibling, candidate[0]) is not int siblingOrder
                || byOrder.ContainsKey(siblingOrder))
            {
                continue;
            }
            byOrder[siblingOrder] = sibling;
        }
        return byOrder.Count == 1 ? candidate : [.. byOrder.Values];
    }

    // The single lane each component reads through elementwise operations, ignoring
    // leaves every component reads (broadcasts); null where it reads several.
    private int?[] LanesRead(IList<HlslTreeNode> components)
    {
        List<HashSet<HlslTreeNode>> leaves = [];
        foreach (HlslTreeNode component in components)
        {
            HashSet<HlslTreeNode> found = HlslTreeNode.NewNodeSet();
            HashSet<HlslTreeNode> seen = HlslTreeNode.NewNodeSet();
            var pending = new Stack<HlslTreeNode>([component]);
            while (pending.Count != 0)
            {
                HlslTreeNode node = pending.Pop();
                if (!seen.Add(node))
                {
                    continue;
                }
                if (node is IHasComponentIndex
                    || (node is Operation operation && !NodeCompiler.IsElementwise(operation))
                    || node is not Operation and not ConstantNode)
                {
                    found.Add(node);
                    continue;
                }
                foreach (HlslTreeNode input in node.Inputs)
                {
                    pending.Push(input);
                }
            }
            leaves.Add(found);
        }
        return [.. leaves.Select(found =>
        {
            // A matrix row's lane is its row (see SourceLane).
            int?[] lanes = [.. found
                .Where(leaf => !leaves.All(other => other.Contains(leaf)))
                .Select(leaf => leaf is IHasComponentIndex indexed
                    ? indexed.ComponentIndex
                    : leaf is DotProductOperation dot
                        ? _grouper.MatrixMultiplicationGrouper.MatrixRowRegister(dot)
                        : null)
                .Distinct()];
            return lanes.Length == 1 ? lanes[0] : null;
        })];
    }

    // Lanes of a named vector, as InWrittenOrder lays them out: by lanes read where
    // all distinct, else by lanes written.
    private int?[] LanesOf(IList<HlslTreeNode> components)
    {
        if (components.All(n => n.SourceInstruction != 0 && HlslTreeNode.IsSameInstruction(n, components[0])))
        {
            int?[] read = LanesRead(components);
            if (read.All(c => c != null) && read.Distinct().Count() == components.Count)
            {
                return read;
            }
        }
        return [.. components.Select(SourceLane)];
    }

    // A value's lane: a matrix row's row number, else the component its
    // instruction wrote, else the one it reads.
    private int? SourceLane(HlslTreeNode node)
    {
        if (node is DotProductOperation dot
            && _grouper.MatrixMultiplicationGrouper.MatrixRowRegister(dot) is int row)
        {
            return row;
        }
        return node.SourceInstruction != 0 ? node.SourceComponent : ComponentOrder(node);
    }

    // A sibling's slot: the written lane for lanes of one structured load (they all
    // read the same index), else the component read.
    private static int? SiblingOrder(HlslTreeNode node, HlslTreeNode candidate)
    {
        return HlslTreeNode.IsSameInstruction(node, candidate) && node is LoadStructuredNode
            ? node.SourceComponent
            : ComponentOrder(node);
    }

    // The first register component an expression reads, for ordering lanes.
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

    // Smaller than RepeatedTextBudget: a name here saves a whole write.
    private const int SharedRootBudget = 12;

    /// <summary>
    /// Names one value several registers are written from. Nothing else names it:
    /// SharedRoots needs multi-component groups and SplitRead skips roots.
    /// </summary>
    private List<HlslTreeNode[]> RootOfSeveralRegisters(
        IList<HlslTreeNode[]> registerGroups,
        List<(HlslTreeNode[] Nodes, string Text)> recording)
    {
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
            // Cost: its text, once per register past the first.
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
    /// Names a register's computed value that the statement writes again inside
    /// another expression. In hull shaders any repeat counts, whatever its length:
    /// fxc recomputes it in each fork/join phase. Elsewhere fxc reuses it, so only
    /// a vector read a lane at a time is named.
    /// </summary>
    private List<HlslTreeNode[]> RootReadAgain(
        IList<HlslTreeNode[]> registerGroups,
        List<(HlslTreeNode[] Nodes, string Text)> recording,
        List<HlslTreeNode[]> named)
    {
        bool isHull = _shader.Type == ShaderType.Hull;
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
            if (isHull && written > 1)
            {
                return [group];
            }
            if (!isHull && group.Length > 1 && IsReadALaneAtATime(group, recording, named))
            {
                return [group];
            }
        }
        return null;
    }

    /// <summary>
    /// Whether every lane of a root vector is written again on its own elsewhere;
    /// named, the grouper can take the lanes back together.
    /// </summary>
    private bool IsReadALaneAtATime(
        HlslTreeNode[] group,
        List<(HlslTreeNode[] Nodes, string Text)> recording,
        List<HlslTreeNode[]> named)
    {
        if (recording.Count(r => r.Nodes.Length == group.Length
                && r.Nodes.Zip(group).All(pair => ReferenceEquals(pair.First, pair.Second))) != 1)
        {
            return false;
        }
        // Not where a reader of one lane feeds another lane, even through an
        // already named variable: that is a cycle no statement order writes.
        var writtenValues = new Dictionary<int, HlslTreeNode>();
        foreach (HlslTreeNode[] assignmentGroup in named)
        {
            foreach (TempAssignmentNode assignment
                in assignmentGroup.OfType<TempAssignmentNode>()
                    .Where(a => a.TempVariable.DeclarationIndex != null))
            {
                writtenValues[assignment.TempVariable.DeclarationIndex.Value] = assignment.Value;
            }
        }
        bool Reaches(HlslTreeNode target, HlslTreeNode node)
        {
            HashSet<HlslTreeNode> visited = HlslTreeNode.NewNodeSet();
            return Inside(target, node);

            bool Inside(HlslTreeNode at, HlslTreeNode here)
            {
                if (ReferenceEquals(at, here))
                {
                    return true;
                }
                if (!visited.Add(here))
                {
                    return false;
                }
                if (here is TempVariableNode read
                    && read.DeclarationIndex is int index
                    && writtenValues.TryGetValue(index, out HlslTreeNode value))
                {
                    return Inside(at, value);
                }
                return HlslTreeNode.TraversableInputs(here).Any(input => Inside(at, input));
            }
        }
        foreach (HlslTreeNode lane in group)
        {
            foreach (HlslTreeNode reader in lane.Outputs)
            {
                if (!group.Contains(reader, ReferenceEqualityComparer.Instance)
                    && group.Any(other => Reaches(reader, other)))
                {
                    return false;
                }
            }
        }
        // A constructor writes each lane alone once already.
        int own = IsConstructorOnly(group) ? 1 : 0;
        return group.All(node => recording.Count(r => r.Nodes.Length == 1
            && ReferenceEquals(r.Nodes[0], node)) > own);
    }

    /// <summary>
    /// Names a register's whole value where something else reads it, whole so a
    /// `mul(v, m)` is not split into loose dots. Judged by text cost.
    /// </summary>
    private List<HlslTreeNode[]> SharedRoots(
        IList<HlslTreeNode[]> registerGroups,
        HashSet<HlslTreeNode> readers,
        List<(HlslTreeNode[] Nodes, string Text)> recording,
        List<HlslTreeNode[]> groupMatches)
    {
        foreach (HlslTreeNode[] group in registerGroups)
        {
            // Multi-component groups a grouper matched (SplitRead does single ones);
            // unrelated lanes are a constructor, which cannot hold an assignment.
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
    /// Names one instruction's components that are written out in several places,
    /// so readers become swizzles of one variable. Judged by text cost.
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
            HlslTreeNode[] group = [.. instruction.OrderBy(node => node.SourceComponent)];
            if (group.Any(node => node.Outputs.Any(reader => reader is TempAssignmentNode)))
            {
                continue;
            }
            List<(HlslTreeNode[] Nodes, string Text)> written = [.. recording
                .Where(r => r.Nodes.All(group.Contains))];
            // Only where something reads them as a vector; otherwise fxc would have
            // to build a vector the shader never had.
            if (written.Count < 2 || !written.Any(r => r.Nodes.Length > 1))
            {
                continue;
            }
            int saved = written.Sum(r => r.Text.Length) - written.Max(r => r.Text.Length);
            // Or whole and part both written: fxc recomputes `f16tof32(v.x)` beside
            // `f16tof32(v)`.
            bool wholeAndPart = written.Any(r => r.Nodes.Length == group.Length)
                && written.Any(r => r.Nodes.Length < group.Length)
                && group.All(node => node is Operation);
            // Packed arithmetic lanes must form one vector expression, not a
            // constructor; other ops (a sincos pair) may be a constructor.
            bool packedArithmetic = group.All(node => node is AddOperation or SubtractOperation
                or MultiplyOperation or MultiplyAddOperation or NegateOperation);
            if ((saved >= RepeatedTextBudget || wholeAndPart) && !(packedArithmetic && IsConstructorOnly(group)))
            {
                return [group];
            }
        }
        return null;
    }

    /// <summary>
    /// Names one instruction's components that are only ever written apart, as the
    /// vector it made: `step(abs(t3), 0.5)` rather than three scalar steps. Only
    /// where together they compile to one vector expression, not a constructor.
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

    // Whether the components, compiled together, are a constructor of them.
    private bool IsConstructorOnly(IEnumerable<HlslTreeNode> components)
    {
        string together = _compiler.Measure([components]).Recording[^1].Text;
        return System.Text.RegularExpressions.Regex.IsMatch(together, @"^(float|int|uint|half|double|bool)[234]\(");
    }

    /// <summary>
    /// Whether every swizzle in the text is a prefix (.xy, .xyz, .xyzw). Shuffled
    /// swizzles mean fxc packed unrelated scalars into one instruction.
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

    // Length at which a single write is worth naming anyway.
    private const int SingleWriteBudget = 28;

    // Write count, bumped to 2 for a single write of long text.
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

    /// <summary>
    /// Names an instruction written out more than once (fxc computes it each time).
    /// Only nodes that cost an instruction; cheap arithmetic is free to repeat.
    /// </summary>
    private List<HlslTreeNode[]> SplitRead(
        HashSet<HlslTreeNode> readers,
        HashSet<HlslTreeNode> grouped,
        HashSet<HlslTreeNode> roots,
        CompileMeasurement measurement)
    {
        // Writes, not readers: `sample.y + sample.x` writes the sample twice.
        Dictionary<HlslTreeNode, int> writes = measurement.WrittenCounts();

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
            // Not already named, or it would be named every round.
            .Where(node => !node.Outputs.Any(reader => reader is TempAssignmentNode))
            .Select(node => (Node: node, Read: WorthNaming(node, writes, text)))
            .Where(node => node.Read > 1)
            .OrderByDescending(node => node.Read)
            .Select(node => node.Node)
            .FirstOrDefault();
        return chosen == null ? null : [[chosen]];
    }

    // Whether recomputing the node costs fxc an instruction (it does not CSE these).
    private static bool CostsAnInstruction(HlslTreeNode node)
    {
        return node is LoadStructuredNode
            or TextureLoadOutputNode
            or SamplePositionNode
            or NormalizeOutputNode
            or ReflectOutputNode
            or MatrixMultiplyOutputNode
            or PartialDerivativeXOperation
            or PartialDerivativeYOperation;
    }

    // Operations and call-like multi-output nodes; register reads and variables
    // already have names.
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

    // All occurrences of text that repeats past the budget from different nodes.
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
            // All one width: Broadcast can shrink one occurrence to a single node.
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
    /// Reorders occurrence components into the value's own order, not the swizzle
    /// the first reader used, so the name is not a permutation of the value.
    /// </summary>
    private List<HlslTreeNode[]> InWrittenOrder(List<HlslTreeNode[]> occurrences)
    {
        HlslTreeNode[] first = occurrences[0];
        if (first.Length < 2)
        {
            return occurrences;
        }
        int[] order = null;
        // Matrix rows in matrix order, so the dots regroup into one mul.
        int?[] rows = [.. first.Select(n => n is DotProductOperation dot
            && first.All(other => ReferenceEquals(other, n)
                || (other is DotProductOperation otherDot && _grouper.MatrixMultiplicationGrouper.AreRowsOfOneMatrix(dot, otherDot)))
            ? _grouper.MatrixMultiplicationGrouper.MatrixRowRegister(dot)
            : null)];
        // A normalize by the input lane each component divides; fxc may store it rotated.
        int?[] lanes = first[0] is NormalizeOutputNode firstNormalize
            ? LanesOf(firstNormalize.Inputs)
            : null;
        int?[] normalized = [.. first.Select(n => lanes != null
            && n is NormalizeOutputNode normalize
            && normalize.ComponentIndex < lanes.Length
            && first.All(other => other is NormalizeOutputNode o && o.Inputs.SequenceEqual(normalize.Inputs))
            ? lanes[normalize.ComponentIndex]
            : null)];
        if (rows.All(r => r != null) && rows.Distinct().Count() == first.Length)
        {
            order = [.. Enumerable.Range(0, first.Length).OrderBy(i => rows[i])];
        }
        else if (normalized.All(c => c != null) && normalized.Distinct().Count() == first.Length)
        {
            order = [.. Enumerable.Range(0, first.Length).OrderBy(i => normalized[i])];
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
            // By lanes read where all distinct (fxc may write rotated), else lanes written.
            int?[] read = LanesRead(first);
            order = read.All(c => c != null) && read.Distinct().Count() == first.Length
                ? [.. Enumerable.Range(0, first.Length).OrderBy(i => read[i])]
                : [.. Enumerable.Range(0, first.Length).OrderBy(i => first[i].SourceComponent)];
        }
        if (order == null || order.SequenceEqual(Enumerable.Range(0, first.Length)))
        {
            return occurrences;
        }
        return [.. occurrences.Select(occurrence => order.Select(i => occurrence[i]).ToArray())];
    }

    /// <summary>
    /// Renumbers hoisted variables in writing order; outer repeats are named first,
    /// so creation order is roughly the reverse.
    /// </summary>
    private List<HlslTreeNode[]> Renumber(List<HlslTreeNode[]> assignments)
    {
        List<HlslTreeNode[]> sorted = TempAssignmentOrder.Sort(InReadOrder(assignments));
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

    /// <summary>
    /// Sorts resource reads among themselves by text, in their existing slots, so
    /// fxc's scheduling of independent samples does not flip between round trips.
    /// </summary>
    private List<HlslTreeNode[]> InReadOrder(List<HlslTreeNode[]> assignments)
    {
        List<int> places = [.. Enumerable.Range(0, assignments.Count).Where(i => IsResourceRead(assignments[i]))];
        if (places.Count < 2)
        {
            return assignments;
        }
        List<HlslTreeNode[]> reads = [.. places
            .Select(i => assignments[i])
            .OrderBy(group => _compiler.Measure([group.Select(a => ((TempAssignmentNode)a).Value)]).Recording[^1].Text,
                StringComparer.Ordinal)];
        List<HlslTreeNode[]> ordered = [.. assignments];
        for (int k = 0; k < places.Count; k++)
        {
            ordered[places[k]] = reads[k];
        }
        return ordered;
    }

    private static bool IsResourceRead(HlslTreeNode[] group)
    {
        return group.All(a => a is TempAssignmentNode { Value: TextureLoadOutputNode or ResourceLoadNode or LoadStructuredNode });
    }

    // A node broadcast to every component counts as one node.
    private static HlslTreeNode[] Broadcast(HlslTreeNode[] nodes)
    {
        return nodes.All(n => SameValue(n, nodes[0])) ? [nodes[0]] : nodes;
    }

    private static bool SameValue(HlslTreeNode a, HlslTreeNode b)
    {
        return ReferenceEquals(a, b)
            || (a is ConstantNode ca && b is ConstantNode cb && ca.Value == cb.Value);
    }

    // Grouping key; equal constants compare equal.
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

    private TempVariableNode[] CreateTempVariables(IList<HlslTreeNode> nodes)
    {
        TempVariableNode[] variables = _compiler.CreateTempVariables(nodes.Count);
        ApplyValueTypes(variables, nodes);
        return variables;
    }

    /// <summary>
    /// Types variables from the values they will hold; a type holds only if every
    /// component has it.
    /// </summary>
    private void ApplyValueTypes(TempVariableNode[] variables, IList<HlslTreeNode> nodes)
    {
        bool isInteger = nodes.All(node => ValueTypes.IsIntegerValue(node) == true);
        bool isBits = nodes.All(node => ValueTypes.IsBitsVariable(node, isInteger));
        // The node cannot tell a double; the parser's record of double writes does.
        bool isDouble = !isInteger && nodes.All(_doubleValues.Contains);
        // Bits are a float's, never uint.
        bool isUnsigned = isInteger && !isBits
            && nodes.All(node => ValueTypes.IsUnsignedValue(node) == true);
        // A condition only conditions read (as HoldsOnlyACondition in the finalizer).
        bool isBool = nodes.All(node => node is ComparisonNode or LogicalAndOperation or LogicalOrOperation
            && node.Outputs.Count != 0
            && node.Outputs.All(reader => (reader is MoveConditionalOperation select
                    && ReferenceEquals(select.Condition, node))
                || reader is LogicalAndOperation or LogicalOrOperation));
        foreach (TempVariableNode variable in variables)
        {
            variable.IsInteger = isInteger;
            variable.IsBits = isBits;
            variable.IsUnsigned = isUnsigned;
            variable.IsDouble = isDouble;
            variable.IsBool = isBool;
        }
    }

    /// <summary>
    /// Names each reachable resinfo call as one variable for its GetDimensions out
    /// parameters.
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

            // The overload sets the variable's width. The no-mip form has 1 (1D),
            // 2 (2D, cube) or 3 (arrays, 3D) components; reading past that (e.g. the
            // mip count) needs the 4-wide mip form.
            ResourceDimension? dimension = _registers
                .GetTextureDefinition(info.Resource.RegisterComponentKey.RegisterKey)?.Dimension;
            bool hasDepth = dimension is ResourceDimension.Texture2DArray
                or ResourceDimension.TextureCubeArray or ResourceDimension.Texture3D;
            bool is1D = dimension == ResourceDimension.Texture1D;
            int noMipComponents = is1D ? 1 : hasDepth ? 3 : 2;
            bool mipForm = !IsConstantZero(info.MipLevel)
                || call.Any(c => c.InfoComponent >= noMipComponents);
            // Multisampled: width, height[, elements], sample count; no mip level.
            ResourceInfoNode sampleCount = call.FirstOrDefault(c => c.IsSampleCount);
            TempVariableNode[] variables = _compiler.CreateTempVariables(
                info.IsBuffer ? (info.ReportsStride ? 2 : 1)
                : sampleCount != null ? sampleCount.SampleCountComponent + 1
                // The sample count is in the overload even when not read.
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
    /// Names each reachable Consume call as one variable for the whole element.
    /// </summary>
    private List<HlslTreeNode[]> NameConsumes(IList<HlslTreeNode> order)
    {
        var assignments = new List<HlslTreeNode[]>();
        var named = HlslTreeNode.NewNodeSet();
        // In instruction order: each call takes the next element, and the graph
        // does not order independent calls.
        foreach (ConsumeNode consume in order.OfType<ConsumeNode>()
            .OrderBy(candidate => candidate.Slot.SourceInstruction))
        {
            if (named.Contains(consume) || consume.NamedAs != null)
            {
                continue;
            }
            // One call is one slot (one imm_atomic_consume).
            List<ConsumeNode> call = [.. order.OfType<ConsumeNode>()
                .Where(other => other.NamedAs == null
                    && ReferenceEquals(other.Slot, consume.Slot))
                .OrderBy(other => other.ComponentIndex)];
            foreach (ConsumeNode component in call)
            {
                named.Add(component);
            }
            // The whole element, remembered by slot, so a second statement reading
            // the call does not consume again.
            if (!_consumeVariables.TryGetValue(consume.Slot, out TempVariableNode[] variables))
            {
                variables = _compiler.CreateTempVariables(
                    _registers.GetStructuredBufferComponents(
                        consume.Buffer.RegisterComponentKey.RegisterKey));
                // Typed from the components read, so float readers reinterpret bits.
                ApplyValueTypes(variables, [.. call]);
                RegisterKey consumedKey = consume.Buffer.RegisterComponentKey.RegisterKey;
                if (_registers.FindStructuredMemberRuns(consumedKey, "t", 0, [0]) != null)
                {
                    foreach (TempVariableNode variable in variables)
                    {
                        variable.StructuredElementOf = consumedKey;
                    }
                }
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
    /// Names each double split into words as one variable: `asuint(double, ...)`
    /// only has an out-parameter form.
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
            List<DoubleBitsNode> call = [.. order.OfType<DoubleBitsNode>()
                .Where(other => other.NamedAs == null
                    && ReferenceEquals(other.Value, bits.Value))
                .OrderBy(other => other.ComponentIndex)];
            foreach (DoubleBitsNode component in call)
            {
                named.Add(component);
            }
            // Always both words, remembered by the double so the call is made once.
            if (!_doubleBitsVariables.TryGetValue(bits.Value, out TempVariableNode[] variables))
            {
                variables = _compiler.CreateTempVariables(2);
                foreach (TempVariableNode variable in variables)
                {
                    // Bits, so a float reader reinterprets.
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
        // resinfo and sampleinfo of one call may differ in return type.
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
    /// Names the candidates, the components of one operation together; named one
    /// by one, a vector instruction could never be regrouped.
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
                // Never one computed from another (e.g. `mul r1.x, 2, r1.x`).
                if (other.IsInputOf(candidate) || candidate.IsInputOf(other))
                {
                    continue;
                }
                // Groupable with the first, or made by the same instruction.
                if (nodeGrouper.CanGroupComponents(candidate, other)
                    || HlslTreeNode.IsSameInstruction(candidate, other))
                {
                    group.Add(other);
                    named.Add(other);
                }
            }

            // Add the instruction's other lanes, or a lane read once is recomputed
            // inline; only where they form one vector expression, not packed scalars.
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

            // The value's order, not discovery order (the walk runs backwards).
            group = [.. InWrittenOrder([[.. group]])[0]];
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

    // Number of distinct nodes in the expression.
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

    // Points the node's readers (all, or those in `among`) at the variable.
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
    /// Output components grouped by declaration, not register: fxc packs several
    /// outputs into one register.
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