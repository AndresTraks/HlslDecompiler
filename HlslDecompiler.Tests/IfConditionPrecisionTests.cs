using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using static HlslDecompiler.Tests.ShaderAssembler;

namespace HlslDecompiler.Tests;

// A D3D9 `if_<rel>` hands the AST writer one comparison per component, but the
// operand is usually one component read through a swizzle, so all four carry the
// same test. When that operand is a partial-precision value - the `_pp` this binary
// carries on the writing instruction - each component of the condition is cast to
// `half` sized to the group, so four of them came out `(half4)`, and comparing
// those is X3019 rather than the scalar test the instruction is. fxc folds this
// idiom (`cmp -r,1,0` then `if_ne r,-r`) away, so no HLSL source produces it:
// assembled by hand.
public class IfConditionPrecisionTests
{
    [Test]
    public void ScalarizesAPartialPrecisionBranchTest()
    {
        const uint X = 0x00; // .x swizzle, replicated
        const uint Y = 0x55; // .y swizzle, replicated
        const uint Z = 0xAA; // .z swizzle, replicated
        ShaderModel shader = Assemble(ShaderType.Pixel, 3, [
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 0),   // dcl_texcoord v0
            Constant(0, 0.0f, 1.0f, 0.0f, 0.0f),                      // def c0, 0, 1, 0, 0
            Instruction(Opcode.Mov, Destination(RegisterType.Temp, 0),
                Source(RegisterType.Input, 0)),                       // mov r0, v0
            Instruction(Opcode.Mov, Destination(RegisterType.Temp, 1),
                Source(RegisterType.Const, 0)),                       // mov r1, c0
            // r2.z = (half)(r0.x + r0.y) - the sum is one value read across four
            // components, which is what used to widen the cast below.
            Instruction(Opcode.Add, Destination(RegisterType.Temp, 2, 0x4, ResultModifier.PartialPrecision),
                Source(RegisterType.Temp, 0, SourceModifier.None, X),
                Source(RegisterType.Temp, 0, SourceModifier.None, Y)),
            // cmp r2.z, -r2.z, c0.y, c0.x  =>  r2.z = (-r2.z >= 0) ? 1 : 0
            Instruction(Opcode.Cmp, Destination(RegisterType.Temp, 2, 0x4),
                Source(RegisterType.Temp, 2, SourceModifier.Negate, Z),
                Source(RegisterType.Const, 0, SourceModifier.None, 0x55),
                Source(RegisterType.Const, 0, SourceModifier.None, 0x00)),
            // if_ne r2.z, -r2.z - r2.z != -r2.z, i.e. r2.z != 0, folded to <= 0.
            Instruction(Opcode.IfC, IfComparison.NE,
                Source(RegisterType.Temp, 2, SourceModifier.None, Z),
                Source(RegisterType.Temp, 2, SourceModifier.Negate, Z)),
            Instruction(Opcode.Mov, Destination(RegisterType.ColorOut, 0),
                Source(RegisterType.Temp, 0)),
            Instruction(Opcode.Else),
            Instruction(Opcode.Mov, Destination(RegisterType.ColorOut, 0),
                Source(RegisterType.Temp, 1)),
            Instruction(Opcode.Endif),
            Instruction(Opcode.End),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(WriteAsm(shader), Is.EqualTo("""
                ps_3_0
                dcl_texcoord v0
                def c0, 0, 1, 0, 0
                mov r0, v0
                mov r1, c0
                add_pp r2.z, r0.x, r0.y
                cmp r2.z, -r2.z, c0.y, c0.x
                if_ne r2.z, -r2.z
                mov oC0, r0
                else
                mov oC0, r1
                endif

                """));
            Assert.That(WriteHlslAst(shader), Is.EqualTo("""
                float4 main(float4 texcoord : TEXCOORD) : COLOR
                {
                	if ((half)(texcoord.x + texcoord.y) <= 0) {
                		return texcoord;
                	} else {
                		return float4(0, 1, 0, 0);
                	}
                }

                """));
        });
    }
}
