using HlslDecompiler.DirectXShaderModel.Xenos;
using NUnit.Framework;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace HlslDecompiler.Tests;

/// <summary>
/// Xbox 360 shaders, compiled from the .fx beside each by XNA Game Studio 3.1
/// (tools/xna-xbox). The listing must match its golden, and, with the XNA
/// helper built, XNA's assembler must turn the listing back into the same
/// shader: program header, literals and microcode, but for the fields no
/// listing can state (see <see cref="Normalize"/>).
/// </summary>
[Category("Xenos")]
[Parallelizable(ParallelScope.All)]
public class XenosTests
{
    private const string Root = "XenosShaders";

    // XNA_XBOX_EXE, else the helper as tools/xna-xbox/build.ps1 leaves it.
    private static readonly Lazy<string> Xna = new(() =>
    {
        string path = Environment.GetEnvironmentVariable("XNA_XBOX_EXE")
            ?? Path.Combine(Goldens.ProjectDirectory, "..", "tools", "xna-xbox", "bin", "XnaXbox.exe");
        return OperatingSystem.IsWindows() && File.Exists(path) ? Path.GetFullPath(path) : null;
    });

    public static IEnumerable<TestCaseData> Shaders()
    {
        string root = Goldens.PathOf(Root);
        if (!Directory.Exists(root))
        {
            yield break;
        }
        foreach (string profileDirectory in Directory.EnumerateDirectories(root).OrderBy(d => d))
        {
            string profile = Path.GetFileName(profileDirectory);
            foreach (string shader in Directory.EnumerateFiles(profileDirectory, "*.bin").OrderBy(f => f))
            {
                yield return new TestCaseData(profile, Path.GetFileNameWithoutExtension(shader));
            }
        }
    }

    private static string Fixture(string profile, string baseFilename, string extension) =>
        Goldens.PathOf(Path.Combine(Root, profile, baseFilename + extension));

    private static string Disassemble(string profile, string baseFilename)
    {
        using var input = File.OpenRead(Fixture(profile, baseFilename, ".bin"));
        return new XenosAsmWriter(new XenosReader(input).ReadShader()).Write();
    }

    [TestCaseSource(nameof(Shaders))]
    public void DisassemblesToTheExpectedListing(string profile, string baseFilename)
    {
        string output = Path.Combine("Xenos", profile, baseFilename + ".out.asm");
        FileUtil.MakeFolder(output);
        File.WriteAllText(output, Disassemble(profile, baseFilename));
        Goldens.AssertMatches(output, Path.Combine(Root, profile, baseFilename + ".asm"),
            "Listing not equal at " + output);
    }

    [TestCaseSource(nameof(Shaders))]
    public void ListingAssemblesToTheSameShader(string profile, string baseFilename)
    {
        if (Xna.Value == null)
        {
            Assert.Ignore("XnaXbox.exe not found: build it with tools/xna-xbox/build.ps1 or set XNA_XBOX_EXE.");
        }

        string listing = Path.GetFullPath(Path.Combine("Xenos", profile, baseFilename + ".rt.asm"));
        string reassembled = Path.ChangeExtension(listing, ".bin");
        FileUtil.MakeFolder(listing);
        File.WriteAllText(listing, Disassemble(profile, baseFilename));
        File.Delete(reassembled);

        var startInfo = new ProcessStartInfo(Xna.Value)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("assemble");
        startInfo.ArgumentList.Add(listing);
        startInfo.ArgumentList.Add(reassembled);
        using (var process = Process.Start(startInfo))
        {
            process.StandardOutput.ReadToEnd();
            string errors = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.That(process.ExitCode, Is.Zero, $"{listing} does not assemble:{Environment.NewLine}{errors}");
        }

        Parts original = Parts.Read(File.ReadAllBytes(Fixture(profile, baseFilename, ".bin")));
        Parts roundTrip = Parts.Read(File.ReadAllBytes(reassembled));
        Assert.That(Hex(roundTrip.Header), Is.EqualTo(Hex(original.Header)), "Program header");
        Assert.That(Hex(roundTrip.Literals), Is.EqualTo(Hex(original.Literals)), "Literals");
        Assert.That(Hex(Normalize(roundTrip.Microcode, original.PatchedAddresses)),
            Is.EqualTo(Hex(Normalize(original.Microcode, original.PatchedAddresses))), "Microcode");
    }

    // What the translation into shader model 3 cannot express. A listed shader
    // that starts translating fails, so its entry gets removed.
    private static readonly Dictionary<string, string> KnownUntranslatable = new()
    {
        ["xps_3_0/dynindex"] = "A pixel shader indexes constants through a0, which ps_3_0 does not have.",
    };

