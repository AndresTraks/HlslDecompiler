using HlslDecompiler.DirectXShaderModel;
using System;

namespace HlslDecompiler.Hlsl
{
    public sealed class ConstantDeclarationCompiler
    {
        private int[] _index = new int[Enum.GetNames(typeof(RegisterSet)).Length];

        public string Compile(ConstantDeclaration declaration)
        {
            string typeName = GetTypeName(declaration);
            string registerSpecifier = "";
            int registerSet = (int)declaration.RegisterSet;
            if (_index[registerSet] == declaration.RegisterIndex)
            {
                _index[registerSet] += declaration.RegisterCount;
            }
            else
            {
                char type = "btcs"[registerSet];
                registerSpecifier = $" : register({type}{declaration.RegisterIndex})";
            }
            int arrayCount = declaration.RegisterCount / (declaration.Rows * declaration.Columns);
            string arrayCountSpecifier = arrayCount > 1 ? $"[{arrayCount}]" : "";
            return $"{typeName} {declaration.Name}{arrayCountSpecifier}{registerSpecifier};";
        }

        private static string GetTypeName(ConstantDeclaration declaration)
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
                    return declaration.ParameterType switch
                    {
                        ParameterType.Sampler1D => "sampler1D",
                        ParameterType.Sampler2D => "sampler2D",
                        ParameterType.Sampler3D => "sampler3D",
                        ParameterType.SamplerCube => "samplerCUBE",
                        _ => throw new NotImplementedException(),
                    };
            }
            throw new NotImplementedException();
        }
    }
}
