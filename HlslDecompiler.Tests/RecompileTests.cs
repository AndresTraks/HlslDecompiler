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
        ["ps_3_0/struct"] =
            "Struct members decompile to .x/.y instead of member names",
        ["ps_3_0/temp_assignment"] =
            "Temp register assignments are not reconstructed correctly",
    };

    private static readonly Lazy<string> Fxc = new(FindFxc);

    public static IEnumerable<TestCaseData> Shaders()
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
                string baseFilename = Path.GetFileNameWithoutExtension(shader);
                yield return new TestCaseData(profile, baseFilename)
                    .SetName($"Recompile({profile},{baseFilename})");
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

        string failure = Decompile(compiledShaderFilename, hlslOutputFilename)
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

    /// <returns>An error description, or null on success.</returns>
    private static string Decompile(string compiledShaderFilename, string hlslOutputFilename)
    {
        try
        {
            ShaderModel shader = ReadShader(compiledShaderFilename);
            FileUtil.MakeFolder(hlslOutputFilename);
            new HlslAstWriter(shader).Write(hlslOutputFilename);
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
