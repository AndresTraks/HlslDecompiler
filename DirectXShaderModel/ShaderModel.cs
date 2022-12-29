using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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

        public ShaderModel(int majorVersion, int minorVersion, ShaderType type)
        {
            MajorVersion = majorVersion;
            MinorVersion = minorVersion;
            Type = type;

            Instructions = new List<Instruction>();
            InputSignatures = new List<RegisterSignature>();
            OutputSignatures = new List<RegisterSignature>();
        }

        static string ReadStringNullTerminated(Stream stream)
        {
            StringBuilder builder = new StringBuilder();
            char b;
            while ((b = (char)stream.ReadByte()) != 0)
            {
                builder.Append(b);
            }
            return builder.ToString();
        }

        public IList<ConstantDeclaration> ParseConstantTable()
        {
            var constantDeclarations = new List<ConstantDeclaration>();

            byte[] constantTable = GetConstantTableData();
            if (constantTable == null)
            {
                return constantDeclarations;
            }

            var ctabStream = new MemoryStream(constantTable);
            using (var ctabReader = new BinaryReader(ctabStream))
            {
                int ctabSize = ctabReader.ReadInt32();
                System.Diagnostics.Debug.Assert(ctabSize == 0x1C);
                long creatorPosition = ctabReader.ReadInt32();

                int minorVersion = ctabReader.ReadByte();
                int majorVersion = ctabReader.ReadByte();
                System.Diagnostics.Debug.Assert(majorVersion == MajorVersion);
                System.Diagnostics.Debug.Assert(minorVersion == MinorVersion);

                var shaderType = (ShaderType)ctabReader.ReadUInt16();
                System.Diagnostics.Debug.Assert(shaderType == Type);

                int numConstants = ctabReader.ReadInt32();
                long constantInfoPosition = ctabReader.ReadInt32();
                ShaderFlags shaderFlags = (ShaderFlags)ctabReader.ReadInt32();
                Console.WriteLine("Flags: {0}", shaderFlags);

                long shaderModelPosition = ctabReader.ReadInt32();
                //Console.WriteLine("ctabStart = {0}, shaderModelPosition = {1}", ctabStart, shaderModelPosition);


                ctabStream.Position = creatorPosition;
                string compilerInfo = ReadStringNullTerminated(ctabStream);
                Console.WriteLine("Compiler: " + compilerInfo);

                ctabStream.Position = shaderModelPosition;
                string shaderModel = ReadStringNullTerminated(ctabStream);
                Console.WriteLine("Shader model: " + shaderModel);


                for (int i = 0; i < numConstants; i++)
                {
                    ctabStream.Position = constantInfoPosition + i * 20;
                    ConstantDeclaration declaration = ReadConstantDeclaration(ctabReader);
                    constantDeclarations.Add(declaration);
                }
            }

            return constantDeclarations;
        }

        private byte[] GetConstantTableData()
        {
            int ctabToken = FourCC.Make("CTAB");
            var ctabComment = Instructions
                .OfType<D3D9Instruction>()
                .FirstOrDefault(x => x.Opcode == Opcode.Comment && x.Params[0] == ctabToken);
            if (ctabComment == null)
            {
                return null;
            }

            byte[] constantTable = new byte[ctabComment.Params.Count * 4];
            for (int i = 1; i < ctabComment.Params.Count; i++)
            {
                constantTable[i * 4 - 4] = (byte)(ctabComment.Params[i] & 0xFF);
                constantTable[i * 4 - 3] = (byte)((ctabComment.Params[i] >> 8) & 0xFF);
                constantTable[i * 4 - 2] = (byte)((ctabComment.Params[i] >> 16) & 0xFF);
                constantTable[i * 4 - 1] = (byte)((ctabComment.Params[i] >> 24) & 0xFF);
            }

            return constantTable;
        }

        private ConstantDeclaration ReadConstantDeclaration(BinaryReader ctabReader)
        {
            var ctabStream = ctabReader.BaseStream;

            // D3DXSHADER_CONSTANTINFO
            int nameOffset = ctabReader.ReadInt32();
            RegisterSet registerSet = (RegisterSet)ctabReader.ReadInt16();
            short registerIndex = ctabReader.ReadInt16();
            short registerCount = ctabReader.ReadInt16();
            ctabStream.Position += sizeof(short); // Reserved
            int typeInfoOffset = ctabReader.ReadInt32();
            int defaultValueOffset = ctabReader.ReadInt32();
            System.Diagnostics.Debug.Assert(defaultValueOffset == 0);

            ctabStream.Position = nameOffset;
            string name = ReadStringNullTerminated(ctabStream);

            // D3DXSHADER_TYPEINFO
            ctabStream.Position = typeInfoOffset;
            ParameterClass cl = (ParameterClass)ctabReader.ReadInt16();
            ParameterType type = (ParameterType)ctabReader.ReadInt16();
            short rows = ctabReader.ReadInt16();
            short columns = ctabReader.ReadInt16();
            short numElements = ctabReader.ReadInt16();
            short numStructMembers = ctabReader.ReadInt16();
            int structMemberInfoOffset = ctabReader.ReadInt32();
            //System.Diagnostics.Debug.Assert(numElements == 1);
            System.Diagnostics.Debug.Assert(structMemberInfoOffset == 0);

            return new ConstantDeclaration(name, registerSet, registerIndex, registerCount, cl, type, rows, columns);
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
