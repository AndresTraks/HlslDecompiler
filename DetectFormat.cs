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
                if (signature == FourCC.Make("rgxa"))
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
    }
}
