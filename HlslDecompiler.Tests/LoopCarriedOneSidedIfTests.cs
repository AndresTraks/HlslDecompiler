using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using static HlslDecompiler.Tests.ShaderAssembler;

namespace HlslDecompiler.Tests;

// A loop carries a flag that a one-sided if both tests and overwrites, and a
// break after the if reads it. With no else, the value at the join is the new
// flag or, when the if is skipped, the one carried in.
public class LoopCarriedOneSidedIfTests
{
    private const uint X = 0x00;    // .x replicated
    private const uint Y = 0x55;    // .y replicated
    private const uint Z = 0xAA;    // .z replicated
    private const uint W = 0xFF;    // .w replicated

    private static uint Oc(uint mask = 0xF) => Destination(RegisterType.ColorOut, 0, mask);

    [Test]
    public void VectorAdvancedInOneSidedIfReadByTheNextIteration()
    {
        // r4 advances on every lane, and r4.z is read only before the advance, by
        // the next iteration: its update must stay.
        ShaderModel shader = Assemble(ShaderType.Pixel, 3, [
            Constant(0, 0f, 1f, 0.5f, 0f),
            Constant(1, 0.25f, 0.5f, 0.75f, 1f),
            ConstantInt(0, 255, 0, 0, 0),
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 0),
            Instruction(Opcode.Mov, Rd(1, 0x1), V(0, X)),                     // counter
            Instruction(Opcode.Mov, Rd(4), V(0)),                             // carried vector
            Instruction(Opcode.Mov, Rd(5, 0x1), C(0, X)),                     // sum
            Instruction(Opcode.Cmp, Rd(2, 0x1), V(0, Y), C(0, Y), C(0, X)),
            Instruction(Opcode.Cmp, Rd(3, 0x1), V(0, Z), C(0, Y), C(0, X)),
            Instruction(Opcode.Mul, Rd(0, 0x1), R(2, X), R(3, X)),            // flag
            Instruction(Opcode.IfC, IfComparison.NE, R(0, X), C(0, X)),
            Instruction(Opcode.Add, Rd(1, 0x1), R(1, X), C(0, Y)),
            Instruction(Opcode.Endif),
            Instruction(Opcode.Rep, Source(RegisterType.ConstInt, 0)),
            Instruction(Opcode.IfC, IfComparison.NE, R(0, X), C(0, X)),
            Instruction(Opcode.Dp3, Rd(6, 0x1), R(4, 0xF4), C(1)),            // reads r4.xyw
            Instruction(Opcode.Mad, Rd(5, 0x1), R(6, X), R(4, Z), R(5, X)),   // reads r4.z
            Instruction(Opcode.Add, Rd(4), C(1, 0xB4), R(4)),                 // r4 += c1.xywz
            Instruction(Opcode.Add, Rd(1, 0x1), R(1, X), C(0, Z, SourceModifier.Negate)),
            Instruction(Opcode.Cmp, Rd(0, 0x1), R(1, X, SourceModifier.Negate), C(0, X), C(0, Y)),
            Instruction(Opcode.Endif),
            Instruction(Opcode.BreakC, IfComparison.EQ, R(0, X), C(0, X)),
            Instruction(Opcode.EndRep),
            Instruction(Opcode.IfC, IfComparison.NE, R(3, X), C(0, X)),       // after the loop, a
            Instruction(Opcode.Mov, Rd(1, 0x1), C(0, Z)),                     // one-sided if leaves
            Instruction(Opcode.Endif),                                        // the counter or not
            Instruction(Opcode.Add, Oc(), R(5, X), R(1, X)),
            Instruction(Opcode.End),
        ]);

