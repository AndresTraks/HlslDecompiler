using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using System.IO;

namespace HlslDecompiler.Tests
{
    [TestFixture]
    public class DecompileDxbcTests
    {
        [TestCase("ps_4_0", "conditional")]
        [TestCase("ps_4_0", "constant")]
        [TestCase("ps_4_0", "constant_struct")]
        [TestCase("ps_4_0", "dot_product2_add")]
        [TestCase("ps_4_0", "derivative")]
        [TestCase("ps_4_0", "texcoord")]
        [TestCase("ps_4_0", "texcoord_modifier")]
        [TestCase("ps_4_0", "texcoord_swizzle")]
        [TestCase("ps_4_0", "float4_construct")]
        [TestCase("ps_4_0", "float4_construct2")]
        [TestCase("ps_4_0", "float4_constant")]
        [TestCase("ps_4_0", "multiply_negate")]
        [TestCase("ps_4_0", "multiply_subtract")]
        [TestCase("ps_4_0", "absolute_multiply")]
        [TestCase("ps_4_0", "negate_absolute")]
        [TestCase("ps_4_0", "sample_2d")]
        [TestCase("ps_4_0", "sample_2d_swizzle")]
        [TestCase("ps_4_0", "sample_2d_two_samplers")]
        [TestCase("ps_4_0", "clip")]
        [TestCase("vs_4_0", "constant")]
        [TestCase("vs_4_0", "constant_struct")]
        [TestCase("vs_4_0", "dot_product")]
        [TestCase("vs_4_0", "length")]
        [TestCase("vs_4_0", "matrix22_vector2_multiply")]
        [TestCase("vs_4_0", "matrix23_vector2_multiply")]
        [TestCase("vs_4_0", "matrix33_vector3_multiply")]
        [TestCase("vs_4_0", "matrix44_vector4_multiply")]
        [TestCase("vs_4_0", "normalize")]
        [TestCase("vs_4_0", "submatrix43_vector3_multiply")]
        [TestCase("vs_4_0", "vector2_matrix22_multiply")]
        [TestCase("vs_4_0", "vector2_matrix32_multiply")]
        [TestCase("vs_4_0", "vector3_matrix33_multiply")]
        [TestCase("vs_4_0", "vector4_matrix44_multiply")]
        public void DecompileTest(string profile, string baseFilename)
        {
            string compiledShaderFilename = $"CompiledShaders{Path.DirectorySeparatorChar}{profile}{Path.DirectorySeparatorChar}{baseFilename}.fxc";
            string asmExpectedFilename = $"ShaderAssembly{Path.DirectorySeparatorChar}{profile}{Path.DirectorySeparatorChar}{baseFilename}.asm";
            string hlslExpectedFilename = $"ShaderSources{Path.DirectorySeparatorChar}{profile}{Path.DirectorySeparatorChar}{baseFilename}.fx";
            string asmOutputFilename = $"{profile}{Path.DirectorySeparatorChar}{baseFilename}.asm";
            string hlslOutputFilename = $"{profile}{Path.DirectorySeparatorChar}{baseFilename}.fx";

            ShaderModel shader;

            var inputStream = File.Open(Path.GetFullPath(compiledShaderFilename), FileMode.Open, FileAccess.Read);
            using (var input = new DxbcReader(inputStream, true))
            {
                shader = input.ReadShader();
            }

            var asmWriter = new AsmWriter(shader);
            MakeFolder(asmOutputFilename);
            asmWriter.Write(asmOutputFilename);

            var hlslWriter = new HlslAstWriter(shader);
            MakeFolder(hlslOutputFilename);
            hlslWriter.Write(hlslOutputFilename);

            FileAssert.AreEqual(asmExpectedFilename, asmOutputFilename, "Assembly not equal");
            FileAssert.AreEqual(hlslExpectedFilename, hlslOutputFilename, "HLSL not equal");
        }

        private static void MakeFolder(string hlslOutputFilename)
        {
            string directory = Path.GetDirectoryName(hlslOutputFilename);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
