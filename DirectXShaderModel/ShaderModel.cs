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
        public IList<D3D10ConstantDeclaration> ConstantDeclarations { get; }
        public IList<ResourceDefinition> ResourceDefinitions { get; }

        public ShaderModel(int majorVersion,
            int minorVersion,
            ShaderType type,
            IList<RegisterSignature> inputSignatures, 
            IList<RegisterSignature> outputSignatures, 
            IList<D3D10ConstantDeclaration> constantDeclarations, 
            IList<ResourceDefinition> resourceDefinitions,
            IList<Instruction> instructions)
        {
            MajorVersion = majorVersion;
            MinorVersion = minorVersion;
            Type = type;
            InputSignatures = inputSignatures;
            OutputSignatures = outputSignatures;
            ConstantDeclarations = constantDeclarations;
            ResourceDefinitions = resourceDefinitions;
            Instructions = instructions;
        }

        public ShaderModel(int majorVersion, int minorVersion, ShaderType type, IList<Instruction> instructions)
        {
            MajorVersion = majorVersion;
            MinorVersion = minorVersion;
            Type = type;
            InputSignatures = new List<RegisterSignature>();
            OutputSignatures = new List<RegisterSignature>();
            ConstantDeclarations = new List<D3D10ConstantDeclaration>();
            Instructions = instructions;
        }

        public void ToFile(string filename)
        {
            var file = new FileStream(filename, FileMode.Create, FileAccess.Write);
            using (var writer = new BinaryWriter(file))
            {
                writer.Write((byte)MinorVersion);
                writer.Write((byte)MajorVersion);
                writer.Write((ushort)Type);

                foreach (D3D9Instruction i in Instructions)
                {
                    writer.Write(i.InstructionToken);
                    for (int p = 0; p < i.Params.Count; p++)
                    {
                        writer.Write(i.Params[p]);
                    }
                }
            }
        }
    }
}
