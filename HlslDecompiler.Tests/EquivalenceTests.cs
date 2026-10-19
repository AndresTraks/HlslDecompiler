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
        ["cs_4_0/particle_update"] = [("ast",
            "Two dwords the original stores and the recompilation does not. The "
            + "particle's velocity is written back a member at a time, and its x and "
            + "z are the x and z that were loaded from the same element a few lines "
            + "up, so fxc drops those two stores as writes of what is there. The "
            + "buffer ends up the same; the store is what is compared, since a "
            + "buffer is not modelled.")],
        ["cs_5_0/select_float_bits"] = [("instruction",
            "The select. A movc picks between a float's bits and an integer, and the "
            + "instruction writer keeps the integer in a register declared float as "
            + "the number it is, by convention; the store into the uint4 buffer then "
            + "converts the whole register, which is right for the integer and the "
            + "float's value rather than its bits for the other. The AST writer, "
            + "which selects asint(t0) beside the uint, computes the same.")],
        ["cs_5_0/select_raw_store"] = [("instruction",
            "The select of select_float_bits, stored with Store4: the float register "
            + "holds the integer branch as its number, and the store takes its bits. "
            + "The AST writer, which selects asfloat(int4(...)) beside the float, "
            + "computes the same.")],
        ["ps_5_0/coverage_select_bits"] = [("instruction",
            "The stencil reference. The and of a float's bits leaves an integer in a "
            + "register declared float, where the instruction writer keeps it as the "
            + "number it is, by convention, so the output converts it. The AST writer, "
            + "which ands asint(c.x), computes the same.")],
        ["cs_5_0/double_bits_select"] = [("instruction",
            "The double. The structured load's two words go into a register "
            + "declared float, so the instruction writer converts them as numbers, "
            + "by convention, and asdouble joins the converted values rather than "
            + "the bits. The AST writer, which joins the loaded uints, computes the "
            + "same.")],
        ["ds_5_0/mixed_bits_patch"] = [("instruction",
            "The position. The instruction writer reads the uint patch constant in "
            + "a mad as its number, where fxc wrote no utof because its bits were "
            + "meant. The AST writer, which reads asfloat(constants.bits), computes "
            + "the same.")],
        ["gs_5_0/indexed_bits_read"] = [("instruction",
            "The same movc the other way round: the instruction writer selects the "
            + "uint id beside a float, so it is converted to a float where the movc "
            + "copies its bits. The AST writer, which selects asfloat(id), computes "
            + "the same.")],
        ["hs_5_0/mixed_bits_patch"] = [("instruction",
            "A movc between a float input and an integer: the instruction writer "
            + "converts the float operand where the movc copies its bits. The AST "
            + "writer, which reinterprets it, computes the same.")],
        ["ps_5_0/integer_add_bits"] = [("instruction",
            "An iadd of a float input and a uint: the instruction writer adds the "
            + "float's value, not its bits, and the bfi result goes into the depth "
            + "by conversion. The AST writer, which adds asint(texcoord), computes "
            + "the same.")],
    };

    private const int Trials = 8;

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
    /// One sentence per difference. The sides are named because the cost sweep
    /// also uses this, to compare a baseline against a variant.
    /// </summary>
    internal static IEnumerable<string> CompareRuns(
        ShaderModel left, ShaderModel right, string leftName, string rightName)
    {
        // Typed by the left side, the one being matched.
        var kinds = new StoredValueKinds(left);
        for (int trial = 0; trial < Trials; trial++)
        {
            Dictionary<string, float[]> expected = Run(left, trial);
            Dictionary<string, float[]> actual = Run(right, trial);

            // Discarding is an outcome: a discarded pixel has no outputs to compare.
            if ((expected.Count == 0) != (actual.Count == 0))
            {
                yield return $"differs on trial {trial}: "
                    + (expected.Count == 0
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
