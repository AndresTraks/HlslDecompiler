using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using static HlslDecompiler.Tests.ShaderAssembler;

namespace HlslDecompiler.Tests;

// fxc has no intrinsic for dst and rejects asm{} (X2001/X3000), so it is
// hand-assembled here.
public class DstInstructionTests
{
    [Test]
    public void DecompilesDstToItsFourComponents()
    {
        ShaderModel shader = Assemble(ShaderType.Pixel, 3, [
            // ps_3_0 texcoords are Input (v#), not Texture (t#).
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 0),     // dcl_texcoord v0
            Constant(0, 1.0f, 2.0f, 3.0f, 4.0f),                        // def c0, 1, 2, 3, 4
            Instruction(Opcode.Dst,
                Destination(RegisterType.ColorOut, 0),                  // dst oC0, v0, c0
                Source(RegisterType.Input, 0),
                Source(RegisterType.Const, 0)),
            // No ret: fxc drops it in a pixel shader.
            Instruction(Opcode.End),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(WriteAsm(shader), Is.EqualTo("""
                ps_3_0
                dcl_texcoord v0
                def c0, 1, 2, 3, 4
                dst oC0, v0, c0

                """));
            Assert.That(WriteHlsl(shader), Is.EqualTo("""
                float4 main(float4 texcoord : TEXCOORD) : COLOR
                {
                	float4 o;

                	o = float4(1, texcoord.y * float4(1, 2, 3, 4).y, texcoord.z, float4(1, 2, 3, 4).w);

                	return o;
                }

                """));
            Assert.That(WriteHlslAst(shader), Is.EqualTo("""
                float4 main(float4 texcoord : TEXCOORD) : COLOR
                {
                	return float4(1, texcoord.yz * float2(2, 1), 4);
                }

                """));
        });
    }
}
