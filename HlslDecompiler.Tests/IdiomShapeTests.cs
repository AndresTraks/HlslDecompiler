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
    // order are one multiply, read through a swizzle; taken in register order only,
    // they came back as two dot products.
    [Test]
    public void MatrixRowsInAnotherOrderAreOneMultiply()
    {
        string hlsl = Decompile(Path.Combine("IdiomShapes", "ps_4_1", "matrix_rows_swapped.fxc"));
        Assert.That(hlsl, Does.Contain("mul(float4(i.texcoord, 1), (float4x2)cascadeTransform[t0]).yx"));
    }

    private static string Decompile(string filename)
    {
        ShaderModel shader = RecompileTests.ReadShaderModel(filename);
        var writer = new StringWriter();
        new HlslAstWriter(shader).Write(writer);
        return writer.ToString();
    }
}
