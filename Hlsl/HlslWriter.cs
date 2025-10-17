using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HlslDecompiler
{
    public abstract class HlslWriter
    {
        protected readonly ShaderModel _shader;

        TextWriter internalWriter;
        protected string indent = "";

        protected HlslAst _ast;
        protected RegisterState _registers;

        public HlslWriter(ShaderModel shader)
        {
            _shader = shader;
        }

        protected abstract void WriteMethodBody();

        protected void WriteLine()
        {
            internalWriter.WriteLine();
        }

        protected void WriteLine(string value)
        {
            internalWriter.Write(indent);
            internalWriter.WriteLine(value);
        }

        protected void WriteLine(string format, params object[] args)
        {
            internalWriter.Write(indent);
            internalWriter.WriteLine(format, args);
        }

        public void Write(string hlslFilename)
        {
            using var file = new FileStream(hlslFilename, FileMode.Create, FileAccess.Write);
            using (var writer = new StreamWriter(file))
            {
                Write(writer);

                writer.Dispose();
            }

            file.Dispose();
        }

        public void Write(TextWriter writer)
        {
            internalWriter = writer;
            WriteInternal();
        }

        private void WriteInternal()
        {
            _ast = InstructionParser.Parse(_shader);
            _registers = _ast.RegisterState;

            WriteConstantDeclarations();

            if (_registers.MethodInputRegisters.Count > 1)
            {
                WriteInputStructureDeclaration();
            }

            if (_registers.MethodOutputRegisters.Count > 1)
            {
                WriteOutputStructureDeclaration();
            }

            string methodReturnType = GetMethodReturnType();
            string methodParameters = GetMethodParameters();
            string methodSemantic = GetMethodSemantic();

            WriteLine("{0} main({1}){2}", methodReturnType, methodParameters, methodSemantic);
            WriteLine("{");
            indent = "\t";

            WriteMethodBody();

            indent = "";
            WriteLine("}");
        }

        private void WriteConstantDeclarations()
        {
            if (_registers.ConstantDeclarations.Count != 0)
            {
                var compiler = new ConstantDeclarationCompiler();

                foreach (ConstantDeclaration declaration in _registers.ConstantDeclarations)
                {
                    compiler.SetStructOrder(declaration);
                }

                IList<ShaderTypeInfo> structs = compiler.GetStructDeclarations();
                for (int i = 0; i < structs.Count; i++)
                {
                    WriteLine($"struct struct{i + 1}");
                    WriteLine("{");
                    indent = "\t";
                    foreach (var member in structs[i].MemberInfo)
                    {
                        WriteLine(compiler.Compile(member));
                    }
                    indent = "";
                    WriteLine("};");
                    WriteLine();
                }

                foreach (ConstantDeclaration declaration in _registers.ConstantDeclarations)
                {
                    WriteLine(compiler.Compile(declaration));
                }
                WriteLine();
            }
        }

        private void WriteInputStructureDeclaration()
        {
            var inputStructType = _shader.Type == ShaderType.Pixel ? "PS_IN" : "VS_IN";
            WriteLine($"struct {inputStructType}");
            WriteLine("{");
            indent = "\t";
            foreach (var input in _registers.MethodInputRegisters.Values)
            {
                WriteLine(CompileRegisterDeclaration(input) + ';');
            }
            indent = "";
            WriteLine("};");
            WriteLine();
        }

        private void WriteOutputStructureDeclaration()
        {
            var outputStructType = _shader.Type == ShaderType.Pixel ? "PS_OUT" : "VS_OUT";
            WriteLine($"struct {outputStructType}");
            WriteLine("{");
            indent = "\t";
            IList<RegisterDeclaration> outputs = _registers.MethodOutputRegisters;
            if (_shader.MajorVersion == 1)
            {
                outputs = outputs.OrderBy(o => o.Semantic).ToList();
            }
            foreach (var output in outputs)
            {
                WriteLine(CompileRegisterDeclaration(output) + ';');
            }
            indent = "";
            WriteLine("};");
            WriteLine();
        }

        protected string GetMethodReturnType()
        {
            switch (_registers.MethodOutputRegisters.Count)
            {
                case 0:
                    throw new InvalidOperationException();
                case 1:
                    return _registers.MethodOutputRegisters.First().TypeName;
                default:
                    return _shader.Type == ShaderType.Pixel ? "PS_OUT" : "VS_OUT";
            }
        }

        private string GetMethodSemantic()
        {
            switch (_registers.MethodOutputRegisters.Count)
            {
                case 0:
                    throw new InvalidOperationException();
                case 1:
                    string semantic = _registers.MethodOutputRegisters.First().Semantic;
                    return $" : {semantic}";
                default:
                    return string.Empty;
            }
        }

        private string GetMethodParameters()
        {
            if (_registers.MethodInputRegisters.Count == 0)
            {
                return string.Empty;
            }
            else if (_registers.MethodInputRegisters.Count == 1)
            {
                var input = _registers.MethodInputRegisters.Values.First();
                return CompileRegisterDeclaration(input);
            }

            return _shader.Type == ShaderType.Pixel
                    ? "PS_IN i"
                    : "VS_IN i";
        }

        private static string CompileRegisterDeclaration(RegisterDeclaration input)
        {
            return $"{input.TypeName} {input.Name} : {input.Semantic}";
        }
    }
}