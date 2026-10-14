using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HlslDecompiler.Tests;

// Assembles D3D9 shaders from tokens (layout documented on D3D9Instruction) for
// shapes fxc never emits. The result goes through the real reader.
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

    // Register type: bits 28-30 and 11-12 (0x70001800).
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

    // Shorthands: R/V/C read r#/v#/c#, Rd/Od write r#/o#; swizzle or mask first.
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

    // Shader model 1 tokens carry no length field.
    public static uint[] FixedSizeInstruction(Opcode opcode, params uint[] parameters)
    {
        return [(uint)opcode, .. parameters];
    }

    // The comparison goes in bits 16+ of the instruction token (ifc, breakc, setp).
    public static uint[] Instruction(Opcode opcode, IfComparison comparison, params uint[] parameters)
    {
        return [(uint)opcode | (uint)parameters.Length << 24 | (uint)comparison << 16, .. parameters];
    }

    // The usage token needs the sign bit like any parameter token.
    public static uint[] Declaration(DeclUsage usage, RegisterType type, int number)
    {
        return Declaration(usage, type, number, 0);
    }

    // Semantic index in bits 16-19 of the usage token.
    public static uint[] Declaration(DeclUsage usage, RegisterType type, int number, int semanticIndex)
    {
        return [(uint)Opcode.Dcl | 2u << 24,
            0x80000000 | (uint)usage | (uint)semanticIndex << 16, Destination(type, number)];
    }

    // Texture type in bits 27-30 of the usage token.
    public static uint[] SamplerDeclaration(SamplerTextureType textureType, int number)
    {
        return [(uint)Opcode.Dcl | 2u << 24,
            0x80000000 | (uint)textureType << 27, Destination(RegisterType.Sampler, number)];
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

    // Normalized to \n, as the expected texts are written.
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
