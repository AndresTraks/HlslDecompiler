using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler
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

            var parser = new BytecodeParser();
            HlslAst ast = parser.Parse(_shader);
            ast.ReduceTree();

            WriteAst(ast);
        }

        private void WriteAst(HlslAst ast)
        {
            var compiler = new NodeCompiler(_registers);

            var noOutputInstructionRoots = ast.NoOutputInstructions.GroupBy(r => r.Key.RegisterKey);
            foreach (var rootGroup in noOutputInstructionRoots)
            {
                string statement = CompileRootStatement(compiler, rootGroup);
                WriteLine($"{statement};");
            }

            var rootGroups = ast.Roots.GroupBy(r => r.Key.RegisterKey);
            if (_registers.MethodOutputRegisters.Count == 1)
            {
                string statement = CompileRootStatement(compiler, rootGroups.Single());
                WriteLine($"return {statement};");
            }
            else
            {
                foreach (var rootGroup in rootGroups)
                {
                    RegisterDeclaration outputRegister = _registers.MethodOutputRegisters[rootGroup.Key];
                    string statement = CompileRootStatement(compiler, rootGroup);
                    WriteLine($"o.{outputRegister.Name} = {statement};");
                }

                WriteLine();
                WriteLine($"return o;");
            }
        }

        private static string CompileRootStatement(NodeCompiler compiler, 
            IGrouping<RegisterKey, KeyValuePair<RegisterComponentKey, HlslTreeNode>> rootGroup)
        {
            var registerKey = rootGroup.Key;
            var roots = rootGroup.OrderBy(r => r.Key.ComponentIndex).Select(r => r.Value).ToList();
            return compiler.Compile(roots, roots.Count);
        }
    }
}
