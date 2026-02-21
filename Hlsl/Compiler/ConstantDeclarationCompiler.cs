using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public sealed class ConstantDeclarationCompiler
{
    private int[] _index = new int[Enum.GetNames(typeof(RegisterSet)).Length];
    private IDictionary<ShaderTypeInfo, int> _structIndices = new Dictionary<ShaderTypeInfo, int>();
    private int _structIndex = 1;

    public IList<ShaderTypeInfo> GetOrderedStructs()
    {
        return _structIndices.OrderBy(s => s.Value).Select(s => s.Key).ToList();
    }

    public void SetStructOrder(ConstantDeclaration declaration)
    {
        if (declaration.TypeInfo.MemberInfo != null)
        {
            if (!_structIndices.ContainsKey(declaration.TypeInfo))
            {
                _structIndices.Add(declaration.TypeInfo, _structIndex);
                _structIndex++;
            }
        }
    }

    public string Compile(ShaderStructMemberInfo member)
    {
        string typeName = GetTypeName(member.TypeInfo);
        return $"{typeName} {member.Name};";
    }

    public string Compile(ConstantDeclaration declaration)
    {
        if (declaration is D3D9ConstantDeclaration d3D9ConstantDeclaration)
        {
            return Compile(d3D9ConstantDeclaration);
        }
        string typeName = GetTypeName(declaration.TypeInfo);
        return $"{typeName} {declaration.Name};";
    }

    public string Compile(D3D9ConstantDeclaration declaration)
    {
        string typeName = GetTypeName(declaration.TypeInfo);
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
        int arrayCount = declaration.RegisterCount / (declaration.TypeInfo.Rows * declaration.TypeInfo.Columns);
        string arrayCountSpecifier = arrayCount > 1 ? $"[{arrayCount}]" : "";
        return $"{typeName} {declaration.Name}{arrayCountSpecifier}{registerSpecifier};";
    }

    private string GetTypeName(ShaderTypeInfo typeInfo)
    {
        switch (typeInfo.ParameterClass)
        {
            case ParameterClass.Scalar:
                return typeInfo.ParameterType.ToString().ToLower();
            case ParameterClass.Vector:
                if (typeInfo.ParameterType == ParameterType.Float)
                {
                    return "float" + typeInfo.Columns;
                }
                else
                {
                    throw new NotImplementedException();
                }
            case ParameterClass.MatrixColumns:
            case ParameterClass.MatrixRows:
                if (typeInfo.ParameterType == ParameterType.Float)
                {
                    return $"float{typeInfo.Rows}x{typeInfo.Columns}";
                }
                else
                {
                    throw new NotImplementedException();
                }
            case ParameterClass.Object:
                return typeInfo.ParameterType switch
                {
                    ParameterType.Sampler1D => "sampler1D",
                    ParameterType.Sampler2D => "sampler2D",
                    ParameterType.Sampler3D => "sampler3D",
                    ParameterType.SamplerCube => "samplerCUBE",
                    _ => throw new NotImplementedException(),
                };
            case ParameterClass.Struct:
                return "struct" + _structIndices[typeInfo];
        }
        throw new NotImplementedException();
    }
}
