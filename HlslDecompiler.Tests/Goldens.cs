using NUnit.Framework;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace HlslDecompiler.Tests;

/// <summary>
/// The expected outputs the tests compare against, read where they are kept in
/// the working tree rather than from copies beside the test assembly. The copies
/// were made by the build, so a golden changed since the last build was compared
/// against the old one: `dotnet test --no-build` reported four correct changes as
/// failures.
///
/// With HLSL_BLESS=1 set, a golden that differs - or is missing - is rewritten
/// with what the writer produced, byte for byte, and the test passes with a
/// warning naming it. `git diff` is then the record of what a change did to the
/// output, which is the review it needs.
/// </summary>
public static class Goldens
{
    public static bool Blessing { get; } =
        Environment.GetEnvironmentVariable("HLSL_BLESS") == "1";

    /// <summary>The test project's folder in the working tree.</summary>
    public static string ProjectDirectory { get; } = FindProjectDirectory();

    /// <summary>Where a golden, named relative to the test project, is kept.</summary>
    public static string PathOf(string relativePath)
    {
        return Path.Combine(ProjectDirectory, relativePath);
    }

    /// <summary>
    /// That the file a writer produced matches its golden - or, blessing, makes it
    /// so. Compared as text, as the tests always have; copied as bytes, so that a
    /// blessed golden is exactly what the writer wrote.
    /// </summary>
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

    // From the path this file was compiled at, which is in the working tree; and
    // where that is not there - the assembly built elsewhere - up from the test
    // assembly to the folder holding the project file.
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
