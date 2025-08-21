using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using System;
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

        private static string GetConstantTypeName(ConstantDeclaration declaration)
        {
            switch (declaration.ParameterClass)
            {
                case ParameterClass.Scalar:
                    return declaration.ParameterType.ToString().ToLower();
                case ParameterClass.Vector:
                    if (declaration.ParameterType == ParameterType.Float)
                    {
                        return "float" + declaration.Columns;
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                case ParameterClass.MatrixColumns:
                case ParameterClass.MatrixRows:
                    if (declaration.ParameterType == ParameterType.Float)
                    {
                        return $"float{declaration.Rows}x{declaration.Columns}";
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                case ParameterClass.Object:
                    switch (declaration.ParameterType)
                    {
                        case ParameterType.Sampler1D:
                            return "sampler1D";
                        case ParameterType.Sampler2D:
                            return "sampler2D";
                        case ParameterType.Sampler3D:
                            return "sampler3D";
                        case ParameterType.SamplerCube:
                            return "samplerCUBE";
                        default:
                            throw new NotImplementedException();
                    }
            }
            throw new NotImplementedException();
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
                int samplerIndex = 0;
                foreach (ConstantDeclaration declaration in _registers.ConstantDeclarations)
                {
                    string typeName = GetConstantTypeName(declaration);
                    string registerSpecifier = "";
                    if (declaration.RegisterSet == RegisterSet.Sampler)
                    {
                        if (samplerIndex != declaration.RegisterIndex)
                        {
                            registerSpecifier = $" : register(s{declaration.RegisterIndex})";
                        }
                        samplerIndex++;
                    }
                    WriteLine("{0} {1}{2};", typeName, declaration.Name, registerSpecifier);
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
                WriteLine($"{input.TypeName} {input.Name} : {input.Semantic};");
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
            foreach (var output in _registers.MethodOutputRegisters.Values)
            {
                WriteLine($"{output.TypeName} {output.Name} : {output.Semantic};");
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
                    return _registers.MethodOutputRegisters.Values.First().TypeName;
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
                    string semantic = _registers.MethodOutputRegisters.Values.First().Semantic;
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
                return $"{input.TypeName} {input.Name} : {input.Semantic}";
            }

            return _shader.Type == ShaderType.Pixel
                    ? "PS_IN i"
                    : "VS_IN i";
        }
    }
}