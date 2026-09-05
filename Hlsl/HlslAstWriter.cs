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
    private TempAssignmentOrder _tempAssignmentOrder = new TempAssignmentOrder();
    private int _loopDepth;

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

        StatementFinalizer.Finalize(ast.Statements, GetMethodReturnType() != "void");
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
        string compiledDestination = _compiler.Compile(Reduce(storeStructured.Destination));
        string compiledAddress = _compiler.Compile(Reduce(storeStructured.Address));
        string compiledValue = _compiler.Compile(Reduce(storeStructured.Value));
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
                string size = variable.VariableSize != 1 ? variable.VariableSize.ToString() : "";
                WriteLine($"float{size} t{variable.DeclarationIndex};");
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

        if (outputs.Count == 1)
        {
            string compiled = _compiler.Compile(outputs.Single().Value);
            WriteLine($"return {compiled};");
        }
        else
        {
            foreach (var rootGroup in outputs.OrderBy(t => t.Value, _tempAssignmentOrder).ThenBy(o => o.Key.Number))
            {
                RegisterDeclaration outputRegister = _registers.RegisterDeclarations[rootGroup.Key];
                string compiled = _compiler.Compile(rootGroup.Value);
                WriteLine($"o.{outputRegister.Name} = {compiled};");
            }
            WriteLine();
            WriteLine($"return o;");
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
        foreach (var registerGroup in outputs
            .Where(o => o.Key.RegisterKey.IsTempRegister || o.Key.RegisterKey.IsOutput)
            .OrderBy(o => o.Key.ComponentIndex)
            .GroupBy(o => o.Key.RegisterKey)
            .Select(o => o.Select(c => Reduce(c.Value)).ToArray())
            .Order(_tempAssignmentOrder))
        {
            var registerNodes = registerGroup.ToList();
            _compiler.Compile(registerNodes);
            foreach (var componentGroup in nodeGrouper.GroupComponents(registerNodes)) {
                groups.Add(componentGroup.ToArray());
            }
        }
        groups.Sort(_tempAssignmentOrder);
        return groups;
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