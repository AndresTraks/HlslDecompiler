using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.DirectXShaderModel.Xenos;
using HlslDecompiler.Util;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace HlslDecompiler;

public enum ShaderFileFormat
{
    Unknown,
    ShaderModel,
    Dxbc,
    Rgxa,
    Effect,
    Xenos
}

public class FormatDetector
{
    public static ShaderFileFormat Detect(Stream stream)
    {
        long tempPosition = stream.Position;
        var format = ShaderFileFormat.Unknown;

        using (var reader = new BinaryReader(stream, new UTF8Encoding(), true))
        {
            uint signature = (uint)reader.ReadInt32();
            if (signature == FourCC.Make("rgxa"))
            {
                format = ShaderFileFormat.Rgxa;
            }
            else
            {
                stream.Position = tempPosition;
                signature = reader.ReadUInt32();
                if (signature == FourCC.Make("DXBC"))
                {
                    format = HasChunk(reader, tempPosition, "FX10")
                        ? ShaderFileFormat.Effect
                        : ShaderFileFormat.Dxbc;
                }
                else if (EffectReader.IsEffectTag(signature))
                {
                    format = ShaderFileFormat.Effect;
                }
                else if (XenosReader.IsXenos(BinaryPrimitives.ReverseEndianness(signature)))
                {
                    format = ShaderFileFormat.Xenos;
                }
                else
                {
                    ShaderType versionToken = (ShaderType)(signature >> 16);
                    if (versionToken == ShaderType.Vertex || versionToken == ShaderType.Pixel)
                    {
                        format = ShaderFileFormat.ShaderModel;
                    }
                }
            }
        }

        stream.Position = tempPosition;
        return format;
    }

    // An fx_4_x effect is a DXBC file too, with only an FX10 chunk.
    private static bool HasChunk(BinaryReader reader, long start, string chunkType)
    {
        reader.BaseStream.Position = start + 28;
        int chunkCount = reader.ReadInt32();
        int[] chunkOffsets = new int[chunkCount];
        for (int i = 0; i < chunkCount; i++)
        {
            chunkOffsets[i] = reader.ReadInt32();
        }
        foreach (int chunkOffset in chunkOffsets)
        {
            reader.BaseStream.Position = start + chunkOffset;
            if (reader.ReadInt32() == FourCC.Make(chunkType))
            {
                return true;
            }
        }
        return false;
    }
}
