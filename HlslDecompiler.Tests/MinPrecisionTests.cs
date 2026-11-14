using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HlslDecompiler.Tests;

/// <summary>
/// With <see cref="Hlsl.HlslAstWriter.KeepsMinimumPrecision"/>, the recompile computes
/// at minimum precision as often as the original: as many operands carry a
/// min16float, min10float, min16int or min16uint precision.
/// </summary>
[TestFixture]
[Category("Recompile")]
// Each case writes to its own path and shares nothing.
[Parallelizable(ParallelScope.All)]
public class MinPrecisionTests
{
    // A listed shader that starts keeping its precision fails, so its entry gets removed.
    private static readonly Dictionary<string, string> KnownDifferences = new()
    {
        ["ps_5_0/min16_sample_temp"] =
            "The sample's destination is min16 because the original's texture was "
            + "Texture2D<min16float4>, which neither the bytecode nor the reflection "
            + "records (both say float4). The decompilation samples a float4 texture, "
            + "and fxc converts it into the min16float4 variable instead.",
        ["cs_5_0/min16_groupshared"] =
            "The sum reads a full precision load beside a min16 one, which the "
            + "original converted down ({def32 as min16f}). Without a cast on that "
            + "operand, HLSL promotes the sum to float.",
    };

    public static IEnumerable<TestCaseData> Shaders()
    {
        foreach (TestCaseData data in RecompileTests.Shaders())
        {
            yield return new TestCaseData(data.Arguments)
                .SetName($"KeepsMinPrecision({data.Arguments[0]},{data.Arguments[1]})");
        }
    }

    [TestCaseSource(nameof(Shaders))]
    public void MinPrecisionIsKept(string profile, string baseFilename)
    {
        if (!ShaderCompiler.IsAvailable)
        {
            Assert.Ignore(ShaderCompiler.UnavailableMessage);
        }

        ShaderModel original = RecompileTests.ReadShaderModel(
            Path.Combine("CompiledShaders", profile, baseFilename + ".fxc"));
        int expected = CountMinPrecisionOperands(original);
        if (expected == 0)
        {
            Assert.Ignore("No minimum precision operands.");
        }

        string hlslFilename = Path.Combine("MinPrecision", profile, baseFilename + ".fx");
        string objectFilename = Path.ChangeExtension(hlslFilename, ".fxo");
        FileUtil.MakeFolder(hlslFilename);
        new Hlsl.HlslAstWriter(original) { KeepsMinimumPrecision = true }.Write(hlslFilename);
        string failure = RecompileTests.RunFxc(profile, hlslFilename, objectFilename);
        Assert.That(failure, Is.Null, $"The output does not compile:\n{failure}");
        int actual = CountMinPrecisionOperands(RecompileTests.ReadShaderModel(objectFilename));

        string key = $"{profile}/{baseFilename}";
        if (KnownDifferences.TryGetValue(key, out string reason))
        {
            Assert.That(actual, Is.Not.EqualTo(expected),
                $"{key} now keeps its minimum precision. Remove it from {nameof(KnownDifferences)}.");
            Assert.Ignore($"Known: {reason}");
        }
        Assert.That(actual, Is.EqualTo(expected),
            $"The recompile has {actual} minimum precision operands where the original has {expected}.");
    }

    private static int CountMinPrecisionOperands(ShaderModel shader)
    {
        return shader.Instructions.OfType<D3D10Instruction>()
            .Where(instruction => !instruction.Opcode.IsDeclaration() && instruction.Opcode != D3D10Opcode.CustomData)
            .Sum(instruction => Enumerable.Range(0, instruction.OperandTokens.OperandCount)
                .Count(operand => instruction.GetOperandMinPrecision(operand) != 0));
    }
}
