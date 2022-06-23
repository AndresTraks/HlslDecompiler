using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
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
            ast.ReduceTree(new NodeGrouper(_registers));

            WriteAst(ast);
        }

        private void WriteAst(HlslAst ast)
        {
            var compiler = new NodeCompiler(_registers);

            foreach (var rootGroup in ast.NoOutputInstructions)
            {
                string statement = compiler.Compile(rootGroup.Value);
                WriteLine($"{statement};");
            }

            if (ast.Roots.Count == 1)
            {
                string statement = compiler.Compile(ast.Roots.Single().Value);
                WriteLine($"return {statement};");
            }
            else
            {
                foreach (var rootGroup in ast.Roots)
                {
                    RegisterDeclaration outputRegister = _registers.MethodOutputRegisters[rootGroup.Key];
                    string statement = compiler.Compile(rootGroup.Value);
                    WriteLine($"o.{outputRegister.Name} = {statement};");
                }

                WriteLine();
                WriteLine($"return o;");
            }
        }
    }
}
