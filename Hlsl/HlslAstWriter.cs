using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using HlslDecompiler.Hlsl.TemplateMatch;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class HlslAstWriter : HlslWriter
    {
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
            var compiler = new NodeCompiler(_registers);
            var nodeGrouper = new NodeGrouper(_registers);
            var templateMatcher = new TemplateMatcher(nodeGrouper);

            for (int i = 0; i < ast.Statements.Count; i++)
            {
                IStatement statement = ast.Statements[i];
                if (statement is StatementSequence sequence)
                {
                    bool isLastStatement = i == ast.Statements.Count - 1;

                    Dictionary<RegisterKey, HlslTreeNode> roots = isLastStatement
                        ? sequence.GroupOutputs()
                        : sequence.GroupAssignments();
                    roots = roots.ToDictionary(r => r.Key, r => templateMatcher.Reduce(r.Value));

                    if (isLastStatement)
                    {
                        if (roots.Count == 1)
                        {
                            string compiled = compiler.Compile(roots.Single().Value);
                            WriteLine($"return {compiled};");
                        }
                        else
                        {
                            foreach (var rootGroup in roots)
                            {
                                RegisterDeclaration outputRegister = _registers.MethodOutputRegisters[rootGroup.Key];
                                string compiled = compiler.Compile(rootGroup.Value);
                                WriteLine($"o.{outputRegister.Name} = {compiled};");
                            }
                            WriteLine();
                            WriteLine($"return o;");
                        }
                    }
                    else
                    {
                        foreach (var rootGroup in roots)
                        {
                            string registerName = _registers.GetRegisterName(rootGroup.Key);
                            string compiled = compiler.Compile(rootGroup.Value);
                            WriteLine($"float4 {registerName} = {compiled};");
                        }
                    }
                }
                else if (statement is ClipStatement clip)
                {
                    string compiled = compiler.Compile(clip.Value);
                    WriteLine($"clip({compiled});");
                }
            }
        }
    }
}
