using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace HlslDecompiler.Tests;

/// <summary>
/// A constant table's default value is written as an initializer. The value is
/// stored in registers - one per array element, struct member and matrix row or
/// column - so the writer unpacks it into a constructor that fxc reads back to
/// the same bytes.
/// </summary>
[TestFixture]
public class DefaultValueTests
{
    private static ShaderTypeInfo Scalar(ParameterType type) =>
        new(ParameterClass.Scalar, type, 1, 1, 1, null);

    private static ShaderTypeInfo Vector(ParameterType type, int columns, int elements = 1) =>
        new(ParameterClass.Vector, type, 1, columns, elements, null);

    private static ShaderTypeInfo Matrix(ParameterClass matrixClass, int rows, int columns) =>
        new(matrixClass, ParameterType.Float, rows, columns, 1, null);

    // Each register holds four components; a short one is padded with zeros.
    private static byte[] Registers(params float[] components)
    {
        var bytes = new List<byte>();
        for (int i = 0; i < components.Length; i += 4)
        {
            for (int c = 0; c < 4; c++)
            {
                bytes.AddRange(BitConverter.GetBytes(i + c < components.Length ? components[i + c] : 0));
            }
        }
        return [.. bytes];
    }

    private static string Compile(ShaderTypeInfo typeInfo, byte[] defaultValue, short registerCount = 1)
    {
        var declaration = new D3D9ConstantDeclaration(
            "g_Value", RegisterSet.Float4, 176, registerCount, typeInfo, defaultValue);
        var compiler = new ConstantDeclarationCompiler();
        compiler.SetStructOrder(declaration);
        return compiler.Compile(declaration);
    }

    [Test]
    public void AScalarIsOneLiteral()
    {
        Assert.That(Compile(Scalar(ParameterType.Float), Registers(0.5f)),
            Is.EqualTo("float g_Value : register(c176) = 0.5;"));
    }

    [Test]
    public void AVectorIsItsConstructor()
    {
        Assert.That(Compile(Vector(ParameterType.Float, 4), Registers(1, 2, 3, 4)),
            Is.EqualTo("float4 g_Value : register(c176) = float4(1, 2, 3, 4);"));
    }

    [Test]
    public void AZeroVectorOrMatrixIsTheScalarZero()
    {
        // A scalar converts to either, and fxc stores the same default.
        Assert.That(Compile(Matrix(ParameterClass.MatrixColumns, 4, 4), Registers(
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)),
            Is.EqualTo("float4x4 g_Value : register(c176) = 0;"));
    }

    [Test]
    public void AColumnMajorMatrixIsUnpackedByColumn()
    {
        // float2x3(1, 2, 3, 4, 5, 6) fills the rows (1, 2, 3) and (4, 5, 6), so a
        // column-major matrix stores the columns (1, 4), (2, 5) and (3, 6).
        Assert.That(Compile(Matrix(ParameterClass.MatrixColumns, 2, 3), Registers(
                1, 4, 0, 0, 2, 5, 0, 0, 3, 6, 0, 0)),
            Is.EqualTo("float2x3 g_Value : register(c176) = float2x3(1, 2, 3, 4, 5, 6);"));
    }

    [Test]
    public void ARowMajorMatrixIsUnpackedByRow()
    {
        Assert.That(Compile(Matrix(ParameterClass.MatrixRows, 2, 3), Registers(1, 2, 3, 0, 4, 5, 6, 0)),
            Is.EqualTo("row_major float2x3 g_Value : register(c176) = float2x3(1, 2, 3, 4, 5, 6);"));
    }

    [Test]
    public void AnArrayIsBracesOfElements()
    {
        Assert.That(Compile(Vector(ParameterType.Float, 2, 2), Registers(1, 2, 0, 0, 3, 4, 0, 0),
                registerCount: 2),
            Is.EqualTo("float2 g_Value[2] : register(c176) = { float2(1, 2), float2(3, 4) };"));
    }

    [Test]
    public void AnElementPastTheStoredOnesIsZero()
    {
        // Four float4 elements of three registers each in a window of fourteen
        // reach a fifth element the table gave no value.
        Assert.That(Compile(Vector(ParameterType.Float, 4, 4), Registers(
                1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16), registerCount: 14),
            Is.EqualTo("float4 g_Value[5] : register(c176) = { float4(1, 2, 3, 4), "
                + "float4(5, 6, 7, 8), float4(9, 10, 11, 12), float4(13, 14, 15, 16), 0 };"));
    }

    [Test]
    public void ABoolAndAnIntAreTheirLiterals()
    {
        Assert.That(Compile(Scalar(ParameterType.Bool), Registers(1)),
            Is.EqualTo("bool g_Value : register(c176) = true;"));
        Assert.That(Compile(Scalar(ParameterType.Int), Registers(-7)),
            Is.EqualTo("int g_Value : register(c176) = -7;"));
    }

    [Test]
    public void AStructIsBracesOfItsMembers()
    {
        var float4 = Vector(ParameterType.Float, 4);
        var float1 = Scalar(ParameterType.Float);
        var float2 = Vector(ParameterType.Float, 2);
        var int1 = Scalar(ParameterType.Int);
        var structType = new ShaderTypeInfo(ParameterClass.Struct, ParameterType.Void, 1, 8, 1, [
            new ShaderStructMemberInfo("a", float4),
            new ShaderStructMemberInfo("b", float1),
            new ShaderStructMemberInfo("c", float2),
            new ShaderStructMemberInfo("d", int1),
        ]);

        Assert.That(Compile(structType, Registers(1, 2, 3, 4, 5, 0, 0, 0, 6, 7, 0, 0, 8, 0, 0, 0),
                registerCount: 4),
            Is.EqualTo("struct1 g_Value : register(c176) = { float4(1, 2, 3, 4), 5, float2(6, 7), 8 };"));
    }
}
