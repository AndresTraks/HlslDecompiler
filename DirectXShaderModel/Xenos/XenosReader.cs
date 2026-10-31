using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace HlslDecompiler.DirectXShaderModel.Xenos;

/// <summary>
/// Reads the container XNA Game Studio 3.1's ShaderCompiler writes for
/// TargetPlatform.Xbox360. Every field is big-endian. The layout, decoded from
/// its output and checked against its assembler:
///   0x00  magic, 0x102A1100 for a pixel shader and 0x102A1101 for a vertex shader
///   0x04  offset of the data: the literal constants, then the microcode
///   0x10  offset of the constant table's size, which the table follows
///   0x14  offset of the compiler's literal table, or 0: its word 5 is the
///         first float literal's register in the unified float file (a pixel
///         shader's start at 256) above the number of floats. Integer
///         literals follow as (0x2320 + 4 * register) << 16 | count, then
///         count values; the unified integer file has a pixel shader's at 16.
///   0x18  offset of the program header, which runs up to the data
/// </summary>
public sealed class XenosReader
{
    public const uint PixelMagic = 0x102A1100;
    public const uint VertexMagic = 0x102A1101;

    private readonly byte[] _data;

    public XenosReader(Stream input)
    {
        using var copy = new MemoryStream();
        input.CopyTo(copy);
        _data = copy.ToArray();
    }

    public static bool IsXenos(uint bigEndianMagic) =>
        bigEndianMagic == PixelMagic || bigEndianMagic == VertexMagic;

    public XenosShader ReadShader()
    {
        uint magic = Word(0x00);
        if (!IsXenos(magic))
        {
            throw new InvalidDataException($"Not an Xbox 360 shader: 0x{magic:X8}");
        }
        ShaderType type = magic == VertexMagic ? ShaderType.Vertex : ShaderType.Pixel;

        int dataOffset = (int)Word(0x04);
        int tableOffset = (int)Word(0x10);
        int literalTableOffset = (int)Word(0x14);
        int programOffset = (int)Word(0x18);
        uint[] header = Words(programOffset, (dataOffset - programOffset) / 4);
        int literalBytes = (int)header[0];
        int microcodeBytes = (int)header[1];

        ConstantTable constants = new();
        if (tableOffset != 0)
        {
            int tableSize = (int)Word(tableOffset);
            using var table = new MemoryStream(_data, tableOffset + 4, tableSize, writable: false);
            using var tableReader = new ConstantTableCommentReader(table, bigEndian: true);
            constants = tableReader.ReadTable();
        }

        int literalRegister = 0;
        var integerLiterals = new List<(int, uint)>();
        if (literalTableOffset != 0)
        {
            literalRegister = (int)(Word(literalTableOffset + 5 * 4) >> 16) - (type == ShaderType.Pixel ? 256 : 0);
            for (int offset = literalTableOffset + 5 * 4; offset < programOffset; offset += 4)
            {
                uint entry = Word(offset);
                int code = (int)(entry >> 16), count = (int)(entry & 0xFFFF);
                if (code >= 0x2320 && code < 0x23A0 && (code & 3) == 0 && count is > 0 and <= 32)
                {
                    for (int i = 0; i < count; i++)
                    {
                        integerLiterals.Add(((code - 0x2320) / 4 + i, Word(offset + 4 + i * 4)));
                    }
                    offset += count * 4;
                }
            }
        }

        var shader = new XenosShader(type, constants,
            Words(dataOffset + literalBytes, microcodeBytes / 4),
            Words(dataOffset, literalBytes / 4))
        {
            LiteralRegister = literalRegister,
            IntegerLiterals = integerLiterals,
        };
        return type == ShaderType.Vertex ? ReadVertexHeader(shader, header) : ReadPixelHeader(shader, header);
    }

