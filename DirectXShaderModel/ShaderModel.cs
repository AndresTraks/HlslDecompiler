using System.Collections.Generic;
using System.IO;

namespace HlslDecompiler.DirectXShaderModel
{
    public class ShaderModel
    {
        public int MajorVersion { get; }
        public int MinorVersion { get; }
        public ShaderType Type { get; }

        public IList<Instruction> Instructions { get; }
        public IList<RegisterSignature> InputSignatures { get; }
        public IList<RegisterSignature> OutputSignatures { get; }
        public IList<ConstantDeclaration> ConstantDeclarations { get; }
        public IList<ConstantBufferDescription> ConstantBufferDescriptions { get; }

        public ShaderModel(int majorVersion, int minorVersion, ShaderType type, IList<RegisterSignature> inputSignatures, IList<RegisterSignature> outputSignatures, IList<ConstantBufferDescription> constantBufferDescriptions, IList<Instruction> instructions)
        {
            MajorVersion = majorVersion;
            MinorVersion = minorVersion;
            Type = type;
            InputSignatures = inputSignatures;
            OutputSignatures = outputSignatures;
            ConstantDeclarations = new List<ConstantDeclaration>();
            ConstantBufferDescriptions = constantBufferDescriptions;
            Instructions = instructions;
        }

        public ShaderModel(int majorVersion, int minorVersion, ShaderType type, IList<ConstantDeclaration> constantDeclarations, IList<Instruction> instructions)
        {
            MajorVersion = majorVersion;
            MinorVersion = minorVersion;
            Type = type;
            InputSignatures = new List<RegisterSignature>();
            OutputSignatures = new List<RegisterSignature>();
            ConstantDeclarations = constantDeclarations;
            ConstantBufferDescriptions = new List<ConstantBufferDescription>();
            Instructions = instructions;
        }

        public void ToFile(string filename)
        {
            FileStream file = new FileStream(filename, FileMode.Create, FileAccess.Write);
            using (BinaryWriter writer = new BinaryWriter(file))
            {
                writer.Write((byte)MinorVersion);
                writer.Write((byte)MajorVersion);
                writer.Write((ushort)Type);

                foreach (D3D9Instruction i in Instructions)
                {
                    uint instruction =
                        (uint)i.Opcode |
                        (uint)(i.Modifier << 16) |
                        ((uint)(i.Params.Count << (i.Opcode == Opcode.Comment ? 16 : 24))) |
                        (i.Predicated ? (uint)(0x10000000) : 0);
                    writer.Write(instruction);
                    for (int p = 0; p < i.Params.Count; p++)
                    {
                        writer.Write(i.Params[p]);
                    }
                }
            }
        }
    }
}
