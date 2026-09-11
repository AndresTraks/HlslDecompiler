using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace HlslDecompiler.Tests;

/// <summary>
/// Decompiles, recompiles, and counts what fxc made of it.
///
/// Recompiling proves the output is valid HLSL, not that it means the same thing.
/// A decompiler can turn one instruction into four and still compile - the
/// components of a cmp all read the register as it was before it, and writing them
/// as separate statements makes each later one read what the earlier one wrote.
/// That is invisible to every other test here.
///
/// Comparing the assembly outright does not work: fxc allocates registers and
/// orders commutative operands as it likes, and the decompiled source is higher
/// level than what it was built from, so it often compiles to something different
/// and just as good. What does carry meaning is the instruction count going up.
/// </summary>
[TestFixture]
[Category("Recompile")]
public class RoundTripCostTests
{
    /// <summary>
    /// Shaders that cost more after a round trip than before, and why. The number is
    /// what it costs today: a change either way fails, so an improvement is noticed
    /// rather than quietly absorbed.
    /// </summary>
    /// <summary>
    /// Shaders that cost more after a round trip, with what is known about why. The
    /// number is what it costs today, and failing either way means an improvement is
    /// noticed rather than absorbed.
    ///
    /// Costing more is not the same as being wrong. fxc is compiling source that is
    /// higher level than what it was built from, and it often picks a different
    /// instruction mix - it unrolls a counted loop, or declines a trick the original
    /// used. Each entry below says which of the two it is, because a reason inferred
    /// from the number alone is worse than no reason.
    /// </summary>
    private static readonly Dictionary<string, (int Cost, string Reason)> KnownRegressions = new()
    {
        // The decompiler's doing.
        ["ps_3_0/continue_nested"] = (18,
            "The four components of one cmp all read r1 as it was before it. Written as "
            + "two statements, so the second reads what the first wrote and the "
            + "condition is computed twice. The graph cannot tell a read of the value "
            + "before an instruction from a read of the value after it: temp lowering "
            + "gives both the same variable."),
        ["vs_4_0/skinning"] = (21,
            "The per bone blend does not group. Each component is a weighted sum of two "
            + "dots and the weights are applied before the components could be, so the "
            + "matrix multiply is nested inside something the grouper stops at: "
            + "CanGroupComponents refuses two dot products outright, under a FIXME about "
            + "unrelated matrix rows. Letting two group when their operands group gives "
            + "`dot(dot(transpose(bones[0])[0], i.position), i.blendweight.xy)`, which is "
            + "not an expression at all - the two bone matrices merge as though they "
            + "were rows of one. The guard is doing real work and wants replacing with "
            + "something that knows a matrix from a row, not loosening."),
        ["ps_3_0/temp_assignment"] = (27,
            "The final cmp writes r1 + (1, 0, 3, 4). AddZeroTemplate folds the zero out "
            + "of the y component, so y is t1.y where its siblings are t1.x + 1, and the "
            + "four no longer group: the return comes out as three conditionals over the "
            + "same texture read instead of one. fxc folds the read but has to carry "
            + "t1.zw in a register of its own, which is the `mov r2.xy, r1.zwzw` in each "
            + "branch. Confirmed by disabling AddZeroTemplate, which collapses the return "
            + "to one expression - and breaks twelve other fixtures, so the template is "
            + "earning its place and the fix has to be narrower than removing it."),
        ["ps_3_0/shared_subexpression"] = (20,
            "Naming the shared subexpression is what stops the output exploding, and it "
            + "also stops fxc folding it back. That is the trade, not a defect."),
        ["ps_4_0/int_divide"] = (17,
            "The conversions around udiv are moves rather than casts - the TODO in "
            + "InstructionParser about relying on implicit conversion."),


        ["ps_4_0/reflect_cube"] = (15,
            "Was eighteen, and wrong: (world - eye) / length(...) printed without "
            + "parentheses as world - eye / length(...). Correct now. The two left are "
            + "fxc's: the original multiplies by rsq where the decompiled source says "
            + "divide by length, so it emits sqrt and div."),

        ["vs_3_0/partial_overwrite"] = (15,
            "The original computes a lerp over all four components and then overwrites "
            + "y with the height lookup. An expression has nowhere to put that: x and "
            + "zw carry the lerp, y carries the lerp plus the lookup, and the shared "
            + "part is three separate nodes rather than one, so naming it would not "
            + "help either. Correct, and the four instructions are the cost of saying "
            + "it as one expression."),

        ["ps_4_0/resource_swizzle"] = (31,
            "Correct now that the resource return swizzle is honoured. The eight over "
            + "are a matrix multiply that cannot reform: the original does "
            + "mul(clip, invViewProj) and then divides by w, so the w component is "
            + "wanted on its own and takes a temp of its own, and the other three each "
            + "fold the divide into themselves. The four dot products end up in four "
            + "statements and never meet as components of one thing. Naming the vector "
            + "would fix it, but the vector is not a node - each dot has one consumer, "
            + "so there is no shared subexpression to name."),

        ["cs_4_1/integer_multiply"] = (11,
            "The index is written out twice, once for the bound test and once for the "
            + "store."),

        // fxc's doing: the output is right and it compiles it differently.
        ["vs_3_0/loop_repeat_count"] = (8,
            "fxc unrolls the eight iteration loop the decompiled source spells out."),
        ["vs_3_0/vertex_texture"] = (8,
            "The original adds to one component with a swizzle over a def'd constant, "
            + "`mad r0, r0.x, c4.yxyy, v0`. The decompiled float4 construction says the "
            + "same thing and fxc writes it as an add and a mov."),
        ["ps_4_0/struct_cbuffer"] = (7,
            "Fused differently: two mads in the original, mul and mad and add here."),
        ["ps_4_0/conditional_return"] = (12,
            "The original returns conditionally with retc_nz. HLSL has no spelling for "
            + "that, so `if (c) return x;` compiles to if, ret, endif."),
        ["ps_3_0/dynamic_index"] = (6,
            "The original selects with cmp over def'd constants; the decompiled "
            + "comparison compiles to abs and a compare."),
        ["gs_4_1/circle"] = (20, "One extra move around the stream append."),
        ["vs_3_0/loop_nested_uniform"] = (19, "Not looked into."),
    };

