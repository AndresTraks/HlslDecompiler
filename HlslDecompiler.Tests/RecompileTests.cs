using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace HlslDecompiler.Tests;

/// <summary>
/// Recompiles decompiler output with fxc.
///
/// The golden-file tests only prove the output has not changed. These prove it is
/// still valid HLSL, which is what stops an expected-output fixture from drifting
/// into something that could never have been produced.
/// </summary>
[TestFixture]
[Category("Recompile")]
public class RecompileTests
{
    /// <summary>
    /// Shaders whose decompiled output does not compile yet, and why. A shader that
    /// starts compiling fails the test so that it gets removed from here.
    /// </summary>
    private static readonly Dictionary<string, string> KnownFailures = new()
    {
        ["ps_3_0/component_chain"] =
            "Writes to components of one register inside a loop, each reading what the "
            + "previous wrote, so one component is assigned more than once in a block. "
            + "AssignmentStatement.Outputs is keyed by register component and holds only "
            + "the last of them, while the temp variables the earlier ones defined stay "
            + "wired into the expressions that read them. They come out as t1 and t2, "
            + "declared nowhere, and the surviving assignment redeclares its own "
            + "variable. Correct without the loop - inside one, the incoming value is "
            + "already a temp assignment, which is what makes every write to the "
            + "component another one. Correct from the instruction writer either way. "
            + "Fixing it means letting a statement carry more than one assignment per "
            + "component, in order, rather than a dictionary.",
    };

    private static readonly Lazy<string> Fxc = new(FindFxc);

    public static IEnumerable<TestCaseData> InstructionShaders()
    {
        // Its own names: sharing Shaders() gave both tests the same one, so a failure
        // pointed at whichever you assumed it was.
        foreach ((string profile, string baseFilename) in ShaderNames())
        {
            yield return new TestCaseData(profile, baseFilename)
                .SetName($"RecompileInstructions({profile},{baseFilename})");
        }
    }

    public static IEnumerable<TestCaseData> Shaders()
    {
        foreach ((string profile, string baseFilename) in ShaderNames())
        {
            yield return new TestCaseData(profile, baseFilename)
                .SetName($"Recompile({profile},{baseFilename})");
        }
    }

    private static IEnumerable<(string Profile, string BaseFilename)> ShaderNames()
    {
        const string root = "CompiledShaders";
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (string profileDirectory in Directory.EnumerateDirectories(root).OrderBy(d => d))
        {
            string profile = Path.GetFileName(profileDirectory);
            foreach (string shader in Directory.EnumerateFiles(profileDirectory, "*.fxc").OrderBy(f => f))
            {
                yield return (profile, Path.GetFileNameWithoutExtension(shader));
            }
        }
    }

    [TestCaseSource(nameof(Shaders))]
    public void DecompiledOutputCompiles(string profile, string baseFilename)
    {
        if (Fxc.Value == null)
        {
            Assert.Ignore("fxc.exe not found. Install the Windows SDK to run recompilation tests.");
        }

        string compiledShaderFilename = Path.Combine("CompiledShaders", profile, baseFilename + ".fxc");
        string hlslOutputFilename = Path.Combine("Recompile", profile, baseFilename + ".fx");

        string failure = Decompile(compiledShaderFilename, hlslOutputFilename, s => new HlslAstWriter(s))
            ?? Recompile(profile, hlslOutputFilename);

        string key = $"{profile}/{baseFilename}";
        if (KnownFailures.TryGetValue(key, out string reason))
        {
            Assert.That(failure, Is.Not.Null,
                $"{key} now recompiles. Remove it from {nameof(KnownFailures)}.");
            Assert.Ignore($"Known failure: {reason}");
        }

        Assert.That(failure, Is.Null,
            $"Decompiled output at {hlslOutputFilename} does not compile:{Environment.NewLine}{failure}");
    }

    /// <summary>
    /// Shaders whose instruction writer output does not compile yet, and why. Kept
    /// apart from <see cref="KnownFailures"/>: the two writers fail on different
    /// things, and a shader can round trip through one and not the other.
    /// </summary>
    private static readonly Dictionary<string, string> KnownInstructionFailures = new()
    {
        ["ps_3_0/struct"] = "Subscripts a struct member as though it were a vector.",
        ["ps_4_0/nested_struct"] = "Subscripts a struct member as though it were a vector.",
        ["ps_4_0/logical_and"] =
            "A bitwise operator applied to a float register. The register holds the "
            + "mask a float comparison writes, which IntegerOperandAnalysis does not "
            + "count as integer-producing. Marking comparisons as such types the "
            + "register correctly and breaks saturate_step, below.",
        ["ps_4_0/saturate_step"] =
            "The same bitwise operator on a float register, and the reason the "
            + "obvious fix does not work: the mask is anded with 0x3f800000, the bits "
            + "of 1.0f. Type the register as integer and that immediate reads as the "
            + "integer 1065353216, which is worse - it compiles and is wrong. The "
            + "register is genuinely neither, and the analysis has no way to say so.",
        ["ps_4_0/int_divide"] =
            "An integer immediate prints as a float, and r1 and r2 - the second "
            + "destination of udiv - are used but never declared.",
        ["vs_3_0/loop_nested_uniform"] = "Passes something that is not a value to a loop bound.",
        ["vs_3_0/matrix_array"] = "Calls transpose on a single row rather than the matrix.",
    };

