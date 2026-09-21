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
        SetStructOrder(declaration.TypeInfo);
    }

    // A member can be a struct too, and it has to be declared before the struct
    // holding it, so members are ordered first.
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

    // A member is an array the same way a variable is, and it is read as one -
    // `g_s.weights[2]`. Declared without the bound it is a field the struct does
    // not have, and nothing that names an element of it compiles.
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

    // Elements is the array length straight from the constant table, and a
    // non-array reports 0 or 1. RegisterCount cannot stand in for it: `float4
    // floats[8]` takes 8 registers but each element is one register, not four
    // components, so dividing by the component count gave [2].
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
        if (_index[registerSet] == declaration.RegisterIndex)
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

    // bool, int, uint, float and so on, which the enum already spells.
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
                // int4 and uint4 are as ordinary as float4, and a cbuffer holding
                // one used to throw rather than be named.
                return GetScalarTypeName(typeInfo.ParameterType) + typeInfo.Columns;
            case ParameterClass.MatrixColumns:
                return $"{GetScalarTypeName(typeInfo.ParameterType)}{typeInfo.Rows}x{typeInfo.Columns}";
            // The constant table records which way a matrix was packed and HLSL
            // packs column major unless told otherwise, so a row major one has to
            // say so. Declared without it, the registers holding its rows are read
            // back as columns and every use of it is transposed.
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
                return "struct" + _structIndices[typeInfo];
        }
        throw new NotImplementedException();
    }
}
