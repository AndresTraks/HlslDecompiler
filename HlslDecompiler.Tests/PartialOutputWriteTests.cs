using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using static HlslDecompiler.Tests.ShaderAssembler;

namespace HlslDecompiler.Tests;

// A statement that writes part of an output register has to say so. Assigning a
// scalar or a short vector to the whole member broadcasts it over the components
// the write leaves alone, and packs the components it did write into the register's
// first slots. The binary this fixture is built from writes one output across two
// statements - the position's xyz in one and its w after the loop - and the latest
// fxc never reproduces that split when recompiling HLSL, so no golden can hold one.
// Assembled by hand: the branch writes only .xy of the position.
public class PartialOutputWriteTests
{
    [Test]
    public void NamesTheComponentsAPartialOutputWriteTouches()
    {
        ShaderModel shader = Assemble(ShaderType.Vertex, 3, [
            Declaration(DeclUsage.Position, RegisterType.Input, 0),    // dcl_position v0
            Declaration(DeclUsage.Position, RegisterType.Output, 0),   // dcl_position o0
            Declaration(DeclUsage.TexCoord, RegisterType.Output, 1),   // dcl_texcoord o1
            Constant(0, 1.0f, 2.0f, 3.0f, 4.0f),
            Instruction(Opcode.Mov, Destination(RegisterType.Temp, 0),
                Source(RegisterType.Input, 0)),
            Instruction(Opcode.Mov, Destination(RegisterType.Output, 0),
                Source(RegisterType.Temp, 0)),                          // mov o0, r0
            Instruction(Opcode.If, Source(RegisterType.Temp, 0)),       // if r0.z
            Instruction(Opcode.Mov, Destination(RegisterType.Output, 0, 0x3),
                Source(RegisterType.Const, 0)),                         // mov o0.xy, c0.xy
            Instruction(Opcode.Endif),
            Instruction(Opcode.Mov, Destination(RegisterType.Output, 1),
                Source(RegisterType.Temp, 0)),                          // mov o1, r0
            Instruction(Opcode.End),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(WriteAsm(shader), Is.EqualTo("""
                vs_3_0
                dcl_position v0
                dcl_position o0
                dcl_texcoord o1
                def c0, 1, 2, 3, 4
                mov r0, v0
                mov o0, r0
                if r0.z
                mov o0.xy, c0.xy
                endif
                mov o1, r0

                """));
            Assert.That(WriteHlslAst(shader), Is.EqualTo("""
                struct VS_OUT
                {
                	float4 position : POSITION;
                	float4 texcoord : TEXCOORD;
                };

                VS_OUT main(float4 position : POSITION)
                {
                	VS_OUT o;

                	o.position = position;
                	if (position.x) {
                		o.position.xy = float2(1, 2);
                	}
                	o.texcoord = position;

                	return o;
                }

                """));
        });
    }
}
