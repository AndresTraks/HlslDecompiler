using System;
using System.Collections.Generic;
using System.IO;

namespace HlslDecompiler
{
    public enum ShaderType
    {
        Vertex = 0xFFFE,
        Pixel = 0xFFFF
    }

    // D3DXSHADER
    [Flags]
    enum ShaderFlags
    {
        Debug = 1,
        SkipValidation = 2,
        SkipOptimization = 4,
        RowMajor = 8,
        ColumnMajor = 16,
        PartialPrecision = 32,
        ForceVSSoftwareNoOpt = 64,
        ForcePSSoftwareNoOpt = 128,
        NoPreShader = 256,
        AvoidFlowControl = 512,
        PreferFlowControl = 1024,
        EnableBackwardsCompatibility = 2048,
        IEEEStrictness = 4096,
        UseLegacyD3DX931Dll = 8192
    }

    public class ShaderModel
    {
        public int MajorVersion { get; private set; }
        public int MinorVersion { get; private set; }
        public ShaderType Type { get; private set; }

        public ICollection<Instruction> Instructions { get; private set; }

        public ShaderModel(int majorVersion, int minorVersion, ShaderType type)
        {
            MajorVersion = majorVersion;
            MinorVersion = minorVersion;
            Type = type;

            Instructions = new List<Instruction>();
        }

        public void ToFile(string filename)
        {
            FileStream file = new FileStream(filename, FileMode.Create, FileAccess.Write);
            using (BinaryWriter writer = new BinaryWriter(file))
            {
                writer.Write((byte)MinorVersion);
                writer.Write((byte)MajorVersion);
                writer.Write((ushort)Type);

                foreach (Instruction i in Instructions)
                {
                    uint instruction =
                        (uint)i.Opcode |
                        (uint)(i.Modifier << 16) |
                        ((uint)(i.Params.Length << (i.Opcode == Opcode.Comment ? 16 : 24))) |
                        (i.Predicated ? (uint)(0x10000000) : 0);
                    writer.Write(instruction);
                    foreach (uint param in i.Params)
                    {
                        writer.Write(param);
                    }
                }
            }
        }
    }
}
