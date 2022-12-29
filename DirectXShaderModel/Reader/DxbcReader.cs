using HlslDecompiler.Util;
using System;
using System.IO;
using System.Text;

namespace HlslDecompiler.DirectXShaderModel
{
    public class DxbcReader : BinaryReader
    {
        public DxbcReader(Stream input, bool leaveOpen = false)
            : base(input, new UTF8Encoding(false, true), leaveOpen)
        {
        }

        virtual public ShaderModel ReadShader()
        {
            int dxbc = ReadInt32();
            System.Diagnostics.Debug.Assert(dxbc == FourCC.Make("DXBC"));

            ReadBytes(16); // checksum
            ReadInt32(); // 1

            ReadInt32(); // totalSize

            int chunkCount = ReadInt32();
            int[] chunkOffsets = new int[chunkCount];
            for (int i = 0; i < chunkCount; i++)
            {
                chunkOffsets[i] = ReadInt32();
            }

            ShaderModel shader = null;
            foreach (int chunkOffset in chunkOffsets)
            {
                BaseStream.Position = chunkOffset;
                string chunkType = FourCC.Decode(ReadInt32());
                if (chunkType == "RDEF")
                {
                    ReadBytes(20);
                    byte majorVersion = ReadByte();
                    byte minorVersion = ReadByte();
                    ShaderType shaderType = (ShaderType)ReadUInt16();
                    shader = new ShaderModel(minorVersion, majorVersion, shaderType);
                }
                else if (chunkType == "ISGN")
                {
                    ReadInt32();
                    int elementCount = ReadInt32();
                    ReadInt32();
                    long elementOffset = BaseStream.Position;
                    for (int i = 0; i < elementCount; i++)
                    {
                        BaseStream.Position = elementOffset + i * 24;
                        int nameOffset = ReadInt32();
                        int index = ReadInt32();
                        int operandType = ReadInt32();
                        int componentType = ReadInt32();
                        int registerNumber = ReadInt32();
                        byte mask = ReadByte();
                        byte readWriteMask = ReadByte();

                        BaseStream.Position = chunkOffset + nameOffset + 8;
                        string name = ReadStringNullTerminated();

                        var register = new D3D10RegisterKey(OperandType.Input, registerNumber);
                        var signature = new RegisterSignature(register, name, index, mask);
                        shader.InputSignatures.Add(signature);
                    }
                }
                else if (chunkType == "OSGN")
                {
                    ReadInt32();
                    int elementCount = ReadInt32();
                    ReadInt32();
                    long elementOffset = BaseStream.Position;
                    for (int i = 0; i < elementCount; i++)
                    {
                        BaseStream.Position = elementOffset + i * 24;
                        RegisterSignature signature = ReadSignature(chunkOffset);
                        shader.OutputSignatures.Add(signature);
                    }
                }
                else if (chunkType == "SHDR")
                {
                    ReadBytes(8);
                    int chunkSize = ReadInt32() * 4;
                    long chunkEnd = BaseStream.Position + chunkSize - 8;
                    while (BaseStream.Position < chunkEnd)
                    {
                        D3D10Instruction instruction = ReadInstruction();
                        InstructionVerifier.Verify(instruction);
                        shader.Instructions.Add(instruction);
                    }
                }
            }

            return shader;
        }

        private D3D10Instruction ReadInstruction()
        {
            uint opcodeToken = ReadUInt32();
            D3D10Opcode opcode = (D3D10Opcode)(opcodeToken & 0x7FF);

            int operandCount = (int)((opcodeToken >> 24) & 0x7F) - 1;

            bool isExtended = (opcodeToken & 0x80000000) != 0;
            if (isExtended)
            {
                throw new NotImplementedException();
            }

            uint[] operandTokens = new uint[operandCount];
            for (int i = 0; i < operandCount; i++)
            {
                operandTokens[i] = ReadUInt32();
            }
            var instruction = new D3D10Instruction(opcode, operandTokens);
            return instruction;
        }

        private string ReadStringNullTerminated()
        {
            StringBuilder builder = new StringBuilder();
            char b;
            while ((b = (char)BaseStream.ReadByte()) != 0)
            {
                builder.Append(b);
            }
            return builder.ToString();
        }

        private RegisterSignature ReadSignature(int chunkOffset)
        {
            int nameOffset = ReadInt32();
            int index = ReadInt32();
            int operandType = ReadInt32();
            int componentType = ReadInt32();
            int registerNumber = ReadInt32();
            byte mask = ReadByte();
            byte readWriteMask = ReadByte();

            BaseStream.Position = chunkOffset + nameOffset + 8;
            string name = ReadStringNullTerminated();

            var register = new D3D10RegisterKey(OperandType.Output, registerNumber);
            return new RegisterSignature(register, name, index, mask);
        }
    }
}