    // Decompiler bugs the translations expose, by tier. A listed shader that
    // starts passing fails, so its entry gets removed.
    private static readonly Dictionary<string, string> KnownDecompilationFailures = new()
    {
        ["xps_3_0/nested"] = "Nested loops: both counters are named Loop0.",
    };

    private static readonly Dictionary<string, string> KnownRoundTripFailures = new()
    {
        ["xps_3_0/branch"] = "An if inside an if writes a lane-split assignment as float3(t1.x = t3;, ...).",
        ["xps_3_0/nested"] = "Nested loops: both counters are named Loop0.",
        ["xvs_3_0/skin"] = "dp4s of wzxy-swizzled rows group into mul(...).yzx, which permutes the result.",
    };

    // Runs check; for a listed shader, asserts it still fails and ignores the test.
    private static void Expect(Dictionary<string, string> known, string profile, string baseFilename, Action check)
    {
        string key = $"{profile}/{baseFilename}";
        if (!known.TryGetValue(key, out string reason))
        {
            check();
            return;
        }
        bool failed;
        try
        {
            check();
            failed = false;
        }
        catch (Exception e) when (e is not IgnoreException)
        {
            failed = true;
        }
        Assert.That(failed, Is.True, $"{key} now passes. Remove it from the known failures.");
        Assert.Ignore($"Known failure: {reason}");
    }

    /// <summary>The translation, or an ignored test for a known gap.</summary>
    private static DirectXShaderModel.ShaderModel Translate(string profile, string baseFilename)
    {
        XenosShader shader;
        using (var input = File.OpenRead(Fixture(profile, baseFilename, ".bin")))
        {
            shader = new XenosReader(input).ReadShader();
        }
        if (KnownUntranslatable.TryGetValue($"{profile}/{baseFilename}", out string reason))
        {
            Assert.Throws<NotSupportedException>(() => new XenosTranslator(shader).TranslateToShaderModel(),
                $"{profile}/{baseFilename} now translates. Remove it from {nameof(KnownUntranslatable)}.");
            Assert.Ignore($"Not translated: {reason}");
        }
        return new XenosTranslator(shader).TranslateToShaderModel();
    }

    /// <summary>
    /// The translation into shader model 3 runs as fxc's D3D9 compile of the
    /// same source does: the Xbox compiler's code, read through the translator,
    /// against an independent compiler's.
    /// </summary>
    [TestCaseSource(nameof(Shaders))]
    public void TranslationComputesWhatTheSourceDoes(string profile, string baseFilename)
    {
        if (!ShaderCompiler.IsAvailable)
        {
            Assert.Ignore(ShaderCompiler.UnavailableMessage);
        }
        string d3d9Profile = profile.Substring(1);
        string compiled = Path.GetFullPath(Path.Combine("Xenos", profile, baseFilename + ".d3d9.fxo"));
        FileUtil.MakeFolder(compiled);
        string failure = ShaderCompiler.Compile(d3d9Profile, "main", Fixture(profile, baseFilename, ".fx"), compiled);
        if (failure != null)
        {
            Assert.Ignore($"fxc does not compile the source for {d3d9Profile}: {failure}");
        }
        DirectXShaderModel.ShaderModel reference = RecompileTests.ReadShaderModel(compiled);

        DirectXShaderModel.ShaderModel translated = Translate(profile, baseFilename);

        List<string> differences;
        try
        {
            // The translation on the left: a trial it leaves undefined (an index
            // past its array, which fxc's code may handle another way) is dropped.
            differences = [.. EquivalenceTests.CompareRuns(translated, reference, "the translation", "fxc's compile")];
        }
        catch (Interpreter.D3D9Machine.UnsupportedException e)
        {
            Assert.Ignore($"Not run: {e.Message}.");
            return;
        }
        Assert.That(differences, Is.Empty, string.Join(Environment.NewLine, differences.Take(12)));
    }

    /// <summary>
    /// The HLSL the AST writer makes of the translation matches its golden.
    /// The translation is kept beside the output, as D3D9 bytecode, to read with --asm.
    /// </summary>
    [TestCaseSource(nameof(Shaders))]
    public void DecompilesToTheExpectedHlsl(string profile, string baseFilename)
    {
        DirectXShaderModel.ShaderModel translated = Translate(profile, baseFilename);
        Expect(KnownDecompilationFailures, profile, baseFilename, () => CheckHlslGolden(profile, baseFilename, translated));
    }

