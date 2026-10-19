using System;
using System.Buffers.Binary;
using System.Linq;

namespace HlslDecompiler.Tests;

// The DXBC container checksum: MD5 over every byte after the 20-byte header,
// except the last block. That holds the bit count in its first dword (the
// remaining bytes and the 0x80 marker move up by 4) and (bits >> 2) | 1 in its
// last; where the two do not fit, the bytes and marker are flushed first and the
// counts get a block of their own. As vkd3d-shader's dxbc_checksum_final.
// d3dcompiler (/dumpbin, D3DDisassemble) and the D3D11 runtime reject a
// container whose checksum is wrong. DxbcChecksumTests holds it to every
// container fxc wrote into CompiledShaders.
internal static class DxbcChecksum
{
    public const int Offset = 4;
    public const int Length = 16;
    private const int HeaderLength = 20;

    public static byte[] Compute(ReadOnlySpan<byte> container)
    {
        ReadOnlySpan<byte> data = container[HeaderLength..];
        uint[] state = [0x67452301, 0xefcdab89, 0x98badcfe, 0x10325476];
        int full = data.Length / 64 * 64;
        for (int offset = 0; offset < full; offset += 64)
        {
            Transform(state, data.Slice(offset, 64));
        }

        uint bits = (uint)data.Length * 8;
        ReadOnlySpan<byte> rest = data[full..];
        Span<byte> block = stackalloc byte[64];
        block.Clear();
        if (rest.Length + 1 > 56)
        {
            rest.CopyTo(block);
            block[rest.Length] = 0x80;
            Transform(state, block);
            block.Clear();
        }
        else
        {
            rest.CopyTo(block[4..]);
            block[4 + rest.Length] = 0x80;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(block, bits);
        BinaryPrimitives.WriteUInt32LittleEndian(block[60..], (bits >> 2) | 1);
        Transform(state, block);

        byte[] checksum = new byte[Length];
        for (int i = 0; i < 4; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(checksum.AsSpan(4 * i), state[i]);
        }
        return checksum;
    }

    // Plain MD5 (RFC 1321) from here on.
    private static readonly int[] Shifts =
    [
        7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22,
        5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20,
        4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23,
        6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21,
    ];

    private static readonly uint[] Constants = [.. Enumerable.Range(1, 64)
        .Select(i => (uint)(long)Math.Floor(Math.Abs(Math.Sin(i)) * 4294967296.0))];

    private static void Transform(uint[] state, ReadOnlySpan<byte> block)
    {
        Span<uint> words = stackalloc uint[16];
        for (int i = 0; i < 16; i++)
        {
            words[i] = BinaryPrimitives.ReadUInt32LittleEndian(block[(4 * i)..]);
        }
        uint a = state[0], b = state[1], c = state[2], d = state[3];
        for (int i = 0; i < 64; i++)
        {
            (uint mixed, int word) = (i / 16) switch
            {
                0 => ((b & c) | (~b & d), i),
                1 => ((d & b) | (~d & c), (5 * i + 1) % 16),
                2 => (b ^ c ^ d, (3 * i + 5) % 16),
                _ => (c ^ (b | ~d), 7 * i % 16),
            };
            uint sum = a + mixed + Constants[i] + words[word];
            (a, d, c) = (d, c, b);
            b += uint.RotateLeft(sum, Shifts[i]);
        }
        state[0] += a;
        state[1] += b;
        state[2] += c;
        state[3] += d;
    }
}
