using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using NUnit.Framework;
using System.IO;

namespace HlslDecompiler.Tests;

/// <summary>
/// Idiom shapes no fixture can hold: ones fxc produces only when recompiling a golden,
/// and ones whose golden would be no fixed point, the recompile losing the shape or
/// fxc reordering it. Each test checks the one line that must come back.
/// </summary>
public class IdiomShapeTests
{
    // Matrix rows landing in swapped components (x in .y, y in .x) are still one
    // multiply, laid out in row order so neither it nor its readers need a swizzle.
    [Test]
    public void MatrixRowsInAnotherOrderAreOneMultiply()
    {
        string hlsl = Decompile(Path.Combine("IdiomShapes", "ps_4_1", "matrix_rows_swapped.fxc"));
        Assert.That(hlsl, Does.Contain("float2(0.5, -0.5) * (mul(float4(i.texcoord, 1), (float4x2)cascadeTransform[t0]) / t2) + 0.5"));
    }

    // fxc unrolls an early-out loop into `c0 || ((c1 || c0) && !c0)` for the first
    // two exits, which is `c0 || c1`; neither the and nor the negation comes back.
    // The recompile of the decompilation is a plain or, so no fixture has it.
    [Test]
    public void AnUnrolledEarlyReturnIsAnOrOfItsExits()
    {
        string hlsl = Decompile(Path.Combine("IdiomShapes", "cs_5_0", "unrolled_early_return.fxc"));
        Assert.That(hlsl, Does.Contain("bool t4 = planes[0].x * t3.x + planes[0].y * t2 + planes[0].z * t1 + planes[0].w * t0 < 0 || planes[1].x * t3.x + planes[1].y * t2 + planes[1].z * t1 + planes[1].w * t0 < 0;"));
    }

    // A skinning loop over a dynamic bone count: each unrolled step tests the count and
    // finds its bone inside the if. A step's index goes dead at the endif, so it is no
    // variable, and the colour unpack still reads as D3DCOLORtoUBYTE4. fxc swaps the
    // operands of the count tests on every recompile.
    [Test]
    public void AnIndexDeadAfterItsBranchIsNoVariable()
    {
        string hlsl = Decompile(Path.Combine("IdiomShapes", "vs_3_0", "skin_loop_dynamic_count.fxc"));
        Assert.That(hlsl, Does.Contain("t1 = t1 + mul(i.position, bones[D3DCOLORtoUBYTE4(i.blendindices).z]) * i.blendweight.z;"));
    }

    private static string Decompile(string filename)
    {
        ShaderModel shader = RecompileTests.ReadShaderModel(filename);
        var writer = new StringWriter();
        new HlslAstWriter(shader).Write(writer);
        return writer.ToString();
    }
}
