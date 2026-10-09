using HlslDecompiler.Hlsl;
using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using System.IO;

namespace HlslDecompiler.Tests;

[TestFixture]
// Every case is independent: it reads one .fxc, writes its output under a path
// named after its profile and shader, and shares nothing with the others.
[Parallelizable(ParallelScope.All)]
public class DecompileTests
{
    [TestCase("ps_2_0", "tex2d")]
    [TestCase("ps_2_0", "shadowed_sampler_name")]
    [TestCase("ps_2_0", "texcoord_struct")]
    [TestCase("ps_2_0", "lerp")]
    [TestCase("ps_2_0", "colour_input")]
    [TestCase("ps_3_0", "conditional")]
    [TestCase("ps_3_0", "constant")]
    [TestCase("ps_3_0", "constant_struct")]
    [TestCase("ps_3_0", "dot_product2_add")]
    [TestCase("ps_3_0", "dot_product2_add_scalar")]
    [TestCase("ps_3_0", "derivative")]
    [TestCase("ps_3_0", "texcoord")]
    [TestCase("ps_3_0", "texcoord_modifier")]
    [TestCase("ps_3_0", "texcoord_swizzle")]
    [TestCase("ps_3_0", "float4_construct")]
    [TestCase("ps_3_0", "float4_construct2")]
    [TestCase("ps_3_0", "float4_constant")]
    [TestCase("ps_3_0", "multiply_subtract")]
    [TestCase("ps_3_0", "absolute_multiply")]
    [TestCase("ps_3_0", "modifier")]
    [TestCase("ps_3_0", "sincos")]
    [TestCase("ps_3_0", "loop_counter_reuse")]
    [TestCase("ps_3_0", "point_lights")]
    [TestCase("ps_3_0", "point_light_struct")]
    [TestCase("ps_3_0", "array_member_struct")]
    [TestCase("ps_3_0", "nested_array_struct")]
    [TestCase("ps_3_0", "matrix_member_struct")]
    [TestCase("ps_3_0", "partial_precision")]
    [TestCase("ps_3_0", "partial_precision_variable")]
    [TestCase("ps_3_0", "partial_precision_sample")]
    [TestCase("ps_3_0", "negate_absolute")]
    [TestCase("ps_3_0", "dynamic_index")]
    [TestCase("ps_3_0", "semantics")]
    [TestCase("ps_3_0", "tex1d")]
    [TestCase("ps_3_0", "tex2d")]
    [TestCase("ps_3_0", "tex2d_swizzle")]
    [TestCase("ps_3_0", "tex2d_two_samplers")]
    [TestCase("ps_3_0", "tex2d_and_constant")]
    [TestCase("ps_3_0", "tex2dlod")]
    [TestCase("ps_3_0", "tex3d")]
    [TestCase("ps_3_0", "texcube")]
    [TestCase("ps_3_0", "clip")]
    [TestCase("ps_3_0", "component_chain")]
    [TestCase("ps_3_0", "continue_loop")]
    [TestCase("ps_3_0", "if")]
    [TestCase("ps_3_0", "if_bool")]
    [TestCase("ps_3_0", "if_no_else")]
    [TestCase("ps_3_0", "if_reassign")]
    [TestCase("ps_3_0", "if_nested")]
    [TestCase("ps_3_0", "vpos_vface")]
    [TestCase("ps_3_0", "intrinsics3")]
    [TestCase("ps_3_0", "mrt")]
    [TestCase("ps_3_0", "loop")]
    [TestCase("ps_3_0", "loop_input_struct")]
    [TestCase("ps_3_0", "loop_nested")]
    [TestCase("ps_3_0", "loop_accumulate")]
    [TestCase("ps_3_0", "sm3_break_acc")]
    [TestCase("ps_3_0", "loop_two_vars")]
    [TestCase("ps_3_0", "if_in_loop")]
    [TestCase("ps_3_0", "if_else_in_loop")]
    [TestCase("ps_3_0", "struct")]
    [TestCase("ps_3_0", "shared_subexpression")]
    [TestCase("ps_3_0", "temp_assignment")]
    [TestCase("vs_1_1", "constant")]
    [TestCase("vs_1_1", "constant_struct")]
    [TestCase("vs_1_1", "dot_product")]
    [TestCase("vs_1_1", "bone_array")]
    [TestCase("vs_1_1", "length")]
    [TestCase("vs_1_1", "lit")]
    [TestCase("vs_1_1", "lighting")]
    [TestCase("vs_1_1", "matrix22_vector2_multiply")]
    [TestCase("vs_1_1", "matrix23_vector2_multiply")]
    [TestCase("vs_1_1", "matrix33_vector3_multiply")]
    [TestCase("vs_1_1", "matrix44_vector4_multiply")]
    [TestCase("vs_1_1", "normalize")]
    [TestCase("vs_1_1", "submatrix43_vector3_multiply")]
    [TestCase("vs_1_1", "vector2_matrix22_multiply")]
    [TestCase("vs_1_1", "vector2_matrix32_multiply")]
    [TestCase("vs_1_1", "vector3_matrix33_multiply")]
    [TestCase("vs_1_1", "vector4_matrix44_multiply")]
    [TestCase("vs_3_0", "const_array")]
    [TestCase("vs_3_0", "bone_array")]
    [TestCase("vs_3_0", "grass_wave")]
    [TestCase("vs_3_0", "constant")]
    [TestCase("vs_3_0", "constant_struct")]
    [TestCase("vs_3_0", "dot_product")]
    [TestCase("vs_3_0", "length")]
    [TestCase("vs_3_0", "matrix_layout")]
    [TestCase("vs_3_0", "matrix22_vector2_multiply")]
    [TestCase("vs_3_0", "matrix23_vector2_multiply")]
    [TestCase("vs_3_0", "matrix33_vector3_multiply")]
    [TestCase("vs_3_0", "matrix44_vector4_multiply")]
    [TestCase("vs_3_0", "normalize")]
    [TestCase("vs_3_0", "relative_address")]
    [TestCase("vs_3_0", "matrix_array")]
    [TestCase("vs_3_0", "skinned_terrain")]
    [TestCase("vs_3_0", "partial_overwrite")]
    [TestCase("vs_3_0", "vertex_outputs")]
    [TestCase("vs_3_0", "vertex_texture")]
    [TestCase("vs_3_0", "cross_sign")]
    [TestCase("vs_3_0", "bool_branches")]
    [TestCase("vs_2_0", "distance_falloff")]
    [TestCase("vs_2_0", "light_tint")]
    [TestCase("ps_3_0", "sample_variants_d3d9")]
    [TestCase("vs_3_0", "nested_select")]
    [TestCase("vs_3_0", "loop_relative_address")]
    [TestCase("vs_3_0", "loop_repeat_count")]
    [TestCase("vs_3_0", "loop_nested_uniform")]
    [TestCase("ps_3_0", "texld_variants")]
    [TestCase("vs_3_0", "bool_constant")]
    [TestCase("ps_2_0", "lighting_intrinsics")]
    [TestCase("vs_1_1", "select_idioms")]
    [TestCase("ps_3_0", "kill_derivatives")]
    [TestCase("ps_3_0", "multiply_negate")]
    [TestCase("ps_3_0", "continue_nested")]
    [TestCase("vs_2_0", "matrix_palette")]
    [TestCase("ps_2_0", "water_ripple")]
    [TestCase("ps_2_0", "alpha_cutout")]
    [TestCase("ps_2_0", "detail_blend")]
    [TestCase("ps_2_0", "shadow_project")]
    [TestCase("vs_2_0", "wave_deform")]
    [TestCase("vs_2_0", "fog_lighting")]
    [TestCase("vs_3_0", "dynamic_struct_index")]
    [TestCase("vs_3_0", "dynamic_struct_matrix")]
    [TestCase("vs_3_0", "row_major_matrix43")]
    [TestCase("vs_3_0", "matrix_member_add")]
    [TestCase("vs_3_0", "submatrix43_vector3_multiply")]
    [TestCase("vs_3_0", "vector2_matrix22_multiply")]
    [TestCase("vs_3_0", "vector2_matrix32_multiply")]
    [TestCase("vs_3_0", "vector3_matrix33_multiply")]
    [TestCase("vs_3_0", "vector4_matrix44_multiply")]
    [TestCase("vs_3_0", "matrix_product")]
    [TestCase("vs_3_0", "matrix_product_rows")]
    [TestCase("vs_3_0", "cross_product_sign")]
    [TestCase("ps_1_1", "detail_modulate")]
    [TestCase("ps_1_1", "bump_dot3")]
    [TestCase("ps_1_3", "signed_blend")]
    [TestCase("ps_1_4", "dependent_read")]
    [TestCase("ps_1_4", "projected_depth")]
    [TestCase("ps_1_1", "bump_env")]
    [TestCase("ps_1_1", "bumpy_reflection")]
    [TestCase("ps_1_2", "dot_lookups")]
    [TestCase("ps_1_2", "register_lookups")]
    [TestCase("ps_1_2", "matrix_colour")]
    [TestCase("ps_1_3", "view_reflection")]
    [TestCase("ps_1_3", "depth_lookup")]
    [TestCase("ps_1_4", "bump_offset")]
    [TestCase("ps_3_0", "float_modulo")]
    [TestCase("ps_3_0", "float_modulo_negate")]
    [TestCase("ps_3_0", "duplicated_component")]
    [TestCase("ps_2_0", "splat_layers")]
    [TestCase("ps_2_0", "luminance_dot")]
    [TestCase("ps_2_0", "matrix2x2_offset")]
    [TestCase("ps_3_0", "guarded_average")]
    public void DecompileShaderTest(string profile, string baseFilename)
    {
        string compiledShaderFilename = $"CompiledShaders{Path.DirectorySeparatorChar}{profile}{Path.DirectorySeparatorChar}{baseFilename}.fxc";
        string asmExpectedFilename = $"ShaderAssembly{Path.DirectorySeparatorChar}{profile}{Path.DirectorySeparatorChar}{baseFilename}.asm";
        string hlslExpectedFilename = $"ShaderSources{Path.DirectorySeparatorChar}{profile}{Path.DirectorySeparatorChar}{baseFilename}.fx";
        string hlslInstructionExpectedFilename = $"ShaderSources{Path.DirectorySeparatorChar}{profile}_instruction{Path.DirectorySeparatorChar}{baseFilename}.fx";
        string asmOutputFilename = $"{profile}{Path.DirectorySeparatorChar}{baseFilename}.asm";
        string hlslOutputFilename = $"{profile}{Path.DirectorySeparatorChar}{baseFilename}.fx";
        string hlslInstructionOutputFilename = $"{profile}{Path.DirectorySeparatorChar}{baseFilename}_instruction.fx";

        ShaderModel shader;

        using var inputStream = File.Open(Path.GetFullPath(compiledShaderFilename), FileMode.Open, FileAccess.Read, FileShare.Read);
        using (var input = new ShaderReader(inputStream, true))
        {
            shader = input.ReadShader();
        }

        var asmWriter = new AsmWriter(shader);
        FileUtil.MakeFolder(asmOutputFilename);
        asmWriter.Write(asmOutputFilename);

        var hlslInstructionWriter = new HlslSimpleWriter(shader);
        FileUtil.MakeFolder(hlslInstructionOutputFilename);
        hlslInstructionWriter.Write(hlslInstructionOutputFilename);

        var hlslWriter = new HlslAstWriter(shader);
        FileUtil.MakeFolder(hlslOutputFilename);
        hlslWriter.Write(hlslOutputFilename);

        Goldens.AssertMatches(asmOutputFilename, asmExpectedFilename,
            "Assembly not equal at " + asmOutputFilename);
        Goldens.AssertMatches(hlslInstructionOutputFilename, hlslInstructionExpectedFilename,
            "HLSL not equal at " + hlslInstructionOutputFilename);
        Goldens.AssertMatches(hlslOutputFilename, hlslExpectedFilename,
            "AST HLSL not equal at " + hlslOutputFilename);
    }
}
