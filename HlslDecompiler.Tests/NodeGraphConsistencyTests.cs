using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using HlslDecompiler.Hlsl.FlowControl;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HlslDecompiler.Tests;

/// <summary>
/// Every edge is stored in both the consumer's Inputs and the producer's Outputs,
/// and they must agree: a stale Outputs entry is a phantom use.
/// </summary>
[TestFixture]
public class NodeGraphConsistencyTests
{
    // Renamed so failures are not reported as Recompile(...).
    public static IEnumerable<TestCaseData> Shaders()
    {
        foreach (TestCaseData data in RecompileTests.Shaders())
        {
            yield return new TestCaseData(data.Arguments)
                .SetName($"NodeGraph({data.Arguments[0]},{data.Arguments[1]})");
        }
    }

    [TestCaseSource(nameof(Shaders))]
    public void EveryEdgeIsRecordedBothWays(string profile, string baseFilename)
    {
        string filename = Path.Combine("CompiledShaders", profile, baseFilename + ".fxc");
        ShaderModel shader = ReadShader(filename);

        // A hull shader is one graph per phase; parsed whole it would throw.
        foreach ((ShaderModel phase, HlslAst ast) in Phases(shader))
        {
            CheckGraph(phase, ast);
        }
    }

    private static IEnumerable<(ShaderModel Shader, HlslAst Ast)> Phases(ShaderModel shader)
    {
        if (shader.Type != ShaderType.Hull)
        {
            yield return (shader, InstructionParser.Parse(shader));
            yield break;
        }
        HullShaderAst hull = InstructionParser.ParseHullShader(shader);
        foreach (HullPhase phase in new[] { hull.ControlPoint, hull.PatchConstant })
        {
            if (phase != null)
            {
                yield return (phase.Shader, phase.Ast);
            }
        }
    }

    private static void CheckGraph(ShaderModel shader, HlslAst ast)
    {
        // The finalizer is what rewires the graph. Compute and geometry shaders
        // have no return value to ask it for.
        bool hasReturnValue = shader.Type is not ShaderType.Compute
            and not ShaderType.Geometry
            && ast.RegisterState.MethodOutputRegisters.Count != 0;
        StatementFinalizer.Finalize(ast.Statements, hasReturnValue,
            ast.RegisterState.MethodOutputRegisters.Count > 1
                || shader.Type == ShaderType.Geometry,
            shader.Instructions.Count != 0 && shader.Instructions[0] is D3D10Instruction
                ? new IntegerOperandAnalysis(shader)
                : null);

        var mismatches = new List<string>();
        var seen = HlslTreeNode.NewNodeSet();
        var stack = new Stack<HlslTreeNode>();
        new StatementVisitor(ast.Statements).Visit(statement =>
        {
            foreach (HlslTreeNode value in statement.Outputs.Values)
            {
                stack.Push(value);
            }
        });

        while (stack.Count != 0)
        {
            HlslTreeNode node = stack.Pop();
            if (!seen.Add(node))
            {
                continue;
            }
            foreach (HlslTreeNode input in HlslTreeNode.TraversableInputs(node))
            {
                int asInput = node.Inputs.Count(i => ReferenceEquals(i, input));
                int asOutput = input.Outputs.Count(o => ReferenceEquals(o, node));
                if (asInput != asOutput)
                {
                    mismatches.Add(
                        $"{input.GetType().Name} -> {node.GetType().Name}: "
                        + $"{asInput} in Inputs, {asOutput} in Outputs");
                }
                stack.Push(input);
            }
        }

        Assert.That(mismatches, Is.Empty,
            string.Join(System.Environment.NewLine, mismatches.Distinct()));
    }

    private static ShaderModel ReadShader(string filename)
    {
        using var stream = File.Open(
            Path.GetFullPath(filename), FileMode.Open, FileAccess.Read, FileShare.Read);

        bool isDxbc;
        using (var peek = new BinaryReader(stream, new UTF8Encoding(), true))
        {
            isDxbc = peek.ReadUInt32() == 0x43425844;
        }
        stream.Position = 0;

        if (isDxbc)
        {
            using var dxbcReader = new DxbcReader(stream, true);
            return dxbcReader.ReadShader();
        }
        using var shaderReader = new ShaderReader(stream, true);
        return shaderReader.ReadShader();
    }
}
