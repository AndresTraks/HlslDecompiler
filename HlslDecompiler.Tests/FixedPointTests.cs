using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;

namespace HlslDecompiler.Tests;

/// <summary>
/// That an AST golden is a fixed point: compiled by fxc and decompiled again, it
/// comes back byte for byte. A golden that does not is a decompilation of one
/// shader that decompiles a second shader into something else, so which of the
/// two texts the golden holds is an accident of where the fixture started.
///
/// The goldens only say the text has not changed since it was written, and every
/// new fixture was checked for this by hand. The corpus as a whole had never been
/// checked, and most of what fails here was committed that way.
///
/// The ps_1_x goldens are left out: they recompile as ps_2_0, and decompile from
/// that as the ps_2_0 shader they are.
/// </summary>
[TestFixture]
[Category("Recompile")]
// Every case is independent: it compiles one golden into a path named after its
// profile and shader, and shares nothing with the others.
[Parallelizable(ParallelScope.All)]
public class FixedPointTests
{
    // The goldens that were not a fixed point when this test was written, in the
    // two classes measured then. Neither is a difference in what the shader
    // computes - SecondRoundComputesTheSame holds every one of them to that.
    //
    // Reordered: the same lines once the temporaries' names are set aside. fxc
    // schedules the recompile its own way, and the decompiler names temporaries and
    // orders commutative operands by where the instructions put them.
    private static readonly string[] Reordered =
    [
        "cs_4_1/compute_loop", "ps_3_0/if_reassign", "ps_3_0/tex2dlod", "ps_3_0/tex3d",
        "ps_3_0/texcube", "ps_4_0/branch_sample", "ps_4_0/derivatives", "ps_4_0/int_minmax",
        "ps_4_0/texture_1d_array", "ps_4_1/sample_offsets", "ps_5_0/cascaded_shadows",
        "ps_5_0/gather_channels", "ps_5_0/gather_cmp_variants", "ps_5_0/gather_compare",
        "ps_5_0/gather_offset", "ps_5_0/packed_input_cross", "vs_1_1/constant_struct",
    ];

