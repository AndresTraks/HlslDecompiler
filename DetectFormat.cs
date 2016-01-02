using System;
using System.IO;
using System.Text;

namespace HlslDecompiler
{
    enum ShaderFileFormat
    {
        Unknown,
        Hlsl,
        Rgxa
    }

    class DetectFormat
    {
        public static ShaderFileFormat Detect(Stream stream)
        {
            long tempPosition = stream.Position;
            var format = ShaderFileFormat.Unknown;

            using (var reader = new BinaryReader(stream, new UTF8Encoding(), true))
            {
                uint signature = (uint)reader.ReadInt32();
                if (signature == MakeFourCC("rgxa"))
                {
                    format = ShaderFileFormat.Rgxa;
                }
                else
                {
                    stream.Position = tempPosition;
                    signature = reader.ReadUInt32();
                    if (signature == 0xFFFE0300 || signature == 0xFFFF0300)
                    {
                        format = ShaderFileFormat.Hlsl;
                    }
                }
            }

            stream.Position = tempPosition;
            return format;
        }

        static int MakeFourCC(string id)
        {
            if (BitConverter.IsLittleEndian)
            {
                return (id[0]) + (id[1] << 8) + (id[2] << 16) + (id[3] << 24);
            }
            return (id[3]) + (id[2] << 8) + (id[1] << 16) + (id[0] << 24);
        }
    }
}
