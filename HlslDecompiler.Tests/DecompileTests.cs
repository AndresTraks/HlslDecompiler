using NUnit.Framework;
using System.IO;

namespace HlslDecompiler.Tests
{
    [TestFixture]
    public class DecompileTests
    {
        [TestCase("ps1")]
        [TestCase("ps2")]
        [TestCase("ps3")]
        [TestCase("ps4")]
        [TestCase("ps5")]
        [TestCase("ps6")]
        [TestCase("ps7")]
        [TestCase("ps8")]
        [TestCase("ps9")]
        [TestCase("ps10")]
        public void DecompileTest(string baseFilename)
        {
            string compiledShaderFilename = $"CompiledShaders{Path.DirectorySeparatorChar}{baseFilename}.fxc";
            string asmExpectedFilename = $"ShaderAssembly{Path.DirectorySeparatorChar}{baseFilename}.asm";
            string hlslExpectedFilename = $"ShaderSources{Path.DirectorySeparatorChar}{baseFilename}.fx";
            string asmOutputFilename = $"{baseFilename}.asm";
            string hlslOutputFilename = $"{baseFilename}.fx";

            var inputStream = File.Open(compiledShaderFilename, FileMode.Open, FileAccess.Read);
            using (var input = new ShaderReader(inputStream, true))
            {
                var shader = input.ReadShader();

                var asmWriter = new AsmWriter(shader);
                asmWriter.Write(asmOutputFilename);

                var hlslWriter = new HlslWriter(shader, true);
                hlslWriter.Write(hlslOutputFilename);
            }

            FileAssert.AreEqual(asmExpectedFilename, asmOutputFilename, "Assembly not equal");
            FileAssert.AreEqual(hlslExpectedFilename, hlslOutputFilename, "HLSL not equal");
        }
    }
}
