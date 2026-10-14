using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.DirectXShaderModel;

/// <summary>
/// Where a preshader operand lives. An operand names a component, not a register.
/// </summary>
public enum PreshaderRegisterTable
{
    /// <summary>The CLIT literals.</summary>
    Literal = 1,
    /// <summary>The uniforms, laid out by the preshader's own constant table.</summary>
    Input = 2,
    /// <summary>The shader's float constant registers.</summary>
    Output = 4,
    OutputBool = 5,
    OutputInt = 6,
    Temp = 7,
}

public enum PreshaderOpcode
{
    Mov = 0x100,
    Neg = 0x101,
    Rcp = 0x103,
    Frc = 0x104,
    Exp = 0x105,
    Log = 0x106,
    Rsq = 0x107,
    Sin = 0x108,
    Cos = 0x109,
    Asin = 0x10A,
    Acos = 0x10B,
    Atan = 0x10C,
    Min = 0x200,
    Max = 0x201,
    Lt = 0x202,
    Ge = 0x203,
    Add = 0x204,
    Mul = 0x205,
    Atan2 = 0x206,
    Div = 0x208,
    Cmp = 0x300,
    Movc = 0x301,
    Dot = 0x500,
    Noise = 0x502,
    DotSwiz6 = 0x70E,
    DotSwiz8 = 0x70F,
}

/// <summary>A table component, and the component an index is read from, if any.</summary>
public sealed record PreshaderOperand(
    PreshaderRegisterTable Table,
    int Offset,
    PreshaderRegisterTable? IndexTable = null,
    int IndexOffset = 0)
{
    public bool IsIndexed => IndexTable != null;
}

/// <summary>
/// Works on ComponentCount consecutive components per operand, except a scalar
/// instruction's first input (one component for all) and dot's single output.
/// </summary>
public sealed record PreshaderInstruction(
    PreshaderOpcode Opcode,
    int ComponentCount,
    bool IsScalar,
    IReadOnlyList<PreshaderOperand> Inputs,
    PreshaderOperand Output);

/// <summary>
/// Uniform-only arithmetic the fx_2_0 compiler moves to the CPU. It reads the
/// uniforms and writes the shader's constant registers. Stored in a PRES comment
/// as a token stream: a version, then CTAB, CLIT and FXLC comments.
/// </summary>
public sealed class Preshader
{
    public ConstantTable Inputs { get; }
    public IReadOnlyList<double> Literals { get; }
    public IReadOnlyList<PreshaderInstruction> Instructions { get; }

    private Preshader(ConstantTable inputs, IReadOnlyList<double> literals, IReadOnlyList<PreshaderInstruction> instructions)
    {
        Inputs = inputs;
        Literals = literals;
        Instructions = instructions;
    }

    public static Preshader Find(ShaderModel shader)
    {
        D3D9Instruction comment = shader.Instructions
            .OfType<D3D9Instruction>()
            .FirstOrDefault(i => i.Opcode == Opcode.Comment && i.Params.Count > 0 && i.Params[0] == FourCC.Make("PRES"));
        return comment == null ? null : Read(comment);
    }

    public static Preshader Read(D3D9Instruction comment)
    {
        uint[] tokens = [.. Enumerable.Range(0, comment.Params.Count).Select(i => comment.Params[i])];

        // tokens[0] is PRES, tokens[1] the version.
        return ReadTokens(tokens, 2);
    }

    /// <summary>
    /// An fx_2_0 state or array-index expression: a preshader as a standalone token
    /// stream.
    /// </summary>
    public static Preshader ReadTokenStream(byte[] data)
    {
        uint[] tokens = [.. Enumerable.Range(0, data.Length / 4).Select(i => BitConverter.ToUInt32(data, i * 4))];
        return ReadTokens(tokens, 1);
    }

