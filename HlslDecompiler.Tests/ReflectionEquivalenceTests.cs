using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HlslDecompiler.Tests;

/// <summary>
/// That a decompilation declares what the shader it came from declares: its
/// constant buffers laid out the same, its resources bound where they were, its
/// signatures, its classes and interfaces, and the declarations that change what it
/// does - interpolation, early depth, thread groups. Compared by recompiling each
/// writer's output and reading both shaders back.
///
/// The other tiers cannot see this. The goldens say the text has not changed, the
/// recompile tier that fxc accepts it, and the equivalence tier that it computes the
/// same - in a machine that reads constants by register and inputs by semantic, so
/// a buffer laid out differently or an interface renamed passes all three and then
/// reads the wrong memory, or binds nothing, on a GPU.
/// </summary>
[TestFixture]
[Category("Recompile")]
// Every case is independent: it reads one .fxc, writes its output under a path
// named after its profile and shader, and shares nothing with the others.
[Parallelizable(ParallelScope.All)]
public class ReflectionEquivalenceTests
{
    // fxc gives a groupshared variable that is not an array raw storage, and HLSL
    // has no way to declare raw groupshared memory: the decompilation declares an
    // array of four-byte elements, which fxc gives structured storage of the same
    // size. The same memory, read and written at the same addresses. Declared
    // before the table that uses it: static fields are initialised in order, and
    // after it this was still null when the table took it.
    private static readonly (string Writer, string Reason)[] RawGroupShared =
    [
        ("ast", "dcl_tgsm_raw comes back as dcl_tgsm_structured of the same size: HLSL cannot declare raw groupshared memory."),
        ("instruction", "dcl_tgsm_raw comes back as dcl_tgsm_structured of the same size: HLSL cannot declare raw groupshared memory."),
    ];

    /// <summary>
    /// Shaders whose decompilation declares something else, by writer, and why. A
    /// listed writer that starts declaring the same fails the test so that its entry
    /// gets removed.
    /// </summary>
    private static readonly Dictionary<string, (string Writer, string Reason)[]> KnownDifferences = new()
    {
        ["cs_5_0/group_shared_counter"] = RawGroupShared,
        ["cs_5_0/stored_loop_counter"] = RawGroupShared,
        ["cs_5_0/tile_depth_bounds"] = RawGroupShared,
        ["ps_3_0/partial_precision"] = [("ast",
            "The input is declared dcl_texcoord_pp where the original's was not. The "
            + "original writes its colour's xy at partial precision and its zw at full; "
            + "the AST writer returns the whole colour as half4, and fxc, finding every "
            + "use of the texture coordinate partial, declares the coordinate partial "
            + "too. What the shader computes is the same; the interpolator is hinted a "
            + "lower precision. The instruction writer keeps the halves apart.")],
        ["hs_5_0/patch_semantic_index"] = [("ast",
            "Two fork phases where the original had six. The patch constants b210 and "
            + "b120 are a weighted sum of two control points each, written as one "
            + "vector expression apiece, and fxc compiles the pair as one phase run "
            + "twice - dcl_hs_fork_phase_instance_count 2, the second point indexed by "
            + "the instance - where the original had a phase per component. The "
            + "equivalence tier shows the same values come out."),
            ("instruction",
            "The same merge of the b210 and b120 phases, from the instruction writer's "
            + "phase-by-phase copy of them: fxc finds the six alike and runs one "
            + "phase per component twice.")],
    };

    public static IEnumerable<TestCaseData> Shaders()
    {
        foreach (TestCaseData data in RecompileTests.Shaders())
        {
            yield return new TestCaseData(data.Arguments)
                .SetName($"ReflectionEquivalent({data.Arguments[0]},{data.Arguments[1]})");
        }
    }

    [TestCaseSource(nameof(Shaders))]
    public void DecompiledOutputDeclaresTheSame(string profile, string baseFilename)
    {
        if (RecompileTests.FxcPath == null)
        {
            Assert.Ignore("fxc.exe not found. Install the Windows SDK to run recompilation tests.");
        }
        // A ps_1_x shader declares nothing - no dcl, no constant table when it was
        // assembled by hand - and recompiles as ps_2_0, which declares everything.
        if (profile.StartsWith("ps_1_"))
        {
            Assert.Ignore("A ps_1_x shader declares nothing to compare against its ps_2_0 recompile.");
        }

        ShaderModel original = RecompileTests.ReadShaderModel(
            Path.Combine("CompiledShaders", profile, baseFilename + ".fxc"));
        List<string> expected = ReflectionSnapshot.Lines(original);

        var differences = new List<(string Writer, string Message)>();
        int compared = 0;
        foreach ((string writer, Func<ShaderModel, HlslWriter> create) in Writers())
        {
            ShaderModel recompiled = Recompile(profile, baseFilename, writer, create, original);
            if (recompiled == null)
            {
                continue;
            }
            compared++;
            List<string> actual = ReflectionSnapshot.Lines(recompiled);
            foreach (string missing in Except(expected, actual))
            {
                differences.Add((writer, $"The {writer} writer's output lacks: {missing}"));
            }
            foreach (string extra in Except(actual, expected))
            {
                differences.Add((writer, $"The {writer} writer's output adds: {extra}"));
            }
        }

        if (compared == 0)
        {
            Assert.Ignore("Neither writer's output could be recompiled.");
        }

        string key = $"{profile}/{baseFilename}";
        List<string> unexpected = [.. differences.Select(d => d.Message)];
        if (KnownDifferences.TryGetValue(key, out (string Writer, string Reason)[] known))
        {
            foreach ((string writer, _) in known)
            {
                Assert.That(differences.Any(d => d.Writer == writer), Is.True,
                    $"The {writer} writer's output for {key} now declares the same. "
                    + $"Remove it from {nameof(KnownDifferences)}.");
            }
            unexpected = [.. differences
                .Where(d => !known.Any(k => k.Writer == d.Writer))
                .Select(d => d.Message)];
        }

        Assert.That(unexpected, Is.Empty, string.Join(Environment.NewLine, unexpected));

        if (known != null)
        {
            Assert.Ignore(string.Join(" ",
                known.Select(k => $"Known difference in the {k.Writer} writer: {k.Reason}")));
        }
    }

    // A multiset difference: a line the original declares twice has to be there twice.
    internal static IEnumerable<string> Except(List<string> lines, List<string> other)
    {
        var remaining = other.GroupBy(l => l).ToDictionary(g => g.Key, g => g.Count());
        foreach (string line in lines)
        {
            if (remaining.TryGetValue(line, out int count) && count > 0)
            {
                remaining[line] = count - 1;
                continue;
            }
            yield return line;
        }
    }

    private static IEnumerable<(string Name, Func<ShaderModel, HlslWriter> Create)> Writers()
    {
        yield return ("ast", s => new HlslAstWriter(s));
        yield return ("instruction", s => new HlslSimpleWriter(s));
    }

    /// <returns>The recompiled shader, or null if this writer's output is not usable.</returns>
    private static ShaderModel Recompile(string profile, string baseFilename, string writer,
        Func<ShaderModel, HlslWriter> create, ShaderModel original)
    {
        string hlslFilename = Path.Combine("ReflectionEquivalence", profile + "_" + writer, baseFilename + ".fx");
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
}
