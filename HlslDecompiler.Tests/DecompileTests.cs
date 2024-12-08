using HlslDecompiler.Hlsl;
using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using System.IO;

namespace HlslDecompiler.Tests
{
    [TestFixture]
    public class DecompileTests
    {
        [TestCase("ps_3_0", "conditional")]
        [TestCase("ps_3_0", "constant")]
        [TestCase("ps_3_0", "constant_struct")]
        [TestCase("ps_3_0", "dot_product2_add")]
        [TestCase("ps_3_0", "derivative")]
        [TestCase("ps_3_0", "texcoord")]
        [TestCase("ps_3_0", "texcoord_modifier")]
        [TestCase("ps_3_0", "texcoord_swizzle")]
        [TestCase("ps_3_0", "float4_construct")]
        [TestCase("ps_3_0", "float4_construct2")]
        [TestCase("ps_3_0", "float4_constant")]
        [TestCase("ps_3_0", "multiply_subtract")]
        [TestCase("ps_3_0", "absolute_multiply")]
        [TestCase("ps_3_0", "negate_absolute")]
        [TestCase("ps_3_0", "semantics")]
        [TestCase("ps_3_0", "tex2d")]
        [TestCase("ps_3_0", "tex2d_swizzle")]
        [TestCase("ps_3_0", "tex2d_two_samplers")]
        [TestCase("ps_3_0", "tex2dlod")]
        [TestCase("ps_3_0", "clip")]
        [TestCase("ps_3_0", "loop")]
        [TestCase("vs_3_0", "constant")]
        [TestCase("vs_3_0", "constant_struct")]
        [TestCase("vs_3_0", "dot_product")]
        [TestCase("vs_3_0", "length")]
        [TestCase("vs_3_0", "matrix22_vector2_multiply")]
        [TestCase("vs_3_0", "matrix23_vector2_multiply")]
        [TestCase("vs_3_0", "matrix33_vector3_multiply")]
        [TestCase("vs_3_0", "matrix44_vector4_multiply")]
        [TestCase("vs_3_0", "normalize")]
        [TestCase("vs_3_0", "submatrix43_vector3_multiply")]
        [TestCase("vs_3_0", "vector2_matrix22_multiply")]
        [TestCase("vs_3_0", "vector2_matrix32_multiply")]
        [TestCase("vs_3_0", "vector3_matrix33_multiply")]
        [TestCase("vs_3_0", "vector4_matrix44_multiply")]
        public void DecompileShaderTest(string profile, string baseFilename)
        {
            string compiledShaderFilename = $"CompiledShaders{Path.DirectorySeparatorChar}{profile}{Path.DirectorySeparatorChar}{baseFilename}.fxc";
            string asmExpectedFilename = $"ShaderAssembly{Path.DirectorySeparatorChar}{profile}{Path.DirectorySeparatorChar}{baseFilename}.asm";
            string hlslExpectedFilename = $"ShaderSources{Path.DirectorySeparatorChar}{profile}{Path.DirectorySeparatorChar}{baseFilename}.fx";
            string hlslInstructionExpectedFilename = $"ShaderSources{Path.DirectorySeparatorChar}{profile}_instruction{Path.DirectorySeparatorChar}{baseFilename}.fx";
            string asmOutputFilename = $"{baseFilename}.asm";
            string hlslOutputFilename = $"{baseFilename}.fx";
            string hlslInstructionOutputFilename = $"{baseFilename}_instruction.fx";

            ShaderModel shader;

            var inputStream = File.Open(Path.GetFullPath(compiledShaderFilename), FileMode.Open, FileAccess.Read);
            using (var input = new ShaderReader(inputStream, true))
            {
                shader = input.ReadShader();
            }

            var asmWriter = new AsmWriter(shader);
            asmWriter.Write(asmOutputFilename);

            var hlslInstructionWriter = new HlslSimpleWriter(shader);
            hlslInstructionWriter.Write(hlslInstructionOutputFilename);

            var hlslWriter = new HlslAstWriter(shader);
            hlslWriter.Write(hlslOutputFilename);

            FileAssert.AreEqual(asmExpectedFilename, asmOutputFilename, "Assembly not equal");
            FileAssert.AreEqual(hlslExpectedFilename, hlslOutputFilename, "HLSL not equal");
            FileAssert.AreEqual(hlslInstructionExpectedFilename, hlslInstructionOutputFilename, "HLSL not equal");
        }
    }
}
