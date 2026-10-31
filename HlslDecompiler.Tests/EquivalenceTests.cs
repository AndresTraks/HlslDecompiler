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
/// Runs the original shader and each writer's recompiled output and compares what
/// they compute; goldens and recompiling alone do not catch wrong arithmetic.
/// </summary>
[TestFixture]
[Category("Recompile")]
// Each case writes to its own path and shares nothing.
[Parallelizable(ParallelScope.All)]
public class EquivalenceTests
{
    // A listed writer that starts agreeing fails, so its entry gets removed.
    private static readonly Dictionary<string, (string Writer, string Reason)[]> KnownDifferences = new()
    {
        // Empty since 2026-10-20: both writers compute the same on every fixture
        // the machine runs.
    };

    // 32, not 8: fxc's compiles dominate the tier's time, and 8 trials missed
    // ps_1_3/signed_blend's discard on trials 10 and 24.
    private const int Trials = 32;

    // Relative to the larger; fxc reassociates (mul+add <-> mad).
    private const float Tolerance = 1e-3f;

    // The same set the recompile tier runs, not a fixed list of profiles.
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
        if (!ShaderCompiler.IsAvailable)
        {
            Assert.Ignore(ShaderCompiler.UnavailableMessage);
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

        // Ignored, not passed: an unrun check must not read as coverage.
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
            // With what differs now, so a reason gone stale shows beside it.
            Assert.Ignore(string.Join(" ",
                known.Select(k => $"Known difference in the {k.Writer} writer: {k.Reason} "
                    + $"Now: {differences.First(d => d.Writer == k.Writer).Message}")));
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
    /// One sentence per difference. The sides are named because the cost sweep
    /// also uses this, to compare a baseline against a variant.
    /// </summary>
    internal static IEnumerable<string> CompareRuns(
        ShaderModel left, ShaderModel right, string leftName, string rightName)
    {
        // Typed by the left side, the one being matched.
        var kinds = new StoredValueKinds(left);
        int ran = 0;
        for (int trial = 0; trial < Trials; trial++)
        {
            // A trial the left side does not finish (a loop stepping by a constant
            // the trial made 0, an interface index past its array) says nothing of
            // the right; the others still count.
            Dictionary<string, float[]> expected = TryRun(left, trial);
            if (expected == null)
            {
                continue;
            }
            ran++;
            Dictionary<string, float[]> actual = TryRun(right, trial);
            if (actual == null)
            {
                yield return $"differs on trial {trial}: {rightName} does not finish, {leftName} does.";
                continue;
            }

            // Discarding is an outcome: a discarded pixel has no outputs to compare,
            // only the UAV writes it made before the discard (D3D10's mark them).
            bool expectedDiscards = IsDiscarded(expected);
            bool actualDiscards = IsDiscarded(actual);
            if (expectedDiscards != actualDiscards)
            {
                yield return $"differs on trial {trial}: "
                    + (expectedDiscards
                        ? $"{leftName} discards the pixel, {rightName} does not."
                        : $"{rightName} discards the pixel, {leftName} does not.");
                continue;
            }

            // Stores are keyed by address, so a key on one side only is a store to
            // the wrong place. Outputs are compared only where both have them: a
            // signature can name what a side never writes.
            foreach (string name in expected.Keys.Concat(actual.Keys).Distinct()
                .Where(n => n.StartsWith("STORE") || n.StartsWith("ATOMIC"))
                .Where(n => !expected.ContainsKey(n) || !actual.ContainsKey(n))
                // A store of what the address already holds leaves the buffer as
                // the side without it has it: fxc drops such a store as dead.
                .Where(n => !IsStoreOfWhatIsThere(left, n, (expected.ContainsKey(n) ? expected : actual)[n], trial,
                    kinds.Of(n, 0)))
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
                    if (!Same(expectedComponents[component], actualComponents[component],
                        kinds.Of(name, component)))
                    {
                        yield return $"differs on trial {trial}, "
                            + $"{name}.{"xyzw"[component]}: "
                            + $"{leftName} computes {expectedComponents[component]}, "
                            + $"{rightName} computes {actualComponents[component]}.";
                    }
                }
            }
        }
        if (ran == 0)
        {
            throw new D3D10Machine.NonTerminationException();
        }
    }

    // Null where the trial leaves the run undefined (past the step limit, an index
    // past its array); other failures propagate.
    private static Dictionary<string, float[]> TryRun(ShaderModel shader, int trial)
    {
        try
        {
            return Run(shader, trial);
        }
        catch (D3D10Machine.UndefinedTrialException)
        {
            return null;
        }
    }

    private static bool IsDiscarded(Dictionary<string, float[]> results)
    {
        return results.Count == 0 || results.ContainsKey(D3D10Machine.DiscardedKey);
    }

    // Bitwise: a store of the loaded value writes back exactly its bits.
    // Typed by the left side's reflection, as its loads are. A float's -0 and +0
    // are the same value: `v + 0` turns a loaded -0 into +0, and fxc drops it.
    private static bool IsStoreOfWhatIsThere(
        ShaderModel shader, string name, float[] stored, int trial, StoredValueKind kind)
    {
        if (D3D10Machine.InitialValueAt(shader, name, trial) is not uint initial)
        {
            return false;
        }
        uint bits = BitConverter.SingleToUInt32Bits(stored[0]);
        return initial == bits
            || kind != StoredValueKind.Integer && ((initial | bits) & 0x7FFFFFFF) == 0;
    }

    // Chosen by how the shader was read, not by profile name.
    private static Dictionary<string, float[]> Run(ShaderModel shader, int trial)
    {
        return shader.Instructions.FirstOrDefault() is D3D10Instruction
            ? D3D10Machine.Run(shader, trial)
            : D3D9Machine.Run(shader, trial);
    }

    private static bool Same(float left, float right, StoredValueKind kind)
    {
        // An integer is its bits: a packed uint above 2^23 reads as an ordinary
        // float, which the tolerance would equate with one missing its low bits.
        if (kind == StoredValueKind.Integer)
        {
            return BitConverter.SingleToUInt32Bits(left) == BitConverter.SingleToUInt32Bits(right);
        }
        if (float.IsNaN(left) && float.IsNaN(right))
        {
            return true;
        }
        if (float.IsInfinity(left) || float.IsInfinity(right))
        {
            return left == right;
        }

        // An integer stored as bits reads as a denormal, which the tolerance's
        // absolute floor would equate with zero; compare bits instead.
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
