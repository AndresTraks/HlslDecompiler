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
        public void DecompileTest(string baseFilename)
        {
            string compiledShaderFilename = string.Format("CompiledShaders{0}{1}.fxc", Path.DirectorySeparatorChar, baseFilename);
            string asmExpectedFilename = string.Format("ShaderAssembly{0}{1}.asm", Path.DirectorySeparatorChar, baseFilename);
            string hlslExpectedFilename = string.Format("ShaderSources{0}{1}.fx", Path.DirectorySeparatorChar, baseFilename);
            string asmOutputFilename = string.Format("{0}.asm", baseFilename);
            string hlslOutputFilename = string.Format("{0}.fx", baseFilename);

            var inputStream = File.Open(compiledShaderFilename, FileMode.Open, FileAccess.Read);
            using (var input = new ShaderReader(inputStream, true))
            {
                var shader = input.ReadShader();

                var asmWriter = new AsmWriter(shader);
                asmWriter.Write(asmOutputFilename);

                var hlslWriter = new HlslWriter(shader);
                hlslWriter.Write(hlslOutputFilename);
            }

            FileAssert.AreEqual(asmExpectedFilename, asmOutputFilename);
            FileAssert.AreEqual(hlslExpectedFilename, hlslOutputFilename);
        }
    }
}