    private static Preshader ReadTokens(uint[] tokens, int position)
    {
        var inputs = new ConstantTable();
        double[] literals = [];
        List<PreshaderInstruction> instructions = [];
        while (position < tokens.Length)
        {
            uint token = tokens[position];
            if (token == 0x0000FFFF)
            {
                break;
            }
            if ((token & 0xFFFF) != (uint)Opcode.Comment)
            {
                throw new InvalidOperationException($"Expected a comment in a preshader, found 0x{token:X8}.");
            }
            int size = (int)((token >> 16) & 0x7FFF);
            uint[] payload = tokens[(position + 1)..(position + 1 + size)];
            position += 1 + size;

            string chunk = FourCC.Decode((int)payload[0]);
            switch (chunk)
            {
                case "CTAB":
                    using (var reader = new ConstantTableCommentReader(new D3D9Instruction(token, payload)))
                    {
                        inputs = reader.ReadTable();
                    }
                    break;
                case "CLIT":
                    literals = ReadLiterals(payload);
                    break;
                case "FXLC":
                    instructions = ReadInstructions(payload);
                    break;
            }
        }
        return new Preshader(inputs, literals, instructions);
    }

    /// <summary>
    /// A D3D10/11 effect expression (e.g. the index of `shaders[mode]`): a preshader
    /// in a DXBC container, with single-precision literals (CLI4).
    /// </summary>
    public static Preshader ReadExpression(byte[] container)
    {
        uint Read(int position) => BitConverter.ToUInt32(container, position);

        var inputs = new ConstantTable();
        double[] literals = [];
        List<PreshaderInstruction> instructions = [];
        int chunkCount = (int)Read(28);
        for (int i = 0; i < chunkCount; i++)
        {
            int chunk = (int)Read(32 + i * 4);
            string type = FourCC.Decode((int)Read(chunk));
            int size = (int)Read(chunk + 4);
            // Shaped like a token stream comment: FOURCC, then contents.
            uint[] payload = [.. Enumerable.Range(0, size / 4 + 1).Select(t => Read(chunk + 4 + t * 4))];
            payload[0] = Read(chunk);
            switch (type)
            {
                case "CTAB":
                    using (var reader = new ConstantTableCommentReader(new D3D9Instruction((uint)Opcode.Comment, payload)))
                    {
                        inputs = reader.ReadTable();
                    }
                    break;
                case "CLI4":
                    literals = [.. Enumerable.Range(0, (int)payload[1])
                        .Select(l => (double)BitConverter.UInt32BitsToSingle(payload[2 + l]))];
                    break;
                case "FXLC":
                    instructions = ReadInstructions(payload);
                    break;
            }
        }
        return new Preshader(inputs, literals, instructions);
    }

    private static double[] ReadLiterals(uint[] payload)
    {
        int count = (int)payload[1];
        var literals = new double[count];
        for (int i = 0; i < count; i++)
        {
            ulong bits = payload[2 + i * 2] | ((ulong)payload[3 + i * 2] << 32);
            literals[i] = BitConverter.UInt64BitsToDouble(bits);
        }
        return literals;
    }

    private static List<PreshaderInstruction> ReadInstructions(uint[] payload)
    {
        int count = (int)payload[1];
        int position = 2;
        var instructions = new List<PreshaderInstruction>(count);
        for (int i = 0; i < count; i++)
        {
            uint code = payload[position++];
            int inputCount = (int)payload[position++];
            var opcode = (PreshaderOpcode)((code >> 20) & 0x7FF);
            if (!Enum.IsDefined(opcode))
            {
                throw new NotImplementedException($"Preshader opcode 0x{(int)opcode:X}.");
            }

            var operands = new PreshaderOperand[inputCount + 1];
            for (int o = 0; o < operands.Length; o++)
            {
                PreshaderRegisterTable? indexTable = null;
                int indexOffset = 0;
                if (payload[position++] != 0)
                {
                    indexTable = (PreshaderRegisterTable)payload[position++];
                    indexOffset = (int)payload[position++];
                }
                var table = (PreshaderRegisterTable)payload[position++];
                int offset = (int)payload[position++];
                operands[o] = new PreshaderOperand(table, offset, indexTable, indexOffset);
            }

            instructions.Add(new PreshaderInstruction(
                opcode,
                (int)(code & 0xFFFF),
                (code & 0x80000000) != 0,
                operands[..inputCount],
                operands[inputCount]));
        }
        return instructions;
    }
}
