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
    private TemplateMatcher _templateMatcher;
    private int _loopDepth;
    private readonly HashSet<HlslTreeNode> _declaredVariables = HlslTreeNode.NewNodeSet();
    // The variables each Consume call was named into, by the slot that identifies
    // it, so that a second statement reading the same call finds them.
    private readonly Dictionary<HlslTreeNode, TempVariableNode[]> _consumeVariables =
        new(ReferenceEqualityComparer.Instance);

    public HlslAstWriter(ShaderModel shader)
        : base(shader)
    {
    }

    protected override void WriteMethodBody()
    {
        if (HasOutputStruct)
        {
            string outputStructType = _shader.Type switch
            {
                ShaderType.Pixel => "PS_OUT",
                ShaderType.Vertex => "VS_OUT",
                ShaderType.Geometry => "GS_OUT",
                _ => throw new NotImplementedException(),
            };
            WriteLine($"{outputStructType} {_registers.OutputVariableName};");
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
        return _shader.Instructions.Count != 0 && _shader.Instructions[0] is D3D10Instruction
            ? new IntegerOperandAnalysis(_shader)
            : null;
    }

    private void WriteAst(HlslAst ast)
    {
        _compiler = new NodeCompiler(_registers);
        _grouper = new NodeGrouper(_registers);
        _templateMatcher = new TemplateMatcher(_grouper);

        StatementFinalizer.Finalize(ast.Statements, GetMethodReturnType() != "void",
            CreateIntegerOperandAnalysis());
        FindDeclaredVariables(ast.Statements);
        WriteStatements(ast.Statements);
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

    // Compiles an assignment group, declaring the variable where nothing else does.
    private string CompileAssignment(HlslTreeNode[] group)
    {
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
        return _compiler.Compile(group);
    }

    private void WriteStatements(IList<IStatement> statements)
    {
        foreach (IStatement statement in statements)
        {
            WriteStatement(statement);
        }
    }

    private void WriteStatement(IStatement statement)
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
            WriteLine("stream.Append(o);");
        }
        else if (statement is RestartStripStatement restartStrip)
        {
            WriteLine("stream.RestartStrip();");
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
            WriteReturnStatement(returnStatement);
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
        Dictionary<RegisterComponentKey, HlslTreeNode[]> outputs =
            GroupComponents(assignmentStatement.Outputs
                    .Where(o => o.Key.RegisterKey.IsOutput)
                    .Where(o => !(assignmentStatement.Inputs.TryGetValue(o.Key, out var inputNode)
                        && o.Value == inputNode)))
                .ToDictionary(r => r.Key, r => r.Value.Select(n => Reduce(n)).ToArray());

        // What the outputs share among themselves and with the temps is named
        // here, the way a return names it: a geometry shader writes its vertex
        // through these statements and never through a return, and the corner
        // offset a position and a texture coordinate both read was written out
        // at each.
        List<RegisterComponentKey> outputKeys = [.. outputs.Keys];
        List<HlslTreeNode[]> hoistRoots = [.. tempGroups, .. outputKeys.Select(key => outputs[key])];
        WriteSharedSubexpressions(hoistRoots);
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
        var writes = new List<(HlslTreeNode[] Nodes, TempAssignmentNode[] Wants, Action Write)>();
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
            writes.Add((nodes, wants, () => WriteLine($"o.{outputRegister.Name} = {CompileOutput(rootGroup.Key.RegisterKey, nodes)};")));
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
        bool storesIntegers = values.All(v => StatementFinalizer.IsIntegerValue(v) == true);
        string value = storesIntegers
            ? _compiler.CompileAsInteger(values)
            : _compiler.Compile(values);
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
        bool storesIntegers = values.All(v => StatementFinalizer.IsIntegerValue(v) == true);
        string value = storesIntegers
            ? _compiler.CompileAsInteger(values)
            : _compiler.Compile(values);
        WriteLine($"{destination}[{coordinate}] = {value};");
    }

    // The dwords a raw store writes: each integer as it is, each float as its bits,
    // and a mix as a uint constructor of both.
    private string CompileRawStoredValue(HlslTreeNode[] values)
    {
        if (values.All(v => StatementFinalizer.IsIntegerValue(v) != true))
        {
            return $"asuint({_compiler.Compile(values)})";
        }
        List<string> dwords = [.. values.Select(v => StatementFinalizer.IsIntegerValue(v) == true
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
        bool storesIntegers = storedValues.All(v => StatementFinalizer.IsIntegerValue(v) == true);
        string compiledValue = storesIntegers
            ? _compiler.CompileAsInteger(storedValues)
            : _compiler.Compile(storedValues);
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
        IList<(string Name, int[] Values)> runs = _registers.FindStructuredMemberRuns(
            bufferKey, storeStructured.ElementByteOffset, storeStructured.Components);
        if (runs != null)
        {
            foreach ((string name, int[] values) in runs)
            {
                string run = storesIntegers
                    ? _compiler.CompileAsInteger(values.Select(v => storedValues[v]))
                    : _compiler.Compile(values.Select(v => storedValues[v]));
                WriteLine($"{compiledDestination}[{compiledAddress}].{name} = {run};");
            }
            return;
        }
        WriteLine($"{compiledDestination}[{compiledAddress}] = {compiledValue};");
    }

    private void WriteAtomicStatement(AtomicStatement atomic)
    {
        RegisterKey resourceKey =
            ((RegisterInputNode)atomic.Destination).RegisterComponentKey.RegisterKey;
        string resource = _registers.GetRegisterName(resourceKey);
        string address = _compiler.Compile(Reduce(atomic.Address));
        string value = _compiler.Compile(Reduce(atomic.Value));
        string compare = atomic.Compare == null
            ? null
            : _compiler.Compile(Reduce(atomic.Compare));

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
                string type = atomic.Original.IsInteger ? atomic.Original.IntegerTypeName : "float";
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
        // which is the element rather than the resource.
        if (atomic.ElementByteOffset == null)
        {
            WriteLine($"{resource}.{atomic.MethodName}({address}, {arguments});");
            return;
        }

        string element = $"{resource}[{address}]";
        IList<(string Name, int[] Values)> runs = _registers.FindStructuredMemberRuns(
            resourceKey, GetElementByteOffset(atomic), [0]);
        if (runs != null && runs.Count == 1)
        {
            element += "." + runs[0].Name;
        }
        WriteLine($"{atomic.MethodName}({element}, {arguments});");
    }

    private static int GetElementByteOffset(AtomicStatement atomic)
    {
        return atomic.ElementByteOffset is ConstantNode constant && constant.IntegerValue.HasValue
            ? constant.IntegerValue.Value
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
        if (NeedsLoopAttribute(loop))
        {
            WriteLine("[loop]");
        }
        string loopVariableName = null;
        IList<IStatement> body = loop.Body;
        if (loop.IsCountedLoop)
        {
            // The initializer and increment compile as statements; the for header
            // wants them as clauses.
            string initializer = _compiler.Compile(Reduce(loop.Initializer)).TrimEnd(';');
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
            roots.AddRange(statement switch
            {
                IfStatement @if => @if.Comparison,
                ClipStatement clip => clip.Values,
                StoreStructuredStatement store => [store.Address, .. store.Values],
                IndexableTempStoreStatement store => [store.Index, .. store.Values],
                BreakStatement @break when @break.Comparison != null => [@break.Comparison],
                ContinueStatement @continue when @continue.Comparison != null => [@continue.Comparison],
                ReturnStatement @return when @return.Comparison != null => [@return.Comparison],
                _ => [],
            });
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

        WriteLine($"if ({_compiler.Compile(comparison.Select(Reduce))}) {{");
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
        var newAssignments = outputs
            .Where(o => o.Value is TempVariableNode)
            .Where(o => !inputs.ContainsKey(o.Key))
            .ToDictionary();
        if (newAssignments.Count > 0)
        {
            foreach (var group in GroupAssignments(newAssignments))
            {
                // Compile variable with all components
                _compiler.Compile(group);

                var variable = group.First() as TempVariableNode;
                // An enclosing block may already declare it, when a nested if merged
                // into the same variable. Declaring it again would shadow it.
                if (!_declaredVariables.Add(variable))
                {
                    continue;
                }
                string size = variable.VariableSize != 1 ? variable.VariableSize.ToString() : "";
                string type = variable.IsInteger ? variable.IntegerTypeName : "float";
                WriteLine($"{type}{size} t{variable.DeclarationIndex};");
            }
        }
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
        Dictionary<RegisterComponentKey, HlslTreeNode[]> outputs =
            GroupComponents(returnStatement.Outputs
                    .Where(o => o.Key.RegisterKey.IsOutput)
                    .Where(o => !(hasOutputStruct
                        && returnStatement.Inputs.TryGetValue(o.Key, out var inputNode)
                        && o.Value == inputNode)))
                .ToDictionary(r => r.Key, r => r.Value.Select(n => Reduce(n)).ToArray());

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
                string compiled = CompileOutput(rootGroup.Key.RegisterKey, rootGroup.Value);
                WriteLine($"{_registers.OutputVariableName}.{outputRegister.Name} = {compiled};");
            }
            if (outputs.Count != 0)
            {
                WriteLine();
            }
            WriteLine($"return {_registers.OutputVariableName};");
        }
    }

    // An output the signature types as a float takes a float, so bits reaching one
    // are reinterpreted. One it types as an integer takes the integer as it is.
    private string CompileOutput(RegisterKey outputKey, IEnumerable<HlslTreeNode> nodes)
    {
        return _registers.RegisterDeclarations[outputKey].TypeName.Contains("int")
            ? _compiler.Compile(nodes)
            : _compiler.CompileAsFloat(nodes);
    }

    private void WriteSharedSubexpressions(IList<HlslTreeNode[]> roots)
    {
        List<HlslTreeNode[]> assignments = TempAssignmentOrder.Sort(
            HoistSharedSubexpressions(roots));
        foreach (HlslTreeNode[] assignment in assignments)
        {
            WriteLine(_compiler.Compile(assignment));
        }
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
        return TempAssignmentOrder.Sort(groups);
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
        Dictionary<HlslTreeNode, int> written = WrittenCounts(registerGroups);

        // Deepest first, so that a shared node inside another one is named before
        // the node containing it stops being reachable from here.
        var candidates = new List<HlslTreeNode>();
        for (int i = order.Count - 1; i >= 0; i--)
        {
            HlslTreeNode node = order[i];
            if (roots.Contains(node) || node is not Operation)
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
        resourceInfo.AddRange(NameCandidates(candidates));
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
    private static bool MergeVectorReads(
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
    /// How many times each node is written, from compiling the statement once and
    /// throwing it away. A node appears here when it is a component of a group the
    /// compiler wrote; one the grouper absorbed - a normalize's length, a matrix
    /// multiply's dot products - appears not at all, however many readers it has.
    /// </summary>
    private Dictionary<HlslTreeNode, int> WrittenCounts(IEnumerable<HlslTreeNode[]> groups)
    {
        var recording = new List<(HlslTreeNode[] Nodes, string Text)>();
        _compiler.Recording = recording;
        try
        {
            foreach (HlslTreeNode[] group in groups)
            {
                // The values, not the assignments, for the reason NameRepeatedText
                // gives: compiling an assignment numbers its variable.
                _compiler.Compile(group.Select(root =>
                    root is TempAssignmentNode assignment ? assignment.Value : root));
            }
        }
        finally
        {
            _compiler.Recording = null;
        }
        var counts = new Dictionary<HlslTreeNode, int>(ReferenceEqualityComparer.Instance);
        foreach ((HlslTreeNode[] nodes, _) in recording)
        {
            foreach (HlslTreeNode node in nodes)
            {
                counts[node] = counts.TryGetValue(node, out int count) ? count + 1 : 1;
            }
        }
        return counts;
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
            var recording = new List<(HlslTreeNode[] Nodes, string Text)>();
            HashSet<HlslTreeNode> grouped = HlslTreeNode.NewNodeSet();
            var groupMatches = new List<HlslTreeNode[]>();
            _compiler.Recording = recording;
            _compiler.Grouped = grouped;
            _compiler.GroupMatches = groupMatches;
            try
            {
                foreach (HlslTreeNode[] group in groups)
                {
                    // The values, not the assignments: compiling an assignment numbers
                    // its variable, and the names would come out in the order of the
                    // measuring rather than of the writing.
                    _compiler.Compile(group.Select(root =>
                        root is TempAssignmentNode assignment ? assignment.Value : root));
                }
            }
            finally
            {
                _compiler.Recording = null;
                _compiler.Grouped = null;
                _compiler.GroupMatches = null;
            }

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
                    ?? SplitRead(readers, grouped, roots)
                    ?? SharedRoots(registerGroups, readers, recording, groupMatches)
                    ?? SharedInstruction(readers, recording, roots);
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

    private List<HlslTreeNode[]> SplitRead(
        HashSet<HlslTreeNode> readers, HashSet<HlslTreeNode> grouped, HashSet<HlslTreeNode> roots)
    {
        HlslTreeNode chosen = readers
            .Where(node => CostsAnInstruction(node)
                && !roots.Contains(node)
                && !grouped.Contains(node))
            // Not one that has been named already. Counting over the instruction
            // rather than the node means naming a component does not bring its own
            // count down - every component still answers for all of them - so
            // without this the pass names the same sample round after round.
            .Where(node => !node.Outputs.Any(reader => reader is TempAssignmentNode))
            .Select(node => (Node: node, Read: CountExpressions(node, readers)))
            .Where(node => node.Read > 1)
            .OrderByDescending(node => node.Read)
            .Select(node => node.Node)
            .FirstOrDefault();
        return chosen == null ? null : [[chosen]];
    }

    /// <summary>
    /// How many expressions read a node, counting the components of one instruction
    /// as the one reader they are written as. A sample's four output nodes all read
    /// its coordinate and the coordinate is written once; without this every input
    /// of a multi output instruction looks read four times over.
    /// </summary>
    private int CountExpressions(HlslTreeNode node, HashSet<HlslTreeNode> readers)
    {
        var expressions = new List<HlslTreeNode>();
        foreach (HlslTreeNode component in InstructionComponents(node, readers))
        foreach (HlslTreeNode reader in component.Outputs)
        {
            if (!readers.Contains(reader)
                || expressions.Any(e => _templateMatcher.CanGroupComponents(e, reader, false)))
            {
                continue;
            }
            expressions.Add(reader);
        }
        return expressions.Count;
    }

    private static IEnumerable<HlslTreeNode> InstructionComponents(
        HlslTreeNode node, HashSet<HlslTreeNode> readers)
    {
        yield return node;
        if (node is not IHasComponentIndex || node.Inputs.Count == 0)
        {
            yield break;
        }
        foreach (HlslTreeNode sibling in node.Inputs[0].Outputs)
        {
            if (!ReferenceEquals(sibling, node)
                && sibling.GetType() == node.GetType()
                && sibling is IHasComponentIndex
                && readers.Contains(sibling))
            {
                yield return sibling;
            }
        }
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
    private static List<HlslTreeNode[]> InWrittenOrder(List<HlslTreeNode[]> occurrences)
    {
        HlslTreeNode[] first = occurrences[0];
        if (first.Length < 2)
        {
            return occurrences;
        }
        int[] order = null;
        if (first.All(n => n is IHasComponentIndex)
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
        bool isInteger = nodes.All(node => StatementFinalizer.IsIntegerValue(node) == true);
        bool isBits = nodes.All(node => StatementFinalizer.IsBitsVariable(node, isInteger));
        // Unsigned only where every component is, and not for bits: those are a
        // float's, and calling them uint says something about them that is not so.
        bool isUnsigned = isInteger && !isBits
            && nodes.All(node => StatementFinalizer.IsUnsignedValue(node) == true);
        foreach (TempVariableNode variable in variables)
        {
            variable.IsInteger = isInteger;
            variable.IsBits = isBits;
            variable.IsUnsigned = isUnsigned;
        }
        return variables;
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

            // Width and height at mip 0 is the two-argument overload, and reads out
            // of a two-wide variable; anything more takes the full form.
            bool isSize = call.All(c => c.InfoComponent < 2) && IsConstantZero(info.MipLevel);
            // A multisampled texture's overload is three wide - width, height and
            // the sample count - and has no mip level to ask about.
            ResourceInfoNode sampleCount = call.FirstOrDefault(c => c.IsSampleCount);
            TempVariableNode[] variables = _compiler.CreateTempVariables(
                info.IsBuffer ? (info.IsRawBuffer ? 1 : 2)
                : sampleCount != null ? sampleCount.SampleCountComponent + 1
                : isSize ? 2 : 4);
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
        foreach (ConsumeNode consume in order.OfType<ConsumeNode>())
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
    private List<HlslTreeNode[]> NameCandidates(List<HlslTreeNode> candidates)
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

            // In the order the value has them, not the order they were found in:
            // the candidates are collected from the graph, which is walked from the
            // last component back, and a variable named backwards is read by every
            // swizzle reversed.
            group = [.. InWrittenOrder([[.. group]])[0]];
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
        return outputsByComponent
            .GroupBy(o => (o.Key.RegisterKey, _registers.GetOutputDeclaration(o.Key).Semantic))
            .ToDictionary(
                o => o.OrderBy(c => c.Key.ComponentIndex).First().Key,
                o => o
                    .OrderBy(o => o.Key.ComponentIndex)
                    .Select(o => o.Value)
                    .ToArray());
    }
}