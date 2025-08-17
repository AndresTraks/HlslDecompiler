using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using HlslDecompiler.Hlsl.TemplateMatch;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class HlslAstWriter : HlslWriter
    {
        private NodeCompiler _compiler;
        private NodeGrouper _grouper;
        private TemplateMatcher _templateMatcher;
        private TempAssignmentOrder _tempAssignmentOrder = new TempAssignmentOrder();

        public HlslAstWriter(ShaderModel shader)
            : base(shader)
        {
        }

        protected override void WriteMethodBody()
        {
            if (_registers.MethodOutputRegisters.Count > 1)
            {
                var outputStructType = _shader.Type == ShaderType.Pixel ? "PS_OUT" : "VS_OUT";
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

            StatementFinalizer.Optimize(ast.Statements);
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
            else if (statement is ClipStatement clip)
            {
                WriteClipStatement(clip);
            }
            else if (statement is LoopStatement loop)
            {
                WriteLoopStatement(loop);
            }
            else if (statement is BreakStatement breakStatement)
            {
                WriteBreakStatement(breakStatement);
            }
            else if (statement is IfStatement ifStatement)
            {
                WriteIfStatement(ifStatement);
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

        private void WriteAssignmentStatement(AssignmentStatement assignmentStatement)
        {
            IEnumerable<KeyValuePair<RegisterComponentKey, HlslTreeNode>> tempComponents =
                assignmentStatement.Outputs
                    .Where(o => {
                        if (!o.Key.RegisterKey.IsTempRegister)
                        {
                            return false;
                        }
                        if (assignmentStatement.Inputs.TryGetValue(o.Key, out var inputNode) && o.Value == inputNode)
                        {
                            return false;
                        }
                        return true;
                    });
            Dictionary<RegisterKey, HlslTreeNode> temps = GroupComponents(tempComponents)
                    .ToDictionary(r => r.Key, r => Reduce(r.Value));
            foreach (var temp in temps.OrderBy(t => t.Value, _tempAssignmentOrder))
            {
                string compiled = _compiler.Compile(temp.Value);
                if (temp.Value is GroupNode && temp.Value.Inputs.All(i => i is TempAssignmentNode))
                {
                    WriteLine(compiled);
                }
                else
                {
                    string size = temp.Value.Inputs.Count > 1 ? temp.Value.Inputs.Count.ToString() : "";
                    WriteLine($"float{size} t{temp.Key.Number} = {compiled};");
                }
            }

            Dictionary<RegisterKey, HlslTreeNode> outputs =
                GroupComponents(assignmentStatement.Outputs.Where(o => o.Key.RegisterKey.IsOutput))
                    .ToDictionary(r => r.Key, r => Reduce(r.Value));
            foreach (var rootGroup in outputs.OrderBy(o => o.Key.Number))
            {
                RegisterDeclaration outputRegister = _registers.MethodOutputRegisters[rootGroup.Key];
                string compiled = _compiler.Compile(rootGroup.Value);
                WriteLine($"o.{outputRegister.Name} = {compiled};");
            }
        }

        private void WriteClipStatement( ClipStatement clip)
        {
            string compiled = _compiler.Compile(Reduce(clip.Value));
            WriteLine($"clip({compiled});");
        }

        private void WriteLoopStatement(LoopStatement loop)
        {
            string variableName = "i";
            WriteLine($"for (int {variableName} = 0; {variableName} < {loop.RepeatCount}; {variableName}++) {{");
            indent += "\t";
            WriteStatements(loop.Body);
            indent = indent.Substring(0, indent.Length - 1);
            WriteLine("}");
        }

        private void WriteBreakStatement(BreakStatement breakStatement)
        {
            bool? constantComparison = ConstantMatcher.TryEvaluateComparison(breakStatement.Comparison);
            if (constantComparison.HasValue && constantComparison.Value)
            {
                WriteLine("break;");
            }
            else
            {
                string comparison = _compiler.Compile(Reduce(breakStatement.Comparison));
                WriteLine($"if ({comparison}) {{");
                indent += "\t";
                WriteLine("break;");
                indent = indent.Substring(0, indent.Length - 1);
                WriteLine("}");
            }
        }

        private void WriteIfStatement(IfStatement ifStatement)
        {
            WriteIfStatementTempVariables(ifStatement);

            string comparison = _compiler.Compile(Reduce(ifStatement.Comparison));
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
            var newAssignments = ifStatement.Outputs
                .Where(o => o.Value is TempVariableNode)
                .Where(o => !ifStatement.Inputs.ContainsKey(o.Key))
                .ToDictionary();
            if (newAssignments.Count > 0)
            {
                foreach (var group in GroupAssignments(newAssignments))
                {
                    // Compile variable with all components
                    _compiler.Compile(group.Value);

                    var variable = group.Value.First() as TempVariableNode;
                    string size = variable.VariableSize != 1 ? variable.VariableSize.ToString() : "";
                    WriteLine($"float{variable.VariableSize} t{variable.DeclarationIndex};");
                }
            }
        }

        private void WriteReturnStatement(ReturnStatement returnStatement)
        {
            Dictionary<RegisterKey, HlslTreeNode> outputs =
                GroupComponents(returnStatement.Outputs.Where(o => o.Key.RegisterKey.IsOutput))
                    .ToDictionary(r => r.Key, r => Reduce(r.Value));

            if (outputs.Count == 1)
            {
                string compiled = _compiler.Compile(outputs.Single().Value);
                WriteLine($"return {compiled};");
            }
            else
            {
                foreach (var rootGroup in outputs.OrderBy(o => o.Key.Number))
                {
                    RegisterDeclaration outputRegister = _registers.MethodOutputRegisters[rootGroup.Key];
                    string compiled = _compiler.Compile(rootGroup.Value);
                    WriteLine($"o.{outputRegister.Name} = {compiled};");
                }
                WriteLine();
                WriteLine($"return o;");
            }
        }

        private HlslTreeNode Reduce(HlslTreeNode node)
        {
            return _templateMatcher.Reduce(node);
        }

        private static Dictionary<RegisterKey, HlslTreeNode[]> GroupAssignments(IDictionary<RegisterComponentKey, HlslTreeNode> outputs)
        {
            return outputs
                .Where(o => o.Key.RegisterKey.IsTempRegister || o.Key.RegisterKey.IsOutput)
                .OrderBy(o => o.Key.ComponentIndex)
                .GroupBy(o => o.Key.RegisterKey)
                .ToDictionary(
                    o => o.Key,
                    o => o.Select(o => o.Value).ToArray());
        }

        private static Dictionary<RegisterKey, GroupNode> GroupComponents(IEnumerable<KeyValuePair<RegisterComponentKey, HlslTreeNode>> outputsByComponent)
        {
            return outputsByComponent
                .OrderBy(o => o.Key.ComponentIndex)
                .GroupBy(o => o.Key.RegisterKey)
                .ToDictionary(
                    o => o.Key,
                    o => new GroupNode(o.Select(o => o.Value).ToArray()));
        }
    }
}