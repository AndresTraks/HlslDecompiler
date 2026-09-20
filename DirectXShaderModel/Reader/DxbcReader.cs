using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HlslDecompiler.DirectXShaderModel;

public class DxbcReader : BinaryReader
{
    private bool _isGeometryShader = false;

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
        var patchConstantSignatures = new List<RegisterSignature>();
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
                _isGeometryShader = shaderType == ShaderType.Geometry;

                var structuredElements = new Dictionary<string, ShaderTypeInfo>();
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
                    BaseStream.Position = chunkOffset + nameOffset + 8;
                    string bufferName = ReadStringNullTerminated();

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

                        ShaderTypeInfo typeInfo = ReadShaderTypeInfo(chunkOffset, variableTypeOffset);

                        // TODO
                        short registerNumber = (short)i;
                        short elementOffset = (short)j;
                        // A resource bind info buffer describes a structured
                        // buffer rather than being one, and is named after it.
                        if (bufferType == D3DCbufferType.ResourceBindInfo)
                        {
                            structuredElements[bufferName] = typeInfo;
                            continue;
                        }

                        var description = new D3D10ConstantDeclaration(name, registerNumber, variableSize, variableOffset, typeInfo, elementOffset)
                        {
                            BufferName = bufferName,
                            IsTextureBuffer = bufferType == D3DCbufferType.Tbuffer,
                        };
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
                    if (structuredElements.TryGetValue(name, out ShaderTypeInfo element))
                    {
                        resourceDefinition.ElementType = element;
                    }
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
            // A geometry shader that names its stream writes the output signature
            // with the stream beside each element, in a chunk of its own. Reading
            // only OSGN left a shader model 5 geometry shader with no output
            // semantics at all, so every output fell back to SV_Target.
            else if (chunkType == "OSG5")
            {
                ReadSignatures(chunkOffset, OperandType.Output, outputSignatures,
                    hasStream: true);
            }
            else if (chunkType == "PCSG")
            {
                // What the hull shader computed once for the whole patch. A domain
                // shader reads these; they are not in the input signature, and the
                // tessellation factors among them are there whether it reads them
                // or not.
                ReadSignatures(chunkOffset, OperandType.InputPatchConstant,
                    patchConstantSignatures);
            }
            else if (chunkType == "SHDR" || chunkType == "SHEX")
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
            patchConstantSignatures,
            constantDeclarations,
            resourceDefinitions,
            instructions);
    }

    private D3D10Instruction ReadInstruction()
    {
        uint opcodeToken = ReadUInt32();
        D3D10Opcode opcode = (D3D10Opcode)(opcodeToken & 0x7FF);

        // customdata does not carry its length where every other instruction does.
        // Its payload - an immediate constant buffer, say - is longer than the seven
        // bits in the opcode token, so the dword after it holds the length instead,
        // counting both. Reading it the usual way asks for an array of -1 dwords.
        if (opcode == D3D10Opcode.CustomData)
        {
            uint customDwordCount = ReadUInt32();
            var customData = new uint[customDwordCount < 2 ? 0 : customDwordCount - 2];
            for (int i = 0; i < customData.Length; i++)
            {
                customData[i] = ReadUInt32();
            }
            return D3D10Instruction.CreateCustomData(customData, _isGeometryShader);
        }

        int operandDwordCount = (int)((opcodeToken >> 24) & 0x7F) - 1;

        // An extended opcode token carries the texel offsets of sample_aoffimmi,
        // among other things, and counts towards the instruction length. Dropping it
        // would sample the wrong texels without saying so.
        int[] sampleOffsets = null;
        uint extendedToken = opcodeToken;
        while ((extendedToken & 0x80000000) != 0)
        {
            extendedToken = ReadUInt32();
            operandDwordCount--;
            const int SampleControls = 1;
            if ((extendedToken & 0x3F) == SampleControls)
            {
                sampleOffsets =
                [
                    SignExtend4((extendedToken >> 9) & 0xF),
                    SignExtend4((extendedToken >> 13) & 0xF),
                    SignExtend4((extendedToken >> 17) & 0xF),
                ];
            }
        }

        if (opcode == D3D10Opcode.DclGlobalFlags)
        {
            D3D10GlobalFlags globalFlags = (D3D10GlobalFlags)((opcodeToken >> 11) & 0x1ff);
            return new D3D10Instruction(opcode, globalFlags, _isGeometryShader);
        }

        if (opcode == D3D10Opcode.DclGSInputPrimitive)
        {
            D3D10Primitive inputPrimitive = (D3D10Primitive)((opcodeToken >> 11) & 0xff);
            return new D3D10Instruction(opcode, inputPrimitive, _isGeometryShader);
        }

        if (opcode == D3D10Opcode.DclGSOutputPrimitiveTopology)
        {
            D3D10PrimitiveTopology primitiveTopology = (D3D10PrimitiveTopology)((opcodeToken >> 11) & 0xff);
            return new D3D10Instruction(opcode, primitiveTopology, _isGeometryShader);
        }

        uint[] operandTokens = new uint[operandDwordCount];
        for (int i = 0; i < operandDwordCount; i++)
        {
            operandTokens[i] = ReadUInt32();
        }

        // A typed UAV declares its dimension in the same bits a texture does: it
        // is a texture that can be written to, and has no sample count.
        if (opcode is D3D10Opcode.DclResource or D3D10Opcode.DclUnorderedAccessViewTyped)
        {
            var resourceDimension = (ResourceDimension)((opcodeToken >> 11) & 0x1F);
            // A multisampled texture carries its sample count in the seven bits above
            // the dimension: dcl_resource_texture2dms(4).
            int sampleCount = (int)((opcodeToken >> 16) & 0x7F);
            return new D3D10Instruction(opcode, operandTokens, resourceDimension, _isGeometryShader)
            {
                ResourceSampleCount = sampleCount,
            };
        }

        var instruction = new D3D10Instruction(opcode, operandTokens, _isGeometryShader);
        instruction.Saturate = !opcode.IsDeclaration() && (opcodeToken & 0x2000) != 0;
        instruction.SampleOffsets = sampleOffsets;
        if (opcode.HasBooleanTest())
        {
            instruction.TestNonZero = (opcodeToken & 0x40000) != 0;
        }
        if (opcode == D3D10Opcode.ResInfo)
        {
            instruction.ResInfoReturnType = (D3D10ResInfoReturnType)((opcodeToken >> 11) & 0x3);
        }
        // sampleinfo reports one number and has one bit for how, where resinfo has
        // two: the reciprocal form is not one of its choices, so 1 here means the
        // uint that is 2 there.
        if (opcode == D3D10Opcode.SampleInfo)
        {
            instruction.ResInfoReturnType = ((opcodeToken >> 11) & 0x1) != 0
                ? D3D10ResInfoReturnType.Uint
                : D3D10ResInfoReturnType.Float;
        }
        // The tessellator domain and the control point count ride in the opcode
        // token the way a resource dimension does.
        if (opcode == D3D10Opcode.DclTessDomain)
        {
            instruction.TessellatorDomain = (D3D10TessellatorDomain)((opcodeToken >> 11) & 0x3);
        }
        if (opcode is D3D10Opcode.DclInputControlPointCount
            or D3D10Opcode.DclOutputControlPointCount)
        {
            instruction.ControlPointCount = (int)((opcodeToken >> 11) & 0x7F);
        }
        if (opcode == D3D10Opcode.Sync)
        {
            instruction.SyncFlags = (D3D10SyncFlags)((opcodeToken >> 11) & 0xF);
        }
        if (opcode == D3D10Opcode.DclInputPS
            || opcode == D3D10Opcode.DclInputPSSgv
            || opcode == D3D10Opcode.DclInputPSSiv)
        {
            instruction.InterpolationMode = (D3D10InterpolationMode)((opcodeToken >> 11) & 0xF);
        }
        if (opcode == D3D10Opcode.DclSampler)
        {
            instruction.SamplerMode = (D3D10SamplerMode)((opcodeToken >> 11) & 0xF);
        }
        return instruction;
    }

    // A variable type, and the members of a struct one. The member list was being
    // discarded, so a struct in a constant buffer had no members to name.
    private ShaderTypeInfo ReadShaderTypeInfo(int chunkOffset, int typeOffset)
    {
        BaseStream.Position = chunkOffset + typeOffset + 8;
        ParameterClass variableClass = (ParameterClass)ReadInt16();
        ParameterType variableType = (ParameterType)ReadInt16();
        short rows = ReadInt16();
        short columns = ReadInt16();
        short numElements = ReadInt16();
        short numStructMembers = ReadInt16();
        int firstMemberOffset = ReadInt32();

        List<ShaderStructMemberInfo> memberInfo = null;
        if (numStructMembers > 0)
        {
            memberInfo = [];
            for (int member = 0; member < numStructMembers; member++)
            {
                // Reading a member type moves the stream, so the position is set
                // again for every one of them.
                BaseStream.Position = chunkOffset + firstMemberOffset + member * 12 + 8;
                int memberNameOffset = ReadInt32();
                int memberTypeOffset = ReadInt32();
                // The third word of the member record, which was being strided
                // past: where the member starts within the structure. A load from
                // a structured buffer names a byte offset and nothing else, so
                // this is what says which member it reads.
                int memberByteOffset = ReadInt32();

                BaseStream.Position = chunkOffset + memberNameOffset + 8;
                string memberName = ReadStringNullTerminated();
                memberInfo.Add(new ShaderStructMemberInfo(
                    memberName, ReadShaderTypeInfo(chunkOffset, memberTypeOffset), memberByteOffset));
            }
        }

        return new ShaderTypeInfo(
            variableClass, variableType, rows, columns, numElements, memberInfo);
    }

    // A texel offset is four bits, signed.
    private static int SignExtend4(uint value)
    {
        return (int)((value ^ 8) - 8);
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

    private void ReadSignatures(int chunkOffset, OperandType operandType,
        IList<RegisterSignature> signatures, bool hasStream = false)
    {
        ReadInt32();
        int elementCount = ReadInt32();
        ReadInt32();
        long elementOffset = BaseStream.Position;

        int elementSize = hasStream ? 28 : 24;
        for (int i = 0; i < elementCount; i++)
        {
            BaseStream.Position = elementOffset + i * elementSize;
            if (hasStream)
            {
                ReadInt32();
            }
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
        return new RegisterSignature(register, name, index, mask, valueType, componentType, readWriteMask);
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
