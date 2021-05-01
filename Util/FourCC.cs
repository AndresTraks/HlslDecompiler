using System;

namespace HlslDecompiler.Util
{
    class FourCC
    {
        public static int Make(string id)
        {
            if (BitConverter.IsLittleEndian)
            {
                return (id[0]) + (id[1] << 8) + (id[2] << 16) + (id[3] << 24);
            }
            return (id[3]) + (id[2] << 8) + (id[1] << 16) + (id[0] << 24);
        }
    }
}
