using NUnit.Framework;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace HlslDecompiler.Tests;

/// <summary>
/// Goldens read from the working tree, not from build copies that may be stale.
/// With HLSL_BLESS=1, a differing or missing golden is overwritten byte for byte
/// and the test passes with a warning.
/// </summary>
public static class Goldens
{
    public static bool Blessing { get; } =
        Environment.GetEnvironmentVariable("HLSL_BLESS") == "1";

    public static string ProjectDirectory { get; } = FindProjectDirectory();

    public static string PathOf(string relativePath)
    {
        return Path.Combine(ProjectDirectory, relativePath);
    }

    // Compared as text, blessed as bytes.
    public static void AssertMatches(string outputFilename, string goldenRelativePath, string message)
    {
        string golden = PathOf(goldenRelativePath);
        string actual = File.ReadAllText(outputFilename);
        if (Blessing)
        {
            if (!File.Exists(golden) || File.ReadAllText(golden) != actual)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(golden));
                File.Copy(outputFilename, golden, overwrite: true);
                Assert.Warn($"Blessed {goldenRelativePath}.");
            }
            return;
        }
        Assert.That(actual, Is.EqualTo(File.ReadAllText(golden)), message);
    }

    // This file's compile-time path, else up from the assembly to the .csproj.
    private static string FindProjectDirectory()
    {
        string compiledAt = Path.GetDirectoryName(ThisFile());
        if (compiledAt != null && File.Exists(Path.Combine(compiledAt, "HlslDecompiler.Tests.csproj")))
        {
            return compiledAt;
        }
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HlslDecompiler.Tests.csproj")))
            {
                return directory.FullName;
            }
        }
        throw new InvalidOperationException("The test project's folder could not be found.");
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
