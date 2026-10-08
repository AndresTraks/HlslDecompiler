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
// Every case is independent: it reads one .fxc, writes its output under a path
// named after its profile and shader, and shares nothing with the others.
[Parallelizable(ParallelScope.All)]
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
    internal static readonly Dictionary<string, (int Cost, string Reason)> KnownRegressions = new()
    {
        ["hs_5_0/quad_tess_factors"] = (34,
            "Three instructions, and all of them one value written twice: "
            + "`0.25 * (p0 + p1 + p2 + p3)`, the centre of the patch, which the "
            + "shader writes to an output of its own and also measures the distance "
            + "from the eye with.\n\n"
            + "Nothing names it. RootOfSeveralRegisters names a value several "
            + "registers are written from, which is what took the two clamps in this "
            + "same function out of four writes and two; the centre is written to one "
            + "register and read once inside another expression, which is not that "
            + "shape. SplitRead is the pass for a value several expressions read and "
            + "drops a root outright, on the reading that a root is written where it "
            + "stands - true of a root written once and not of this one.\n\n"
            + "What makes it awkward rather than a missing case is the gate. Every "
            + "name here is decided on how much text writing it again costs, and "
            + "`0.25 * t0` is nine characters: no budget that catches it leaves the "
            + "short values alone. The three instructions are not in the text at all "
            + "- one is the second multiply and the other two are what writing it "
            + "twice costs the scheduling around the length - so the question is what "
            + "a root written more than once is worth, and the corpus says nothing "
            + "either way: it has no other shader of this shape. Measured by hand: "
            + "naming the centre and nothing else brings this back to the 31 it was "
            + "read from."),
        // The decompiler's doing.
        ["ps_5_0/split_transform"] = (23,
            "Two instructions, and a matrix multiply the naming took apart. A decal "
            + "reads the local position twice - once as the coordinate it samples "
            + "with, once by the box test - so the writer names the pair of "
            + "components both uses share, and the multiplication grouper then finds "
            + "two rows of the transform where there were three. What comes out is "
            + "`mul(world, (float4x2)decalMatrix)` beside a dot for the row left "
            + "over, and the three steps over the result cannot vectorise either: "
            + "the original compares and masks three components at once, the round "
            + "trip does one and then two.\n\n"
            + "The hoist already knows not to name inside a grouper's match - it "
            + "measures a statement by compiling it and keeps what the groupers "
            + "claimed - and this gets past that because the two reads are in "
            + "different statements, so the name is decided before the statement "
            + "holding the multiply is measured. A volumetric fog raymarch loses two "
            + "instructions the same way, which is what says this is the class and "
            + "not the shader.\n\n"
            + "Tried, and it is not the grouped set. The two naming passes were not "
            + "told the same things - NameRepeatedText measured with Recording, "
            + "Grouped and GroupMatches and dropped any candidate the groupers "
            + "claimed, while the measurement HoistSharedSubexpressions takes before "
            + "NameCandidates picks had Recording alone - and three ways of closing "
            + "that were measured over the corpus:\n\n"
            + "Giving it the grouped set and skipping what is in it moved eleven "
            + "fixtures and most of them for the worse: gbuffer_write wrote "
            + "`normalize(t8)` twice and normal_transform `mul(i.position, world)` "
            + "twice, because a value a grouper matched whole is exactly the value "
            + "worth naming. Allowing a whole match through on GroupMatches brought "
            + "it to one fixture, still worse. Rejecting only a candidate group that "
            + "is a proper subset of a match - the shape that splits one - moved "
            + "three: this shader's invViewProjection multiply came back whole and "
            + "its cost stayed at 23 all the same, packed_cbuffer went from 33 "
            + "instructions to 36, and instance_clip grew a variable.\n\n"
            + "Which locates it. The multiply that costs the two instructions is the "
            + "decalMatrix one, and no rule read off the measurement catches it, "
            + "because the match the measurement recorded is the match before the "
            + "naming: afterwards the grouper finds two components where it had "
            + "found three. A measurement taken before a change does not predict the "
            + "result of the change, and the only thing that would is applying it "
            + "and measuring again - which the free probe NodeCompiler.Measure now "
            + "allows for text, and which says nothing about instructions, since "
            + "those are fxc's answer and not the writer's."),
        ["ps_5_0/transposed_basis"] = (46,
            "Eight instructions, and the same transpose splat_layers was cured of, "
            + "reached over values the shader computed instead of values it read. "
            + "Three basis vectors weighted by one kernel entry's components - three "
            + "vector mads in the original - come back as a dot per component, so "
            + "fxc transposes tangent, bitangent and normal once outside the loop and "
            + "does three dp3 inside it where three mads would have done.\n\n"
            + "The guard in DotProduct2Template and DotProduct3Template cannot see "
            + "this one. It recognises a transpose as one component index taken from "
            + "each of several values, and a component index is something a read "
            + "carries: here the three values are a normalize, a cross product and a "
            + "mad, which carry none. What makes them a transpose is that the three "
            + "sibling dots take the first component of each, then the second, then "
            + "the third - an observation across the output's components, where a "
            + "template only ever sees one of them. It belongs wherever the "
            + "components are grouped, not in the templates.\n\n"
            + "Tried as a rewrite where the components are compiled, which is the "
            + "one place that does see them together, and the arithmetic works: this "
            + "shader came back to thirty-eight and a PBR normal map went from "
            + "sixty-four out and seventy-two back to sixty-three. What sank it was "
            + "that deciding by compiling has side effects - compiling numbers "
            + "variables and records what it wrote - so asking the question of "
            + "shaders it then declined moved nine fixtures and took three "
            + "normalizes apart into a length and a divide, for nothing. Recording "
            + "the match with MarkGrouped did not prevent it. Doing every check on "
            + "the nodes first and compiling only once certain cut it to three "
            + "fixtures, and point_lights still lost a normalize - and the narrowing "
            + "needed to get there, that the basis already be in variables, gives up "
            + "the normal map, which is the common case. So the rewrite has to come "
            + "with the grouping rather than with the writing, and a candidate that "
            + "has to compile to decide is the wrong shape for this writer."),
        ["cs_4_0/particle_update"] = (12,
            "One instruction, and the price of naming the members. The original "
            + "loads the whole particle in two sixteen byte loads, writes it back in "
            + "two stores, and reads each member out of the registers in between. "
            + "Written a member at a time - which is what the source said, and what "
            + "makes it readable - fxc reloads the member that is read after a store "
            + "to a different member of the same element. Was 13 while each load was "
            + "written out at every use: the components one instruction wrote are "
            + "named together now, so the sixteen bytes are read once and the "
            + "members are swizzles of it, which is the 12 this entry predicted for "
            + "naming them. The struct shaped temporary that would cost 10 - one "
            + "under the original - wants a struct typed variable and a declaration "
            + "of a kind the writer has not got."),
        ["ps_3_0/continue_nested"] = (16,
            "The four components of one cmp all read r1 as it was before it, and they "
            + "are written as two statements. Naming the condition first keeps it the "
            + "value the instruction saw - it used to be recomputed in the second "
            + "statement, from a r1.y the first had already overwritten, which was "
            + "wrong as well as an instruction dearer. What is left is the naming "
            + "itself: fxc has no reason to keep a variable the shader never asked "
            + "for, and the two statements do not fold back into one cmp. Was 17 "
            + "while the if side was empty and the body sat in the else."),
        ["cs_5_0/tile_luminance"] = (47,
            "The exit test of a for loop. `for (uint stride = 32; stride > 0; "
            + "stride >>= 1)` is what the source said and what is written back, "
            + "and fxc compiles its condition as a compare, an if and a break where "
            + "the original had one breakc_nz - the same shape conditional_return "
            + "has for retc. The rest of the shader comes back instruction for "
            + "instruction. Was 48 while the if over two conditions was written "
            + "`!= 0`."),

        // fxc's doing: the output is right and it compiles it differently.
        ["ps_3_0/loop_counter_reuse"] = (53,
            "A loop over smoothstep, sign, fmod and clamp, and fxc unrolls it three "
            + "times over: the decompiled source marks a loop [loop] only where fxc "
            + "would otherwise refuse it, and this one it can count. Marked, it "
            + "recompiles to the original's 26. What the source says is `fmod` - the "
            + "fold reads its quotient through the loop-invariant temp the "
            + "reciprocal was hoisted into - and ps_3_0 has no instruction for that, "
            + "so fxc writes the reciprocal, the fraction and the sign selection out "
            + "again inside each unrolled copy, sharing only the reciprocal across "
            + "the three. The original spelled it the same way once, in a loop body "
            + "fxc left alone. It computes the right answer now, which it did not "
            + "when this entry was written."),
        ["vs_3_0/loop_repeat_count"] = (8,
            "fxc unrolls the eight iteration loop the decompiled source spells out, "
            + "which is unmarked because fxc can count it; marked [loop] it "
            + "recompiles to the original's 5."),
        ["vs_3_0/vertex_texture"] = (8,
            "The original adds to one component with a swizzle over a def'd constant, "
            + "`mad r0, r0.x, c4.yxyy, v0`. The decompiled float4 construction says the "
            + "same thing and fxc writes it as an add and a mov. Written as the mad it "
            + "came from - `height * float4(0, 1, 0, 0) + i.position` - it is one "
            + "cheaper, and that is the bytecode's trick rather than the shader's "
            + "meaning: the constructor says which component the height goes in and "
            + "the mad makes the reader work it out. Kept, knowingly."),
        ["ps_3_0/point_lights"] = (58,
            "Two instructions, and they are fxc's. The tangent is `normalize(t3)` "
            + "in the source and in what is written back, and the original has one "
            + "nrm for it; recompiled, fxc stores the mad feeding it with its "
            + "components rotated - to save a swizzle on the cross product after - "
            + "and spells the normalize out as a dp3, an rsq and a mul. The other "
            + "two normalizes in the shader come back as nrm."),
        ["vs_3_0/skinned_terrain"] = (54,
            "One instruction, and it is the fourth bone's blend split by the "
            + "height. The last mad blends all four components of the skinned "
            + "position at once, and the y of it then takes the height map's "
            + "offset in a mad of its own; the y is written inside that second "
            + "expression and the xzw as a vector of their own, so fxc blends "
            + "twice. The components one instruction wrote are named together "
            + "where the text writes them apart, but not where one of them is "
            + "folded into a longer expression."),
        ["ps_4_1/decal_blend"] = (66,
            "Five instructions, and they are a register reused. The decal's "
            + "position is one mul over three rows, and the z of it is compared and "
            + "its register taken over by the mask, so the xy come back as a "
            + "float4x2 multiply and the z as a dot on its own, and the one lt over "
            + "three components is three - twice over, for the two decals. And the "
            + "flag `decalCount <= 1` is tested again as `decalCount > 1` where the "
            + "original read the register. Telling a reused register from a carried "
            + "one is the liveness the finalizer has not got."),
        ["ps_4_0/conditional_return"] = (12,
            "The original returns conditionally with retc_nz. HLSL has no spelling for "
            + "that, so `if (c) return x;` compiles to if, ret, endif."),
        ["ps_4_0/screen_position"] = (15,
            "One instruction, and it is the position read as a pair. The shader "
            + "divides sv_position.xy by the resolution and casts it to int2, and "
            + "both were written out twice - once scalar for the checker pattern, "
            + "once as a pair in the return. Named, each is written once and reads "
            + "far better, and the int2 the loads address with becomes one vector "
            + "add where the original added a scalar 1 to the x alone. Paid for "
            + "what the same pass takes off partial_overwrite and particle_update."),
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
        // fxc's doing.
        ["hs_5_0/carried_constants"] = (41,
            "One instruction, and fxc's own arrangement of the phases. The patch "
            + "constant function computes a centre, keeps it as a field, and works "
            + "its tessellation factors out from it. From the original source fxc "
            + "wrote the centre in three fork phases and read it back in the join "
            + "phase that needs it, which costs nothing; from the decompiled source "
            + "it puts the factors in a phase of their own that computes the centre "
            + "again, and spends a mad on it. The decompiled source cannot steer "
            + "that: writing the read back in by hand costs 43, and naming the "
            + "centre in a local and assigning the field from it costs 43 as well, "
            + "so 41 is the best of the three shapes. It is fxc scheduling phases "
            + "from source higher level than the bytecode, not the decompilation "
            + "doing work twice - both writers compute the same numbers as the "
            + "original on every trial."),
        // ps_1_x, assembled by hand because fxc no longer compiles it, and compiled
        // back as ps_2_0 - which has none of ps_1_x's free modifiers. _bias, _bx2,
        // 1-x and a result scale are each part of the instruction that reads or
        // writes them there and an instruction of their own here, and cnd is a
        // subtraction and a cmp.
        ["ps_1_1/detail_modulate"] = (6,
            "Five instructions, and the sixth is the _x2 on the detail modulate: "
            + "ps_2_0 scales a result with an add of it to itself."),
        ["ps_1_1/bump_dot3"] = (9,
            "Six instructions, and the three more are the two _bx2 that expand the "
            + "normal and the light vector and the 1-x on the alpha, each a mad or "
            + "an add of its own in ps_2_0."),
        ["ps_1_3/signed_blend"] = (26,
            "Ten instructions, nearly every operand of which carries a modifier or "
            + "a result scale ps_2_0 has to spell out - _bias twice, _bx2 negated, "
            + "1-x, _d2, _x4 and _d4 - and a cnd that becomes a subtraction and a "
            + "cmp. Two causes are not ps_1_x's: fxc pads the clip() of a float3 "
            + "with two movs to fill the w its texkill reads, and the dp4's vector "
            + "is three components of one value and a fourth of another, so it "
            + "comes back as a dot() of three and a fourth product, a mad more than "
            + "the dp4. Both writers compute the same numbers as the original."),
        ["ps_1_4/dependent_read"] = (15,
            "Ten instructions, and the five more are what ps_1_4 says in a "
            + "modifier: the _bx2 on the offset map is a mad, the _dw that "
            + "projects the third read is a reciprocal and a mul, the _x2 on its "
            + "alpha is an add, and the cnd is a subtraction before its cmp."),
        ["ps_1_4/projected_depth"] = (19,
            "Ten instructions. Each of the two projected coordinates is a "
            + "reciprocal and a mul, and the one texdepth reads is a mul more: "
            + "its x and y go on to be scaled and offset differently, the AST "
            + "writer builds that float2 a component at a time, and fxc divides "
            + "each apart. texdepth, which divides r5.r by r5.g and takes 1 where "
            + "r5.g is 0, is a reciprocal, a mul, a square to test for zero and a "
            + "cmp; and fxc pads the clip() of a float3 with two movs to fill the "
            + "w its texkill reads."),
        // The ps_1_x texture addressing instructions, which are a texture read and
        // the arithmetic that finds where in one instruction, and which ps_2_0
        // spells as that arithmetic and a texld.
        ["ps_1_1/bump_env"] = (17,
            "Six instructions. Each texbem's offset is mul(du/dv, bumpEnvMat#), "
            + "which fxc compiles to two dp2adds, with a mov of the zero they add "
            + "shared between both; each offset is then an add to the coordinate, "
            + "texbeml's luminance a mad and a mul, and the texreg2gb lookup a mov "
            + "to bring g and b into a texld's x and y."),
        ["ps_1_1/bumpy_reflection"] = (16,
            "Five instructions. texm3x3spec is three dot products of the normal "
            + "with the coordinates, the eye ray reflected about their vector - a "
            + "dot with the eye, a dot with itself, a reciprocal, a doubling and a "
            + "mad - and a texld, and the _bx2 on the normal map is a mad."),
        ["ps_1_2/dot_lookups"] = (8,
            "Five instructions. texdp3 and the two texm3x2 rows are a dp3 each, the "
            + "_bx2 on the normal map they read is a mad, and the vector the rows "
            + "make is a texld of its own."),
        ["ps_1_2/register_lookups"] = (9,
            "Five instructions. texreg2ar and texreg2gb read another texel's (a, r) "
            + "and (g, b) as coordinates, which ps_2_0's texld cannot take swizzled, "
            + "so each is a mov before its texld."),
        ["ps_1_2/matrix_colour"] = (9,
            "Five instructions. texm3x3 is three dot products made a colour with an "
            + "alpha of 1, and the AST writer multiplies that colour by the diffuse "
            + "a component at a time, since the vector is built from three scalars."),
        ["ps_1_3/view_reflection"] = (16,
            "Five instructions. texm3x3vspec is texm3x3spec with the eye taken from "
            + "the fourth components of the three coordinates: three dot products, "
            + "the reflection - its dot with the eye written out over the three "
            + "coordinates' w - and a texld, and the _bx2 on the normal map a mad."),
        ["ps_1_3/depth_lookup"] = (13,
            "Five instructions. texm3x2depth divides one dot product by another into "
            + "the depth, and 1 where the divisor is 0 - a reciprocal, a mul, a "
            + "square to test for zero and a cmp after the two dp3s - and texdp3tex "
            + "is a dp3 and a mov of the zero its texld reads as v."),
        ["ps_1_4/bump_offset"] = (9,
            "Eight instructions, and the one more is bem: the bump matrix applied "
            + "to a texel's red and green and added to a coordinate is one "
            + "instruction there, and mul(du/dv, bumpEnvMat2) plus the coordinate "
            + "is two dp2adds, the mov of their zero and an add here. fxc fuses the "
            + "mul and add at the end into a mad, which gives one of them back."),
        // Instructions fxc has no spelling for, so a round trip cannot keep their
        // count: the shaders were assembled by hand, and what comes back is what
        // fxc writes instead of them.
        ["vs_3_0/matrix_product_rows"] = (8,
            "Seven instructions: rows of three matrix products against def'd "
            + "constants - a 4x3, a 3x4 and a 3x2 - mixed into two outputs by two "
            + "movs each. A dot product against literal weights comes back as "
            + "dot(), one per output component, and the eight components the "
            + "outputs keep are eight dp4s and dp3s. Written as columns - each "
            + "input component times a literal vector, added up - fxc fitted it in "
            + "seven, but that is the shape of a matrix nothing in HLSL compiles "
            + "these from; a dot against weights, which the luminance of a colour "
            + "is, came back as three products and three instructions where the "
            + "shader had one dp3."),
        ["vs_3_0/matrix_product"] = (8,
            "Five instructions, and they are the rows of two matrix products. The "
            + "original computes a four by four and a three by three in one "
            + "instruction each; fxc compiles a matrix product into a mul and a mad "
            + "per row and emits neither m4x4 nor m3x3 at any profile, so the four "
            + "rows of one and the three of the other are seven instructions where "
            + "the bytecode had two. Both writers compute the same numbers as the "
            + "original."),
        ["vs_3_0/cross_product_sign"] = (7,
            "Four instructions, and they are a cross product and a sign. fxc writes "
            + "cross() as a mul and a mad over rotated components, which costs one "
            + "more than crs and a mov to hold the operand it rotates, and sign() "
            + "as the difference of two slt comparisons against the negated value, "
            + "which costs two more than sgn. Neither instruction is one fxc emits."),
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

    internal static int CountInstructions(string binaryFilename)
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
            || line.StartsWith("gs_") || line.StartsWith("cs_")
            || line.StartsWith("ds_") || line.StartsWith("hs_");
    }
}
