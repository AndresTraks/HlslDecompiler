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
/// Decompiles, recompiles, and fails if fxc's instruction count goes up. The
/// assembly itself cannot be compared: fxc reallocates and reorders freely.
/// </summary>
[TestFixture]
[Category("Recompile")]
// Each case writes to its own path and shares nothing.
[Parallelizable(ParallelScope.All)]
public class RoundTripCostTests
{
    /// <summary>
    /// The cost today; a change either way fails, so improvements are noticed. Each
    /// reason says whether the decompiler or fxc is responsible.
    /// </summary>
    internal static readonly Dictionary<string, (int Cost, string Reason)> KnownRegressions = new()
    {
        // The decompiler's doing.
        ["ps_5_0/icb_struct_rows"] = (20,
            "One instruction, and it is the field offsets. The original reads a "
            + "static array of structs, whose fields fxc folds into the operand "
            + "(icb[r0.x + 1]); written back as one int4 array indexed `t0 + 1` "
            + "it computes the three field indices with an imad. Splitting the "
            + "rows into one array per field, as before, read the wrong rows."),
        ["cs_5_0/struct_flags"] = (12,
            "One instruction, and it is the load of the struct. The original "
            + "reads pos and life with one sixteen byte load; read back, life is "
            + "named in the statement that computes the new life and pos in the "
            + "store of the position, a statement of its own, so fxc loads twice. "
            + "It was two while the store read life from the buffer again rather "
            + "than through the name the first statement gave it. The fixture is here for two things the "
            + "probe that found it showed: `t1 | 1` came back as `t1 | 0.000000`, the "
            + "immediate read as a float, and the instruction writer loaded the float "
            + "members into an int register as numbers rather than bits."),
        ["cs_5_0/fog_volume"] = (44,
            "One instruction, and it is the constructor that packs a register. "
            + "fxc put the scattered light in r2.x, r2.z and r2.w and the "
            + "transmittance in r2.y between them, so one mad writes the three "
            + "lanes of the scattering and an unrelated value sits in the middle "
            + "of the register. GroupComponents merges consecutive runs, so x, z "
            + "and w cannot be one group across the y, and the four components "
            + "come back as one float4(...) that interleaves them - after which "
            + "fxc cannot see the three lanes as one instruction and splits the "
            + "mad into a mad over .y and a mad over .zw. The instruction writer "
            + "pays nothing, writing `r2.xzw = ...` and `r2.y = ...` as the two "
            + "statements they are. Fixing it means a register whose components "
            + "are not one vector expression being written as a declaration and "
            + "one assignment per instruction, by swizzle, which is a form the "
            + "ast writer has not got."),
        ["cs_5_0/signed_modulo_uint_divisor"] = (16,
            "One instruction, and it is fxc's pairing: the original takes the "
            + "absolute values of the buffer size and of `id.x - 4` in one imax "
            + "over two lanes; the decompilation names `id.x - 4` for the division "
            + "but writes the modulo's dividend out again, `(int)(id.x - 4)`, so "
            + "fxc takes the two absolute values apart. What the fixture is for "
            + "costs nothing: the signed % by a uint size is cast to int, where "
            + "without the cast it was unsigned and wrong for a negative dividend."),
        ["ps_5_0/edge_select_compare"] = (47,
            "Five instructions, the lane-split class: the original pairs the luma "
            + "neighbours' min, max and differences in two-lane instructions "
            + "(`min r2.xy, r1.ywyy, r1.xzxx`), the decompilation computes each "
            + "lane alone. What the fixture is for costs nothing: the selects "
            + "compared by `>=` are bracketed."),
        ["ps_5_0/decal_clip_lanes"] = (44,
            "Four instructions, the lane-split class: the w lane of one div is its "
            + "own `t5 / t5`, the box test's three lanes are rebuilt from scalars, "
            + "and each utof/add pair of the atlas cell is split. What the fixture "
            + "is for costs nothing: the clip's comparisons read only by `||` are a "
            + "bool3 built as `bool3(...)` (46 as an int3, 45 as bool3 = int3(...))."),
        ["ps_5_0/parallax_while_and"] = (35,
            "One instruction: the loop steps the coordinate and samples at the new "
            + "one, and the decompilation samples at the step's expression "
            + "(`-t1 * t0 + t5`) before assigning it, so the mad is done twice. "
            + "What the fixture is for costs nothing: `int t6 = a && b; if (t6 == 0) "
            + "break;` at the loop top is `while (a && b)` (37 before)."),
        ["cs_5_0/signed_divide_uint_operands"] = (27,
            "One instruction, and it is the absolute value fxc shares: the original "
            + "takes abs(n) once for a % and a / by the same uint constant. Shared, it "
            + "is named, so the % is not recognized as signed (its abs is a variable "
            + "now) and comes back as its expansion, `t5 < 0 ? -(abs(t5) % t0) : ...`. "
            + "What the fixture is for costs nothing: a signed / by a uint buffer "
            + "element is cast to int, and the negated remainder is bracketed - "
            + "`-abs(t5) % t0` divided a huge unsigned number."),
        ["ps_5_0/local_array_offset_index"] = (27,
            "One instruction, and it is fog_volume's: the original doubles the "
            + "four texcoord lanes in one add, whose x goes straight into a store "
            + "of the array and whose yzw are read later. The x lane is written "
            + "inline into its store and the yzw become `t0`, so fxc doubles them "
            + "in two instructions. What the fixture is for costs nothing: "
            + "relative reads `x0[t1.y + 1]` and the signed `% 3` whose sign test "
            + "shares an and with another lane."),

        // fxc's doing.
        ["ps_4_0/loop_reused_register"] = (46,
            "One instruction, and a mov. The binary is shadow_pcf's golden as fxc "
            + "compiled it: fxc hoisted `t - bias.w` out of the first loop, so it "
            + "comes back as a variable declared between the sum's 0 and the "
            + "counter's -1, and fxc then sets those two with two movs where it "
            + "had one. The fixture is here for the register fxc reuses for a "
            + "product's x and the second loop's sample: the dot is not a loop "
            + "variable, and the product reads mul(texcoord, (float4x3)m)."),
        ["cs_5_0/group_shared_tile"] = (23,
            "Eight instructions, and the price of having no spelling for the "
            + "declaration. The original keeps a float4 and a uint2 in raw "
            + "groupshared - one sixteen byte and one eight byte dcl_tgsm_raw, two "
            + "store_raw and two ld_raw. HLSL has no raw array (a raw variable "
            + "wider than one dword cannot be declared at all), so the "
            + "decompilation says so as arrays of dword elements, and fxc "
            + "compiles the four accesses to those as one store and one load per "
            + "element: six store_structured and six ld_structured where the "
            + "original had two of each. The same bytes, named a dword at a time."),
        ["cs_5_0/group_shared_addzero"] = (24,
            "Eight instructions, and the same missing raw spelling as "
            + "cs_5_0/group_shared_tile: the float4 and the uint2 of groupshared "
            + "come back as arrays of dword elements, and fxc compiles their "
            + "accesses one dword at a time - six stores and six loads where the "
            + "original had two store_raw and two ld_raw. What the fixture is for "
            + "costs nothing: an `add 0.0` folded to nothing still reads the tile's "
            + "dwords as floats, and the asfloats it says so with recompile to the "
            + "bit moves the original's adds were."),
        ["cs_5_0/group_shared_round2"] = (24,
            "Seven instructions for the tile's missing raw spelling and the "
            + "schedule built around it: four stores and four loads of the float4's "
            + "elements where the original moved it whole (store_raw and ld_raw), "
            + "and fxc, handed the whole-register operand no more, splits its one "
            + "four-lane mad into three - while the mov that carried the 1.0 into "
            + "the register folds away, since a per-element store can hold the "
            + "immediate itself. The bounds were an array already and cost nothing."),
        ["cs_5_0/group_shared_float4_bits"] = (16,
            "Five instructions, all of them the missing raw spelling of "
            + "cs_5_0/group_shared_tile: the float4 comes back as an array of four "
            + "dwords, stored and loaded one element at a time - four "
            + "store_structured and four ld_structured where the original had one "
            + "store_raw and one ld_raw, the mov that carried the 1.0 folding into "
            + "its store. What the fixture is for costs nothing: the four int "
            + "elements read back through asfloat(int4(...)) recompile to the bit "
            + "moves the original made, where a float4 constructor inside the "
            + "asfloat was an itof."),
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
        ["ps_4_0/conditional_return"] = (12,
            "The original returns conditionally with retc_nz. HLSL has no spelling for "
            + "that, so `if (c) return x;` compiles to if, ret, endif."),
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
        // ps_1_x recompiled as ps_2_0, where its free modifiers cost instructions.
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
        ["ps_1_4/projected_depth"] = (18,
            "Nine instructions. Each of the two projected coordinates is a "
            + "reciprocal and a mul. The one texdepth reads was a mul more while "
            + "its x and y, which go on to be scaled and offset differently, were "
            + "divided a component at a time; named together as the one division "
            + "they were (ScatteredInstruction) they are divided once. texdepth, "
            + "which divides r5.r by r5.g and takes 1 where "
            + "r5.g is 0, is a reciprocal, a mul, a square to test for zero and a "
            + "cmp; and fxc pads the clip() of a float3 with two movs to fill the "
            + "w its texkill reads."),
        // ps_1_x texture addressing: arithmetic plus a texld in ps_2_0.
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
        ["ps_1_2/matrix_colour"] = (7,
            "Three instructions. texm3x3 is three dot products made a colour with an "
            + "alpha of 1, which ps_2_0 writes as three dp3s and a mov where ps_1_2 has "
            + "the one texm3x3. It was five while the colour was multiplied by the "
            + "diffuse a component at a time; the dots scaled by one vector's "
            + "components now group into one multiply."),
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
        // Hand-assembled instructions fxc never emits.
        ["vs_1_1/partial_exp_log"] = (7,
            "Seven instructions for four. One expp writes 2^v whole, split, and a "
            + "one; fxc assembles the same from a frc, the add that makes it a "
            + "floor, two exps for the whole and partial powers, and the mov of "
            + "the one into w. The logp and the two movs come back one for one."),
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

    // Renamed so failures are not reported as Recompile(...).
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
        startInfo.ArgumentList.Add(RecompileTests.AsFxcPath(binaryFilename));

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
