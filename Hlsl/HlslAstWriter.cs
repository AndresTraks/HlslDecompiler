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

            WriteStatement(ast.Statement);
        }

        private void WriteStatement(IStatement statement)
        {
            if (statement is ClipStatement clip)
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
            else if (statement is StatementSequence sequence)
            {
                foreach (IStatement subStatement in sequence.Statements)
                {
                    WriteStatement(subStatement);
                }
            }
        }

        private void WriteClipStatement( ClipStatement clip)
        {
            WriteTempVariableAssignments(clip.Closure);

            string compiled = _compiler.Compile(Reduce(clip.Value));
            WriteLine($"clip({compiled});");
        }

        private void WriteLoopStatement(LoopStatement loop)
        {
            WriteTempVariableAssignments(loop.Closure);

            string variableName = "i";
            WriteLine($"for (int {variableName} = 0; {variableName} < {loop.RepeatCount}; {variableName}++) {{");
            indent += "\t";
            WriteStatement(loop.Body);
            WriteTempVariableAssignments(loop.EndClosure);
            indent = indent.Substring(0, indent.Length - 1);
            WriteLine("}");
        }

        private void WriteBreakStatement(BreakStatement breakStatement)
        {
            WriteTempVariableAssignments(breakStatement.Closure);

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
            WriteTempVariableAssignments(ifStatement.Closure);

            string comparison = _compiler.Compile(Reduce(ifStatement.Comparison));
            WriteLine($"if ({comparison}) {{");
            indent += "\t";
            WriteStatement(ifStatement.TrueBody);
            WriteTempVariableAssignments(ifStatement.TrueEndClosure);
            indent = indent.Substring(0, indent.Length - 1);
            if (ifStatement.FalseBody != null)
            {
                WriteLine("} else {");
                indent += "\t";
                WriteStatement(ifStatement.FalseBody);
                WriteTempVariableAssignments(ifStatement.FalseEndClosure);
                indent = indent.Substring(0, indent.Length - 1);
                WriteLine("}");
            }
            else
            {
                WriteLine("}");
            }
        }

        private void WriteReturnStatement(ReturnStatement returnStatement)
        {
            Dictionary<RegisterKey, HlslTreeNode> outputs =
                GroupComponents(returnStatement.Closure.Outputs.Where(o => o.Key.RegisterKey.IsOutput))
                    .ToDictionary(r => r.Key, r => Reduce(r.Value));

            if (outputs.Count == 1)
            {
                string compiled = _compiler.Compile(outputs.Single().Value);
                WriteLine($"return {compiled};");
            }
            else
            {
                foreach (var rootGroup in outputs)
                {
                    RegisterDeclaration outputRegister = _registers.MethodOutputRegisters[rootGroup.Key];
                    string compiled = _compiler.Compile(rootGroup.Value);
                    WriteLine($"o.{outputRegister.Name} = {compiled};");
                }
                WriteLine();
                WriteLine($"return o;");
            }
        }

        private void WriteTempVariableAssignments(Closure closure)
        {
            Dictionary<RegisterKey, TempAssignmentNode[]> assignments = GroupAssignments(closure);
            foreach (var assignment in assignments)
            {
                foreach (var declaration in assignment.Value.GroupBy(v => v.TempVariable.DeclarationIndex))
                {
                    var group = new GroupNode(declaration.ToArray());
                    var reduced = Reduce(group);
                    string compiled = _compiler.Compile(reduced);
                    WriteLine(compiled);
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