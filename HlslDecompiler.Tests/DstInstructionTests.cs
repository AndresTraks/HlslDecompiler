using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
        ShaderModel shader = Assemble([
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

    // Assembles the instruction list into the binary the reader reads, and reads
    // it back: the reader's operand-count table for dst is part of what is tested.
    private static ShaderModel Assemble(IList<uint[]> instructions)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((byte)0);              // minor version
            writer.Write((byte)3);              // major version
            writer.Write((ushort)ShaderType.Pixel);
            foreach (uint[] instruction in instructions)
            {
                foreach (uint token in instruction)
                {
                    writer.Write(token);
                }
            }
        }
        stream.Position = 0;
        using var reader = new ShaderReader(stream, true);
        return reader.ReadShader();
    }

    // The register-type field, as documented on D3D9Instruction: bits 28-30 and
    // 11-12 (the 0x70001800 of the parameter token).
    private static uint RegisterBits(RegisterType type)
    {
        return ((uint)type & 7) << 28 | ((uint)type & 0x18) << 8;
    }

    private static uint Destination(RegisterType type, int number, uint mask = 0xF)
    {
        return 0x80000000 | RegisterBits(type) | mask << 16 | (uint)number;
    }

    private static uint Source(RegisterType type, int number, uint modifier = 0, uint swizzle = 0xE4)
    {
        return 0x80000000 | RegisterBits(type) | modifier << 24 | swizzle << 16 | (uint)number;
    }

    private static uint[] Instruction(Opcode opcode, params uint[] parameters)
    {
        return [(uint)opcode | (uint)parameters.Length << 24, .. parameters];
    }

    // A dcl carries two parameters: the usage token - semantic in its low bits,
    // with the sampler texture type and index further up - and the register. The
    // usage token carries the sign bit like any parameter token, which the
    // InstructionVerifier insists on.
    private static uint[] Declaration(DeclUsage usage, RegisterType type, int number)
    {
        return [(uint)Opcode.Dcl | 2u << 24, 0x80000000 | (uint)usage, Destination(type, number)];
    }

    private static uint[] Constant(int number, params float[] values)
    {
        return [(uint)Opcode.Def | (uint)(values.Length + 1) << 24,
            Destination(RegisterType.Const, number),
            .. values.Select(BitConverter.SingleToUInt32Bits)];
    }

    // The writers end lines with the environment's newline; the expected texts
    // below spell them with \n.
    private static string WriteAsm(ShaderModel shader)
    {
        var stream = new MemoryStream();
        new AsmWriter(shader).Write(stream);
        return Encoding.UTF8.GetString(stream.ToArray()).ReplaceLineEndings("\n");
    }

    private static string WriteHlsl(ShaderModel shader)
    {
        var writer = new StringWriter();
        new HlslSimpleWriter(shader).Write(writer);
        return writer.ToString().ReplaceLineEndings("\n");
    }

    private static string WriteHlslAst(ShaderModel shader)
    {
        var writer = new StringWriter();
        new HlslAstWriter(shader).Write(writer);
        return writer.ToString().ReplaceLineEndings("\n");
    }
}
