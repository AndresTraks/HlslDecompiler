using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HlslDecompiler.Tests;

/// <summary>
/// An AST golden compiled by fxc and decompiled again must come back byte for
/// byte. ps_1_x goldens are skipped: they recompile as ps_2_0.
/// </summary>
[TestFixture]
[Category("Recompile")]
// Each case writes to its own path and shares nothing.
[Parallelizable(ParallelScope.All)]
public class FixedPointTests
{
    // Reason classes for KnownNonFixedPoints.txt. Neither changes what the shader
    // computes; SecondRoundComputesTheSame checks that.
    private static readonly Dictionary<string, string> Reasons = new()
    {
        ["reordered"] = "fxc reorders the recompile, and the temporaries are named and the "
            + "commutative operands ordered by where its instructions put them.",
        ["reworded"] = "fxc reorders the recompile, and the same arithmetic comes back "
            + "spelled another way - operand order, which component is named first, "
            + "an idiom recovered or not.",
    };

    private const string KnownFile = "KnownNonFixedPoints.txt";

    // A listed golden that becomes a fixed point fails, or is removed when blessing.
    private static readonly Dictionary<string, string> KnownNonFixedPoints = ReadKnownNonFixedPoints();

    // Removed from the list after all cases run.
    private static readonly ConcurrentDictionary<string, bool> BecameFixedPoints = new();

    private static Dictionary<string, string> ReadKnownNonFixedPoints()
    {
        var known = new Dictionary<string, string>();
        foreach (string line in File.ReadAllLines(Goldens.PathOf(KnownFile)))
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }
            string[] parts = line.Split(' ', 2);
            known[parts[1]] = Reasons[parts[0]];
        }
        return known;
    }

    [OneTimeTearDown]
    public void RemoveBlessedFixedPoints()
    {
        if (BecameFixedPoints.IsEmpty)
        {
            return;
        }
        string path = Goldens.PathOf(KnownFile);
        string[] kept = [.. File.ReadAllLines(path).Where(line =>
            line.Length == 0 || line.StartsWith('#')
            || !BecameFixedPoints.ContainsKey(line.Split(' ', 2)[1]))];
        File.WriteAllText(path, string.Join("\r\n", kept) + "\r\n");
    }

    public static IEnumerable<TestCaseData> Shaders()
    {
        foreach (TestCaseData data in RecompileTests.Shaders())
        {
            yield return new TestCaseData(data.Arguments)
                .SetName($"FixedPoint({data.Arguments[0]},{data.Arguments[1]})");
        }
    }

    [TestCaseSource(nameof(Shaders))]
    public void GoldenDecompilesToItself(string profile, string baseFilename)
    {
        if (!ShaderCompiler.IsAvailable)
        {
            Assert.Ignore(ShaderCompiler.UnavailableMessage);
        }
        if (profile.StartsWith("ps_1_"))
        {
            Assert.Ignore("A ps_1_x golden recompiles as ps_2_0.");
        }

        string golden = Goldens.PathOf(Path.Combine("ShaderSources", profile, baseFilename + ".fx"));
        string objectFilename = Path.Combine("FixedPoint", profile, baseFilename + ".fxo");
        string decompiled = Path.Combine("FixedPoint", profile, baseFilename + ".fx");
        FileUtil.MakeFolder(objectFilename);

        string failure = RecompileTests.RunFxc(profile, golden, objectFilename);
        Assert.That(failure, Is.Null, $"The golden {golden} does not compile:\n{failure}");

        ShaderModel shader = RecompileTests.ReadShaderModel(objectFilename);
        new Hlsl.HlslAstWriter(shader).Write(decompiled);

        string key = $"{profile}/{baseFilename}";
        bool isFixedPoint = File.ReadAllText(decompiled) == File.ReadAllText(golden);
        if (KnownNonFixedPoints.TryGetValue(key, out string reason))
        {
            if (isFixedPoint && Goldens.Blessing)
            {
                BecameFixedPoints[key] = true;
                Assert.Warn($"{key} is now a fixed point, and comes off {KnownFile}.");
                return;
            }
            Assert.That(isFixedPoint, Is.False,
                $"{key} is now a fixed point. Remove it from {KnownFile}, or bless.");
            Assert.Ignore($"Known not to be a fixed point: {reason}");
        }
        Assert.That(File.ReadAllText(decompiled), Is.EqualTo(File.ReadAllText(golden)),
            $"{golden} decompiles from its own compilation as {decompiled}.");
    }

    public static IEnumerable<TestCaseData> SecondRoundShaders()
    {
        foreach (TestCaseData data in RecompileTests.Shaders())
        {
            yield return new TestCaseData(data.Arguments)
                .SetName($"SecondRoundEquivalent({data.Arguments[0]},{data.Arguments[1]})");
        }
    }

    /// <summary>
    /// The golden compiled and its decompilation compiled compute the same: the
    /// equivalence tier covers only the first round.
    /// </summary>
    [TestCaseSource(nameof(SecondRoundShaders))]
    public void SecondRoundComputesTheSame(string profile, string baseFilename)
    {
        if (!ShaderCompiler.IsAvailable)
        {
            Assert.Ignore(ShaderCompiler.UnavailableMessage);
        }
        if (profile.StartsWith("ps_1_"))
        {
            Assert.Ignore("A ps_1_x golden recompiles as ps_2_0.");
        }

        string golden = Goldens.PathOf(Path.Combine("ShaderSources", profile, baseFilename + ".fx"));
        string firstObject = Path.Combine("SecondRound", profile, baseFilename + ".1.fxo");
        string decompiled = Path.Combine("SecondRound", profile, baseFilename + ".fx");
        string secondObject = Path.Combine("SecondRound", profile, baseFilename + ".2.fxo");
        FileUtil.MakeFolder(firstObject);

        Assert.That(RecompileTests.RunFxc(profile, golden, firstObject), Is.Null, $"{golden} does not compile.");
        ShaderModel first = RecompileTests.ReadShaderModel(firstObject);
        new Hlsl.HlslAstWriter(first).Write(decompiled);
        string failure = RecompileTests.RunFxc(profile, decompiled, secondObject);
        Assert.That(failure, Is.Null, $"{decompiled}, the decompilation of {golden}, does not compile:\n{failure}");
        ShaderModel second = RecompileTests.ReadShaderModel(secondObject);

        List<string> differences;
        try
        {
            differences = [.. EquivalenceTests.CompareRuns(first, second,
                "the golden", "its decompilation")];
        }
        catch (System.Exception e) when (e is Interpreter.D3D9Machine.UnsupportedException
            or Interpreter.D3D10Machine.UnsupportedException)
        {
            Assert.Ignore($"Not run: {e.Message}.");
            return;
        }
        Assert.That(differences, Is.Empty, string.Join(System.Environment.NewLine, differences));
    }
}
