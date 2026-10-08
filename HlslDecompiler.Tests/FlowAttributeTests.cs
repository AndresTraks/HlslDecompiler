using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HlslDecompiler.Tests;

/// <summary>
/// That with <see cref="HlslWriter.WritesFlowAttributes"/> a decompilation branches
/// and loops where its shader did: recompiled, it holds as many ifs and as many
/// loops as the original. Without the attributes fxc flattens an if it can and
/// unrolls a loop it can count, which computes the same and is the shape the
/// default output leaves it free to choose; this is what the option is for.
/// </summary>
[TestFixture]
[Category("Recompile")]
// Every case is independent: it reads one .fxc, writes its output under a path
// named after its profile and shader, and shares nothing with the others.
[Parallelizable(ParallelScope.All)]
public class FlowAttributeTests
{
    /// <summary>
    /// Shaders whose recompile branches or loops otherwise even with the attributes,
    /// by writer, and why.
    /// </summary>
    private static readonly Dictionary<string, (string Writer, string Reason)[]> KnownDifferences = new()
    {
        ["ps_4_0/conditional_return"] = [("ast",
            "One if more, which is one early return spelled the other way: the "
            + "original moves its colour into the output and then returns with "
            + "retc_nz, and `if (t < texcoord.x) return a;` recompiles as an if around "
            + "the move and a ret. The same return either way; the if this test "
            + "counts as a branch has a move in it besides the ret."),
            ("instruction",
            "The same early return: the instruction writer keeps the move in front "
            + "of `if (r0.x != 0) return o;`, and fxc puts it inside the if it "
            + "compiles the return to all the same.")],
    };

    public static IEnumerable<TestCaseData> Shaders()
    {
        foreach (TestCaseData data in RecompileTests.Shaders())
        {
            yield return new TestCaseData(data.Arguments)
                .SetName($"KeepsFlow({data.Arguments[0]},{data.Arguments[1]})");
        }
    }

    [TestCaseSource(nameof(Shaders))]
    public void FlowAttributesKeepTheBranchesAndLoops(string profile, string baseFilename)
    {
        if (RecompileTests.FxcPath == null)
        {
            Assert.Ignore("fxc.exe not found. Install the Windows SDK to run recompilation tests.");
        }

        ShaderModel original = RecompileTests.ReadShaderModel(
            Path.Combine("CompiledShaders", profile, baseFilename + ".fxc"));
        (int ifs, int loops) expected = CountFlow(original);
        if (expected == (0, 0))
        {
            Assert.Ignore("Neither branches nor loops.");
        }

        var differences = new List<(string Writer, string Message)>();
        foreach ((string writer, Func<ShaderModel, HlslWriter> create) in Writers())
        {
            string hlslFilename = Path.Combine("FlowAttributes", profile + "_" + writer, baseFilename + ".fx");
            string objectFilename = Path.ChangeExtension(hlslFilename, ".fxo");
            FileUtil.MakeFolder(hlslFilename);
            create(original).Write(hlslFilename);
            string failure = RecompileTests.RunFxc(profile, hlslFilename, objectFilename);
            if (failure != null)
            {
                differences.Add((writer, $"The {writer} writer's output does not compile:\n{failure}"));
                continue;
            }
            (int ifs, int loops) actual = CountFlow(RecompileTests.ReadShaderModel(objectFilename));
            if (actual != expected)
            {
                differences.Add((writer, $"The {writer} writer's recompile has {actual.ifs} ifs and "
                    + $"{actual.loops} loops where the original has {expected.ifs} and {expected.loops}."));
            }
        }

        string key = $"{profile}/{baseFilename}";
        List<string> unexpected = [.. differences.Select(d => d.Message)];
        if (KnownDifferences.TryGetValue(key, out (string Writer, string Reason)[] known))
        {
            foreach ((string writer, _) in known)
            {
                Assert.That(differences.Any(d => d.Writer == writer), Is.True,
                    $"The {writer} writer now keeps {key}'s flow. Remove it from {nameof(KnownDifferences)}.");
            }
            unexpected = [.. differences.Where(d => !known.Any(k => k.Writer == d.Writer)).Select(d => d.Message)];
        }
        Assert.That(unexpected, Is.Empty, string.Join(Environment.NewLine, unexpected));
        if (known != null)
        {
            Assert.Ignore(string.Join(" ", known.Select(k => $"Known in the {k.Writer} writer: {k.Reason}")));
        }
    }

    private static IEnumerable<(string Name, Func<ShaderModel, HlslWriter> Create)> Writers()
    {
        yield return ("ast", s => new Hlsl.HlslAstWriter(s) { WritesFlowAttributes = true });
        yield return ("instruction", s => new HlslSimpleWriter(s) { WritesFlowAttributes = true });
    }

    // The ifs and the loops in the bytecode, by the opcodes that open them. An if
    // whose whole body is one break, continue, return or discard is a conditional
    // jump, not a branch: fxc writes `if (c) break;` as a breakc or as if, break,
    // endif as it likes, and inside a [loop] it picks the second.
    private static (int Ifs, int Loops) CountFlow(ShaderModel shader)
    {
        IList<Instruction> instructions = shader.Instructions;
        int ifs = 0, loops = 0;
        for (int i = 0; i < instructions.Count; i++)
        {
            switch (instructions[i])
            {
                case D3D9Instruction d3d9:
                    if (d3d9.Opcode is Opcode.If or Opcode.IfC && !IsJump(instructions, i)) ifs++;
                    if (d3d9.Opcode is Opcode.Loop or Opcode.Rep) loops++;
                    break;
                case D3D10Instruction d3d10:
                    if (d3d10.Opcode == D3D10Opcode.If && !IsJump(instructions, i)) ifs++;
                    if (d3d10.Opcode == D3D10Opcode.Loop) loops++;
                    break;
            }
        }
        return (ifs, loops);
    }

    private static bool IsJump(IList<Instruction> instructions, int ifIndex)
    {
        if (ifIndex + 2 >= instructions.Count)
        {
            return false;
        }
        return (instructions[ifIndex + 1], instructions[ifIndex + 2]) switch
        {
            (D3D9Instruction body, D3D9Instruction end) =>
                body.Opcode is Opcode.Break or Opcode.Ret or Opcode.TexKill && end.Opcode == Opcode.Endif,
            (D3D10Instruction body, D3D10Instruction end) =>
                body.Opcode is D3D10Opcode.Break or D3D10Opcode.Continue or D3D10Opcode.Ret
                    or D3D10Opcode.Discard
                && end.Opcode == D3D10Opcode.EndIf,
            _ => false,
        };
    }
}