    private static void CheckHlslGolden(string profile, string baseFilename, DirectXShaderModel.ShaderModel translated)
    {
        string output = Path.Combine("Xenos", profile, baseFilename + ".out.hlsl");
        FileUtil.MakeFolder(output);
        using (var input = File.OpenRead(Fixture(profile, baseFilename, ".bin")))
        {
            File.WriteAllBytes(Path.ChangeExtension(output, ".d3d9.fxc"),
                new XenosTranslator(new XenosReader(input).ReadShader()).Translate());
        }
        new Hlsl.HlslAstWriter(translated).Write(output);
        Goldens.AssertMatches(output, Path.Combine(Root, profile, baseFilename + ".hlsl"),
            "HLSL not equal at " + output);
    }

    /// <summary>
    /// The decompiled HLSL compiles with XNA for the Xbox 360, and what it
    /// compiles to computes what the original does, both read through the
    /// translation: the decompiler's round trip, on the console's compiler.
    /// </summary>
    [TestCaseSource(nameof(Shaders))]
    public void DecompiledHlslRecompilesToTheSameShader(string profile, string baseFilename)
    {
        if (Xna.Value == null)
        {
            Assert.Ignore("XnaXbox.exe not found: build it with tools/xna-xbox/build.ps1 or set XNA_XBOX_EXE.");
        }
        DirectXShaderModel.ShaderModel translated = Translate(profile, baseFilename);
        Expect(KnownRoundTripFailures, profile, baseFilename, () => CheckRoundTrip(profile, baseFilename, translated));
    }

    private static void CheckRoundTrip(string profile, string baseFilename, DirectXShaderModel.ShaderModel translated)
    {
        string hlsl = Path.GetFullPath(Path.Combine("Xenos", profile, baseFilename + ".rt.hlsl"));
        string recompiled = Path.ChangeExtension(hlsl, ".xbin");
        FileUtil.MakeFolder(hlsl);
        new Hlsl.HlslAstWriter(translated).Write(hlsl);
        File.Delete(recompiled);

        var startInfo = new ProcessStartInfo(Xna.Value)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("compile");
        startInfo.ArgumentList.Add(profile.Substring(1));
        startInfo.ArgumentList.Add(hlsl);
        startInfo.ArgumentList.Add(recompiled);
        using (var process = Process.Start(startInfo))
        {
            process.StandardOutput.ReadToEnd();
            string errors = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.That(process.ExitCode, Is.Zero, $"{hlsl} does not compile for the Xbox 360:{Environment.NewLine}{errors}");
        }

        DirectXShaderModel.ShaderModel roundTrip;
        using (var input = File.OpenRead(recompiled))
        {
            roundTrip = new XenosTranslator(new XenosReader(input).ReadShader()).TranslateToShaderModel();
        }
        List<string> differences;
        try
        {
            differences = [.. EquivalenceTests.CompareRuns(translated, roundTrip, "the original", "its decompilation")];
        }
        catch (Interpreter.D3D9Machine.UnsupportedException e)
        {
            Assert.Ignore($"Not run: {e.Message}.");
            return;
        }
        Assert.That(differences, Is.Empty, string.Join(Environment.NewLine, differences.Take(12)));
    }

    private static string[] Hex(IEnumerable<uint> words) => words.Select(w => w.ToString("x8")).ToArray();

    private sealed record Parts(uint[] Header, uint[] Literals, uint[] Microcode, int[] PatchedAddresses)
    {
        public static Parts Read(byte[] data)
        {
            uint Word(int offset) => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset));
            uint[] Words(int offset, int count) => Enumerable.Range(0, count).Select(i => Word(offset + i * 4)).ToArray();