    /// <summary>
    /// The same check for HlslSimpleWriter. It had none, which is how four shaders
    /// came to be emitting output fxc rejects without anyone noticing.
    /// </summary>
    [TestCaseSource(nameof(InstructionShaders))]
    [Category("Recompile")]
    public void InstructionOutputCompiles(string profile, string baseFilename)
    {
        if (Fxc.Value == null)
        {
            Assert.Ignore("fxc.exe not found. Install the Windows SDK to run recompilation tests.");
        }

        string compiledShaderFilename = Path.Combine("CompiledShaders", profile, baseFilename + ".fxc");
        string hlslOutputFilename = Path.Combine("Recompile", profile + "_instruction", baseFilename + ".fx");

        string failure = Decompile(compiledShaderFilename, hlslOutputFilename, s => new HlslSimpleWriter(s))
            ?? Recompile(profile, hlslOutputFilename);

        string key = $"{profile}/{baseFilename}";
        if (KnownInstructionFailures.TryGetValue(key, out string reason))
        {
            Assert.That(failure, Is.Not.Null,
                $"{key} now recompiles. Remove it from {nameof(KnownInstructionFailures)}.");
            Assert.Ignore($"Known failure: {reason}");
        }

        Assert.That(failure, Is.Null,
            $"Instruction writer output at {hlslOutputFilename} does not compile:{Environment.NewLine}{failure}");
    }

    /// <returns>An error description, or null on success.</returns>
    private static string Decompile(
        string compiledShaderFilename,
        string hlslOutputFilename,
        Func<ShaderModel, HlslWriter> createWriter)
    {
        try
        {
            ShaderModel shader = ReadShader(compiledShaderFilename);
            FileUtil.MakeFolder(hlslOutputFilename);
            createWriter(shader).Write(hlslOutputFilename);
            return null;
        }
        catch (Exception e)
        {
            return $"Decompilation threw: {e}";
        }
    }

    /// <returns>The fxc diagnostics, or null on success.</returns>
    private static string Recompile(string profile, string hlslOutputFilename)
    {
        var startInfo = new ProcessStartInfo(Fxc.Value)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("/nologo");
        startInfo.ArgumentList.Add("/T");
        startInfo.ArgumentList.Add(profile);
        startInfo.ArgumentList.Add("/E");
        startInfo.ArgumentList.Add("main");
        startInfo.ArgumentList.Add(hlslOutputFilename);
        startInfo.ArgumentList.Add("/Fo");
        startInfo.ArgumentList.Add(Path.ChangeExtension(hlslOutputFilename, ".fxo"));

        using var process = Process.Start(startInfo);
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();

        return process.ExitCode == 0 ? null : output.Trim();
    }

    private static ShaderModel ReadShader(string compiledShaderFilename)
    {
        // Share the read: the golden-file fixtures leave their own handles open.
        using var stream = File.Open(
            Path.GetFullPath(compiledShaderFilename), FileMode.Open, FileAccess.Read, FileShare.Read);

        bool isDxbc;
        using (var peek = new BinaryReader(stream, new UTF8Encoding(), true))
        {
            isDxbc = peek.ReadUInt32() == 0x43425844; // "DXBC"
        }
        stream.Position = 0;

        if (isDxbc)
        {
            using var dxbcReader = new DxbcReader(stream, true);
            return dxbcReader.ReadShader();
        }

        using var shaderReader = new ShaderReader(stream, true);
        return shaderReader.ReadShader();
    }

    private static string FindFxc()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string[] roots =
        [
            Path.Combine(programFiles, "Windows Kits", "10", "bin"),
            Path.Combine(programFiles, "Windows Kits", "8.1", "bin"),
        ];

        return roots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "fxc.exe", SearchOption.AllDirectories))
            .Where(path => path.Contains(@"\x64\") || path.Contains(@"\x86\"))
            // Prefer the newest SDK, and x64 over x86.
            .OrderByDescending(path => path)
            .FirstOrDefault();
    }
}
