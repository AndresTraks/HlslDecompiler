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
                WriteTempVariableAssignments(breakStatement.Closure);
                WriteLine("break;");
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

        private void WriteClipStatement(ClipStatement clip)
        {
            WriteTempVariableAssignments(clip.Closure);

            string compiled = _compiler.Compile(Reduce(clip.Value));
            WriteLine($"clip({compiled});");
        }

        private void WriteLoopStatement(LoopStatement loop)
        {
            WriteTempVariableAssignments(loop.Closure);

            WriteLine($"for (int i = 0; i < {loop.RepeatCount}; i++) {{");
            indent += "\t";
            WriteStatement(loop.Body);
            WriteTempVariableAssignments(loop.EndClosure);
            indent = indent.Substring(0, indent.Length - 1);
            WriteLine("}");
        }

        private void WriteIfStatement(IfStatement ifStatement)
        {
            WriteTempVariableAssignments(ifStatement.Closure);

            var left = _compiler.Compile(ifStatement.Left);
            var right = _compiler.Compile(ifStatement.Right);
            string comparison;
            switch (ifStatement.Comparison)
            {
                case IfComparison.GT: comparison = ">"; break;
                case IfComparison.EQ: comparison = "=="; break;
                case IfComparison.GE: comparison = ">="; break;
                case IfComparison.LT: comparison = "<"; break;
                case IfComparison.NE: comparison = "!="; break;
                case IfComparison.LE: comparison = "<="; break;
                default: throw new NotImplementedException(ifStatement.Comparison.ToString());
            }
            WriteLine($"if ({left} {comparison} {right}) {{");
            indent += "\t";
            WriteStatement(ifStatement.TrueBody);
            WriteTempVariableAssignments(ifStatement.EndClosure);
            indent = indent.Substring(0, indent.Length - 1);
            WriteLine("}");
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
            Dictionary<RegisterKey, HlslTreeNode> roots = GroupAssignments(closure)
                                .ToDictionary(r => r.Key, r => Reduce(r.Value));

            foreach (var rootGroup in roots)
            {
                string registerName = _registers.GetRegisterName(rootGroup.Key);
                string compiled = _compiler.Compile(rootGroup.Value);
                WriteLine(compiled);
            }
        }

        private HlslTreeNode Reduce(HlslTreeNode node)
        {
            return _templateMatcher.Reduce(node);
        }

        public static Dictionary<RegisterKey, HlslTreeNode> GroupAssignments(Closure closure)
        {
            return GroupComponents(closure.Outputs.Where(o => o.Value is TempAssignmentNode));
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
