using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;

namespace HlslDecompiler.Tests;

/// <summary>
/// The equivalence tier only sees what its trials tell apart. Each case compiles a
/// shader and a variant that computes something else, and fails if the machine
/// calls them the same: a fill that goes back to float bits for an integer or a
/// double member shows here, not as a quietly weaker tier.
/// </summary>
[TestFixture]
[Category("Recompile")]
[Parallelizable(ParallelScope.All)]
public class MachineSensitivityTests
{
    // A structured uint read as a float's bits was always above 3, so every
    // `n > k` here went one way.
    [Test]
    public void TellsAStructuredIntegerComparisonApart()
    {
        string source = File.ReadAllText(
            Goldens.PathOf(Path.Combine("ShaderSources", "cs_5_0", "nested_struct_name.fx")));
        AssertDiffers(source, source.Replace("t7 > 1", "t7 > 2"));
    }

    private const string StructConstant =
        "struct P { float4 scale; int mode; double d; };\n"
        + "cbuffer C { float f; P p; };\n"
        + "RWStructuredBuffer<float> o : register(u0);\n"
        + "[numthreads(1, 1, 1)] void main(uint3 id : SV_DispatchThreadID)\n"
        + "{ o[id.x] = p.mode == 1 ? p.scale.x : (float)(int)(p.d * 4) + f; }\n";

    // An int member of a struct cbuffer variable was a float's bits, never 1 or 2.
    [Test]
    public void TellsAStructConstantIntegerApart()
    {
        AssertDiffers(StructConstant, StructConstant.Replace("p.mode == 1", "p.mode == 2"));
    }

    // A double member was two floats' bits, ~2^-7, so (int)(d * k) was always 0.
    [Test]
    public void TellsAStructConstantDoubleApart()
    {
        AssertDiffers(StructConstant, StructConstant.Replace("p.d * 4", "p.d * 2"));
    }

    // Thread ids drawn at random in 0-7 made (0, 0) a 1-in-64 chance a trial,
    // so the first thread's work went uncompared in most runs of 32 trials.
    [Test]
    public void TellsTheFirstThreadsWorkApart()
    {
        const string Source =
            "cbuffer C { float f; };\n"
            + "RWStructuredBuffer<float> o : register(u0);\n"
            + "[numthreads(8, 8, 1)] void main(uint3 id : SV_GroupThreadID)\n"
            + "{ if (all(id.xy == 0)) o[0] = f * 2; else o[1] = f; }\n";
        AssertDiffers(Source, Source.Replace("f * 2", "f * 3"));
    }

    // `>=` and `>` part only where the two are equal, which drawn values never
    // were; every fourth trial rounds them to -2..2.
    [TestCase("float")]
    [TestCase("double")]
    public void TellsAnEqualityBoundaryApart(string type)
    {
        string source = $"cbuffer C {{ {type} a; {type} b; }};\n"
            + "RWStructuredBuffer<float> o : register(u0);\n"
            + "[numthreads(1, 1, 1)] void main(uint3 id : SV_DispatchThreadID)\n"
            + "{ o[id.x] = a >= b ? 1 : 0; }\n";
        AssertDiffers(source, source.Replace("a >= b", "a > b"));
    }

    // A box test after a matrix, as decal_blend has: with every value in
    // [-2, 2] the product was almost never inside it.
    [Test]
    public void TellsANarrowBoxTestApart()
    {
        const string Source =
            "cbuffer C { float4x4 m; float3 p; };\n"
            + "RWStructuredBuffer<float> o : register(u0);\n"
            + "[numthreads(1, 1, 1)] void main(uint3 id : SV_DispatchThreadID)\n"
            + "{ float3 v = mul(float4(p, 1), m).xyz; o[id.x] = all(abs(v) < 0.5) ? v.x * 2 : 0; }\n";
        AssertDiffers(Source, Source.Replace("v.x * 2", "v.x * 3"));
    }

    // Each thread reads its neighbour's slot after the barrier. One thread run
    // alone never read a slot another thread wrote, so what thread 0 stored for
    // thread 7 went uncompared.
    [Test]
    public void TellsANeighboursGroupSharedValueApart()
    {
        const string Source =
            "cbuffer C { float f; };\n"
            + "RWStructuredBuffer<float> o : register(u0);\n"
            + "groupshared float g[8];\n"
            + "[numthreads(8, 1, 1)] void main(uint gi : SV_GroupIndex)\n"
            + "{ g[gi] = gi == 0 ? f : f * gi; GroupMemoryBarrierWithGroupSync(); o[gi] = g[(gi + 1) % 8]; }\n";
        AssertDiffers(Source, Source.Replace("gi == 0 ? f :", "gi == 0 ? 2 * f :"));
    }

    // A float converted to an integer and shifted: within [-2, 2], both shifts
    // left 0.
    [Test]
    public void TellsAShiftOfAConvertedFloatApart()
    {
        const string Source =
            "cbuffer C { float f; };\n"
            + "RWStructuredBuffer<uint> o : register(u0);\n"
            + "[numthreads(1, 1, 1)] void main(uint3 id : SV_DispatchThreadID)\n"
            + "{ o[id.x] = (uint)abs(f) >> 2; }\n";
        AssertDiffers(Source, Source.Replace(">> 2", ">> 3"));
    }

    // Integer constants and inputs were never negative, so abs of an int changed
    // nothing.
    [Test]
    public void TellsTheAbsoluteOfAnIntApart()
    {
        const string Source =
            "cbuffer C { int i; };\n"
            + "RWStructuredBuffer<int> o : register(u0);\n"
            + "[numthreads(1, 1, 1)] void main(uint3 id : SV_DispatchThreadID)\n"
            + "{ o[id.x] = abs(i); }\n";
        AssertDiffers(Source, Source.Replace("abs(i)", "i"));
    }

    // A vs_1_1 texture coordinate is an undeclared oT#, and the D3D9 machine
    // recorded only the position, so a wrong oT# went uncompared.
    [Test]
    public void TellsAShaderModel1TextureCoordinateApart()
    {
        const string Source =
            "float4 main(float4 p : POSITION, out float4 t : TEXCOORD1) : POSITION\n"
            + "{ t = p * 2; return p; }\n";
        AssertDiffers(Source, Source.Replace("p * 2", "p * 3"), "vs_1_1");
    }

    private static void AssertDiffers(string source, string variant, string profile = "cs_5_0")
    {
        if (RecompileTests.FxcPath == null)
        {
            Assert.Ignore("fxc.exe not found. Install the Windows SDK to run recompilation tests.");
        }
        Assert.That(variant, Is.Not.EqualTo(source), "The variant changes nothing.");

        string folder = Path.Combine("MachineSensitivity", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        ShaderModel original = Compile(folder, "original", source, profile);
        ShaderModel changed = Compile(folder, "variant", variant, profile);
        Assert.That(EquivalenceTests.CompareRuns(original, changed, "the shader", "its variant").ToList(),
            Is.Not.Empty, "The machine computes the same for a shader and a variant that differs.");
    }

    private static ShaderModel Compile(string folder, string name, string source, string profile)
    {
        string hlsl = Path.Combine(folder, name + ".fx");
        string compiled = Path.ChangeExtension(hlsl, ".fxo");
        File.WriteAllText(hlsl, source);
        Assert.That(RecompileTests.RunFxc(profile, hlsl, compiled), Is.Null);
        return RecompileTests.ReadShaderModel(compiled);
    }
}
