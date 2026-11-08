using HlslDecompiler.Util;
using NUnit.Framework;

namespace HlslDecompiler.Tests;

// HLSL has no INF or NaN identifier: the Xbox 360's compiler and fxc from shader
// model 4 read the MSVC spellings. The disassembly keeps its own.
public class ConstantFormatterTests
{
    [TestCase(float.PositiveInfinity, "1.#INF")]
    [TestCase(float.NegativeInfinity, "-1.#INF")]
    [TestCase(float.NaN, "1.#QNAN")]
    [TestCase(0.5f, "0.5")]
    public void HlslSpellsSpecialFloatsAsTheCompilersReadThem(float value, string expected)
    {
        Assert.Multiple(() =>
        {
            Assert.That(ConstantFormatter.FormatHlsl(value), Is.EqualTo(expected));
            Assert.That(ConstantFormatter.FormatHlsl((double)value), Is.EqualTo(expected));
        });
    }

    [TestCase(float.PositiveInfinity, "INF")]
    [TestCase(float.NegativeInfinity, "-INF")]
    [TestCase(float.NaN, "NaN")]
    public void DisassemblyKeepsItsSpelling(float value, string expected)
    {
        Assert.That(ConstantFormatter.Format(value), Is.EqualTo(expected));
    }
}
