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

    // A value a loop carries through nested ifs leaves it as the join of the outer
    // if, over the loop's variable: an if after the loop that writes it in one
    // branch still holds that variable, rather than one of its own left unassigned
    // on the other path.
    [Test]
    public void ValueCarriedThroughNestedIfsStaysOnTheLoopVariable()
    {
        const uint Z = 0xAA;
        ShaderModel shader = Assemble(ShaderType.Pixel, 3, [
            Constant(0, 0f, 1f, -0.5f, 0.5f),
            ConstantInt(0, 255, 0, 0, 0),
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 0),
            Instruction(Opcode.Mov, Rd(0, 0x2), V(0, X)),
            Instruction(Opcode.Loop, Source(RegisterType.Loop, 0), Source(RegisterType.ConstInt, 0)),
            Instruction(Opcode.IfC, IfComparison.NE, V(0, W), C(0, X)),
            Instruction(Opcode.IfC, IfComparison.NE, V(0, Y), C(0, X)),
            Instruction(Opcode.Add, Rd(0, 0x2), R(0, Y), C(0, Z)),            // r0.y -= 0.5
            Instruction(Opcode.Endif),
            Instruction(Opcode.Endif),
            Instruction(Opcode.BreakC, IfComparison.EQ, V(0, W), C(0, X)),
            Instruction(Opcode.EndLoop),
            Instruction(Opcode.IfC, IfComparison.NE, V(0, Z), C(0, X)),
            Instruction(Opcode.Mov, Rd(0, 0x2), C(0, W)),                     // r0.y = 0.5
            Instruction(Opcode.Endif),
            Instruction(Opcode.Mov, Destination(RegisterType.ColorOut, 0), R(0, Y)),
            Instruction(Opcode.End),
        ]);

        SelfUpdatingBranchJoinTests.AssertDecompiles(shader, """
            float4 main(float4 texcoord : TEXCOORD) : COLOR
            {
            	float t0 = texcoord.x;
            	for (int i = 0; i < 255; i++) {
            		if (texcoord.w != 0) {
            			if (texcoord.y != 0) {
            				t0 = t0 - 0.5;
            			}
            		}
            		if (texcoord.w == 0) {
            			break;
            		}
            	}
            	if (texcoord.z != 0) {
            		t0 = 0.5;
            	}
            	return t0;
            }

            """);
    }
}
