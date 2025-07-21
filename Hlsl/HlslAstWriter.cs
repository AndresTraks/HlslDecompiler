using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using HlslDecompiler.Hlsl.TemplateMatch;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace HlslDecompiler.Hlsl
{
    public class HlslAstWriter : HlslWriter
    {
        private NodeCompiler _compiler;
        private NodeGrouper _grouper;
        private TemplateMatcher _templateMatcher;

        public HlslAstWriter(ShaderModel shader)
            : base(shader)
        {
        }

        protected override void WriteMethodBody(TextWriter writer)
        {
            if (_registers.MethodOutputRegisters.Count > 1)
            {
                var outputStructType = _shader.Type == ShaderType.Pixel ? "PS_OUT" : "VS_OUT";
                WriteLine(writer, $"{outputStructType} o;");
                WriteLine(writer);
            }

            WriteAst(writer, _ast);
        }

        private void WriteAst(TextWriter writer, HlslAst ast)
        {
            _compiler = new NodeCompiler(_registers);
            _grouper = new NodeGrouper(_registers);
            _templateMatcher = new TemplateMatcher(_grouper);

            WriteStatement(writer, ast.Statement);
        }

        private void WriteStatement(TextWriter writer, IStatement statement)
        {
            if (statement is ClipStatement clip)
            {
                WriteClipStatement(writer, clip);
            }
            else if (statement is LoopStatement loop)
            {
                WriteLoopStatement(writer, loop);
            }
            else if (statement is BreakStatement breakStatement)
            {
                WriteBreakStatement(writer, breakStatement);
            }
            else if (statement is IfStatement ifStatement)
            {
                WriteIfStatement(writer, ifStatement);
            }
            else if (statement is ReturnStatement returnStatement)
            {
                WriteReturnStatement(writer, returnStatement);
            }
            else if (statement is StatementSequence sequence)
            {
                foreach (IStatement subStatement in sequence.Statements)
                {
                    WriteStatement(writer, subStatement);
                }
            }
        }

        private void WriteClipStatement(TextWriter writer, ClipStatement clip)
        {
            WriteTempVariableAssignments(writer, clip.Closure);

            string compiled = _compiler.Compile(Reduce(clip.Value));
            WriteLine(writer, $"clip({compiled});");
        }

        private void WriteLoopStatement(TextWriter writer, LoopStatement loop)
        {
            WriteTempVariableAssignments(writer, loop.Closure);

            string variableName = "i";
            WriteLine(writer, $"for (int {variableName} = 0; {variableName} < {loop.RepeatCount}; {variableName}++) {{");
            indent += "\t";
            WriteStatement(writer, loop.Body);
            WriteTempVariableAssignments(writer, loop.EndClosure);
            indent = indent.Substring(0, indent.Length - 1);
            WriteLine(writer, "}");
        }

        private void WriteBreakStatement(TextWriter writer, BreakStatement breakStatement)
        {
            WriteTempVariableAssignments(writer, breakStatement.Closure);

            bool? constantComparison = ConstantMatcher.TryEvaluateComparison(breakStatement.Comparison);
            if (constantComparison.HasValue && constantComparison.Value)
            {
                WriteLine(writer, "break;");
            }
            else
            {
                string comparison = _compiler.Compile(Reduce(breakStatement.Comparison));
                WriteLine(writer, $"if ({comparison}) {{");
                indent += "\t";
                WriteLine(writer, "break;");
                indent = indent.Substring(0, indent.Length - 1);
                WriteLine(writer, "}");
            }
        }

        private void WriteIfStatement(TextWriter writer, IfStatement ifStatement)
        {
            WriteTempVariableAssignments(writer, ifStatement.Closure);

            string comparison = _compiler.Compile(Reduce(ifStatement.Comparison));
            WriteLine(writer, $"if ({comparison}) {{");
            indent += "\t";
            WriteStatement(writer, ifStatement.TrueBody);
            WriteTempVariableAssignments(writer, ifStatement.TrueEndClosure);
            indent = indent.Substring(0, indent.Length - 1);
            if (ifStatement.FalseBody != null)
            {
                WriteLine(writer, "} else {");
                indent += "\t";
                WriteStatement(writer, ifStatement.FalseBody);
                WriteTempVariableAssignments(writer, ifStatement.FalseEndClosure);
                indent = indent.Substring(0, indent.Length - 1);
                WriteLine(writer, "}");
            }
            else
            {
                WriteLine(writer, "}");
            }
        }

        private void WriteReturnStatement(TextWriter writer, ReturnStatement returnStatement)
        {
            Dictionary<RegisterKey, HlslTreeNode> outputs =
                GroupComponents(returnStatement.Closure.Outputs.Where(o => o.Key.RegisterKey.IsOutput))
                    .ToDictionary(r => r.Key, r => Reduce(r.Value));

            if (outputs.Count == 1)
            {
                string compiled = _compiler.Compile(outputs.Single().Value);
                WriteLine(writer, $"return {compiled};");
            }
            else
            {
                foreach (var rootGroup in outputs)
                {
                    RegisterDeclaration outputRegister = _registers.MethodOutputRegisters[rootGroup.Key];
                    string compiled = _compiler.Compile(rootGroup.Value);
                    WriteLine(writer, $"o.{outputRegister.Name} = {compiled};");
                }
                WriteLine(writer);
                WriteLine(writer, $"return o;");
            }
        }

        private void WriteTempVariableAssignments(TextWriter writer, Closure closure)
        {
            Dictionary<RegisterKey, TempAssignmentNode[]> assignments = GroupAssignments(closure);
            foreach (var assignment in assignments)
            {
                foreach (var declaration in assignment.Value.GroupBy(v => v.TempVariable.DeclarationIndex))
                {
                    var group = new GroupNode(declaration.ToArray());
                    var reduced = Reduce(group);
                    string compiled = _compiler.Compile(reduced);
                    WriteLine(writer, compiled);
                }
            }
        }

        private HlslTreeNode Reduce(HlslTreeNode node)
        {
            return _templateMatcher.Reduce(node);
        }

        private static Dictionary<RegisterKey, TempAssignmentNode[]> GroupAssignments(Closure closure)
        {
            return closure.Outputs
                .Where(o => o.Value is TempAssignmentNode)
                .Select(o => o.Value as TempAssignmentNode)
                .OrderBy(o => o.TempVariable.RegisterComponentKey.ComponentIndex)
                .GroupBy(o => o.TempVariable.RegisterComponentKey.RegisterKey)
                .ToDictionary(
                    o => o.Key,
                    o => o.ToArray());
        }

        private static Dictionary<RegisterKey, HlslTreeNode> GroupComponents(IEnumerable<KeyValuePair<RegisterComponentKey, HlslTreeNode>> outputsByComponent)
        {
            return outputsByComponent
                .OrderBy(o => o.Key.ComponentIndex)
                .GroupBy(o => o.Key.RegisterKey)
                .ToDictionary(
                    o => o.Key,
                    o => (HlslTreeNode)new GroupNode(o.Select(o => o.Value).ToArray()));
        }
    }
}