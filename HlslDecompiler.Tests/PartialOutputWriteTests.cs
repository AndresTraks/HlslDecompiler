using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using static HlslDecompiler.Tests.ShaderAssembler;

namespace HlslDecompiler.Tests;

// A partial output write must name its components, or assigning to the whole
// member broadcasts and misplaces them. fxc never splits an output write like this,
// so it is hand-assembled: the branch writes only .xy of the position.
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
