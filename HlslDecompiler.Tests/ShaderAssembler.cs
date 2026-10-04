using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HlslDecompiler.Tests;

// Assembles D3D9 shaders from instruction tokens, for the instructions no golden
// can hold because fxc never compiles HLSL into them. The tokens are laid out as
// documented on D3D9Instruction, and the result goes through the reader, so the
// reader's operand-count table is part of what every test built on this asserts.
internal static class ShaderAssembler
{
    public static ShaderModel Assemble(ShaderType type, int majorVersion, IList<uint[]> instructions)
    {
        return Assemble(type, majorVersion, 0, instructions);
    }

    public static ShaderModel Assemble(ShaderType type, int majorVersion, int minorVersion,
        IList<uint[]> instructions)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((byte)minorVersion);
            writer.Write((byte)majorVersion);
            writer.Write((ushort)type);
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

    public static uint Destination(RegisterType type, int number, uint mask = 0xF, ResultModifier modifier = ResultModifier.None)
    {
        return 0x80000000 | RegisterBits(type) | (uint)modifier << 20 | mask << 16 | (uint)number;
    }

    public static uint Source(RegisterType type, int number, SourceModifier modifier = SourceModifier.None, uint swizzle = 0xE4)
    {
        return 0x80000000 | RegisterBits(type) | (uint)modifier << 24 | swizzle << 16 | (uint)number;
    }

    // The registers a hand-assembled body is mostly made of, named the way the
    // assembly names them: R, V and C read the temp, input and constant float
    // registers, and Rd and Od write the temp and output ones. The swizzle and
    // the write mask come before the modifier, since an instruction names those
    // - and only carries a modifier once in a while. The Source and Destination
    // above spell the registers these do not: samplers, integers, the loop's own.
    public static uint R(int number, uint swizzle = 0xE4, SourceModifier modifier = SourceModifier.None)
    {
        return Source(RegisterType.Temp, number, modifier, swizzle);
    }

    public static uint V(int number, uint swizzle = 0xE4, SourceModifier modifier = SourceModifier.None)
    {
        return Source(RegisterType.Input, number, modifier, swizzle);
    }

    public static uint C(int number, uint swizzle = 0xE4, SourceModifier modifier = SourceModifier.None)
    {
        return Source(RegisterType.Const, number, modifier, swizzle);
    }

    public static uint Rd(int number, uint mask = 0xF, ResultModifier modifier = ResultModifier.None)
    {
        return Destination(RegisterType.Temp, number, mask, modifier);
    }

    public static uint Od(int number, uint mask = 0xF)
    {
        return Destination(RegisterType.Output, number, mask);
    }

    public static uint[] Instruction(Opcode opcode, params uint[] parameters)
    {
        return [(uint)opcode | (uint)parameters.Length << 24, .. parameters];
    }

    // Shader model 1 carries no length field: the reader takes the operand count
    // from the opcode, so the token is the opcode alone.
    public static uint[] FixedSizeInstruction(Opcode opcode, params uint[] parameters)
    {
        return [(uint)opcode, .. parameters];
    }

    // setp carries its comparison in the instruction token, where ifc and breakc
    // carry theirs.
    public static uint[] Instruction(Opcode opcode, IfComparison comparison, params uint[] parameters)
    {
        return [(uint)opcode | (uint)parameters.Length << 24 | (uint)comparison << 16, .. parameters];
    }

    // A dcl carries two parameters: the usage token - semantic in its low bits,
    // with the sampler texture type and index further up - and the register. The
    // usage token carries the sign bit like any parameter token, which the
    // InstructionVerifier insists on.
    public static uint[] Declaration(DeclUsage usage, RegisterType type, int number)
    {
        return Declaration(usage, type, number, 0);
    }

    // The semantic index - TEXCOORD1's 1 - is bits 16 to 19 of the usage token.
    public static uint[] Declaration(DeclUsage usage, RegisterType type, int number, int semanticIndex)
    {
        return [(uint)Opcode.Dcl | 2u << 24,
            0x80000000 | (uint)usage | (uint)semanticIndex << 16, Destination(type, number)];
    }

    public static uint[] ConstantInt(int number, params int[] values)
    {
        return [(uint)Opcode.DefI | 5u << 24,
            Destination(RegisterType.ConstInt, number),
            .. values.Select(v => (uint)v)];
    }

    public static uint[] ConstantBool(int number, bool value)
    {
        return [(uint)Opcode.DefB | 2u << 24,
            Destination(RegisterType.ConstBool, number), value ? 1u : 0u];
    }

    public static uint[] Constant(int number, params float[] values)
    {
        return [(uint)Opcode.Def | (uint)(values.Length + 1) << 24,
            Destination(RegisterType.Const, number),
            .. values.Select(System.BitConverter.SingleToUInt32Bits)];
    }

    // The writers end lines with the environment's newline; the expected texts in
    // the tests spell them with \n.
    public static string WriteAsm(ShaderModel shader)
    {
        var stream = new MemoryStream();
        new AsmWriter(shader).Write(stream);
        return Encoding.UTF8.GetString(stream.ToArray()).ReplaceLineEndings("\n");
    }

    public static string WriteHlsl(ShaderModel shader)
    {
        var writer = new StringWriter();
        new HlslSimpleWriter(shader).Write(writer);
        return writer.ToString().ReplaceLineEndings("\n");
    }

    public static string WriteHlslAst(ShaderModel shader)
    {
        var writer = new StringWriter();
        new HlslAstWriter(shader).Write(writer);
        return writer.ToString().ReplaceLineEndings("\n");
    }
}
