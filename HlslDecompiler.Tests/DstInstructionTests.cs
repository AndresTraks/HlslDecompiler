using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using static HlslDecompiler.Tests.ShaderAssembler;

namespace HlslDecompiler.Tests;

// dst is the fixed-function distance-step of D3D8/9 assembly: fxc has no intrinsic
// that compiles to it and rejects asm{} embedding (X2001/X3000), so no golden can
// hold one. The shader is assembled by hand from the token layout documented on
// D3D9Instruction and handed to the reader, so this is also the only test the
// reader's dst operand-count table and the interpreter's semantics have to match.
public class DstInstructionTests
{
    [Test]
    public void DecompilesDstToItsFourComponents()
    {
        ShaderModel shader = Assemble(ShaderType.Pixel, 3, [
            // ps_3_0 declares its inputs with the input register type: Texture is
            // what ps_2_x and earlier read texture coordinates from, and the asm
            // writer names those t rather than v.
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 0),     // dcl_texcoord v0
            Constant(0, 1.0f, 2.0f, 3.0f, 4.0f),                        // def c0, 1, 2, 3, 4
            Instruction(Opcode.Dst,
                Destination(RegisterType.ColorOut, 0),                  // dst oC0, v0, c0
                Source(RegisterType.Input, 0),
                Source(RegisterType.Const, 0)),
            // No trailing ret: fxc peepholes it away in a pixel shader, and the
            // goldens show the asm listing ends with the last write.
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
