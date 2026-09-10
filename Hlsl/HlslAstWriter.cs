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
            WriteLine($"{outputStructType} o;");
            WriteLine();
        }

        WriteAst(_ast);
    }

    private void WriteAst(HlslAst ast)
    {
        _compiler = new NodeCompiler(_registers);
        _grouper = new NodeGrouper(_registers);
        _templateMatcher = new TemplateMatcher(_grouper);

        StatementFinalizer.Finalize(ast.Statements, GetMethodReturnType() != "void",
            _shader.Instructions.Count != 0 && _shader.Instructions[0] is D3D10Instruction
                ? new IntegerOperandAnalysis(_shader)
                : null);
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
        foreach (var temp in GroupAssignments(tempComponents))
        {
            string compiled = _compiler.Compile(temp);
            WriteLine(compiled);
        }
    }

    private void WriteAssignmentStatement(AssignmentStatement assignmentStatement)
    {
        WriteStatementTempAssignments(assignmentStatement);

        // With one output register there is no struct to write into: every return
        // compiles the live value, so writing `o.name = ...` first names something
        // that was never declared. A geometry shader is the exception - it writes the
        // struct and appends it rather than returning.
        if (_shader.Type != ShaderType.Geometry && _registers.MethodOutputRegisters.Count <= 1)
        {
            return;
        }

        // Skip output registers the statement merely carries forward unchanged, the
        // same way temps are filtered above. Without this every statement re-emits
        // every output, which shows up as duplicated writes after a stream append.
        Dictionary<RegisterKey, HlslTreeNode[]> outputs =
            GroupComponents(assignmentStatement.Outputs
                    .Where(o => o.Key.RegisterKey.IsOutput)
                    .Where(o => !(assignmentStatement.Inputs.TryGetValue(o.Key, out var inputNode)
                        && o.Value == inputNode)))
                .ToDictionary(r => r.Key, r => r.Value.Select(n => Reduce(n)).ToArray());
        foreach (var rootGroup in outputs.OrderBy(o => o.Key.Number))
        {
            RegisterDeclaration outputRegister = _registers.RegisterDeclarations[rootGroup.Key];
            string compiled = _compiler.Compile(rootGroup.Value);
            WriteLine($"o.{outputRegister.Name} = {compiled};");
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
        WriteLine($"{compiledDestination}[{compiledAddress}] = {compiledValue};");
    }

    private void WriteClipStatement( ClipStatement clip)
    {
        string compiled = _compiler.Compile(clip.Values.Select(Reduce));
        WriteLine($"clip({compiled});");
    }

    private void WriteLoopStatement(LoopStatement loop)
    {
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

    // Nested loops must not shadow the enclosing loop's counter.
    private static string GetLoopVariableName(int depth)
    {
        return depth < 3 ? new string((char)('i' + depth), 1) : $"i{depth}";
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

        Dictionary<RegisterKey, HlslTreeNode[]> outputs =
            GroupComponents(returnStatement.Outputs.Where(o => o.Key.RegisterKey.IsOutput))
                .ToDictionary(r => r.Key, r => r.Value.Select(n => Reduce(n)).ToArray());

        // The returned expression is compiled straight from here rather than through
        // GroupAssignments, so the hoist has to happen here too.
        WriteSharedSubexpressions(outputs.Values.ToList());

        string condition = returnStatement.Comparison == null
            ? null
            : _compiler.Compile(Reduce(returnStatement.Comparison));

        if (outputs.Count == 1)
        {
            string compiled = _compiler.Compile(outputs.Single().Value);
            WriteLine(condition == null
                ? $"return {compiled};"
                : $"if ({condition}) return {compiled};");
        }
        else if (condition != null)
        {
            // The outputs were written by the statements before this one; a
            // conditional return only chooses whether to leave with them.
            WriteLine($"if ({condition}) return o;");
        }
        else
        {
            foreach (var rootGroup in TempAssignmentOrder.Sort(
                outputs.OrderBy(o => o.Key.Number), o => o.Value))
            {
                RegisterDeclaration outputRegister = _registers.RegisterDeclarations[rootGroup.Key];
                string compiled = _compiler.Compile(rootGroup.Value);
                WriteLine($"o.{outputRegister.Name} = {compiled};");
            }
            WriteLine();
            WriteLine($"return o;");
        }
    }

    private void WriteSharedSubexpressions(IList<HlslTreeNode[]> roots)
    {
        List<TempAssignmentNode> assignments = HoistSharedSubexpressions(roots).ToList();
        assignments = TempAssignmentOrder.SortNodes(assignments);
        foreach (TempAssignmentNode assignment in assignments)
        {
            WriteLine(_compiler.Compile([assignment]));
        }
    }

    private HlslTreeNode Reduce(HlslTreeNode node)
    {
        node = _templateMatcher.Reduce(node);
        NodeFinalizer.Finalize([node]);
        return node;
    }

    private List<HlslTreeNode[]> GroupAssignments(IDictionary<RegisterComponentKey, HlslTreeNode> outputs)
    {
        var nodeGrouper = new NodeGrouper(_registers);

        var groups = new List<HlslTreeNode[]>();
        var registerGroups = outputs
            .Where(o => o.Key.RegisterKey.IsTempRegister || o.Key.RegisterKey.IsOutput)
            .OrderBy(o => o.Key.ComponentIndex)
            .GroupBy(o => o.Key.RegisterKey)
            .Select(o => o.Select(c => Reduce(c.Value)).ToArray())
            .ToList();
        registerGroups = TempAssignmentOrder.Sort(registerGroups);

        // After reducing, not before: naming a subexpression hides it from the
        // templates, and a node feeding four components would be named rather than
        // broadcast.
        foreach (TempAssignmentNode hoisted in HoistSharedSubexpressions(registerGroups))
        {
            groups.Add([hoisted]);
        }

        foreach (var registerGroup in registerGroups)
        {
            var registerNodes = registerGroup.ToList();
            _compiler.Compile(registerNodes);
            foreach (var componentGroup in nodeGrouper.GroupComponents(registerNodes)) {
                groups.Add(componentGroup.ToArray());
            }
        }
        return TempAssignmentOrder.Sort(groups);
    }

    /// <summary>
    /// An expression read in more than one place is written out at each of them, so a
    /// value built on top of a value built on top of a value doubles the output at
    /// every level. Ten instructions of that reach sixty thousand characters.
    ///
    /// Naming one costs a line, so only the ones big enough to be worth it are named:
    /// below the threshold the output stays as it was, which is the whole of every
    /// shader here.
    /// </summary>
    private const int SharedSubexpressionThreshold = 8;

    /// <summary>
    /// How large the expression has to get, written out in full, before any of it is
    /// worth naming. Naming costs a line and hides the expression from the grouping
    /// that turns four dot products back into a matrix multiply, so it is only done
    /// where the alternative is unreadable anyway.
    /// </summary>
    private const int InlinedSizeBudget = 500;

    private IEnumerable<TempAssignmentNode> HoistSharedSubexpressions(
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

        // Sharing alone is not a reason to name something - almost every expression
        // shares a register read. Only an expression that explodes when written out is.
        var inlinedSize = new Dictionary<HlslTreeNode, long>(ReferenceEqualityComparer.Instance);
        long total = 0;
        foreach (HlslTreeNode root in roots)
        {
            total += InlinedSize(root, order, inlinedSize);
        }
        if (total <= InlinedSizeBudget)
        {
            return [];
        }

        var assignments = new List<TempAssignmentNode>();
        // Deepest first, so that a shared node inside another one is named before the
        // node containing it stops being reachable from here.
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
            assignments.Add(NameSubexpression(node, _compiler.CreateScalarTempVariable()));
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
        HlslTreeNode[] readers = node.Outputs.ToArray();
        foreach (HlslTreeNode reader in readers)
        {
            for (int i = 0; i < reader.Inputs.Count; i++)
            {
                if (ReferenceEquals(reader.Inputs[i], node))
                {
                    reader.Inputs[i] = variable;
                    variable.Outputs.Add(reader);
                }
            }
        }
        node.Outputs.Clear();
        return new TempAssignmentNode(variable, node);
    }

    private static Dictionary<RegisterKey, HlslTreeNode[]> GroupComponents(IEnumerable<KeyValuePair<RegisterComponentKey, HlslTreeNode>> outputsByComponent)
    {
        return outputsByComponent
            .GroupBy(o => o.Key.RegisterKey)
            .ToDictionary(
                o => o.Key,
                o => o
                    .OrderBy(o => o.Key.ComponentIndex)
                    .Select(o => o.Value)
                    .ToArray());
    }
}