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

        FileStream hlslFile;
        StreamWriter hlslWriter;
        protected string indent = "";

        public RegisterState _registers;

        public HlslWriter(ShaderModel shader)
        {
            _shader = shader;
        }

        protected abstract void WriteMethodBody();

        protected void WriteLine()
        {
            hlslWriter.WriteLine();
        }

        protected void WriteLine(string value)
        {
            hlslWriter.Write(indent);
            hlslWriter.WriteLine(value);
        }

        protected void WriteLine(string format, params object[] args)
        {
            hlslWriter.Write(indent);
            hlslWriter.WriteLine(format, args);
        }

        protected string GetDestinationName(Instruction instruction)
        {
            return _registers.GetDestinationName(instruction);
        }

        protected string GetSourceName(Instruction instruction, int srcIndex)
        {
            return _registers.GetSourceName(instruction, srcIndex);
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
                        case ParameterType.Sampler2D:
                        case ParameterType.Sampler3D:
                            return "sampler";
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

            _registers = new RegisterState(_shader);

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

            hlslWriter.Dispose();
            hlslFile.Dispose();
        }

        private void WriteConstantDeclarations()
        {
            if (_registers.ConstantDeclarations.Count != 0)
            {
                foreach (ConstantDeclaration declaration in _registers.ConstantDeclarations)
                {
                    string typeName = GetConstantTypeName(declaration);
                    WriteLine("{0} {1};", typeName, declaration.Name);
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
