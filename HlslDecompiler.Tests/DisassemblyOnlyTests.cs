using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HlslDecompiler.Tests;

// Shaders the HLSL writers cannot handle yet, kept out of CompiledShaders (which
// runs every tier). Their .asm can be checked with `fxc /nologo /dumpbin`.
public class DisassemblyOnlyTests
{
    public static IEnumerable<TestCaseData> Shaders()
    {
        const string root = "DisassemblyOnly";
        if (!Directory.Exists(root))
        {
            yield break;
        }
        foreach (string profileDirectory in Directory.EnumerateDirectories(root).OrderBy(d => d))
        {
            string profile = Path.GetFileName(profileDirectory);
            foreach (string shader in Directory.EnumerateFiles(profileDirectory, "*.fxc").OrderBy(f => f))
            {
                yield return new TestCaseData(profile, Path.GetFileNameWithoutExtension(shader))
                    .SetName($"DisassemblyOnly({profile},{Path.GetFileNameWithoutExtension(shader)})");
            }
        }
    }

    [TestCaseSource(nameof(Shaders))]
    public void DisassemblesToTheExpectedListing(string profile, string baseFilename)
    {
        char separator = Path.DirectorySeparatorChar;
        string compiled = $"DisassemblyOnly{separator}{profile}{separator}{baseFilename}.fxc";
        string expected = $"DisassemblyOnly{separator}{profile}{separator}{baseFilename}.asm";
        string output = $"DisassemblyOnly{separator}{profile}{separator}{baseFilename}.out.asm";

        ShaderModel shader;
        using (var inputStream = File.Open(Path.GetFullPath(compiled), FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var input = new DxbcReader(inputStream, true))
        {
            shader = input.ReadShader();
        }

        FileUtil.MakeFolder(output);
        new AsmWriter(shader).Write(output);

        Goldens.AssertMatches(output, expected, "Assembly not equal at " + output);
    }
}
