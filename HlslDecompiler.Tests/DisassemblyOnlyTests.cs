using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HlslDecompiler.Tests;

// Shaders the disassembler reads and the HLSL writers do not yet, kept out of
// CompiledShaders because everything in there is run through every tier and these
// would fail the ones that decompile. The listing is still worth pinning: it is
// what says the reader took the instruction apart correctly, and for a construct
// no golden can reach it is the only thing holding the operand layout in place.
//
// Each .fxc here was built by fxc from HLSL, so `fxc /nologo /dumpbin` over the
// same file is the check on the .asm beside it - the differences that remain are
// this writer's own conventions, which the rest of the corpus shares.
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
