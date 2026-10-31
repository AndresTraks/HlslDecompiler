using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HlslDecompiler.Tests;

/// <summary>
/// Recompiles decompiler output with fxc to prove it is valid HLSL.
/// </summary>
[TestFixture]
[Category("Recompile")]
// Each case writes to its own path and shares nothing.
[Parallelizable(ParallelScope.All)]
public class RecompileTests
{
    // A listed shader that starts compiling fails, so its entry gets removed.
    private static readonly Dictionary<string, string> KnownFailures = new()
    {
    };

    public static IEnumerable<TestCaseData> InstructionShaders()
    {
        // Distinct names from Shaders(), so failures say which writer.
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
        if (!ShaderCompiler.IsAvailable)
        {
            Assert.Ignore(ShaderCompiler.UnavailableMessage);
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

    // Separate from KnownFailures: the writers fail on different things.
    private static readonly Dictionary<string, string> KnownInstructionFailures = new()
    {
    };

    [TestCaseSource(nameof(InstructionShaders))]
    [Category("Recompile")]
    public void InstructionOutputCompiles(string profile, string baseFilename)
    {
        if (!ShaderCompiler.IsAvailable)
        {
            Assert.Ignore(ShaderCompiler.UnavailableMessage);
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
    public static string RunFxc(string profile, string hlslFilename, string outputFilename)
    {
        return Recompile(profile, hlslFilename, outputFilename);
    }

    /// <returns>The fxc diagnostics, or null on success.</returns>
    private static string Recompile(string profile, string hlslOutputFilename)
    {
        return Recompile(profile, hlslOutputFilename, Path.ChangeExtension(hlslOutputFilename, ".fxo"));
    }

    // fxc rejects ps_1_x (X3539); its decompilation is ps_2_0 HLSL.
    public static string CompileProfile(string profile) =>
        profile.StartsWith("ps_1_") ? "ps_2_0" : profile;

    private static string Recompile(string profile, string hlslOutputFilename, string objectFilename)
    {
        return ShaderCompiler.Compile(CompileProfile(profile), "main", hlslOutputFilename, objectFilename);
    }

    public static ShaderModel ReadShaderModel(string compiledShaderFilename)
    {
        return ReadShader(compiledShaderFilename);
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

}
