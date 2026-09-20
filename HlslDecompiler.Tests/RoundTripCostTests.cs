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
        ["ps_4_0/bit_field"] = (28,
            "One instruction. The exponent is masked out of the bits and shifted back "
            + "in, and the decompiled source says that as two statements over a named "
            + "temp where the original kept it in one register, so fxc folds one fewer "
            + "shift. The AST writer's reading of this shader is recorded in "
            + "EquivalenceTests.KnownDifferences as well."),
        ["ps_3_0/loop_counter_reuse"] = (53,
            "A loop over smoothstep, sign, fmod and clamp, and fxc unrolls it three "
            + "times over: the decompiled source marks a loop [loop] only where fxc "
            + "would otherwise refuse it, and this one it can count. Marked, it "
            + "recompiles to the original's 26. fmod compares against zero rather "
            + "than against its own negation, so its template does not recognise it "
            + "and it is written out longhand, which fxc expands again inside each "
            + "unrolled copy. It computes the right answer now, which it did not "
            + "when this entry was written."),
        ["ps_4_0/gbuffer_decode"] = (36,
            "Four instructions, three of them downstream of one split. The two halves of a "
            + "normal packed into a uint are decoded differently - one masks the low "
            + "sixteen bits, the other shifts down the high ones - so the mad that "
            + "scales both is written as two scalar statements rather than one two "
            + "wide. Everything after them stays scalar: the comparison and the "
            + "select that fix up the octahedral fold were one pair of two wide "
            + "instructions and are now two pairs, and the add after them two "
            + "rather than one. Correct throughout - it is the vector shape that is "
            + "lost, not the arithmetic. Was 39 while the dot product over the "
            + "result was a mul and two mads; it is the dp3_sat the original had, "
            + "now that a dot takes a side whose components are each their own "
            + "expression. Was 35 while `t8 / length(t8)` was a division by a named "
            + "length; it is `normalize(t8)` now, which fxc takes the length for "
            + "again rather than sharing the one the saturate above it already "
            + "computes. One instruction for the word, paid knowingly - five "
            + "shaders read `normalize(v)` where they read a division by a length "
            + "on a line of its own. The normal is the second of them since a dot "
            + "of a vector with itself stopped needing its components to group: "
            + "`sqrt(t5 * t5 + t6 * t6 + t3 * t3)` named on a line of its own, with "
            + "`float3(t5, t6, t3) / t7` after it, is `normalize(float3(t5, t6, "
            + "t3))` now. No change in the count, one temp fewer and a line that "
            + "says what it does."),
        ["cs_4_0/particle_update"] = (13,
            "Two instructions, and the price of naming the members. The original "
            + "loads the whole particle in two sixteen byte loads, writes it back in "
            + "two stores, and reads each member out of the registers in between. "
            + "Written a member at a time - which is what the source said, and what "
            + "makes it readable - fxc reloads the two members that are read after a "
            + "store to a different member of the same element. Correct, and the "
            + "alternative is a struct shaped temporary the writer has no way to "
            + "know was there. Measured, that temporary costs 10, which is one "
            + "under what the original costs - so it is not only available to a "
            + "writer that could see it, it is the better shape. The element is "
            + "read in five statements and the hoist is per statement. Naming the "
            + "four members separately instead, which wants no struct typed "
            + "variable and no new kind of declaration, costs 12: it takes one of "
            + "the two instructions and leaves the other, because fxc loads four "
            + "members where the original loaded the element twice. The cheap "
            + "version is not enough, which is the thing to know before trying "
            + "it."),
        ["vs_4_0/skin_buffer"] = (37,
            "One instruction, and the shape of the source rather than its "
            + "arithmetic. `i.indices[b]` over a loop counter compiles to a chain of "
            + "comparisons selecting one of four components, and the decompiled "
            + "source says that chain rather than the subscript it came from - fxc "
            + "has no subscript to put back and compiles the chain it is given. Was "
            + "39 while the loop's exit test was an if around a break."),
        ["ps_3_0/continue_nested"] = (16,
            "The four components of one cmp all read r1 as it was before it, and they "
            + "are written as two statements. Naming the condition first keeps it the "
            + "value the instruction saw - it used to be recomputed in the second "
            + "statement, from a r1.y the first had already overwritten, which was "
            + "wrong as well as an instruction dearer. What is left is the naming "
            + "itself: fxc has no reason to keep a variable the shader never asked "
            + "for, and the two statements do not fold back into one cmp. Was 17 "
            + "while the if side was empty and the body sat in the else."),
        ["vs_2_0/matrix_palette"] = (28,
            "Two instructions, the blend index: the input has to be declared float, "
            + "the bytecode not saying otherwise, and fxc floors a float subscript "
            + "with a frc and an add where the original rounds it into the address "
            + "register. The blend itself is `mul(p, bones[i.x]) * w.x + mul(p, "
            + "bones[i.y]) * w.y` now, the source; was 38 while a row read through "
            + "the address register was not a row."),


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
            "fxc unrolls the eight iteration loop the decompiled source spells out, "
            + "which is unmarked because fxc can count it; marked [loop] it "
            + "recompiles to the original's 5."),
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
        ["gs_4_1/circle"] = (20,
            "One instruction, and it is the position's components not grouping. "
            + "The original writes `mad r2.xyzw, r0.xyzw, l(0.5, 0.5, 0, 0), "
            + "v[0][0].xyzw` and one `mov o0.xyzw` after it: the multiplier's zw "
            + "are zero, so z and w pass the input through and all four components "
            + "are one instruction. In the decompiled source x and y are mads and "
            + "zw is a read, which do not group, so fxc writes two scalar mads and "
            + "three movs. Writing it by hand as one four wide mad does not recover "
            + "it either - fxc splits `* float4(0.5, 0.5, 0, 0)` into a mul and an "
            + "add, the original's shape depending on a 0.5 it had hoisted into a "
            + "register outside the loop, which source cannot ask for. A folded zero "
            + "is behind it, as it was behind ps_3_0/temp_assignment - but that one "
            + "was an addend, which the grouper puts back, and this is a multiplier "
            + "in a mad, which it does not."),
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
