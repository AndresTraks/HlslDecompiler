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
    /// Shaders whose decompilation computes something else, and why. A shader that
    /// starts agreeing fails the test so that it gets removed from here.
    /// </summary>
    private static readonly Dictionary<string, string> KnownDifferences = new()
    {
        ["ps_4_0/step_mask"] =
            "The instruction writer declares one variable per register for the whole "
            + "shader, and fxc reuses a register: here one carries a comparison mask "
            + "at one point and a max at another. IntegerOperandAnalysis marks it "
            + "integer, so the max is truncated. Excluding registers a float "
            + "instruction writes does not work either - the same register genuinely "
            + "holds an integer earlier - and neither does reinterpreting every "
            + "integer operation with asint, which breaks the shaders where the "
            + "integer declaration is right. It wants the register split by live "
            + "range, so that the two uses become two variables.",
        // The same register reuse as step_mask, in two more shaders.
        ["ps_4_0/sign_intrinsic"] =
            "The register carrying the sign masks also carries a float later, and is "
            + "declared int4. See ps_4_0/step_mask. Hidden until the machine learned "
            + "imul: a writer whose output the machine cannot run is not compared at "
            + "all, so implementing an opcode can uncover a difference rather than "
            + "cause one.",
        ["gs_4_1/circle"] =
            "`int2 r1` holds a loop counter and then an angle in radians, so "
            + "`r1.y = r1.y * 0.392699093` truncates. See ps_4_0/step_mask.",
        ["ps_3_0/loop_counter_reuse"] =
            "`t1 = t1 + 1` is the last instruction of the loop body and is emitted "
            + "first, so every read in the body sees a counter one too high. The "
            + "assignment order puts an assignment before anything reading its "
            + "variable, which is right for a chain of instructions - component_chain "
            + "depends on it - and exactly wrong for a value read before it is "
            + "overwritten. Three attempts and what each found: reversing the rule "
            + "for reassignments breaks component_chain; recording the previous value "
            + "on the assignment does nothing, because lowering rewires the readers "
            + "of a loop header phi onto the variable and the phi is then an input to "
            + "nothing; recording the readers the variable already has finds none, "
            + "because the increment is given a variable of its own and unified with "
            + "the loop variable only afterwards. The body is one statement with the "
            + "expression inlined, so instruction order is not in the graph either. "
            + "Recording them where the two variables are unified - in "
            + "ReplaceTempVariable, the last point at which they are distinct - does "
            + "capture the right nodes, ten of them, and still does not match: "
            + "reducing rebuilds nodes rather than editing them, so a reference taken "
            + "before it names something no longer in the graph. TemplateMatcher "
            + "memoises what each node reduced to, which is where those references "
            + "could be followed, and that memo is per call and discarded. Note also "
            + "that the constraint has to decide the pair rather than join the other "
            + "rules: a reader of the old value also reads the variable the "
            + "assignment writes, so both directions hold at once and the sort falls "
            + "back to arrival order.",
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
    /// Geometry and compute shaders have no return value, so the machine reports
    /// what they put on their stream and what they wrote to their buffers instead.
    /// </summary>
    private static readonly string[] Profiles =
    [
        "ps_2_0", "ps_3_0", "vs_1_1", "vs_3_0",
        "ps_4_0", "ps_4_1", "vs_4_0", "gs_4_0", "gs_4_1", "cs_4_1",
    ];

    public static IEnumerable<TestCaseData> Shaders()
    {
        const string root = "CompiledShaders";
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (string profile in Profiles)
        {
            string directory = Path.Combine(root, profile);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (string shader in Directory.EnumerateFiles(directory, "*.fxc").OrderBy(f => f))
            {
                yield return new TestCaseData(profile, Path.GetFileNameWithoutExtension(shader))
                    .SetName($"Equivalent({profile},{Path.GetFileNameWithoutExtension(shader)})");
            }
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

        var differences = new List<string>();
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
                differences.AddRange(Compare(writer, original, recompiled).ToList());
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
        if (KnownDifferences.TryGetValue(key, out string reason))
        {
            Assert.That(differences, Is.Not.Empty,
                $"{key} now computes the same. Remove it from {nameof(KnownDifferences)}.");
            Assert.Ignore($"Known difference: {reason}");
        }

        Assert.That(differences, Is.Empty,
            string.Join(Environment.NewLine, differences));
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
