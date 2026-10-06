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
/// Direct3D 10 and 11 effects, for the shaders in them.
///
/// The effects live apart from CompiledShaders, under CompiledEffects, because
/// every tier that enumerates CompiledShaders reads one shader from each file and
/// an effect is several. What is checked here is what the effect adds: that it is
/// recognised as one, that every shader in it is found and nothing else is, and
/// that each decompiles to HLSL that recompiles and computes what it did. The
/// techniques and passes around them are not decompiled yet.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class EffectTests
{
    private const string Root = "CompiledEffects";

    /// <summary>
    /// The profiles of the shaders each effect holds, in the order it stores them -
    /// which is not the order the source names them in. fxc writes a shader set by
    /// name where the variable is declared, ahead of those compiled in a pass, so
    /// the geometry shader in passes comes before the pixel shader of the first pass.
    /// </summary>
    [TestCase("fx_4_0", "passes", "vs_4_0 gs_4_0 ps_4_0 ps_4_0")]
    [TestCase("fx_4_1", "gather", "vs_4_1 ps_4_1")]
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

    /// <summary>
    /// A shader on its own is still a shader: fx_4_x is a DXBC file as well, and
    /// telling the two apart is by its chunks.
    /// </summary>
    [TestCase("ps_4_0", "constant")]
    [TestCase("cs_5_0", "histogram")]
    public void ShaderIsNotAnEffect(string profile, string baseFilename)
    {
        using var stream = File.OpenRead(Path.Combine("CompiledShaders", profile, baseFilename + ".fxc"));
        Assert.That(FormatDetector.Detect(stream), Is.EqualTo(ShaderFileFormat.Dxbc));
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
    /// The recompile and equivalence tiers, for each shader of each effect and both
    /// writers. Each shader is recompiled alone for its own profile, as fxc would
    /// compile it if it were not in an effect.
    /// </summary>
    [TestCaseSource(nameof(Effects))]
    [Category("Recompile")]
    public void ShadersRecompileAndComputeTheSame(string profile, string baseFilename)
    {
        if (RecompileTests.FxcPath == null)
        {
            Assert.Ignore("fxc.exe not found. Install the Windows SDK to run recompilation tests.");
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
