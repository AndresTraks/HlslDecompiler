using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using HlslDecompiler.Tests.Interpreter;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace HlslDecompiler.Tests;

/// <summary>
/// Effects (fx_2_0 to fx_5_0). Kept under CompiledEffects, apart from
/// CompiledShaders, whose tiers read one shader per file.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class EffectTests
{
    private const string Root = "CompiledEffects";

    /// <summary>
    /// In storage order, not source order: shader variables come first, then pass
    /// shaders (fx_2_0: last pass first, pixel before vertex).
    /// </summary>
    [TestCase("fx_2_0", "passes", "ps_2_0 ps_2_0 vs_2_0 ps_2_0 vs_1_1")]
    [TestCase("fx_2_0", "preshader", "ps_3_0 ps_2_0 ps_3_0 ps_2_0 ps_2_0 vs_2_0")]
    [TestCase("fx_2_0", "shader_model_3", "ps_3_0 vs_3_0")]
    [TestCase("fx_2_0", "structure", "ps_2_0 ps_2_0 vs_2_0 ps_2_0 vs_2_0")]
    [TestCase("fx_4_0", "linkage", "vs_4_0 gs_4_0 ps_4_0")]
    [TestCase("fx_4_0", "passes", "vs_4_0 gs_4_0 ps_4_0 ps_4_0")]
    [TestCase("fx_4_0", "structure", "vs_4_0 gs_4_0 ps_4_0 ps_4_0 ps_4_0 vs_4_0 gs_4_0")]
    [TestCase("fx_5_0", "structure", "gs_5_0 vs_5_0 ps_5_0 vs_5_0 hs_5_0 ds_5_0 cs_5_0")]
    [TestCase("fx_4_1", "gather", "vs_4_1 ps_4_1")]
    [TestCase("fx_5_0", "linkage", "vs_5_0 gs_5_0 ps_5_0 vs_5_0 hs_5_0 ds_5_0 ps_5_0")]
    [TestCase("fx_5_0", "local_names", "ps_5_0 ps_5_0 cs_5_0 cs_5_0")]
    [TestCase("fx_5_0", "mixed_models", "vs_4_0 ps_4_0 vs_5_0 ps_5_0")]
    [TestCase("fx_5_0", "stages", "vs_5_0 hs_5_0 ds_5_0 ps_5_0 cs_5_0")]
    public void ReadsEveryShader(string profile, string baseFilename, string shaderProfiles)
    {
        using var stream = File.OpenRead(Path.Combine(Root, profile, baseFilename + ".fxc"));

        Assert.That(FormatDetector.Detect(stream), Is.EqualTo(ShaderFileFormat.Effect));

        using var reader = new EffectReader(stream, true);
        Assert.That(string.Join(" ", reader.ReadShaders().Select(s => s.Profile)),
            Is.EqualTo(shaderProfiles));
    }

    // fx_4_x is DXBC too; the chunks tell them apart.
    [TestCase("ps_4_0", "constant")]
    [TestCase("cs_5_0", "histogram")]
    public void ShaderIsNotAnEffect(string profile, string baseFilename)
    {
        using var stream = File.OpenRead(Path.Combine("CompiledShaders", profile, baseFilename + ".fxc"));
        Assert.That(FormatDetector.Detect(stream), Is.EqualTo(ShaderFileFormat.Dxbc));
    }

    // Arithmetic split between preshader and shader reads back as one expression.
    [TestCase("ast", "o.position = mul(i.position, mul(world, viewProjection));")]
    [TestCase("ast", "return t0 * tint + (2 * scale + 1) * tint;")]
    public void PreshadedArithmeticReadsAsWritten(string writer, string line)
    {
        using var reader = new EffectReader(File.OpenRead(Path.Combine(Root, "fx_2_0", "preshader.fxc")));
        var hlsl = new StringWriter();
        new D3D9EffectWriter(reader.ReadD3D9Effect(), doAstAnalysis: writer == "ast").Write(hlsl);
        Assert.That(hlsl.ToString().Split('\n').Select(l => l.Trim()), Does.Contain(line));
    }

    public static IEnumerable<TestCaseData> PreshadedEffects()
    {
        foreach (TestCaseData data in Effects().Where(data => (string)data.Arguments[0] == "fx_2_0"))
        {
            yield return new TestCaseData(data.Arguments)
                .SetName($"PreshadersComputeTheSame(fx_2_0,{data.Arguments[1]})");
        }
    }

    /// <summary>
    /// Checks the preshader interpreter against the effect compiled with /Op, which
    /// keeps the arithmetic in the shaders.
    /// </summary>
    [TestCaseSource(nameof(PreshadedEffects))]
    [Category("Recompile")]
    public void PreshadersComputeWhatTheShadersWould(string profile, string baseFilename)
    {
        if (!ShaderCompiler.IsAvailable)
        {
            Assert.Ignore(ShaderCompiler.UnavailableMessage);
        }

        string sourceFilename = Path.Combine("EffectSources", profile, baseFilename + ".fx");
        string withoutFilename = Path.Combine("Effects", profile + "_op", baseFilename + ".fxc");
        FileUtil.MakeFolder(withoutFilename);
        string diagnostics = RunFxc(profile, sourceFilename, withoutFilename, ShaderCompiler.NoPreshader);
        Assert.That(diagnostics, Is.Null, diagnostics);

        IList<ShaderModel> preshaded = ReadEffectShaders(Path.Combine(Root, profile, baseFilename + ".fxc"));
        IList<ShaderModel> without = ReadEffectShaders(withoutFilename);
        Assert.That(without.Select(s => s.Profile), Is.EqualTo(preshaded.Select(s => s.Profile)),
            "Compiled without preshaders, the effect has other shaders.");
        Assert.That(preshaded.Any(s => Preshader.Find(s) != null), Is.True,
            "Nothing in this effect has a preshader to check.");

        var differences = new List<string>();
        for (int i = 0; i < preshaded.Count; i++)
        {
            if (Preshader.Find(preshaded[i]) == null)
            {
                continue;
            }
            differences.AddRange(EquivalenceTests
                .CompareRuns(preshaded[i], without[i], "the preshaded shader", "the shader compiled without it")
                .Select(difference => $"{baseFilename} shader {i}: {difference}"));
        }
        Assert.That(differences, Is.Empty, string.Join(Environment.NewLine, differences));
    }

    public static IEnumerable<TestCaseData> WholeEffects()
    {
        foreach (TestCaseData data in Effects())
        {
            yield return new TestCaseData(data.Arguments)
                .SetName($"EffectRoundTrip({data.Arguments[0]},{data.Arguments[1]})");
        }
    }

    /// <summary>
    /// A round-tripped effect has the same <see cref="EffectDescription"/>, and each
    /// shader computes the same and has the same <see cref="ReflectionSnapshot"/>.
    /// </summary>
    [TestCaseSource(nameof(WholeEffects))]
    [Category("Recompile")]
    public void EffectRecompilesToItself(string profile, string baseFilename)
    {
        if (!ShaderCompiler.IsAvailable)
        {
            Assert.Ignore(ShaderCompiler.UnavailableMessage);
        }

        string compiledFilename = Path.Combine(Root, profile, baseFilename + ".fxc");
        bool isD3D9 = profile == "fx_2_0";
        string originalDescription = Describe(compiledFilename, isD3D9);
        IList<ShaderModel> originalShaders = ReadEffectShaders(compiledFilename);

        var failures = new List<string>();
        var unsupported = new List<string>();
        foreach ((string writer, _) in Writers())
        {
            string hlslFilename = Path.Combine("Effects", $"{profile}_effect_{writer}", baseFilename + ".fx");
            string objectFilename = Path.ChangeExtension(hlslFilename, ".fxo");
            FileUtil.MakeFolder(hlslFilename);
            try
            {
                using var reader = new EffectReader(File.OpenRead(compiledFilename));
                if (isD3D9)
                {
                    new D3D9EffectWriter(reader.ReadD3D9Effect(), doAstAnalysis: writer == "ast").Write(hlslFilename);
                }
                else
                {
                    new EffectWriter(reader.ReadEffect(), doAstAnalysis: writer == "ast").Write(hlslFilename);
                }
            }
            catch (Exception e)
            {
                failures.Add($"The {writer} writer threw: {e}");
                continue;
            }

            string diagnostics = RunFxc(profile, hlslFilename, objectFilename);
            if (diagnostics != null)
            {
                failures.Add($"The {writer} writer's effect at {hlslFilename} does not compile:{Environment.NewLine}{diagnostics}");
                continue;
            }

            string recompiledDescription = Describe(objectFilename, isD3D9);
            if (recompiledDescription != originalDescription)
            {
                failures.Add($"The {writer} writer's effect compiles to another effect:{Environment.NewLine}"
                    + FirstDifference(originalDescription, recompiledDescription));
                continue;
            }

            IList<ShaderModel> recompiledShaders = ReadEffectShaders(objectFilename);
            for (int i = 0; i < originalShaders.Count; i++)
            {
                try
                {
                    failures.AddRange(EquivalenceTests
                        .CompareRuns(originalShaders[i], recompiledShaders[i], "the original", "its decompilation")
                        .Select(difference => $"The {writer} writer's shader {i} ({originalShaders[i].Profile}) {difference}"));
                }
                catch (Exception e) when (e is D3D9Machine.UnsupportedException
                    or D3D10Machine.UnsupportedException)
                {
                    unsupported.Add($"shader {i} ({originalShaders[i].Profile}): {e.Message}");
                }

                // The description has only each shader's profile.
                List<string> expected = ReflectionSnapshot.Lines(originalShaders[i]);
                List<string> actual = ReflectionSnapshot.Lines(recompiledShaders[i]);
                string shader = $"The {writer} writer's shader {i} ({originalShaders[i].Profile})";
                failures.AddRange(ReflectionEquivalenceTests.Except(expected, actual)
                    .Select(missing => $"{shader} lacks: {missing}"));
                failures.AddRange(ReflectionEquivalenceTests.Except(actual, expected)
                    .Select(extra => $"{shader} adds: {extra}"));
            }
        }

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
        if (unsupported.Count != 0)
        {
            Assert.Warn("Compiled, and not run: " + string.Join(" ", unsupported.Distinct()));
        }
    }

    private static string FirstDifference(string expected, string actual)
    {
        string[] expectedLines = expected.Split('\n');
        string[] actualLines = actual.Split('\n');
        for (int i = 0; i < Math.Max(expectedLines.Length, actualLines.Length); i++)
        {
            string e = i < expectedLines.Length ? expectedLines[i].TrimEnd() : "(end)";
            string a = i < actualLines.Length ? actualLines[i].TrimEnd() : "(end)";
            if (e != a)
            {
                string context = i > 0 ? expectedLines[i - 1].TrimEnd() : "";
                return $"after: {context}{Environment.NewLine}expected: {e}{Environment.NewLine}actual:   {a}";
            }
        }
        return "(no line differs)";
    }

    private static string Describe(string filename, bool isD3D9)
    {
        using var reader = new EffectReader(File.OpenRead(filename));
        return isD3D9
            ? EffectDescription.Describe(reader.ReadD3D9Effect())
            : EffectDescription.Describe(reader.ReadEffect());
    }

    private static IList<ShaderModel> ReadEffectShaders(string filename)
    {
        using var reader = new EffectReader(File.OpenRead(filename));
        return reader.ReadShaders();
    }

    // No entry point for an effect.
    private static string RunFxc(string profile, string sourceFilename, string objectFilename, uint flags = 0)
    {
        return ShaderCompiler.Compile(profile, null, sourceFilename, objectFilename, flags);
    }

    [Test]
    public void EveryCompiledEffectIsListed()
    {
        List<string> corpus = [.. Effects()
            .Select(data => $"{data.Arguments[0]}/{data.Arguments[1]}")
            .OrderBy(name => name)];
        List<string> listed = [.. typeof(EffectTests).GetMethod(nameof(ReadsEveryShader))
            .GetCustomAttributes<TestCaseAttribute>()
            .Select(testCase => $"{testCase.Arguments[0]}/{testCase.Arguments[1]}")
            .OrderBy(name => name)];

        Assert.That(listed, Is.EqualTo(corpus),
            $"Every effect in {Root} wants the shaders it holds listed for {nameof(ReadsEveryShader)}.");
    }

    public static IEnumerable<TestCaseData> Effects()
    {
        if (!Directory.Exists(Root))
        {
            yield break;
        }

        foreach (string profileDirectory in Directory.EnumerateDirectories(Root).OrderBy(d => d))
        {
            string profile = Path.GetFileName(profileDirectory);
            foreach (string effect in Directory.EnumerateFiles(profileDirectory, "*.fxc").OrderBy(f => f))
            {
                yield return new TestCaseData(profile, Path.GetFileNameWithoutExtension(effect))
                    .SetName($"EffectShadersRoundTrip({profile},{Path.GetFileNameWithoutExtension(effect)})");
            }
        }
    }

    /// <summary>
    /// Each shader recompiled alone for its own profile. A preshaded fx_2_0 shader
    /// runs with the preshader's constants; its decompilation computes them itself.
    /// </summary>
    [TestCaseSource(nameof(Effects))]
    [Category("Recompile")]
    public void ShadersRecompileAndComputeTheSame(string profile, string baseFilename)
    {
        if (!ShaderCompiler.IsAvailable)
        {
            Assert.Ignore(ShaderCompiler.UnavailableMessage);
        }

        IList<ShaderModel> shaders;
        using (var reader = new EffectReader(File.OpenRead(Path.Combine(Root, profile, baseFilename + ".fxc"))))
        {
            shaders = reader.ReadShaders();
        }

        var failures = new List<string>();
        var unsupported = new List<string>();
        var stageCounts = new Dictionary<string, int>();
        foreach (ShaderModel original in shaders)
        {
            int index = stageCounts.GetValueOrDefault(original.Stage);
            stageCounts[original.Stage] = index + 1;
            string name = $"{baseFilename}_{original.Stage}{index}";

            foreach ((string writer, Func<ShaderModel, HlslWriter> create) in Writers())
            {
                string hlslFilename = Path.Combine("Effects", $"{profile}_{writer}", name + ".fx");
                string objectFilename = Path.ChangeExtension(hlslFilename, ".fxo");
                FileUtil.MakeFolder(hlslFilename);

                try
                {
                    create(original).Write(hlslFilename);
                }
                catch (Exception e)
                {
                    failures.Add($"{name}: the {writer} writer threw: {e}");
                    continue;
                }

                string diagnostics = RecompileTests.RunFxc(original.Profile, hlslFilename, objectFilename);
                if (diagnostics != null)
                {
                    failures.Add($"{name}: the {writer} writer's output at {hlslFilename} does not compile:"
                        + $"{Environment.NewLine}{diagnostics}");
                    continue;
                }

                ShaderModel recompiled = RecompileTests.ReadShaderModel(objectFilename);
                try
                {
                    failures.AddRange(EquivalenceTests
                        .CompareRuns(original, recompiled, "the original", "its decompilation")
                        .Select(difference => $"{name}: the {writer} writer's output {difference}"));
                }
                catch (Exception e) when (e is D3D9Machine.UnsupportedException
                    or D3D10Machine.UnsupportedException)
                {
                    unsupported.Add($"{name}: {e.Message}");
                }
            }
        }

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
        if (unsupported.Count != 0)
        {
            Assert.Warn("Compiled, and not run: " + string.Join(" ", unsupported.Distinct()));
        }
    }

    private static IEnumerable<(string Name, Func<ShaderModel, HlslWriter> Create)> Writers()
    {
        yield return ("ast", s => new HlslAstWriter(s));
        yield return ("instruction", s => new HlslSimpleWriter(s));
    }
}
