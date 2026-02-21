using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Util;
using System;
using System.IO;
using System.Text;

namespace HlslDecompiler;

enum ShaderFileFormat
{
    Unknown,
    ShaderModel,
    Dxbc,
    Rgxa
}

class FormatDetector
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
                    format = ShaderFileFormat.Dxbc;
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
}