    // Its own names. Taking RecompileTests.Shaders() as it stands reports these as
    // Recompile(...), which is the third test to have done that.
    public static IEnumerable<TestCaseData> Shaders()
    {
        foreach (TestCaseData data in RecompileTests.Shaders())
        {
            yield return new TestCaseData(data.Arguments)
                .SetName($"RoundTripCost({data.Arguments[0]},{data.Arguments[1]})");
        }
    }

    [TestCaseSource(nameof(Shaders))]
    public void RoundTripDoesNotCostMore(string profile, string baseFilename)
    {
        if (RecompileTests.FxcPath == null)
        {
            Assert.Ignore("fxc.exe not found. Install the Windows SDK to run recompilation tests.");
        }

        string compiled = Path.Combine("CompiledShaders", profile, baseFilename + ".fxc");
        string decompiled = Path.Combine("RoundTrip", profile, baseFilename + ".fx");
        string recompiled = Path.ChangeExtension(decompiled, ".fxo");

        ShaderModel shader = RecompileTests.ReadShaderModel(compiled);
        FileUtil.MakeFolder(decompiled);
        new HlslAstWriter(shader).Write(decompiled);
        if (RecompileTests.RunFxc(profile, decompiled, recompiled) != null)
        {
            Assert.Ignore("Covered by the recompile test; it does not compile yet.");
        }

        int before = CountInstructions(compiled);
        int after = CountInstructions(recompiled);

        string key = $"{profile}/{baseFilename}";
        if (KnownRegressions.TryGetValue(key, out var known))
        {
            Assert.That(after, Is.EqualTo(known.Cost),
                $"{key} costs {after} instructions, recorded as {known.Cost}. "
                + $"Update KnownRegressions.{Environment.NewLine}{known.Reason}");
            return;
        }

        Assert.That(after, Is.LessThanOrEqualTo(before),
            $"{key} went from {before} instructions to {after}. Either the output means "
            + "something more expensive than it was given, or it is doing work twice.");
    }

    private static int CountInstructions(string binaryFilename)
    {
        var startInfo = new ProcessStartInfo(RecompileTests.FxcPath)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("/nologo");
        startInfo.ArgumentList.Add("/dumpbin");
        startInfo.ArgumentList.Add(binaryFilename);

        using var process = Process.Start(startInfo);
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return output.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length != 0 && !line.StartsWith("//"))
            .Count(line => !line.StartsWith("dcl") && !line.StartsWith("def")
                && !IsVersion(line));
    }

    private static bool IsVersion(string line)
    {
        return line.StartsWith("vs_") || line.StartsWith("ps_")
            || line.StartsWith("gs_") || line.StartsWith("cs_");
    }
}
