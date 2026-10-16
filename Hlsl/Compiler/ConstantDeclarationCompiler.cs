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

    /// <summary>
    /// The struct's reflected name where it is an identifier no other struct here
    /// has; else structN. A host may look a struct up by name in the reflection.
    /// </summary>
    public string GetStructName(ShaderTypeInfo typeInfo)
    {
        string own = typeInfo.Name;
        if (!string.IsNullOrEmpty(own)
            && System.Text.RegularExpressions.Regex.IsMatch(own, "^[A-Za-z_][A-Za-z0-9_]*$")
            && HlslKeywords.Escape(own) == own
            && _structIndices.Keys.Count(other => other.Name == own) == 1)
        {
            return own;
        }
        return "struct" + _structIndices[typeInfo];
    }

    public void SetStructOrder(ConstantDeclaration declaration)
    {
        SetStructOrder(declaration.TypeInfo);
    }

    // A structured buffer's element type is named after the buffer, so only its
    // struct members are ordered.
    public void SetMemberStructOrder(ShaderTypeInfo typeInfo)
    {
        if (typeInfo.MemberInfo == null)
        {
            return;
        }
        foreach (ShaderStructMemberInfo member in typeInfo.MemberInfo)
        {
            SetStructOrder(member.TypeInfo);
        }
    }

    // Members first: a struct must be declared before the struct holding it.
    private void SetStructOrder(ShaderTypeInfo typeInfo)
    {
        if (typeInfo.MemberInfo == null)
        {
            return;
        }
        foreach (ShaderStructMemberInfo member in typeInfo.MemberInfo)
        {
            SetStructOrder(member.TypeInfo);
        }
        if (!_structIndices.ContainsKey(typeInfo))
        {
            _structIndices.Add(typeInfo, _structIndex);
            _structIndex++;
        }
    }

    public string Compile(ShaderStructMemberInfo member)
    {
        string typeName = GetTypeName(member.TypeInfo);
        return $"{typeName} {member.Name}{GetArrayCountSpecifier(member.TypeInfo)};";
    }

    public string Compile(ConstantDeclaration declaration)
    {
        if (declaration is D3D9ConstantDeclaration d3D9ConstantDeclaration)
        {
            return Compile(d3D9ConstantDeclaration);
        }
        string typeName = GetTypeName(declaration.TypeInfo);
        return $"{typeName} {declaration.Name}{GetArrayCountSpecifier(declaration.TypeInfo)};";
    }

    // A non-array reports 0 or 1. RegisterCount is no substitute: an element can
    // take one register or several.
    private static string GetArrayCountSpecifier(ShaderTypeInfo typeInfo)
    {
        int arrayCount = Math.Max(typeInfo.NumElements, 1);
        return arrayCount > 1 ? $"[{arrayCount}]" : "";
    }

    public string Compile(D3D9ConstantDeclaration declaration)
    {
        string typeName = GetTypeName(declaration.TypeInfo);
        string registerSpecifier = "";
        int registerSet = (int)declaration.RegisterSet;
        if (declaration.IsPreshaderInput)
        {
            // Where it was in the preshader says nothing about where it goes.
        }
        else if (_index[registerSet] == declaration.RegisterIndex)
        {
            _index[registerSet] += declaration.RegisterCount;
        }
        else
        {
            char type = "btcs"[registerSet];
            registerSpecifier = $" : register({type}{declaration.RegisterIndex})";
        }
        return $"{typeName} {declaration.Name}"
            + $"{GetArrayCountSpecifier(declaration.TypeInfo)}{registerSpecifier};";
    }

    private static string GetScalarTypeName(ParameterType parameterType)
    {
        return parameterType.ToString().ToLower();
    }

    private string GetTypeName(ShaderTypeInfo typeInfo)
    {
        switch (typeInfo.ParameterClass)
        {
            case ParameterClass.Scalar:
                return GetScalarTypeName(typeInfo.ParameterType);
            case ParameterClass.Vector:
                return GetScalarTypeName(typeInfo.ParameterType) + typeInfo.Columns;
            case ParameterClass.MatrixColumns:
                return $"{GetScalarTypeName(typeInfo.ParameterType)}{typeInfo.Rows}x{typeInfo.Columns}";
            // HLSL defaults to column major; without the keyword every use transposes.
            case ParameterClass.MatrixRows:
                return $"row_major {GetScalarTypeName(typeInfo.ParameterType)}{typeInfo.Rows}x{typeInfo.Columns}";
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
                return GetStructName(typeInfo);
        }
        throw new NotImplementedException();
    }
}
