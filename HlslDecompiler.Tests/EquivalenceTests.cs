using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using HlslDecompiler.Tests.Interpreter;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HlslDecompiler.Tests;

/// <summary>
/// Runs the original shader and a recompilation of each writer's output, and
/// compares what they compute.
///
/// The golden files prove the output has not changed and the recompile tests prove
/// it is valid HLSL. Neither says it is the same shader. A wrong constant, a
/// dropped conversion or a swapped argument compiles perfectly, which is how three
/// fixtures came to hold arithmetic that was simply wrong.
/// </summary>
[TestFixture]
[Category("Recompile")]
// Every case is independent: it reads one .fxc, writes its output under a path
// named after its profile and shader, and shares nothing with the others.
[Parallelizable(ParallelScope.All)]
public class EquivalenceTests
{
    /// <summary>
    /// Shaders whose decompilation computes something else, by which writer, and
    /// why. A writer not listed is still held to computing the same, and a listed
    /// one that starts agreeing fails the test so that its entry gets removed from
    /// here. Both writers can be listed, on different causes.
    /// </summary>
    private static readonly Dictionary<string, (string Writer, string Reason)[]> KnownDifferences = new()
    {
        ["cs_4_0/particle_update"] = [("ast",
            "Two dwords the original stores and the recompilation does not. The "
            + "particle's velocity is written back a member at a time, and its x and "
            + "z are the x and z that were loaded from the same element a few lines "
            + "up, so fxc drops those two stores as writes of what is there. The "
            + "buffer ends up the same; the store is what is compared, since a "
            + "buffer is not modelled.")],
    };

    /// <summary>How many sets of inputs each shader is run over.</summary>
    private const int Trials = 8;

    /// <summary>
    /// Two results count as the same within this much, relative to the larger.
    /// fxc reassociates freely - folding a multiply and an add into a mad, or the
    /// other way about - so the last bits are not expected to agree.
    /// </summary>
    private const float Tolerance = 1e-3f;

    /// <summary>
    /// Every compiled shader there is, whatever its profile - the same set the
    /// recompile tier runs. A fixed list of profiles here let a shader in a new
    /// folder go uncompared, and its decompilation was wrong. Geometry and compute
    /// shaders have no return value, so the machine reports what they put on their
    /// stream and what they wrote to their buffers instead.
    /// </summary>
    public static IEnumerable<TestCaseData> Shaders()
    {
        foreach (TestCaseData data in RecompileTests.Shaders())
        {
            yield return new TestCaseData(data.Arguments)
                .SetName($"Equivalent({data.Arguments[0]},{data.Arguments[1]})");
        }
    }

    [TestCaseSource(nameof(Shaders))]
    public void DecompiledOutputComputesTheSame(string profile, string baseFilename)
    {
        if (RecompileTests.FxcPath == null)
        {
            Assert.Ignore("fxc.exe not found. Install the Windows SDK to run recompilation tests.");
        }

        ShaderModel original = RecompileTests.ReadShaderModel(
            Path.Combine("CompiledShaders", profile, baseFilename + ".fxc"));

        var differences = new List<(string Writer, string Message)>();
        string unsupported = null;
        int compared = 0;
        foreach ((string writer, Func<ShaderModel, HlslWriter> create) in Writers())
        {
            ShaderModel recompiled = Recompile(profile, baseFilename, writer, create, original);
            if (recompiled == null)
            {
                continue;
            }

            try
            {
                differences.AddRange(Compare(writer, original, recompiled).Select(d => (writer, d)).ToList());
                compared++;
            }
            catch (Exception e) when (e is D3D9Machine.UnsupportedException
                or D3D10Machine.UnsupportedException)
            {
                unsupported = e.Message;
            }
        }

        // Said out loud rather than passed quietly: a machine that cannot run the
        // shader has checked nothing, and a silent pass would read as coverage.
        if (compared == 0)
        {
            Assert.Ignore(unsupported == null
                ? "Neither writer's output could be recompiled."
                : $"Not run: {unsupported}.");
        }

        string key = $"{profile}/{baseFilename}";
        List<string> unexpected = [.. differences.Select(d => d.Message)];
        if (KnownDifferences.TryGetValue(key, out (string Writer, string Reason)[] known))
        {
            foreach ((string writer, _) in known)
            {
                Assert.That(differences.Any(d => d.Writer == writer), Is.True,
                    $"The {writer} writer's output for {key} now computes the same. "
                    + $"Remove it from {nameof(KnownDifferences)}.");
            }
            unexpected = [.. differences
                .Where(d => !known.Any(k => k.Writer == d.Writer))
                .Select(d => d.Message)];
        }

        Assert.That(unexpected, Is.Empty,
            string.Join(Environment.NewLine, unexpected));

        if (known != null)
        {
            Assert.Ignore(string.Join(" ",
                known.Select(k => $"Known difference in the {k.Writer} writer: {k.Reason}")));
        }
    }

    private static IEnumerable<(string Name, Func<ShaderModel, HlslWriter> Create)> Writers()
    {
        yield return ("ast", s => new HlslAstWriter(s));
        yield return ("instruction", s => new HlslSimpleWriter(s));
    }