        SelfUpdatingBranchJoinTests.AssertDecompiles(shader, """
            float4 main(float4 texcoord : TEXCOORD) : COLOR
            {
            	float t1 = texcoord.x;
            	float4 t2 = texcoord;
            	float t3 = 0;
            	float t0 = texcoord.z >= 0 ? 1 : 0;
            	float t4 = (texcoord.y >= 0 ? 1 : 0) * t0;
            	if (t4 != 0) {
            		t1 = t1 + 1;
            	}
            	for (int i = 0; i < 255; i++) {
            		if (t4 != 0) {
            			t1 = t1 - 0.5;
            			t3 = t3 + dot(float3(0.25, 0.5, 0.75), t2.xyw) * t2.z;
            			t2 = t2 + float4(0.25, 0.5, 1, 0.75);
            			t4 = -t1 >= 0 ? 0 : 1;
            		}
            		if (t4 == 0) {
            			break;
            		}
            	}
            	if (t0 != 0) {
            		t1 = 0.5;
            	}
            	return t1 + t3;
            }

            """);
    }

    [Test]
    public void LaneAdvancedInLoopKeptByOneSidedIfAfterIt()
    {
        // r2.y enters the loop in an assignment and leaves it in the loop's
        // variable; the one-sided if after the loop must overwrite that variable,
        // not a new one left unset when the if is skipped.
        ShaderModel shader = Assemble(ShaderType.Pixel, 3, [
            Constant(0, 0f, 1f, 0.5f, -0.5f),
            ConstantInt(0, 255, 0, 0, 0),
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 0),
            Instruction(Opcode.Mov, Rd(2, 0x2), V(0, X)),
            Instruction(Opcode.Mov, Rd(2, 0x8), V(0, Z)),
            Instruction(Opcode.Cmp, Rd(0, 0x1), V(0, Y), C(0, Y), C(0, X)),
            Instruction(Opcode.Cmp, Rd(3, 0x1), V(0, Z), C(0, Y), C(0, X)),
            Instruction(Opcode.Rep, Source(RegisterType.ConstInt, 0)),
            Instruction(Opcode.IfC, IfComparison.NE, R(0, X), C(0, X)),
            Instruction(Opcode.Add, Rd(2, 0xA), R(2), C(0, 0x7F)),           // r2.yw += (-0.5, 1)
            Instruction(Opcode.Cmp, Rd(0, 0x1), R(2, Y, SourceModifier.Negate), C(0, X), C(0, Y)),
            Instruction(Opcode.Endif),
            Instruction(Opcode.BreakC, IfComparison.EQ, R(0, X), C(0, X)),
            Instruction(Opcode.EndRep),
            Instruction(Opcode.IfC, IfComparison.NE, R(3, X), C(0, X)),
            Instruction(Opcode.Mov, Rd(2, 0x2), C(0, Z)),
            Instruction(Opcode.Endif),
            Instruction(Opcode.Add, Oc(), R(2, Y), R(2, W)),
            Instruction(Opcode.End),
        ]);

        SelfUpdatingBranchJoinTests.AssertDecompiles(shader, """
            float4 main(float4 texcoord : TEXCOORD) : COLOR
            {
            	float t0 = texcoord.y >= 0 ? 1 : 0;
            	float t1 = texcoord.z >= 0 ? 1 : 0;
            	float2 t2 = texcoord.xz;
            	for (int i = 0; i < 255; i++) {
            		if (t0 != 0) {
            			t2 = t2 + float2(-0.5, 1);
            			t0 = -t2.x >= 0 ? 0 : 1;
            		}
            		if (t0 == 0) {
            			break;
            		}
            	}
            	if (t1 != 0) {
            		t2.x = 0.5;
            	}
            	return t2.x + t2.y;
            }

            """);
    }

    [Test]
    public void FlagAssignedInOneSidedIfAndReadByTheBreak()
    {
        ShaderModel shader = Assemble(ShaderType.Pixel, 3, [
            Constant(0, 0f, 1f, 0.5f, 0f),
            ConstantInt(0, 255, 0, 0, 0),
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 0),
            Instruction(Opcode.Mov, Rd(1, 0x1), V(0, X)),                     // counter
            Instruction(Opcode.Cmp, Rd(2, 0x1), V(0, Y), C(0, Y), C(0, X)),   // v.y >= 0
            Instruction(Opcode.Cmp, Rd(3, 0x1), V(0, Z), C(0, Y), C(0, X)),   // v.z >= 0
            Instruction(Opcode.Mul, Rd(0, 0x1), R(2, X), R(3, X)),            // flag = both
            Instruction(Opcode.IfC, IfComparison.NE, R(0, X), C(0, X)),       // tested before the loop
            Instruction(Opcode.Add, Rd(1, 0x1), R(1, X), C(0, Y)),
            Instruction(Opcode.Endif),
            Instruction(Opcode.Rep, Source(RegisterType.ConstInt, 0)),
            Instruction(Opcode.IfC, IfComparison.NE, R(0, X), C(0, X)),       // if (flag != 0)
            Instruction(Opcode.Add, Rd(1, 0x1), R(1, X), C(0, Z, SourceModifier.Negate)),
            Instruction(Opcode.Cmp, Rd(0, 0x1), R(1, X, SourceModifier.Negate), C(0, X), C(0, Y)),
            Instruction(Opcode.Endif),                                        //   flag = counter > 0
            Instruction(Opcode.BreakC, IfComparison.EQ, R(0, X), C(0, X)),    // break if flag == 0
            Instruction(Opcode.EndRep),
            Instruction(Opcode.Mov, Oc(), R(1, X)),
            Instruction(Opcode.End),
        ]);

        SelfUpdatingBranchJoinTests.AssertDecompiles(shader, """
            float4 main(float4 texcoord : TEXCOORD) : COLOR
            {
            	float t0 = texcoord.x;
            	float t1 = (texcoord.y >= 0 ? 1 : 0) * (texcoord.z >= 0 ? 1 : 0);
            	if (t1 != 0) {
            		t0 = t0 + 1;
            	}
            	for (int i = 0; i < 255; i++) {
            		if (t1 != 0) {
            			t0 = t0 - 0.5;
            			t1 = -t0 >= 0 ? 0 : 1;
            		}
            		if (t1 == 0) {
            			break;
            		}
            	}
            	return t0;
            }

            """);
    }
}
