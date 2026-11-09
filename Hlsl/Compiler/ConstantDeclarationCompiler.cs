using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        => GetArrayCountSpecifier(typeInfo, typeInfo.NumElements);

    private static string GetArrayCountSpecifier(ShaderTypeInfo typeInfo, int elements)
    {
        int arrayCount = Math.Max(elements, 1);
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
        // Bools and ints always say: undeclared, fxc may put one in a float
        // register (`if (useMorph)` read c6, a def's register, not b0).
        else if (_index[registerSet] == declaration.RegisterIndex
            && declaration.RegisterSet is RegisterSet.Float4 or RegisterSet.Sampler)
        {
            _index[registerSet] += declaration.RegisterCount;
        }
        else
        {
            char type = "bics"[registerSet];
            registerSpecifier = $" : register({type}{declaration.RegisterIndex})";
        }
        // A table (an XDK's among them) can declare a window of more registers than
        // its element count covers; the microcode reads the extra elements the
        // window names them by. Declare through the last one the window reaches so
        // those subscripts stand: an array whose registers overlap another's is
        // fine for fxc and for XNA.
        int stride = declaration.RegistersPerElement;
        int elements = Math.Max(declaration.TypeInfo.NumElements,
            (declaration.RegisterCount + stride - 1) / stride);
        string initializer = declaration.DefaultValue != null
            ? $" = {DefaultValue(declaration.TypeInfo, declaration.DefaultValue, elements)}"
            : "";
        return $"{typeName} {declaration.Name}"
            + $"{GetArrayCountSpecifier(declaration.TypeInfo, elements)}{registerSpecifier}{initializer};";
    }

    /// <summary>
    /// The constant table's initializer as HLSL. A value is stored in registers;
    /// each array element, struct member and matrix row or column starts one, and
    /// an array the register window widened past its element count ends in zeros.
    /// </summary>
    private static string DefaultValue(ShaderTypeInfo typeInfo, byte[] data, int elements)
    {
        int position = 0;
        int stored = Math.Max(typeInfo.NumElements, 1);
        var values = new List<string>();
        for (int i = 0; i < elements; i++)
        {
            values.Add(i < stored
                ? FormatValue(typeInfo, data, ref position, withElements: false)
                : ZeroValue(typeInfo));
        }
        return elements > 1 ? $"{{ {string.Join(", ", values)} }}" : values[0];
    }

    private static string FormatValue(ShaderTypeInfo typeInfo, byte[] data, ref int position, bool withElements)
    {
        if (withElements && typeInfo.NumElements > 1)
        {
            var elements = new List<string>();
            for (int i = 0; i < typeInfo.NumElements; i++)
            {
                elements.Add(FormatValue(typeInfo, data, ref position, withElements: false));
            }
            return $"{{ {string.Join(", ", elements)} }}";
        }

        switch (typeInfo.ParameterClass)
        {
            case ParameterClass.Scalar:
            case ParameterClass.Vector:
                {
                    int components = typeInfo.ParameterClass == ParameterClass.Scalar ? 1 : typeInfo.Columns;
                    var values = new List<string>();
                    for (int i = 0; i < components; i++)
                    {
                        values.Add(FormatComponent(typeInfo.ParameterType, data, position + i * 4));
                    }
                    position += 16;
                    return JoinValue(typeInfo, values);
                }

            // A matrix stores its columns (or rows) one register each, four
            // components in a register, and the constructor takes rows in order.
            case ParameterClass.MatrixColumns:
            case ParameterClass.MatrixRows:
                {
                    bool byRows = typeInfo.ParameterClass == ParameterClass.MatrixRows;
                    int outer = byRows ? typeInfo.Rows : typeInfo.Columns;
                    int inner = byRows ? typeInfo.Columns : typeInfo.Rows;
                    int registersPerOuter = (inner + 3) / 4;
                    string[,] components = new string[typeInfo.Rows, typeInfo.Columns];
                    for (int o = 0; o < outer; o++)
                    {
                        for (int i = 0; i < inner; i++)
                        {
                            int offset = position + (o * registersPerOuter + i / 4) * 16 + (i % 4) * 4;
                            int row = byRows ? o : i;
                            int column = byRows ? i : o;
                            components[row, column] = FormatComponent(typeInfo.ParameterType, data, offset);
                        }
                    }
                    position += outer * registersPerOuter * 16;
                    return JoinValue(typeInfo, [.. Enumerable.Range(0, typeInfo.Rows * typeInfo.Columns)
                        .Select(i => components[i / typeInfo.Columns, i % typeInfo.Columns])]);
                }

            // Each struct member starts at a register boundary, so the values
            // advance the position by their own register counts.
            case ParameterClass.Struct:
                {
                    var members = new List<string>();
                    foreach (ShaderStructMemberInfo member in typeInfo.MemberInfo)
                    {
                        members.Add(FormatValue(member.TypeInfo, data, ref position, withElements: true));
                    }
                    return $"{{ {string.Join(", ", members)} }}";
                }

            default:
                return "0";
        }
    }

    private static string JoinValue(ShaderTypeInfo typeInfo, IList<string> components)
    {
        if (components.Count == 1)
        {
            return components[0];
        }
        // A scalar converts to any numeric vector or matrix, and fxc reads the
        // same initializer back as one.
        if (typeInfo.ParameterType is ParameterType.Float or ParameterType.Int or ParameterType.Uint
            && components.All(component => component == "0"))
        {
            return "0";
        }
        return $"{ConstructorTypeName(typeInfo)}({string.Join(", ", components)})";
    }

    private static string ConstructorTypeName(ShaderTypeInfo typeInfo)
    {
        return typeInfo.ParameterClass switch
        {
            ParameterClass.Scalar => GetScalarTypeName(typeInfo.ParameterType),
            ParameterClass.Vector => GetScalarTypeName(typeInfo.ParameterType) + typeInfo.Columns,
            ParameterClass.MatrixRows or ParameterClass.MatrixColumns =>
                $"{GetScalarTypeName(typeInfo.ParameterType)}{typeInfo.Rows}x{typeInfo.Columns}",
            _ => "",
        };
    }

    private static string FormatComponent(ParameterType type, byte[] data, int offset)
    {
        uint bits = BitConverter.ToUInt32(data, offset);
        float value = BitConverter.UInt32BitsToSingle(bits);
        return type switch
        {
            ParameterType.Float => ConstantFormatter.FormatHlsl(value),
            ParameterType.Bool => value != 0 ? "true" : "false",
            ParameterType.Int => ((int)value).ToString(CultureInfo.InvariantCulture),
            ParameterType.Uint => ((uint)value).ToString(CultureInfo.InvariantCulture),
            _ => ((int)bits).ToString(CultureInfo.InvariantCulture),
        };
    }

    // An element past the ones the table stored: an array's window can be wider.
    private static string ZeroValue(ShaderTypeInfo typeInfo)
    {
        switch (typeInfo.ParameterClass)
        {
            case ParameterClass.Scalar:
            case ParameterClass.Vector:
            case ParameterClass.MatrixRows:
            case ParameterClass.MatrixColumns:
                {
                    int count = typeInfo.ParameterClass switch
                    {
                        ParameterClass.Scalar => 1,
                        ParameterClass.Vector => typeInfo.Columns,
                        _ => typeInfo.Rows * typeInfo.Columns,
                    };
                    return JoinValue(typeInfo, [.. Enumerable.Repeat(ZeroComponent(typeInfo.ParameterType), count)]);
                }
            case ParameterClass.Struct:
                return $"{{ {string.Join(", ", typeInfo.MemberInfo.Select(m => ZeroValue(m.TypeInfo)))} }}";
            default:
                return "0";
        }
    }

    private static string ZeroComponent(ParameterType type)
    {
        return type == ParameterType.Bool ? "false" : "0";
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
