using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HlslDecompiler.DirectXShaderModel;

/// <summary>
/// Reads the shaders out of an effect - fx_2_0, fx_4_0, fx_4_1 or fx_5_0.
///
/// An effect is variables, techniques, passes and state, and the shaders the passes
/// set, each of those stored exactly as fxc writes a single shader: a shader model
/// 1 to 3 token stream with its constant table in fx_2_0, a complete DXBC file with
/// its signatures and reflection in the later ones. This takes the shaders out and
/// leaves the rest: the decompiler reads them as it would any other.
///
/// fx_2_0 is the Direct3D 9 format, a tag and then the offset of the structured
/// data, which the effect's names, values and types come before and its shaders
/// after, each as its length followed by its tokens.
///
/// fxc wraps fx_4_x in a DXBC container holding a single FX10 chunk, and writes
/// fx_5_0 bare, the same body without the container around it. The body starts
/// with a header of counts, then the "unstructured" data that the structured part
/// after it points into - strings, default values, types, and the shaders, each
/// stored as its length followed by its bytes. The structured part says which of
/// those are shaders: an expression the effect evaluates is a DXBC container too.
/// </summary>
public class EffectReader : BinaryReader
{
    public const uint Fx20 = 0xFEFF0901;
    public const uint Fx40 = 0xFEFF1001;
    public const uint Fx41 = 0xFEFF1011;
    public const uint Fx50 = 0xFEFF2001;

    public EffectReader(Stream input, bool leaveOpen = false)
        : base(input, new UTF8Encoding(false, true), leaveOpen)
    {
    }

    /// <summary>Whether this is the version tag an effect body starts with.</summary>
    public static bool IsEffectTag(uint tag)
    {
        return tag is Fx20 or Fx40 or Fx41 or Fx50;
    }

    public IList<ShaderModel> ReadShaders()
    {
        long start = BaseStream.Position;
        bool isD3D9 = ReadUInt32() == Fx20;
        BaseStream.Position = start;

        return [.. ReadShaderBlobs().Select(blob =>
        {
            var stream = new MemoryStream(blob);
            if (isD3D9)
            {
                using var shaderReader = new ShaderReader(stream);
                return shaderReader.ReadShader();
            }
            using var dxbcReader = new DxbcReader(stream);
            return dxbcReader.ReadShader();
        })];
    }

    /// <summary>
    /// Each shader in the effect, as it is stored: a token stream for fx_2_0, a DXBC
    /// file for the rest.
    /// </summary>
    public IList<byte[]> ReadShaderBlobs()
    {
        long start = BaseStream.Position;
        if (ReadUInt32() == Fx20)
        {
            return FindD3D9ShaderBlobs(ReadBytes((int)(BaseStream.Length - BaseStream.Position)));
        }
        BaseStream.Position = start;

        // The variables' shaders, then those the passes compile, in order.
        Effect effect = ReadEffect();
        IEnumerable<EffectShader> variableShaders = effect.ObjectVariables.SelectMany(v => v.Shaders);
        IEnumerable<EffectShader> passShaders = effect.Groups
            .SelectMany(g => g.Techniques)
            .SelectMany(t => t.Passes)
            .SelectMany(p => p.Assignments)
            .Select(a => a.Shader)
            .Where(shader => shader != null);
        return [.. variableShaders.Concat(passShaders)
            .Select(shader => shader.Bytecode)
            .Where(bytecode => bytecode != null)];
    }

    /// <summary>
    /// The parameters, techniques and passes of an fx_2_0 effect, with the shaders
    /// where they are set.
    /// </summary>
    public D3D9Effect ReadD3D9Effect()
    {
        byte[] file = ReadBytes((int)(BaseStream.Length - BaseStream.Position));
        uint tag = BitConverter.ToUInt32(file, 0);
        if (tag != Fx20)
        {
            throw new InvalidDataException($"Not an fx_2_0 effect: tag 0x{tag:X8}.");
        }
        return D3D9EffectStructureReader.Read(file);
    }

    /// <summary>
    /// The variables, state objects, techniques and passes of a Direct3D 10 or 11
    /// effect, with the shaders where they are set.
    /// </summary>
    public Effect ReadEffect()
    {
        (long bodyStart, long bodySize) = FindBodyAndSize();
        BaseStream.Position = bodyStart;
        byte[] body = ReadBytes((int)bodySize);
        uint tag = BitConverter.ToUInt32(body, 0);
        if (tag is not (Fx40 or Fx41 or Fx50))
        {
            throw new InvalidDataException($"Not a Direct3D 10 or 11 effect: tag 0x{tag:X8}.");
        }
        return EffectStructureReader.Read(body);
    }

    /// <returns>Where the effect body starts, past the DXBC container's FX10 chunk header if there is one, and how long it is.</returns>
    private (long Start, long Size) FindBodyAndSize()
    {
        long start = BaseStream.Position;
        if (ReadInt32() != FourCC.Make("DXBC"))
        {
            return (start, BaseStream.Length - start);
        }

        ReadBytes(16); // checksum
        ReadInt32(); // 1
        ReadInt32(); // total size
        int chunkCount = ReadInt32();
        int[] chunkOffsets = new int[chunkCount];
        for (int i = 0; i < chunkCount; i++)
        {
            chunkOffsets[i] = ReadInt32();
        }

        foreach (int chunkOffset in chunkOffsets)
        {
            BaseStream.Position = start + chunkOffset;
            if (ReadInt32() == FourCC.Make("FX10"))
            {
                return (start + chunkOffset + 8, ReadInt32());
            }
        }
        throw new InvalidDataException("A DXBC container with no FX10 chunk is not an effect.");
    }

    /// <summary>
    /// The shaders in an fx_2_0 effect, found by what they look like: a length, then a vertex
    /// or pixel shader version token, and the end token as the last of the tokens
    /// the length covers. A preshader is a token stream too, of a version of its own
    /// that is neither, and it is left where it is - the shader it computes
    /// constants for carries its own copy, in a comment.
    /// </summary>
    private static List<byte[]> FindD3D9ShaderBlobs(byte[] data)
    {
        const uint endToken = 0x0000FFFF;
        var blobs = new List<byte[]>();
        int position = 4;
        while (position + 8 <= data.Length)
        {
            int size = ReadInt32(data, position - 4);
            if (size >= 8
                && size % 4 == 0
                && position + size <= data.Length
                && IsD3D9Version((uint)ReadInt32(data, position))
                && (uint)ReadInt32(data, position + size - 4) == endToken)
            {
                blobs.Add(data[position..(position + size)]);
                position += size + 4;
                continue;
            }
            position++;
        }
        return blobs;
    }

    private static bool IsD3D9Version(uint token)
    {
        var type = (ShaderType)(token >> 16);
        int major = (int)(token >> 8) & 0xFF;
        return (type == ShaderType.Vertex || type == ShaderType.Pixel) && major is >= 1 and <= 3;
    }

    private static int ReadInt32(byte[] data, int position)
    {
        return BitConverter.ToInt32(data, position);
    }
}