    // Reworded: the same arithmetic, spelled differently from fxc's schedule - the
    // operands of an addition or a comparison in register order, a different
    // component of a load named first, an idiom recovered in one round and not the
    // next. Fifty-two oscillate between two texts, twenty-five settle on a second
    // after one round, and twenty-four were still moving after two.
    private static readonly string[] Reworded =
    [
        "cs_4_0/atomic_free_sum", "cs_4_0/bitpack", "cs_4_0/particle_update", "cs_4_0/raw_buffer",
        "cs_4_1/compute_hash", "cs_4_1/groupshared_reduce", "cs_4_1/integer_multiply",
        "cs_4_1/shared_float_int_index", "cs_5_0/double_select", "cs_5_0/groupshared_neighbours",
        "cs_5_0/groupshared_scan", "cs_5_0/half_pack", "cs_5_0/high_bit",
        "cs_5_0/local_array_window", "cs_5_0/prefix_flags",
        "cs_5_0/switch_stored_index", "cs_5_0/tile_depth_bounds", "cs_5_0/tile_luminance",
        "ds_5_0/isoline_curve", "ds_5_0/quad_patch", "ds_5_0/terrain_quad",
        "gs_4_0/adjacency_viewport", "gs_4_0/primitive_id", "hs_5_0/carried_constants",
        "hs_5_0/culled_patch", "hs_5_0/isoline_detail", "hs_5_0/looped_control_points",
        "hs_5_0/quad_tess_factors", "hs_5_0/split_control_points", "hs_5_0/tri_distance",
        "ps_3_0/continue_nested", "ps_3_0/dynamic_index", "ps_3_0/guarded_average",
        "ps_3_0/if_else_in_loop", "ps_3_0/if_nested", "ps_3_0/loop_counter_reuse",
        "ps_3_0/multiply_negate", "ps_3_0/partial_precision", "ps_3_0/point_lights",
        "ps_3_0/shared_subexpression", "ps_3_0/temp_assignment", "ps_3_0/tex1d",
        "ps_3_0/vpos_vface", "ps_4_0/ambient_occlusion", "ps_4_0/bit_field", "ps_4_0/branch_flag",
        "ps_4_0/conditional_return", "ps_4_0/divide_product", "ps_4_0/dxbc_continue",
        "ps_4_0/float_modulo_complement", "ps_4_0/gbuffer_decode", "ps_4_0/half_packing",
        "ps_4_0/index_and_scale", "ps_4_0/int_divide", "ps_4_0/int_float_mix",
        "ps_4_0/integer_hash", "ps_4_0/integer_vector", "ps_4_0/logical_and",
        "ps_4_0/mixed_register", "ps_4_0/multiply_negate", "ps_4_0/packed_cbuffer",
        "ps_4_0/packed_interpolator", "ps_4_0/precedence_mix", "ps_4_0/resource_swizzle",
        "ps_4_0/shadow_pcf", "ps_4_0/uint_convert", "ps_4_0/vector_subscript",
        "ps_4_1/alpha_to_coverage", "ps_4_1/cascade_shadow", "ps_4_1/decal_blend",
        "ps_4_1/sample_count", "ps_5_0/depth_aware_blur", "ps_5_0/double_compare",
        "ps_5_0/double_immediate", "ps_5_0/environment_lighting", "ps_5_0/gbuffer_write",
        "ps_5_0/integer_target", "ps_5_0/integer_texture_load", "ps_5_0/microfacet_lighting",
        "ps_5_0/nested_array_cbuffer", "ps_5_0/occlusion_kernel", "ps_5_0/octahedral_normal",
        "ps_5_0/parallax_steps", "ps_5_0/split_transform", "ps_5_0/tangent_lighting",
        "ps_5_0/transposed_basis", "ps_5_0/typed_view_load", "vs_2_0/distance_falloff",
        "vs_3_0/loop_nested_uniform", "vs_3_0/loop_repeat_count", "vs_3_0/nested_select",
        "vs_4_0/bitwise", "vs_4_0/integer_inputs", "vs_4_0/normal_transform",
        "vs_4_0/packed_bits_uniform", "vs_4_0/packed_colour", "vs_4_0/particle_draw",
        "vs_4_0/skin_buffer", "vs_5_0/packed_matrix_members", "vs_5_0/skinned_instances",
    ];

    /// <summary>
    /// Goldens that are not a fixed point, and why. One that becomes one fails the
    /// test so that its entry gets removed; a golden not listed is held to being one.
    /// </summary>
    private static readonly Dictionary<string, string> KnownNonFixedPoints = BuildKnownNonFixedPoints();

    private static Dictionary<string, string> BuildKnownNonFixedPoints()
    {
        var known = new Dictionary<string, string>();
        foreach (string key in Reordered)
        {
            known[key] = "fxc reorders the recompile, and the temporaries are named and the "
                + "commutative operands ordered by where its instructions put them.";
        }
        foreach (string key in Reworded)
        {
            known[key] = "fxc reorders the recompile, and the same arithmetic comes back "
                + "spelled another way - operand order, which component is named first, "
                + "an idiom recovered or not.";
        }
        return known;
    }

    public static IEnumerable<TestCaseData> Shaders()
    {
        foreach (TestCaseData data in RecompileTests.Shaders())
        {
            yield return new TestCaseData(data.Arguments)
                .SetName($"FixedPoint({data.Arguments[0]},{data.Arguments[1]})");
        }
    }

