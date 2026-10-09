using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using NUnit.Framework;
using System.IO;

namespace HlslDecompiler.Tests;

/// <summary>
/// Idioms recovered from a shape fxc produced compiling a golden of our own, where
/// the fixture's own bytecode had the other shape. The fixed point tier is what
/// found them, and cannot hold them: the shader as a whole still decompiles
/// differently round to round, so it is not a fixture. Each test names the one
/// line that has to come back.
/// </summary>
public class IdiomShapeTests
{
    // cascade_shadow's projection with the matrix's first two rows landing in a
    // register the other way round - its x in .y and its y in .x. The rows in any
    // order are one multiply; taken in register order only, they came back as two
    // dot products. And the value made from them is laid out in the rows' order,
    // so the multiply needs no swizzle and neither do its readers - it was
    // `float2(-0.5, 0.5) * (mul(...).yx / t2) + 0.5`, read back as t3.yx.
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
