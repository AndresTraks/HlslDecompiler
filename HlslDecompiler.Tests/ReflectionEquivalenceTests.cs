using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HlslDecompiler.Tests;

/// <summary>
/// The recompiled decompilation declares what the original does: buffer layout,
/// bindings, signatures, interfaces, and declarations like interpolation and thread
/// groups. The equivalence machine names inputs, so it cannot see these.
/// </summary>
[TestFixture]
[Category("Recompile")]
// Each case writes to its own path and shares nothing.
[Parallelizable(ParallelScope.All)]
public class ReflectionEquivalenceTests
{
    // A listed writer that starts declaring the same fails, so its entry gets removed.
    private static readonly Dictionary<string, (string Writer, string Reason)[]> KnownDifferences = new()
    {
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
        ["cs_5_0/group_shared_tile"] = [("ast",
            "dcl_tgsm_structured where the original declared dcl_tgsm_raw. Raw "
            + "groupshared wider than one dword is the one declaration HLSL has no "
            + "spelling for - raw is a variable without a subscript, and a subscript "
            + "makes it structured - so the decompilation keeps the same bytes as an "
            + "array of dwords and fxc declares the array. The words and what is "
            + "stored in them are the same; only the declaration differs."),
            ("instruction",
            "The same array-for-raw spelling of the two variables, from the "
            + "instruction writer: one store per dword of the raw one rather than "
            + "the one store of several the original makes.")],
        ["cs_5_0/group_shared_addzero"] = [("ast",
            "The same dcl_tgsm_structured for dcl_tgsm_raw as cs_5_0/group_shared_tile: "
            + "a float4 and a uint2 of raw groupshared, spelled as arrays of dword "
            + "elements because HLSL has no raw array. The bytes and what is stored "
            + "in them are the same; only the declaration differs."),
            ("instruction",
            "The same array-for-raw spelling of the two variables, from the "
            + "instruction writer: one store and one load per dword where the "
            + "original moves the whole registers at once.")],
        ["cs_5_0/group_shared_round2"] = [("ast",
            "dcl_tgsm_structured 4, 4 where the original declared dcl_tgsm_raw 16 - "
            + "the tile's missing raw spelling again, as in cs_5_0/group_shared_tile. "
            + "The bounds were declared an array and keep their declaration; the one "
            + "variable HLSL cannot spell raw is the float4. The bytes and their "
            + "values are the same; only the declaration differs."),
            ("instruction",
            "The tile's same array-for-raw spelling, from the instruction writer: "
            + "one store and one load per dword of the float4 where the original "
            + "moves it whole.")],
        ["cs_5_0/group_shared_float4_bits"] = [("ast",
            "dcl_tgsm_structured 4, 4 where the original declared dcl_tgsm_raw 16 - "
            + "a groupshared float4, the raw spelling HLSL does not have, as in "
            + "cs_5_0/group_shared_tile. The four dwords and what is stored in them "
            + "are the same; only the declaration differs."),
            ("instruction",
            "The same array-for-raw spelling of the float4, from the instruction "
            + "writer: one store and one load per dword where the original moves "
            + "the whole register at once.")],
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
        if (!ShaderCompiler.IsAvailable)
        {
            Assert.Ignore(ShaderCompiler.UnavailableMessage);
        }
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

    // Multiset difference.
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
