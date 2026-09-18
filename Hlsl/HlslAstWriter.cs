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

    public HlslAstWriter(ShaderModel shader)
        : base(shader)
    {
    }

    protected override void WriteMethodBody()
    {
        if (_registers.MethodOutputRegisters.Count > 1)
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
        WriteStatements(ast.Statements);
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
        else if (statement is StoreStructuredStatement storeStructured)
        {
            WriteStoreStructuredStatement(storeStructured);
        }
        else if (statement is IndexableTempStoreStatement indexableTempStore)
        {
            WriteIndexableTempStoreStatement(indexableTempStore);
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
            WriteLine(_compiler.Compile(group));
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
                WriteLine(_compiler.Compile(group));
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

        // An output that reads a temp's value as this statement computes it prints
        // after that temp's assignment; one that reads what the register held before
        // prints before the reassignment that overwrites it. Which of the two an
        // output is was recorded at lowering, since afterwards both are the same
        // variable name. Ordering temps and outputs together, rather than as two
        // hardcoded passes, is what lets TempAssignmentOrder see either dependency.
        var writes = new List<(HlslTreeNode[] Nodes, TempAssignmentNode[] Wants, Action Write)>();
        foreach (var group in tempGroups)
        {
            writes.Add((group, [], () => WriteLine(_compiler.Compile(group))));
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

    private void WriteStoreStructuredStatement(StoreStructuredStatement storeStructured)
    {
        // The buffer is named without a swizzle - the subscript selects the element,
        // and any component selection belongs after it, not on the buffer.
        string compiledDestination = _registers.GetRegisterName(
            ((RegisterInputNode)storeStructured.Destination).RegisterComponentKey.RegisterKey);
        string compiledAddress = _compiler.Compile(Reduce(storeStructured.Address));
        string compiledValue = _compiler.Compile(storeStructured.Values.Select(Reduce));
        if (storeStructured.IsRaw)
        {
            // Store, Store2, Store3 or Store4 at the byte offset, by how many dwords
            // are written.
            string method = storeStructured.Values.Length == 1 ? "Store" : $"Store{storeStructured.Values.Length}";
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
                string run = _compiler.Compile(values.Select(v => Reduce(storeStructured.Values[v])));
                WriteLine($"{compiledDestination}[{compiledAddress}].{name} = {run};");
            }
            return;
        }
        WriteLine($"{compiledDestination}[{compiledAddress}] = {compiledValue};");
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
        WriteStatements(loop.Body);
        _compiler.LoopVariableName = enclosingLoopVariable;
        _loopDepth--;
        indent = indent.Substring(0, indent.Length - 1);
        WriteLine("}");
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
        bool outputInScope = _registers.MethodOutputRegisters.Count > 1;

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

        string comparison = _compiler.Compile(ifStatement.Comparison.Select(Reduce));
        WriteLine($"if ({comparison}) {{");
        indent += "\t";
        WriteStatements(ifStatement.TrueBody);
        indent = indent.Substring(0, indent.Length - 1);
        if (ifStatement.FalseBody != null)
        {
            WriteLine("} else {");
            indent += "\t";
            WriteStatements(ifStatement.FalseBody);
            indent = indent.Substring(0, indent.Length - 1);
            WriteLine("}");
        }
        else
        {
            WriteLine("}");
        }
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
                string type = variable.IsInteger ? "int" : "float";
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
        bool hasOutputStruct = _registers.MethodOutputRegisters.Count > 1;
        Dictionary<RegisterComponentKey, HlslTreeNode[]> outputs =
            GroupComponents(returnStatement.Outputs
                    .Where(o => o.Key.RegisterKey.IsOutput)
                    .Where(o => !(hasOutputStruct
                        && returnStatement.Inputs.TryGetValue(o.Key, out var inputNode)
                        && o.Value == inputNode)))
                .ToDictionary(r => r.Key, r => r.Value.Select(n => Reduce(n)).ToArray());

        // The returned expression is compiled straight from here rather than through
        // GroupAssignments, so the hoist has to happen here too.
        WriteSharedSubexpressions(outputs.Values.ToList());

        string condition = returnStatement.Comparison == null
            ? null
            : _compiler.Compile(Reduce(returnStatement.Comparison));

        if (!hasOutputStruct)
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

    /// <summary>
    /// How large the expression has to get, written out in full, before it is named
    /// by size. The text pass below measures what is actually repeated, but it has
    /// to write the expression out to do that, and past this point writing it out
    /// is what cannot be afforded.
    /// </summary>
    private const int InlinedSizeBudget = 500;

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

        // Sharing alone is not a reason to name something - almost every expression
        // shares a register read. What is worth naming is what the text repeats,
        // measured on the text; only an expression that explodes when written out
        // is cut down by size first, so that there is a text to measure.
        var inlinedSize = new Dictionary<HlslTreeNode, long>(ReferenceEqualityComparer.Instance);
        long total = 0;
        foreach (HlslTreeNode root in roots)
        {
            total += InlinedSize(root, order, inlinedSize);
        }
        if (total <= InlinedSizeBudget)
        {
            resourceInfo.AddRange(NameRepeatedText(registerGroups, resourceInfo, roots));
            return Renumber(resourceInfo);
        }

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
            if (CountReachable(node) <= SharedSubexpressionThreshold)
            {
                continue;
            }
            candidates.Add(node);
        }
        resourceInfo.AddRange(NameCandidates(candidates));
        resourceInfo.AddRange(NameRepeatedText(registerGroups, resourceInfo, roots));
        return Renumber(resourceInfo);
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
    private List<HlslTreeNode[]> NameRepeatedText(
        IList<HlslTreeNode[]> registerGroups, List<HlslTreeNode[]> named, HashSet<HlslTreeNode> roots)
    {
        var assignments = new List<HlslTreeNode[]>();
        while (true)
        {
            IEnumerable<HlslTreeNode[]> groups = registerGroups.Concat(named).Concat(assignments);
            var recording = new List<(HlslTreeNode[] Nodes, string Text)>();
            _compiler.Recording = recording;
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
            }

            // A repeat is the same nodes compiled again - or the same but for a
            // constant, which may be a node of its own at each: a template that
            // builds one builds one per match, and the 1 in the w of four dot
            // products is four nodes.
            HlslTreeNode[] candidate = recording
                .GroupBy(r => new NodeList(Broadcast(r.Nodes)))
                .Select(g => (g.Key.Nodes, Repeated: (g.Count() - 1) * g.First().Text.Length))
                .Where(r => r.Repeated >= RepeatedTextBudget)
                .Where(r => r.Nodes.All(n => (n is Operation || n is TextureLoadOutputNode || n is ConstantNode) && !roots.Contains(n)))
                .Where(r => r.Nodes.Any(n => n is not ConstantNode))
                .Where(r => r.Nodes.Distinct(ReferenceEqualityComparer.Instance).Count() == r.Nodes.Length)
                .OrderByDescending(r => r.Repeated)
                .Select(r => r.Nodes)
                .FirstOrDefault();
            if (candidate == null)
            {
                return assignments;
            }

            // Only this statement's readers are given the variable. The graph is
            // shared with every other statement that reads the value, and one of
            // those may be outside the block this one is in.
            HashSet<HlslTreeNode> readers = HlslTreeNode.NewNodeSet();
            foreach (HlslTreeNode node in Reachable(groups.SelectMany(g => g)))
            {
                readers.Add(node);
            }
            TempVariableNode[] variables = CreateTempVariables(candidate);
            for (int i = 0; i < candidate.Length; i++)
            {
                if (candidate[i] is not ConstantNode)
                {
                    Rewire(candidate[i], variables[i], readers);
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
            assignments.Add([.. candidate.Select((node, i) => (HlslTreeNode)new TempAssignmentNode(variables[i], node))]);
        }
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
        foreach (TempVariableNode variable in variables)
        {
            variable.IsInteger = isInteger;
            variable.IsBits = isBits;
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
            TempVariableNode[] variables = _compiler.CreateTempVariables(isSize ? 2 : 4);
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

    private static bool IsSameResourceInfoCall(ResourceInfoNode a, ResourceInfoNode b)
    {
        return a.ReturnType == b.ReturnType
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
                // Against the first, which is how GroupComponents reads a run too.
                if (nodeGrouper.CanGroupComponents(candidate, other))
                {
                    group.Add(other);
                    named.Add(other);
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
    private static long InlinedSize(
        HlslTreeNode root, IList<HlslTreeNode> order, Dictionary<HlslTreeNode, long> sizes)
    {
        for (int i = order.Count - 1; i >= 0; i--)
        {
            HlslTreeNode node = order[i];
            long size = 1;
            foreach (HlslTreeNode input in HlslTreeNode.TraversableInputs(node))
            {
                size += sizes.TryGetValue(input, out long inputSize) ? inputSize : 1;
                if (size > int.MaxValue)
                {
                    size = int.MaxValue;
                }
            }
            sizes[node] = size;
        }
        return sizes.TryGetValue(root, out long rootSize) ? rootSize : 1;
    }

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