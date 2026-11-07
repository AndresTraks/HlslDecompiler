using System.Collections.Generic;
using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using static HlslDecompiler.Tests.ShaderAssembler;

namespace HlslDecompiler.Tests;

// A chain of values each read twice: written out with nothing named, its text
// doubles at every link. The writer must name the links without first compiling
// that text to count what repeats.
public class DoublingChainTests
{
    private const uint X = 0x00;    // .x replicated
    private const uint Y = 0x55;    // .y replicated

    // Past the writer's budget, so it names without measuring (about 0.3 s);
    // measuring the text instead takes over 15 s here and doubles per link.
    private const int Links = 19;

    [Test]
    [MaxTime(5_000)]
    public void ClampedCountdownIsNamedLinkByLink()
    {
        var instructions = new List<uint[]>
        {
            Constant(0, 0f, -1f, 0f, 0f),
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 0),
            Instruction(Opcode.Mov, Rd(0, 0x1), V(0, X)),
        };
        for (int i = 0; i < Links; i++)
        {
            // r0.x = max(r0.x, 0) - 1, as a select reading r0.x twice.
            instructions.Add(Instruction(Opcode.Cmp, Rd(0, 0x1), R(0, X, SourceModifier.Negate), C(0, X), R(0, X)));
            instructions.Add(Instruction(Opcode.Add, Rd(0, 0x1), R(0, X), C(0, Y)));
        }
        instructions.Add(Instruction(Opcode.Mov, Destination(RegisterType.ColorOut, 0), R(0, X)));
        instructions.Add(Instruction(Opcode.End));

        ShaderModel shader = Assemble(ShaderType.Pixel, 3, instructions);

        // One variable per link, the last returned.
        var expected = new System.Text.StringBuilder();
        expected.Append("float4 main(float4 texcoord : TEXCOORD) : COLOR\n{\n");
        expected.Append("\tfloat t0 = (-texcoord.x >= 0 ? 0 : texcoord.x) - 1;\n");
        for (int i = 1; i < Links - 1; i++)
        {
            expected.Append($"\tfloat t{i} = (-t{i - 1} >= 0 ? 0 : t{i - 1}) - 1;\n");
        }
        expected.Append($"\treturn (-t{Links - 2} >= 0 ? 0 : t{Links - 2}) - 1;\n}}\n");
        SelfUpdatingBranchJoinTests.AssertDecompiles(shader, expected.ToString());
    }
}
