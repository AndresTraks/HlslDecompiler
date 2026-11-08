using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using static HlslDecompiler.Tests.ShaderAssembler;

namespace HlslDecompiler.Tests;

// A lane a nested if joins, inside a branch that goes on to write the register's
// other lanes. The statements after the nested if carry the join it replaced, so
// the outer if's unification missed the lane: the nested if kept a variable of
// its own, and the outer if's variable read the lane unassigned on that path.
public class NestedIfLaneJoinTests
{
    private const uint X = 0x00;    // .x replicated
    private const uint Y = 0x55;    // .y replicated
    private const uint W = 0xFF;    // .w replicated
    private const uint XYXY = 0x44;

    [Test]
    public void LaneJoinedByANestedIfStaysOnTheOuterVariable()
    {
        ShaderModel shader = Assemble(ShaderType.Pixel, 3, [
            Constant(0, 0f, 1f, 0f, 0f),
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 0),
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 1, 1),
            Instruction(Opcode.IfC, IfComparison.GE, V(1, Y), C(0, X)),
            Instruction(Opcode.Mov, Rd(0, 0x3), C(0)),                        // r0.xy = (0, 1)
            Instruction(Opcode.Else),
            Instruction(Opcode.IfC, IfComparison.GE, V(1, W), C(0, X)),
            Instruction(Opcode.Mov, Rd(0, 0x2), C(0, Y)),                     // r0.y = 1
            Instruction(Opcode.Else),
            Instruction(Opcode.Mov, Rd(0, 0x2), V(0, W)),                     // r0.y = i.w
            Instruction(Opcode.Endif),
            Instruction(Opcode.Mov, Rd(0, 0x1), V(0, X)),                     // r0.x after it
            Instruction(Opcode.Endif),
            Instruction(Opcode.Mov, Destination(RegisterType.ColorOut, 0, 0x3), R(0)),
            Instruction(Opcode.Mov, Destination(RegisterType.ColorOut, 0, 0xC), C(0, XYXY)),
            Instruction(Opcode.End),
        ]);

        SelfUpdatingBranchJoinTests.AssertDecompiles(shader, """
            struct PS_IN
            {
            	float4 texcoord : TEXCOORD;
            	float4 texcoord1 : TEXCOORD1;
            };

            float4 main(PS_IN i) : COLOR
            {
            	float2 t0;
            	if (i.texcoord1.y >= 0) {
            		t0 = float2(0, 1);
            	} else {
            		if (i.texcoord1.w >= 0) {
            			t0.y = 1;
            		} else {
            			t0.y = i.texcoord.w;
            		}
            		t0.x = i.texcoord.x;
            	}
            	return float4(t0, 0, 1);
            }

            """);
    }
}
