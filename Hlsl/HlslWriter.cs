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

        FileStream hlslFile;
        StreamWriter hlslWriter;
        protected string indent = "";

        protected HlslAst _ast;
        protected RegisterState _registers;

        public HlslWriter(ShaderModel shader)
        {
            _shader = shader;
        }

        protected abstract void WriteMethodBody(TextWriter writer);

        protected void WriteLine()
        {
            hlslWriter.WriteLine(indent);
        }

        protected void WriteLine(TextWriter writer)
        {
            writer.WriteLine(indent);
        }

        protected void WriteLine(TextWriter writer, string value)
        {
            writer.Write(indent);
            writer.WriteLine(value);
        }

        protected void WriteLine(TextWriter writer, string format, params object[] args)
        {
            writer.Write(indent);
            writer.WriteLine(format, args);
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
            hlslFile = new FileStream(hlslFilename, FileMode.Create, FileAccess.Write);
            hlslWriter = new StreamWriter(hlslFile);

            WriteInternal(hlslWriter);

            hlslWriter.Dispose();
            hlslFile.Dispose();
        }

        public void Write(TextWriter writer)
        {
            WriteInternal(writer);
        }

        private void WriteInternal(TextWriter writer)
        {
            _ast = InstructionParser.Parse(_shader);
            _registers = _ast.RegisterState;

            WriteConstantDeclarations(writer);

            if (_registers.MethodInputRegisters.Count > 1)
            {
                WriteInputStructureDeclaration(writer);
            }

            if (_registers.MethodOutputRegisters.Count > 1)
            {
                WriteOutputStructureDeclaration(writer);
            }

            string methodReturnType = GetMethodReturnType();
            string methodParameters = GetMethodParameters();
            string methodSemantic = GetMethodSemantic();

            WriteLine(writer, "{0} main({1}){2}", methodReturnType, methodParameters, methodSemantic);
            WriteLine(writer, "{");
            indent = "\t";

            WriteMethodBody(writer);

            indent = "";
            WriteLine(writer, "}");
        }

        private void WriteConstantDeclarations(TextWriter writer)
        {
            if (_registers.ConstantDeclarations.Count != 0)
            {
                foreach (ConstantDeclaration declaration in _registers.ConstantDeclarations)
                {
                    string typeName = GetConstantTypeName(declaration);
                    WriteLine(writer, "{0} {1};", typeName, declaration.Name);
                }

                WriteLine(writer);
            }
        }

        private void WriteInputStructureDeclaration(TextWriter writer)
        {
            var inputStructType = _shader.Type == ShaderType.Pixel ? "PS_IN" : "VS_IN";
            WriteLine(writer, $"struct {inputStructType}");
            WriteLine(writer, "{");
            indent = "\t";
            foreach (var input in _registers.MethodInputRegisters.Values)
            {
                WriteLine(writer, $"{input.TypeName} {input.Name} : {input.Semantic};");
            }
            indent = "";
            WriteLine(writer, "};");
            WriteLine(writer);
        }

        private void WriteOutputStructureDeclaration(TextWriter writer)
        {
            var outputStructType = _shader.Type == ShaderType.Pixel ? "PS_OUT" : "VS_OUT";
            WriteLine(writer, $"struct {outputStructType}");
            WriteLine(writer, "{");
            indent = "\t";
            foreach (var output in _registers.MethodOutputRegisters.Values)
            {
                WriteLine(writer, $"{output.TypeName} {output.Name} : {output.Semantic};");
            }
            indent = "";
            WriteLine(writer, "};");
            WriteLine(writer);
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