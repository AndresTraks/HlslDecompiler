using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using NUnit.Framework;

namespace HlslDecompiler.Tests;

/// <summary>
/// A constant array's declared length: the window its constant-table entry
/// declares, not just its element count - the microcode reads the elements
/// the window covers, and an index past the declared length is what the
/// compilers refuse (X3504).
/// </summary>
[TestFixture]
public class ConstantDeclarationTests
{
    private static ShaderTypeInfo Float4Array(int elements) =>
        new(ParameterClass.Vector, ParameterType.Float, 1, 4, elements, null);

    private static string Compile(short registerCount, int elements, short registerIndex = 176) =>
        new ConstantDeclarationCompiler().Compile(new D3D9ConstantDeclaration(
            "g_Lights", RegisterSet.Float4, registerIndex, registerCount, Float4Array(elements)));

    [Test]
    public void AWindowWiderThanItsElementsDeclaresTheExtraOnes()
    {
        // Four elements of three registers in a window of fourteen: the fifth
        // element's first register (base + 12) is still inside the window, and
        // the microcode reads it by that name.
        Assert.That(Compile(14, 4), Is.EqualTo("float4 g_Lights[5] : register(c176);"));
    }

    [Test]
    public void AWindowThatExactlyCoversItsElementsAddsNothing()
    {
        Assert.That(Compile(12, 4), Is.EqualTo("float4 g_Lights[4] : register(c176);"));
    }

    [Test]
    public void ASingleRegisterArrayWindowNeedsNoExtension()
    {
        // Four elements in four registers: one register each, and the window
        // is exactly them.
        Assert.That(Compile(4, 4), Is.EqualTo("float4 g_Lights[4] : register(c176);"));
    }

    [Test]
    public void AWindowOfOneElementIsNoArray()
    {
        Assert.That(Compile(4, 1, 216), Is.EqualTo("float4 g_Lights : register(c216);"));
    }
}
