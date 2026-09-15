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
        ["ps_3_0/loop_counter_reuse"] = (53,
            "A loop over smoothstep, sign, fmod and clamp. smoothstep and sign "
            + "reduce on ps_3_0 now, which moved nothing: fxc expands them to what "
            + "was there. fmod compares against zero rather than against its own "
            + "negation, so its template does not recognise it and it is written "
            + "out longhand for fxc to expand again. It computes the right answer "
            + "now, which it did not when this entry was written."),
        ["vs_3_0/bone_array"] = (13,
            "The original loads two indices in one mova and the third in another, "
            + "and the decompiled source has three separate subscripts. fxc gives "
            + "each its own mova rather than packing two into one, which is two "
            + "instructions. The lookups themselves are right, which they were not "
            + "before - all three used to read the same element."),
        ["ps_4_0/comparison_mask"] = (8,
            "The masks anded onto the comparisons are 0x3f800000 and 0x41000000, the "
            + "bits of 1.0f and 8.0f, and they print as 1 and 8 because a whole "
            + "number has no decimal point. HLSL reads those as integers, so fxc "
            + "compiles the whole expression in integers - ishl, and, iadd - and "
            + "converts once at the end where the original never left float. The "
            + "result is the same; giving every whole float a decimal point would "
            + "churn every fixture for this one."),
        ["ps_3_0/continue_nested"] = (17,
            "The four components of one cmp all read r1 as it was before it, and they "
            + "are written as two statements. Naming the condition first keeps it the "
            + "value the instruction saw - it used to be recomputed in the second "
            + "statement, from a r1.y the first had already overwritten, which was "
            + "wrong as well as an instruction dearer. What is left is the naming "
            + "itself: fxc has no reason to keep a variable the shader never asked "
            + "for, and the two statements do not fold back into one cmp."),
        ["ps_4_0/derivatives"] = (8,
            "Two instructions. The mad is one four wide mad again, as in the "
            + "original, now that the constructor sits around its addend rather "
            + "than around two halves of it. What remains is ddx(texcoord.x) being "
            + "read twice - inside fwidth and as .x of the addend - without being "
            + "named: the text repeats nothing, since the addend writes it as .x of "
            + "ddx(texcoord), and the hoist names what the text repeats. fxc takes "
            + "the derivatives once more and packs them with two movs. Was 7 as two "
            + "half-mads, which was cheaper by one and the wrong shape."),
        ["vs_2_0/matrix_palette"] = (28,
            "Two instructions, the blend index: the input has to be declared float, "
            + "the bytecode not saying otherwise, and fxc floors a float subscript "
            + "with a frc and an add where the original rounds it into the address "
            + "register. The blend itself is `mul(p, bones[i.x]) * w.x + mul(p, "
            + "bones[i.y]) * w.y` now, the source; was 38 while a row read through "
            + "the address register was not a row."),
        ["vs_4_0/skinning"] = (15,
            "One instruction: `mul(mul(p, bones[0]) * w.x + mul(p, bones[1]) * w.y, "
            + "viewProj)`, which is the source, and fxc orders the two blends the "
            + "other way about from the original and folds one fewer mad. Was 21 "
            + "while two dot products could not be components of anything - they "
            + "are, when they are rows of one matrix against one vector."),
        ["ps_3_0/temp_assignment"] = (27,
            "The final cmp writes r1 + (1, 0, 3, 4). AddZeroTemplate folds the zero out "
            + "of the y component, so y is t1.y where its siblings are t1.x + 1, and the "
            + "four no longer group: the return comes out as three conditionals over the "
            + "same texture read instead of one. fxc folds the read but has to carry "
            + "t1.zw in a register of its own, which is the `mov r2.xy, r1.zwzw` in each "
            + "branch. Confirmed by disabling AddZeroTemplate, which collapses the return "
            + "to one expression - and breaks twelve other fixtures, so the template is "
            + "earning its place and the fix has to be narrower than removing it."),



        ["vs_3_0/partial_overwrite"] = (13,
            "The original computes a lerp over all four components and then overwrites "
            + "y with the height lookup. An expression has nowhere to put that: x and "
            + "zw carry the lerp, y carries the lerp plus the lookup, and the shared "
            + "part is three separate nodes rather than one, so naming it would not "
            + "help either. Correct, and the two instructions are the cost of saying "
            + "it as one expression. Was 15 while the lookup was added in front of "
            + "the lerp rather than onto it."),



        // fxc's doing: the output is right and it compiles it differently.
        ["vs_3_0/loop_repeat_count"] = (8,
            "fxc unrolls the eight iteration loop the decompiled source spells out."),
        ["vs_3_0/vertex_texture"] = (8,
            "The original adds to one component with a swizzle over a def'd constant, "
            + "`mad r0, r0.x, c4.yxyy, v0`. The decompiled float4 construction says the "
            + "same thing and fxc writes it as an add and a mov."),
        ["ps_4_0/conditional_return"] = (12,
            "The original returns conditionally with retc_nz. HLSL has no spelling for "
            + "that, so `if (c) return x;` compiles to if, ret, endif."),
        ["ps_3_0/dynamic_index"] = (6,
            "The original selects with cmp over def'd constants; the decompiled "
            + "comparison compiles to abs and a compare."),
        ["gs_4_1/circle"] = (20, "One extra move around the stream append."),
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
        var startInfo = RecompileTests.CreateFxcProcessStartInfo();
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
