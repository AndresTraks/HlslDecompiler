using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.DirectXShaderModel.Xenos;
using NUnit.Framework;
using System;

namespace HlslDecompiler.Tests;

/// <summary>
/// The literal constants of a listing: the assembler has no bit form, and its
/// non-finite tokens are the Microsoft float literals. float.ToString's
/// "Infinity" it cannot parse (X2000).
/// </summary>
[TestFixture]
public class XenosListingTests
{
    private static string ListingWithLiterals(params uint[] bits) =>
        new XenosAsmWriter(new XenosShader(ShaderType.Pixel, new ConstantTable(), [], bits)
        {
            LiteralRegister = 252,
        }).Write();

    [Test]
    public void InfinitiesPrintAsTheAssemblerReadsThem()
    {
        string listing = ListingWithLiterals(0x7f800000, 0xff800000, 0x3f800000, 0x40490fdb);
        Assert.That(listing, Does.Contain("def c252, 1.#INF, -1.#INF, 1, 3.1415927"));
    }

    [Test]
    public void ANaNLiteralsPrintsAsTheQuietNaNSoleToken()
    {
        // 1.#QNAN always assembles the same quiet NaN: a listing cannot state
        // a payload, nor the quiet bit's difference from a signalling one.
        Assert.That(ListingWithLiterals(0x7fc00000, 0, 0, 0), Does.Contain("1.#QNAN"));
        Assert.That(ListingWithLiterals(0x7fffffff, 0, 0, 0), Does.Contain("1.#QNAN"));
    }

    [Test]
    public void FiniteLiteralsPrintTheirShortestForm()
    {
        string listing = ListingWithLiterals(0x00000000, 0x80000000, 0x3f800000, 0xc0000000);
        Assert.That(listing, Does.Contain("def c252, 0, -0, 1, -2"));
    }
}
