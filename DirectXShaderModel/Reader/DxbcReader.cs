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
            var constantDeclarations = new List<D3D10ConstantDeclaration>();
            var resourceDefinitions = new List<ResourceDefinition>();

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
                        D3D10ShaderCbufferFlags flags = (D3D10ShaderCbufferFlags)ReadInt32();
                        D3DCbufferType bufferType = (D3DCbufferType)ReadInt32();

                        for (int j = 0; j < variableCount; j++)
                        {
                            BaseStream.Position = chunkOffset + variableDescriptionOffset + j * 24 + 8;
                            int variableNameOffset = ReadInt32();
                            int variableOffset = ReadInt32();
                            int variableSize = ReadInt32();
                            D3D10ShaderVariableFlags variableFlags = (D3D10ShaderVariableFlags)ReadInt32();
                            int variableTypeOffset = ReadInt32();
                            int defaultValueOffset = ReadInt32();

                            BaseStream.Position = chunkOffset + variableNameOffset + 8;
                            string name = ReadStringNullTerminated();

                            BaseStream.Position = chunkOffset + variableTypeOffset + 8;
                            ParameterClass variableClass = (ParameterClass)ReadInt16();
                            ParameterType variableType = (ParameterType)ReadInt16();
                            short rows = ReadInt16();
                            short columns = ReadInt16();
                            short numElements = ReadInt16();
                            short numStructMembers = ReadInt16();
                            int firstMemberOffset = ReadInt32();
                            var typeInfo = new ShaderTypeInfo(variableClass, variableType, rows, columns, numElements, null);

                            // TODO
                            short registerNumber = (short)i;
                            short elementOffset = (short)j;
                            var description = new D3D10ConstantDeclaration(name, registerNumber, (short)variableSize, typeInfo, elementOffset);
                            constantDeclarations.Add(description);
                        }
                    }

                    resourceBindingOffset = chunkOffset + resourceBindingOffset + 8;
                    for (int i = 0; i < resourceBindingCount; i++)
                    {
                        BaseStream.Position = resourceBindingOffset + i * 32;
                        int bindingNameOffset = ReadInt32();
                        D3DShaderInputType shaderInputType = (D3DShaderInputType) ReadInt32();
                        D3DResourceReturnType resourceReturnType = (D3DResourceReturnType) ReadInt32();
                        int resourceViewDimension = ReadInt32();
                        int numSamples = ReadInt32();
                        int bindPoint = ReadInt32();
                        int bindCount = ReadInt32();
                        D3DShaderInputFlags flags = (D3DShaderInputFlags) ReadInt32();

                        BaseStream.Position = chunkOffset + bindingNameOffset + 8;
                        string name = ReadStringNullTerminated();

                        var resourceDefinition = new ResourceDefinition(
                            name,
                            shaderInputType,
                            resourceReturnType,
                            resourceViewDimension,
                            numSamples,
                            bindPoint,
                            bindCount,
                            flags);
                        resourceDefinitions.Add(resourceDefinition);
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

            return new ShaderModel(
                majorVersion.Value,
                minorVersion.Value,
                shaderType.Value,
                inputSignatures,
                outputSignatures,
                constantDeclarations,
                resourceDefinitions,
                instructions);
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

            if (opcode == D3D10Opcode.DclResource)
            {
                var resourceDimension = (ResourceDimension)((opcodeToken >> 11) & 0x1F);
                return new D3D10Instruction(opcode, operandTokens, resourceDimension);
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
            name = NormalizeSystemValueRegisterName(name);

            var register = new D3D10RegisterKey(operandType, registerNumber);
            return new RegisterSignature(register, name, index, mask);
        }

        private static string NormalizeSystemValueRegisterName(string name)
        {
            if (name == null)
            {
                return null;
            }
            if (name.Equals("SV_Position", StringComparison.OrdinalIgnoreCase))
            {
                return "SV_Position";
            }
            return name;
        }
    }
}
