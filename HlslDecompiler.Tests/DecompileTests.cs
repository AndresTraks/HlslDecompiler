using NUnit.Framework;
using System.IO;

namespace HlslDecompiler.Tests
{
    [TestFixture]
    public class DecompileTests
    {
        [TestCase("ps_constant")]
        [TestCase("ps_constant_struct")]
        [TestCase("ps_texcoord")]
        [TestCase("ps_texcoord_modifier")]
        [TestCase("ps_texcoord_swizzle")]
        [TestCase("ps_float4_construct")]
        [TestCase("ps_float4_constant")]
        [TestCase("ps_multiply_subtract")]
        [TestCase("ps_absolute_multiply")]
        [TestCase("ps_negate_absolute")]
        [TestCase("ps_tex2d")]
        [TestCase("ps_tex2d_swizzle")]
        [TestCase("ps_tex2d_two_samplers")]
        [TestCase("vs_constant")]
        [TestCase("vs_constant_struct")]
        public void DecompileTest(string baseFilename)
        {
            string compiledShaderFilename = $"CompiledShaders{Path.DirectorySeparatorChar}{baseFilename}.fxc";
            string asmExpectedFilename = $"ShaderAssembly{Path.DirectorySeparatorChar}{baseFilename}.asm";
            string hlslExpectedFilename = $"ShaderSources{Path.DirectorySeparatorChar}{baseFilename}.fx";
            string asmOutputFilename = $"{baseFilename}.asm";
            string hlslOutputFilename = $"{baseFilename}.fx";

            ShaderModel shader;

            var inputStream = File.Open(compiledShaderFilename, FileMode.Open, FileAccess.Read);
            using (var input = new ShaderReader(inputStream, true))
            {
                shader = input.ReadShader();
            }

            var asmWriter = new AsmWriter(shader);
            asmWriter.Write(asmOutputFilename);

            var hlslWriter = new HlslWriter(shader, true);
            hlslWriter.Write(hlslOutputFilename);

            FileAssert.AreEqual(asmExpectedFilename, asmOutputFilename, "Assembly not equal");
            FileAssert.AreEqual(hlslExpectedFilename, hlslOutputFilename, "HLSL not equal");
        }
    }
}
