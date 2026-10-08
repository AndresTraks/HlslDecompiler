using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HlslDecompiler.Tests;

/// <summary>
/// That an AST golden is a fixed point: compiled by fxc and decompiled again, it
/// comes back byte for byte. A golden that does not is a decompilation of one
/// shader that decompiles a second shader into something else, so which of the
/// two texts the golden holds is an accident of where the fixture started.
///
/// The goldens only say the text has not changed since it was written, and every
/// new fixture was checked for this by hand. The corpus as a whole had never been
/// checked, and most of what fails here was committed that way.
///
/// The ps_1_x goldens are left out: they recompile as ps_2_0, and decompile from
/// that as the ps_2_0 shader they are.
/// </summary>
[TestFixture]
[Category("Recompile")]
// Every case is independent: it compiles one golden into a path named after its
// profile and shader, and shares nothing with the others.
[Parallelizable(ParallelScope.All)]
public class FixedPointTests
{
    // The goldens that were not a fixed point when this test was written, in the
    // two classes measured then, listed in KnownNonFixedPoints.txt. Neither is a
    // difference in what the shader computes - SecondRoundComputesTheSame holds
    // every one of them to that.
    //
    // reordered: the same lines once the temporaries' names are set aside. fxc
    // schedules the recompile its own way, and the decompiler names temporaries and
    // orders commutative operands by where the instructions put them.
    //
    // reworded: the same arithmetic, spelled differently from fxc's schedule - the
    // operands of an addition or a comparison in register order, a different
    // component of a load named first, an idiom recovered in one round and not the
    // next. When the list was first made, fifty-two oscillated between two texts,
    // twenty-five settled on a second after one round, and twenty-four were still
    // moving after two.
    private static readonly Dictionary<string, string> Reasons = new()
    {
        ["reordered"] = "fxc reorders the recompile, and the temporaries are named and the "
            + "commutative operands ordered by where its instructions put them.",
        ["reworded"] = "fxc reorders the recompile, and the same arithmetic comes back "
            + "spelled another way - operand order, which component is named first, "
            + "an idiom recovered or not.",
    };

    private const string KnownFile = "KnownNonFixedPoints.txt";

    /// <summary>
    /// Goldens that are not a fixed point, and why. One that becomes one fails the
    /// test so that its entry gets removed - or, blessing, is removed; a golden not
    /// listed is held to being one.
    /// </summary>
    private static readonly Dictionary<string, string> KnownNonFixedPoints = ReadKnownNonFixedPoints();

    // The listed goldens found to be fixed points while blessing, taken off the list
    // once every case has run.
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
        if (RecompileTests.FxcPath == null)
        {
            Assert.Ignore("fxc.exe not found. Install the Windows SDK to run recompilation tests.");
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
    /// That where a golden is not a fixed point, the text it drifts to still means
    /// what it does: the golden compiled, and the decompilation of that compiled
    /// again, compute the same. The equivalence tier checks the first round, from the
    /// fixture's own bytecode, and nothing checked the second - a groupshared word
    /// stored as the bits of FLT_MAX came back the second time as the float itself,
    /// stored into an integer, which converts it.
    /// </summary>
    [TestCaseSource(nameof(SecondRoundShaders))]
    public void SecondRoundComputesTheSame(string profile, string baseFilename)
    {
        if (RecompileTests.FxcPath == null)
        {
            Assert.Ignore("fxc.exe not found. Install the Windows SDK to run recompilation tests.");
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
