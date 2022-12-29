using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
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

            byte? majorVersion = null;
            byte? minorVersion = null;
            ShaderType? shaderType = null;
            var inputSignatures = new List<RegisterSignature>();
            var outputSignatures = new List<RegisterSignature>();
            var instructions = new List<Instruction>();
            var constantBufferDescriptions = new List<ConstantBufferDescription>();

            foreach (int chunkOffset in chunkOffsets)
            {
                BaseStream.Position = chunkOffset;
                string chunkType = FourCC.Decode(ReadInt32());
                if (chunkType == "RDEF")
                {
                    ReadInt32();
                    int constantBufferCount = ReadInt32();
                    int constantBufferOffset = ReadInt32();
                    int resourceBindingCount = ReadInt32();
                    int resourceBindingOffset = ReadInt32();
                    minorVersion = ReadByte();
                    majorVersion = ReadByte();
                    shaderType = (ShaderType)ReadUInt16();

                    constantBufferOffset = chunkOffset + constantBufferOffset + 8;
                    for (int i = 0; i < constantBufferCount; i++)
                    {
                        BaseStream.Position = constantBufferOffset + i * 24;
                        int nameOffset = ReadInt32();
                        int variableCount = ReadInt32();
                        int variableDescriptionOffset = ReadInt32();
                        int size = ReadInt32();
                        int flags = ReadInt32();
                        int bufferType = ReadInt32();

                        for (int j = 0; j < variableCount; j++)
                        {
                            BaseStream.Position = chunkOffset + variableDescriptionOffset + j * 24 + 8;
                            int variableNameOffset = ReadInt32();
                            int variableOffset = ReadInt32();
                            int variableSize = ReadInt32();
                            int variableTypeOffset = ReadInt32();
                            int defaultValueOffset = ReadInt32();

                            BaseStream.Position = chunkOffset + variableNameOffset + 8;
                            string name = ReadStringNullTerminated();

                            // TODO
                            int registerNumber = variableOffset / 16;
                            int maskedSize = variableSize / 4;
                            var description = new ConstantBufferDescription(registerNumber, maskedSize, name);
                            constantBufferDescriptions.Add(description);
                        }
                    }

                    resourceBindingOffset = chunkOffset + resourceBindingOffset + 8;
                    for (int i = 0; i < resourceBindingCount; i++)
                    {
                        BaseStream.Position = resourceBindingOffset + i * 40;
                        int bindingNameOffset = ReadInt32();
                        int shaderInputType = ReadInt32();
                        int resourceReturnType = ReadInt32();
                        int resourceViewDimension = ReadInt32();
                        int numSamples = ReadInt32();
                        int bindPoint = ReadInt32();
                        int bindCount = ReadInt32();
                        int flags = ReadInt32();

                        BaseStream.Position = chunkOffset + bindingNameOffset + 8;
                        string name = ReadStringNullTerminated();
                        name.ToString();
                    }
                }
                else if (chunkType == "ISGN")
                {
                    ReadSignatures(chunkOffset, OperandType.Input, inputSignatures);
                }
                else if (chunkType == "OSGN")
                {
                    ReadSignatures(chunkOffset, OperandType.Output, outputSignatures);
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
                        instructions.Add(instruction);
                    }
                }
            }

            return new ShaderModel(majorVersion.Value, minorVersion.Value, shaderType.Value, inputSignatures, outputSignatures, constantBufferDescriptions, instructions);
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

        private void ReadSignatures(int chunkOffset, OperandType operandType, IList<RegisterSignature> signatures)
        {
            ReadInt32();
            int elementCount = ReadInt32();
            ReadInt32();
            long elementOffset = BaseStream.Position;

            for (int i = 0; i < elementCount; i++)
            {
                BaseStream.Position = elementOffset + i * 24;
                RegisterSignature signature = ReadSignature(chunkOffset, operandType);
                signatures.Add(signature);
            }
        }

        private RegisterSignature ReadSignature(int chunkOffset, OperandType operandType)
        {
            int nameOffset = ReadInt32();
            int index = ReadInt32();
            int valueType = ReadInt32();
            int componentType = ReadInt32();
            int registerNumber = ReadInt32();
            byte mask = ReadByte();
            byte readWriteMask = ReadByte();

            BaseStream.Position = chunkOffset + nameOffset + 8;
            string name = ReadStringNullTerminated();

            var register = new D3D10RegisterKey(operandType, registerNumber);
            return new RegisterSignature(register, name, index, mask);
        }
    }
}