            int dataOffset = (int)Word(0x04);
            int programOffset = (int)Word(0x18);
            uint[] header = Words(programOffset, (dataOffset - programOffset) / 4);
            int literalBytes = (int)header[0];
            // A vertex shader's vfetch addresses the runtime binds to elements.
            int[] patched = Word(0) == XenosReader.VertexMagic
                ? Enumerable.Range(0, (int)header[7]).Select(i => (int)(header[9 + header[6] + i] & 0xFFF)).ToArray()
                : [];
            return new Parts(header, Words(dataOffset, literalBytes / 4),
                Words(dataOffset + literalBytes, (int)header[1] / 4), patched);
        }
    }

    /// <summary>
    /// Clears the bits a listing cannot state, which the compiler and the
    /// assembler fill differently:
    /// - an exec's address when it runs nothing, and a plain endloop's condition;
    /// - the fetch constant and data format of a vfetch the runtime binds;
    /// - setTexLOD's source past x, and the attributes of setTexLOD and
    ///   setGradientH/V, which take none;
    /// - a scalar operation's unused source lanes, when the vector operation
    ///   leaves the third operand to it;
    /// - an export's constant-0 bit when both operations write every lane.
    /// </summary>
    private static uint[] Normalize(uint[] microcode, int[] patchedAddresses)
    {
        uint[] code = (uint[])microcode.Clone();
        int pairCount = code.Length / 3;
        var execs = new List<ControlFlowInstruction>();
        for (int i = 0; i < pairCount; i++)
        {
            for (int half = 0; half < 2; half++)
            {
                (ControlFlowInstruction a, ControlFlowInstruction b) =
                    ControlFlowInstruction.Unpack(code[i * 3], code[i * 3 + 1], code[i * 3 + 2]);
                ControlFlowInstruction cf = half == 0 ? a : b;
                uint word0 = half == 0 ? code[i * 3] : (code[i * 3 + 1] >> 16) | (code[i * 3 + 2] << 16);
                uint word1 = half == 0 ? code[i * 3 + 1] & 0xFFFF : code[i * 3 + 2] >> 16;
                if (cf.IsExec)
                {
                    pairCount = Math.Min(pairCount, cf.ExecAddress);
                    execs.Add(cf);
                    if (cf.ExecCount == 0)
                    {
                        word0 &= ~0xFFFu;
                    }
                }
                if (cf.Opcode == ControlFlowOpcode.LoopEnd && !cf.IsPredicatedBreak)
                {
                    word1 &= ~(1u << 10);
                }
                if (half == 0)
                {
                    code[i * 3] = word0;
                    code[i * 3 + 1] = (code[i * 3 + 1] & 0xFFFF0000) | word1;
                }
                else
                {
                    code[i * 3 + 1] = (code[i * 3 + 1] & 0xFFFF) | (word0 << 16);
                    code[i * 3 + 2] = (word0 >> 16) | (word1 << 16);
                }
            }
        }

        foreach (ControlFlowInstruction cf in execs)
        {
            for (int k = 0; k < cf.ExecCount; k++)
            {
                int s = (cf.ExecAddress + k) * 3;
                if (((cf.Sequence >> (2 * k)) & 1) != 0)
                {
                    var fetch = new FetchInstruction(code[s], code[s + 1], code[s + 2]);
                    if (fetch.Opcode is FetchOpcode.SetTextureLod or FetchOpcode.SetTextureGradientsHorz
                        or FetchOpcode.SetTextureGradientsVert)
                    {
                        code[s + 1] &= 0x80000FFF;
                        code[s + 2] &= 0x80000000;
                        if (fetch.Opcode == FetchOpcode.SetTextureLod)
                        {
                            code[s] &= ~(0xFu << 28);
                        }
                    }
                    continue;
                }
                var alu = new AluInstruction(code[s], code[s + 1], code[s + 2]);
                bool vectorUsesThird = alu.VectorOpcode is AluVectorOpcode.Mad or AluVectorOpcode.CndEq
                    or AluVectorOpcode.CndGe or AluVectorOpcode.CndGt or AluVectorOpcode.Dp2Add;
                if (!vectorUsesThird)
                {
                    int scalar = (int)alu.ScalarOpcode;
                    bool twoLanes = alu.ScalarOpcode is AluScalarOpcode.Adds or AluScalarOpcode.Muls
                        or AluScalarOpcode.MulsPrev2 or AluScalarOpcode.Maxs or AluScalarOpcode.Mins
                        or AluScalarOpcode.MaxAs or AluScalarOpcode.MaxAsf or AluScalarOpcode.Subs;
                    bool noOperand = alu.ScalarOpcode is AluScalarOpcode.SetpClr or AluScalarOpcode.RetainPrev
                        || scalar == 41 || scalar > 50;
                    bool constantForm = scalar >= 42 && scalar <= 47;
                    // W is the swizzle's top two bits, X its bottom two.
                    code[s + 1] &= twoLanes ? ~0x3Cu : noOperand ? ~0xFFu : constantForm ? ~0u : ~0x3Fu;
                }
                if (alu.IsExport && (alu.VectorWriteMask | alu.ScalarWriteMask) == 0xF)
                {
                    code[s] &= ~(1u << 14);
                }
            }
        }

        foreach (int address in patchedAddresses)
        {
            code[address * 3] &= ~((0x1Fu << 20) | (0x3u << 25));
            code[address * 3 + 1] &= ~(0x3Fu << 16);
        }
        return code;
    }
}
