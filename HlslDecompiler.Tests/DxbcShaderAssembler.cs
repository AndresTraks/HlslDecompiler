using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HlslDecompiler.Tests;

// Assembles DXBC containers from tokens (layout decoded from fxc's own .fxc files)
// for shapes fxc never compiles. The result goes through the real reader, and
// `fxc /dumpbin` reads it too, which is how the listings are checked.
internal static class DxbcShaderAssembler
{
    public static ShaderModel Assemble(ShaderType type, int majorVersion, int minorVersion,
        IList<uint[]> instructions)
    {
        byte[] bytes = AssembleBytes(type, majorVersion, minorVersion, instructions);
        using var stream = new MemoryStream(bytes);
        using var reader = new DxbcReader(stream, true);
        return reader.ReadShader();
    }

    // DXBC container: "DXBC", a 16-byte checksum, 1, the total size, the chunk
    // count, and the chunk offsets. RDEF names the version and type; SHDR (SHEX
    // for compute) holds the code after a version token and a length. Chunk sizes
    // are payload bytes, not dwords, and fxc /dumpbin walks by them.
    public static byte[] AssembleBytes(ShaderType type, int majorVersion, int minorVersion,
        IList<uint[]> instructions)
    {
        List<uint> code = [.. instructions.SelectMany(instruction => instruction)];

        uint versionToken = (uint)ShaderTypeCode(type) << 16 | (uint)(majorVersion << 4 | minorVersion);
        uint rdefVersion = (byte)minorVersion | (uint)majorVersion << 8 | (uint)(ushort)type << 16;
        // The creator name sits past the seven RDEF header dwords and ends in a
        // NUL; both table offsets stay 0 because both counts are.
        uint[] rdef = [0u, 0u, 0u, 0u, rdefVersion, 0u, 28u, FourCC("main"), 0u];
        uint rdefBytes = (uint)rdef.Length * 4;
        uint codeBytes = (uint)(code.Count + 2) * 4;
        uint rdefOffset = 40;
        uint codeOffset = rdefOffset + 8 + rdefBytes;
        uint totalSize = codeOffset + 8 + codeBytes;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(FourCC("DXBC"));
        for (int i = 0; i < 4; i++)
        {
            writer.Write(0u); // checksum, filled in once the rest is written
        }
        writer.Write(1u);
        writer.Write(totalSize);
        writer.Write(2u); // chunk count
        writer.Write(rdefOffset);
        writer.Write(codeOffset);

        writer.Write(FourCC("RDEF"));
        writer.Write(rdefBytes);
        foreach (uint dword in rdef)
        {
            writer.Write(dword);
        }

        // fxc keeps compute's code in SHEX, every other stage's in SHDR; only
        // fxc /dumpbin cares, but it refuses to disassemble the wrong one.
        writer.Write(FourCC(type == ShaderType.Compute ? "SHEX" : "SHDR"));
        writer.Write(codeBytes);
        writer.Write(versionToken);
        // The reader takes the code end from this length, not from the chunk size.
        writer.Write((uint)code.Count + 2);
        foreach (uint dword in code)
        {
            writer.Write(dword);
        }
        writer.Flush();
        byte[] container = stream.ToArray();
        DxbcChecksum.Compute(container).CopyTo(container, DxbcChecksum.Offset);
        return container;
    }

    // D3D10_SB_SHADER_SHADER_TYPE: the numeric type in the version token, not the
    // two-letter code RDEF uses.
    private static int ShaderTypeCode(ShaderType type) => type switch
    {
        ShaderType.Vertex => 0,
        ShaderType.Pixel => 1,
        ShaderType.Geometry => 2,
        ShaderType.Hull => 3,
        ShaderType.Domain => 4,
        ShaderType.Compute => 5,
        _ => throw new NotSupportedException(type.ToString()),
    };

    private static uint FourCC(string fourCc)
    {
        return (uint)(fourCc[0] | fourCc[1] << 8 | fourCc[2] << 16 | fourCc[3] << 24);
    }

    // --- operand tokens -----------------------------------------------------------------
    //
    // A register operand is its descriptor token followed by its number: the
    // descriptor names the type and component selection and asks for one immediate
    // index. An immediate operand holds its values in place of a number.

    private const uint Num4Components = 2;
    private const uint Num1Component = 1;
    private const uint Num0Components = 0;
    private const uint SwizzleSelection = 1u << 2;
    private const uint Select1Selection = 2u << 2;
    private const uint OneIndex = 1u << 20; // dimension 1, representation immediate32

    public static uint[] Destination(OperandType type, uint number, uint mask = 0xF)
    {
        return [((uint)type << 12) | Num4Components | mask << 4 | OneIndex, number];
    }

    // The whole register: the operand reads no components of its own.
    public static uint[] Register(OperandType type, uint number)
    {
        return [((uint)type << 12) | Num0Components | OneIndex, number];
    }

    public static uint[] Source(OperandType type, uint number, uint swizzle = 0xE4)
    {
        return [((uint)type << 12) | Num4Components | SwizzleSelection | swizzle << 4 | OneIndex,
            number];
    }

    // One component tested or indexed: `r0.x` in a callc's or a cb[r0.x] operand.
    public static uint[] Component(OperandType type, uint number, uint component)
    {
        return [((uint)type << 12) | Num4Components | Select1Selection | component << 4 | OneIndex,
            number];
    }

    public static uint[] Null()
    {
        return [(uint)OperandType.Null << 12];
    }

    public static uint[] Immediate(params uint[] values)
    {
        return [(uint)OperandType.Immediate32 << 12
            | (values.Length == 1 ? Num1Component : Num4Components), .. values];
    }

    public static uint[] Label(uint number)
    {
        return [(uint)OperandType.Label << 12 | Num0Components | OneIndex, number];
    }

    // --- instructions -------------------------------------------------------------------
    //
    // The header's length counts every dword including itself; operands and plain
    // dwords follow the same rule, so both are passed as token groups.

    public static uint[] Instruction(D3D10Opcode opcode, params uint[][] operands)
    {
        return InstructionWithTestBit(false, opcode, operands);
    }

    // Test-carrying instructions (callc, retc, ...) keep the bit in the opcode token.
    public static uint[] InstructionWithTestBit(bool nonZeroTest, D3D10Opcode opcode,
        params uint[][] operands)
    {
        List<uint> dwords = [.. operands.SelectMany(operand => operand)];
        return [(uint)opcode | (uint)(dwords.Count + 1) << 24
            | (nonZeroTest ? 1u << 18 : 0), .. dwords];
    }

    // dcl_temps and dcl_thread_group take plain dwords where operands would be.
    public static uint[] Declaration(D3D10Opcode opcode, params uint[] dwords)
    {
        return [(uint)opcode | (uint)(dwords.Length + 1) << 24, .. dwords];
    }

    // A declaration with an operand first: dcl_uav_structured u0, stride, count.
    public static uint[] Declaration(D3D10Opcode opcode, uint[] operand, params uint[] dwords)
    {
        return [(uint)opcode | (uint)(operand.Length + dwords.Length + 1) << 24,
            .. operand, .. dwords];
    }
}
