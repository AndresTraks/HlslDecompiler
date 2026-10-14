using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using NUnit.Framework;
using System.IO;

namespace HlslDecompiler.Tests;

/// <summary>
/// Idiom shapes fxc produces only when recompiling a golden, which no fixture's own
/// bytecode has. Each test checks the one line that must come back.
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

    private static string Decompile(string filename)
    {
        ShaderModel shader = RecompileTests.ReadShaderModel(filename);
        var writer = new StringWriter();
        new HlslAstWriter(shader).Write(writer);
        return writer.ToString();
    }
}
