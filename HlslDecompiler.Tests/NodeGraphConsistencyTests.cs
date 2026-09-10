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
/// Every edge in the node graph is stored twice - once in the consumer's Inputs and
/// once in the producer's Outputs - and a great deal of the decompiler reads the
/// second one. Whether a value is used at all, whether it leaves a statement, and
/// which nodes a rewiring has to visit are all decided from Outputs.
///
/// So the two have to agree. A stale entry reads as a use that is not there, which
/// keeps dead values alive and, once shared subexpressions are named from the same
/// count, would name things nothing shares.
/// </summary>
[TestFixture]
public class NodeGraphConsistencyTests
{
    // Its own names: taking RecompileTests.Shaders() as it stands would report these
    // failures as Recompile(...) too.
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
        HlslAst ast = InstructionParser.Parse(shader);

        // Parsing alone leaves the graph consistent; it is the finalizer that rewires
        // it, so checking before this runs proves nothing.
        StatementFinalizer.Finalize(ast.Statements, true,
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