    // Word 2: PsMaxReg in bits 8-15, PsResource bit 17, a parameter register
    // bit 18, PsGenIndex bit 19 (the export counts above are derived). Word 3:
    // PsGenScreenXY bit 1, PsParamGenReg in bits 8-13. Word 6 masks the
    // interpolated registers, below those sampled at the center. Words 8 on:
    // one interpolator each, as mask << 12 | register << 8 | usage << 4 | index.
    private static XenosShader ReadPixelHeader(XenosShader shader, uint[] header)
    {
        bool hasParamGen = ((header[2] >> 18) & 1) != 0;
        var config = new XenosProgramConfig
        {
            MaxRegister = (int)((header[2] >> 8) & 0xFF),
            Resource = ((header[2] >> 17) & 1) != 0,
            ParamGenRegister = hasParamGen ? (int)((header[3] >> 8) & 0x3F) : null,
            GenScreenXY = ((header[3] >> 1) & 1) != 0,
            GenIndex = ((header[2] >> 19) & 1) != 0,
        };

        uint centroid = header[6] & ~(header[6] >> 16) & 0xFFFF;
        var declarations = new XenosDeclaration[Math.Max(header.Length - 8, 0)];
        for (int i = 0; i < declarations.Length; i++)
        {
            XenosDeclaration declaration = ReadDeclaration(header[8 + i]);
            declarations[i] = declaration with { IsCentroid = ((centroid >> declaration.Register) & 1) != 0 };
        }
        return shader.With(config, declarations, [], null);
    }

    // Word 2: VsMaxReg in bits 0-7, VsResource bit 16, the export count less
    // one in bits 20-23 (derived), VsExportMode in bits 24-27, VsGenIndex bit
    // 31. Words 6, 7 and 8 count the index declarations, the vertex inputs and
    // the outputs, whose entries follow in that order, then one word per output
    // that the runtime patches.
    //   index:  mask << 9 | register << 8 | 0x90, the register r0 or r1
    //   input:  usage index << 16 | usage << 12 | instruction address
    //   output: as a pixel shader's interpolator
    private static XenosShader ReadVertexHeader(XenosShader shader, uint[] header)
    {
        var config = new XenosProgramConfig
        {
            MaxRegister = (int)(header[2] & 0xFF),
            Resource = ((header[2] >> 16) & 1) != 0,
            ExportMode = (int)((header[2] >> 24) & 0xF),
            GenIndex = ((header[2] >> 31) & 1) != 0,
        };

        int indexCount = (int)header[6];
        int inputCount = (int)header[7];
        int outputCount = (int)header[8];
        int next = 9;

        XenosIndexDeclaration? index = null;
        for (int i = 0; i < indexCount; i++, next++)
        {
            uint entry = header[next];
            index = new XenosIndexDeclaration((int)((entry >> 8) & 1),
                BitOperations.TrailingZeroCount((entry >> 9) & 0xF));
        }

        var inputs = new XenosVertexInput[inputCount];
        for (int i = 0; i < inputCount; i++, next++)
        {
            uint entry = header[next];
            inputs[i] = new XenosVertexInput((DeclUsage)((entry >> 12) & 0xF), (int)((entry >> 16) & 0xF),
                (int)(entry & 0xFFF));
        }

        var outputs = new XenosDeclaration[outputCount];
        for (int i = 0; i < outputCount; i++, next++)
        {
            outputs[i] = ReadDeclaration(header[next]);
        }
        return shader.With(config, outputs, inputs, index);
    }

    private static XenosDeclaration ReadDeclaration(uint entry) =>
        new((DeclUsage)((entry >> 4) & 0xF), (int)(entry & 0xF), (int)((entry >> 8) & 0xF), (int)((entry >> 12) & 0xF));

    private uint Word(int offset) => BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(offset));

    private uint[] Words(int offset, int count)
    {
        var words = new uint[count];
        for (int i = 0; i < count; i++)
        {
            words[i] = Word(offset + i * 4);
        }
        return words;
    }
}
