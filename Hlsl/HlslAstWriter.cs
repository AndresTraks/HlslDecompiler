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

            WriteAst(ast);
        }

        private void WriteAst(HlslAst ast)
        {
            var compiler = new NodeCompiler(_registers);

            List<Dictionary<RegisterKey, HlslTreeNode>> sequenceRoots = ast.ReduceTree(new NodeGrouper(_registers));
            foreach (var roots in sequenceRoots)
            {
                if (roots.Count == 1)
                {
                    if (roots.First().Value.Inputs.First() is ClipOperation)
                    {
                        string statement = compiler.Compile(roots.First().Value);
                        WriteLine($"{statement};");
                    }
                    else
                    {
                        string statement = compiler.Compile(roots.Single().Value);
                        WriteLine($"return {statement};");
                    }
                }
                else
                {
                    foreach (var rootGroup in roots)
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
}