    [TestCaseSource(nameof(Shaders))]
    public void GoldenDecompilesToItself(string profile, string baseFilename)
    {
        if (RecompileTests.FxcPath == null)
        {
            Assert.Ignore("fxc.exe not found. Install the Windows SDK to run recompilation tests.");
        }
        if (profile.StartsWith("ps_1_"))
        {
            Assert.Ignore("A ps_1_x golden recompiles as ps_2_0.");
        }

        string golden = Path.Combine("ShaderSources", profile, baseFilename + ".fx");
        string objectFilename = Path.Combine("FixedPoint", profile, baseFilename + ".fxo");
        string decompiled = Path.Combine("FixedPoint", profile, baseFilename + ".fx");
        FileUtil.MakeFolder(objectFilename);

        string failure = RecompileTests.RunFxc(profile, golden, objectFilename);
        Assert.That(failure, Is.Null, $"The golden {golden} does not compile:\n{failure}");

        ShaderModel shader = RecompileTests.ReadShaderModel(objectFilename);
        new Hlsl.HlslAstWriter(shader).Write(decompiled);

        string key = $"{profile}/{baseFilename}";
        bool isFixedPoint = File.ReadAllText(decompiled) == File.ReadAllText(golden);
        if (KnownNonFixedPoints.TryGetValue(key, out string reason))
        {
            Assert.That(isFixedPoint, Is.False,
                $"{key} is now a fixed point. Remove it from {nameof(KnownNonFixedPoints)}.");
            Assert.Ignore($"Known not to be a fixed point: {reason}");
        }
        Assert.That(File.ReadAllText(decompiled), Is.EqualTo(File.ReadAllText(golden)),
            $"{golden} decompiles from its own compilation as {decompiled}.");
    }

    public static IEnumerable<TestCaseData> SecondRoundShaders()
    {
        foreach (TestCaseData data in RecompileTests.Shaders())
        {
            yield return new TestCaseData(data.Arguments)
                .SetName($"SecondRoundEquivalent({data.Arguments[0]},{data.Arguments[1]})");
        }
    }

    /// <summary>
    /// That where a golden is not a fixed point, the text it drifts to still means
    /// what it does: the golden compiled, and the decompilation of that compiled
    /// again, compute the same. The equivalence tier checks the first round, from the
    /// fixture's own bytecode, and nothing checked the second - a groupshared word
    /// stored as the bits of FLT_MAX came back the second time as the float itself,
    /// stored into an integer, which converts it.
    /// </summary>
    [TestCaseSource(nameof(SecondRoundShaders))]
    public void SecondRoundComputesTheSame(string profile, string baseFilename)
    {
        if (RecompileTests.FxcPath == null)
        {
            Assert.Ignore("fxc.exe not found. Install the Windows SDK to run recompilation tests.");
        }
        if (profile.StartsWith("ps_1_"))
        {
            Assert.Ignore("A ps_1_x golden recompiles as ps_2_0.");
        }

        string golden = Path.Combine("ShaderSources", profile, baseFilename + ".fx");
        string firstObject = Path.Combine("SecondRound", profile, baseFilename + ".1.fxo");
        string decompiled = Path.Combine("SecondRound", profile, baseFilename + ".fx");
        string secondObject = Path.Combine("SecondRound", profile, baseFilename + ".2.fxo");
        FileUtil.MakeFolder(firstObject);

        Assert.That(RecompileTests.RunFxc(profile, golden, firstObject), Is.Null, $"{golden} does not compile.");
        ShaderModel first = RecompileTests.ReadShaderModel(firstObject);
        new Hlsl.HlslAstWriter(first).Write(decompiled);
        string failure = RecompileTests.RunFxc(profile, decompiled, secondObject);
        Assert.That(failure, Is.Null, $"{decompiled}, the decompilation of {golden}, does not compile:\n{failure}");
        ShaderModel second = RecompileTests.ReadShaderModel(secondObject);

        List<string> differences;
        try
        {
            differences = [.. EquivalenceTests.CompareRuns(first, second,
                "the golden", "its decompilation")];
        }
        catch (System.Exception e) when (e is Interpreter.D3D9Machine.UnsupportedException
            or Interpreter.D3D10Machine.UnsupportedException)
        {
            Assert.Ignore($"Not run: {e.Message}.");
            return;
        }
        Assert.That(differences, Is.Empty, string.Join(System.Environment.NewLine, differences));
    }
}
