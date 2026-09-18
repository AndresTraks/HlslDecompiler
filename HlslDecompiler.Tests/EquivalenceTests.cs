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
        // All five are one cause, narrowed to what is left of it. fxc does not read
        // asfloat as a reinterpretation: it reads it as producing a float and
        // flushes a denormal to zero, so the bits of any small integer put through
        // one come back as zero. A register the decompiler keeps as a float, whose
        // bits an integer instruction reads, therefore loses them.
        //
        // A register that holds nothing but bits is declared int now and has no
        // asfloat in it at all. What is left is a register fxc uses for a float in
        // one place and for bits in another - a mantissa here, a coordinate there -
        // which is one variable and cannot be both. Per-component storage, with a
        // statement split where one instruction writes components of each, is what
        // that wants; it is not a per-register decision.
        ["ps_4_0/sample_select"] = [
            ("instruction",
                "The loop's `i & 1` selecting between two offsets is reinterpreted through "
                + "a float, and fxc folds the test of it to false."),
        ],
        ["ps_4_0/precedence_mix"] = [
            ("instruction",
                "The sign bit of a signed modulus is reinterpreted through a float, and "
                + "fxc folds the test of it to false."),
        ],
        ["ps_4_0/int_float_mix"] = [
            ("instruction",
                "A register holds a comparison mask, an integer and a float in turn, and "
                + "the writer's one storage for it reads the integer as the float it was "
                + "converted to."),
        ],
        ["ps_4_0/half_packing"] = [
            ("instruction",
                "fxc's own f32tof16, whose every step holds a mantissa or an exponent "
                + "in a float register: each is a denormal as a float, and fxc folds "
                + "the whole shader to a constant."),
            // The AST writer's half of the same question, and the half it can answer
            // properly: it has values rather than registers, so nothing has to hold
            // two things at once. What it has not got is bits as a property of a
            // value. A value it types an integer and a float reader reads is
            // converted, where a packed half float pair wants reinterpreting; and
            // an integer sum of two such is a float's bits and not a number. Telling
            // the two apart is what an integer register and a bits register already
            // are on the other side.
            ("ast",
                "A packed pair of half floats is returned through a conversion "
                + "rather than a reinterpretation, since a value carries no mark "
                + "saying its integer is a float's bits."),
        ],
        ["ps_4_0/gbuffer_decode"] = [
            ("instruction",
                "A normal packed into the low sixteen bits of a uint texel: the mask is "
                + "a denormal as a float, and fxc folds the asint of it to zero, so the "
                + "normal comes out constant."),
        ],
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
        for (int trial = 0; trial < Trials; trial++)
        {
            Dictionary<string, float[]> expected = Run(original, trial);
            Dictionary<string, float[]> actual = Run(recompiled, trial);

            // A discarded pixel has no outputs to compare, so a decompilation that
            // fails to discard where the original does - or discards where it does
            // not - agreed with it on every output it had. Discarding is an outcome.
            if ((expected.Count == 0) != (actual.Count == 0))
            {
                yield return $"The {writer} writer's output differs on trial {trial}: "
                    + (expected.Count == 0
                        ? "the original discards the pixel, its decompilation does not."
                        : "its decompilation discards the pixel, the original does not.");
                continue;
            }

            foreach (string name in expected.Keys.Intersect(actual.Keys).OrderBy(n => n))
            {
                float[] left = expected[name];
                float[] right = actual[name];
                for (int component = 0; component < 4; component++)
                {
                    if (!Same(left[component], right[component]))
                    {
                        yield return $"The {writer} writer's output differs on trial {trial}, "
                            + $"{name}.{"xyzw"[component]}: "
                            + $"the original computes {left[component]}, its decompilation "
                            + $"computes {right[component]}.";
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

        float difference = Math.Abs(left - right);
        float scale = Math.Max(Math.Abs(left), Math.Abs(right));
        return difference <= Tolerance * Math.Max(scale, 1);
    }
}