    /// <returns>The recompiled shader, or null if this writer's output is not usable.</returns>
    private static ShaderModel Recompile(
        string profile,
        string baseFilename,
        string writer,
        Func<ShaderModel, HlslWriter> create,
        ShaderModel original)
    {
        string hlslFilename = Path.Combine("Equivalence", profile + "_" + writer, baseFilename + ".fx");
        string objectFilename = Path.ChangeExtension(hlslFilename, ".fxo");
        FileUtil.MakeFolder(hlslFilename);

        try
        {
            create(original).Write(hlslFilename);
        }
        catch (Exception)
        {
            // Decompilation failures are the other tests' business.
            return null;
        }

        if (RecompileTests.RunFxc(profile, hlslFilename, objectFilename) != null)
        {
            return null;
        }

        return RecompileTests.ReadShaderModel(objectFilename);
    }

    private static IEnumerable<string> Compare(string writer, ShaderModel original, ShaderModel recompiled)
    {
        return CompareRuns(original, recompiled, "the original", "its decompilation")
            .Select(difference => $"The {writer} writer's output {difference}");
    }

    /// <summary>
    /// What two shaders compute differently over the trials, a sentence each, or
    /// nothing where they agree.
    ///
    /// The two sides are named rather than assumed, because this answers two
    /// questions. The test asks whether a decompilation computes what the shader it
    /// came from computes. The cost sweep asks whether a rule changed what a shader
    /// computes, which is the baseline against the variant - and that question needs
    /// no view on whether either agrees with the original, so a shader already known
    /// to differ is not noise to it.
    /// </summary>
    internal static IEnumerable<string> CompareRuns(
        ShaderModel left, ShaderModel right, string leftName, string rightName)
    {
        for (int trial = 0; trial < Trials; trial++)
        {
            Dictionary<string, float[]> expected = Run(left, trial);
            Dictionary<string, float[]> actual = Run(right, trial);

            // A discarded pixel has no outputs to compare, so a decompilation that
            // fails to discard where the original does - or discards where it does
            // not - agreed with it on every output it had. Discarding is an outcome.
            if ((expected.Count == 0) != (actual.Count == 0))
            {
                yield return $"differs on trial {trial}: "
                    + (expected.Count == 0
                        ? $"{leftName} discards the pixel, {rightName} does not."
                        : $"{rightName} discards the pixel, {leftName} does not.");
                continue;
            }

            // A store or an atomic is named by where it went, so one that went
            // somewhere else is a result the other side has no key for. Comparing
            // only the keys both sides had let a histogram bin computed as zero
            // pass: the original wrote one bin and the decompilation another, and
            // neither key was on both sides. The outputs proper are compared where
            // both have them, as before - a signature can name what a side never
            // writes.
            foreach (string name in expected.Keys.Concat(actual.Keys).Distinct()
                .Where(n => n.StartsWith("STORE") || n.StartsWith("ATOMIC"))
                .Where(n => !expected.ContainsKey(n) || !actual.ContainsKey(n))
                .OrderBy(n => n))
            {
                yield return $"differs on trial {trial}: "
                    + (expected.ContainsKey(name)
                        ? $"{leftName} writes {name}, {rightName} does not."
                        : $"{rightName} writes {name}, {leftName} does not.");
            }

            foreach (string name in expected.Keys.Intersect(actual.Keys).OrderBy(n => n))
            {
                float[] expectedComponents = expected[name];
                float[] actualComponents = actual[name];
                for (int component = 0; component < 4; component++)
                {
                    if (!Same(expectedComponents[component], actualComponents[component]))
                    {
                        yield return $"differs on trial {trial}, "
                            + $"{name}.{"xyzw"[component]}: "
                            + $"{leftName} computes {expectedComponents[component]}, "
                            + $"{rightName} computes {actualComponents[component]}.";
                    }
                }
            }
        }
    }

    // The two instruction sets need two machines. Which one a shader wants is
    // decided by how it was read, not by its profile name.
    private static Dictionary<string, float[]> Run(ShaderModel shader, int trial)
    {
        return shader.Instructions.FirstOrDefault() is D3D10Instruction
            ? D3D10Machine.Run(shader, trial)
            : D3D9Machine.Run(shader, trial);
    }

    private static bool Same(float left, float right)
    {
        if (float.IsNaN(left) && float.IsNaN(right))
        {
            return true;
        }
        if (float.IsInfinity(left) || float.IsInfinity(right))
        {
            return left == right;
        }

        // A value stored as bits rather than as a number - an index, a counter, a
        // packed field - reads back as a denormal, and the tolerance has an absolute
        // floor: every integer below 2^23 is below 2^-126 read as a float, so any
        // two of them compared equal, and equal to zero. That is what let a texel
        // stored through asfloat() - which converts the bits into the number they
        // spell, and so into zero - agree with the integer the original wrote.
        // Where either side is one, the bits have to match.
        uint leftBits = BitConverter.SingleToUInt32Bits(left);
        uint rightBits = BitConverter.SingleToUInt32Bits(right);
        if (IsDenormal(leftBits) || IsDenormal(rightBits))
        {
            return leftBits == rightBits;
        }

        float difference = Math.Abs(left - right);
        float scale = Math.Max(Math.Abs(left), Math.Abs(right));
        return difference <= Tolerance * Math.Max(scale, 1);
    }

    // Zero is a number; anything else with an empty exponent is bits.
    private static bool IsDenormal(uint bits)
    {
        return (bits & 0x7F800000) == 0 && (bits & 0x007FFFFF) != 0;
    }
}
