using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HlslDecompiler.DirectXShaderModel;

public class DxbcReader : BinaryReader
{
    // Geometry and hull shader inputs name a vertex too: v[2][0].
    private bool _inputsAreVertexArrays = false;

    // Shader model 5's longer type record, which ends in the type's name.
    private bool _typesHaveNames = false;

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
        ShaderInterfaces interfaces = null;

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
                _inputsAreVertexArrays = shaderType is ShaderType.Geometry or ShaderType.Hull;
                ReadInt32(); // flags
                ReadInt32(); // creator string offset

                // Shader model 5 has a longer variable record, sized in an RD11
                // block after the header.
                int variableRecordSize = 24;
                if (FourCC.Decode(ReadInt32()) == "RD11")
                {
                    ReadInt32(); // header size
                    ReadInt32(); // constant buffer record size
                    ReadInt32(); // resource binding record size
                    variableRecordSize = ReadInt32();
                    _typesHaveNames = true;
                }

                // Each buffer's cb slot, from the resource bindings; the buffer
                // list order is not the slot order.
                var bufferSlots = new Dictionary<string, int>();
                long bindingsStart = chunkOffset + resourceBindingOffset + 8;
                for (int i = 0; i < resourceBindingCount; i++)
                {
                    BaseStream.Position = bindingsStart + i * 32;
                    int boundNameOffset = ReadInt32();
                    var boundType = (D3DShaderInputType)ReadInt32();
                    BaseStream.Position = bindingsStart + i * 32 + 20;
                    int boundPoint = ReadInt32();
                    if (boundType is D3DShaderInputType.CBuffer or D3DShaderInputType.TBuffer)
                    {
                        BaseStream.Position = chunkOffset + boundNameOffset + 8;
                        bufferSlots[ReadStringNullTerminated()] = boundPoint;
                    }
                }

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
                        BaseStream.Position = chunkOffset + variableDescriptionOffset + j * variableRecordSize + 8;
                        int variableNameOffset = ReadInt32();
                        int variableOffset = ReadInt32();
                        int variableSize = ReadInt32();
                        D3D10ShaderVariableFlags variableFlags = (D3D10ShaderVariableFlags)ReadInt32();
                        int variableTypeOffset = ReadInt32();
                        int defaultValueOffset = ReadInt32();

                        BaseStream.Position = chunkOffset + variableNameOffset + 8;
                        string name = ReadStringNullTerminated();

                        ShaderTypeInfo typeInfo = ReadShaderTypeInfo(chunkOffset, variableTypeOffset);

                        short registerNumber = (short)bufferSlots.GetValueOrDefault(bufferName, i);
                        short elementOffset = (short)j;
                        // Describes the structured buffer it is named after.
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
            // The output signature with a stream per element (SM5 geometry shaders).
            else if (chunkType == "OSG5")
            {
                ReadSignatures(chunkOffset, OperandType.Output, outputSignatures,
                    hasStream: true);
            }
            else if (chunkType == "PCSG")
            {
                ReadSignatures(chunkOffset, OperandType.InputPatchConstant,
                    patchConstantSignatures);
            }
            else if (chunkType == "IFCE")
            {
                interfaces = ReadInterfaces(chunkOffset + 8);
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

        AssignOutputStreams(instructions);

        return new ShaderModel(
            majorVersion.Value,
            minorVersion.Value,
            shaderType.Value,
            inputSignatures,
            outputSignatures,
            patchConstantSignatures,
            constantDeclarations,
            resourceDefinitions,
            instructions)
        {
            Interfaces = interfaces,
        };
    }

    /// <summary>
    /// The IFCE chunk. Offsets count from the chunk's data. A class type's ID is
    /// its list index; fxc leaves the record's ID field zero.
    /// </summary>
    private ShaderInterfaces ReadInterfaces(int dataStart)
    {
        BaseStream.Position = dataStart;
        int classInstanceCount = ReadInt32();
        int classTypeCount = ReadInt32();
        int slotRecordCount = ReadInt32();
        ReadInt32(); // slot count
        ReadInt32(); // class instance offset
        int classTypeOffset = ReadInt32();
        int slotRecordOffset = ReadInt32();

        var interfaces = new ShaderInterfaces { ClassInstanceCount = classInstanceCount };
        for (int i = 0; i < classTypeCount; i++)
        {
            BaseStream.Position = dataStart + classTypeOffset + i * 12;
            int nameOffset = ReadInt32();
            BaseStream.Position = dataStart + nameOffset;
            interfaces.ClassTypeNames.Add(ReadStringNullTerminated());
        }
        for (int i = 0; i < slotRecordCount; i++)
        {
            BaseStream.Position = dataStart + slotRecordOffset + i * 16;
            int slotSpan = ReadInt32();
            int count = ReadInt32();
            int typeIdsOffset = ReadInt32();
            int tableIdsOffset = ReadInt32();
            var typeIds = new int[count];
            BaseStream.Position = dataStart + typeIdsOffset;
            for (int j = 0; j < count; j++)
            {
                typeIds[j] = ReadUInt16();
            }
            var tableIds = new int[count];
            BaseStream.Position = dataStart + tableIdsOffset;
            for (int j = 0; j < count; j++)
            {
                tableIds[j] = ReadInt32();
            }
            interfaces.SlotRecords.Add(new InterfaceSlotRecord
            {
                SlotSpan = slotSpan,
                TypeIds = typeIds,
                TableIds = tableIds,
            });
        }
        return interfaces;
    }

    /// <summary>
    /// Marks each instruction with its output stream when a geometry shader has
    /// several, since each stream numbers its own o# registers. A declaration
    /// belongs to the preceding dcl_stream, a write to the next emit.
    /// </summary>
    private static void AssignOutputStreams(List<Instruction> instructions)
    {
        var d3d10 = new List<D3D10Instruction>();
        int streamCount = 0;
        foreach (Instruction instruction in instructions)
        {
            if (instruction is D3D10Instruction d3d10Instruction)
            {
                d3d10.Add(d3d10Instruction);
                if (d3d10Instruction.Opcode == D3D10Opcode.DclStream)
                {
                    streamCount++;
                }
            }
        }
        if (streamCount < 2)
        {
            return;
        }

        int? declaredStream = null;
        foreach (D3D10Instruction instruction in d3d10)
        {
            if (instruction.Opcode == D3D10Opcode.DclStream)
            {
                declaredStream = instruction.GetParamRegisterNumber(0);
            }
            else if (instruction.Opcode.IsDeclaration())
            {
                instruction.Stream = declaredStream;
            }
        }

        int? emittedStream = null;
        for (int i = d3d10.Count - 1; i >= 0; i--)
        {
            D3D10Instruction instruction = d3d10[i];
            if (instruction.Opcode is D3D10Opcode.EmitStream or D3D10Opcode.CutStream
                or D3D10Opcode.EmitThenCutStream)
            {
                emittedStream = instruction.GetParamRegisterNumber(0);
                instruction.Stream = emittedStream;
            }
            else if (!instruction.Opcode.IsDeclaration())
            {
                instruction.Stream = emittedStream;
            }
        }
    }

    private D3D10Instruction ReadInstruction()
    {
        uint opcodeToken = ReadUInt32();
        D3D10Opcode opcode = (D3D10Opcode)(opcodeToken & 0x7FF);

        // customdata's length is in the next dword (counting both), not the opcode
        // token.
        if (opcode == D3D10Opcode.CustomData)
        {
            uint customDwordCount = ReadUInt32();
            var customData = new uint[customDwordCount < 2 ? 0 : customDwordCount - 2];
            for (int i = 0; i < customData.Length; i++)
            {
                customData[i] = ReadUInt32();
            }
            return D3D10Instruction.CreateCustomData(customData, _inputsAreVertexArrays);
        }

        int operandDwordCount = (int)((opcodeToken >> 24) & 0x7F) - 1;

        // Extended opcode tokens count towards the instruction length.
        int[] sampleOffsets = null;
        ResourceDimension? indexableDimension = null;
        int indexableStride = 0;
        int? indexableReturnTypes = null;
        uint extendedToken = opcodeToken;
        while ((extendedToken & 0x80000000) != 0)
        {
            extendedToken = ReadUInt32();
            operandDwordCount--;
            const int SampleControls = 1;
            const int ResourceDim = 2;
            const int ResourceReturnType = 3;
            switch (extendedToken & 0x3F)
            {
                case SampleControls:
                    sampleOffsets =
                    [
                        SignExtend4((extendedToken >> 9) & 0xF),
                        SignExtend4((extendedToken >> 13) & 0xF),
                        SignExtend4((extendedToken >> 17) & 0xF),
                    ];
                    break;
                case ResourceDim:
                    indexableDimension = (ResourceDimension)((extendedToken >> 6) & 0x1F);
                    indexableStride = (int)((extendedToken >> 11) & 0xFFFF);
                    break;
                case ResourceReturnType:
                    indexableReturnTypes = (int)((extendedToken >> 6) & 0xFFFF);
                    break;
            }
        }

        if (opcode == D3D10Opcode.DclGlobalFlags)
        {
            D3D10GlobalFlags globalFlags = (D3D10GlobalFlags)((opcodeToken >> 11) & 0x1ff);
            return new D3D10Instruction(opcode, globalFlags, _inputsAreVertexArrays);
        }

        if (opcode == D3D10Opcode.DclGSInputPrimitive)
        {
            D3D10Primitive inputPrimitive = (D3D10Primitive)((opcodeToken >> 11) & 0xff);
            return new D3D10Instruction(opcode, inputPrimitive, _inputsAreVertexArrays);
        }

        if (opcode == D3D10Opcode.DclGSOutputPrimitiveTopology)
        {
            D3D10PrimitiveTopology primitiveTopology = (D3D10PrimitiveTopology)((opcodeToken >> 11) & 0xff);
            return new D3D10Instruction(opcode, primitiveTopology, _inputsAreVertexArrays);
        }

        uint[] operandTokens = new uint[operandDwordCount];
        for (int i = 0; i < operandDwordCount; i++)
        {
            operandTokens[i] = ReadUInt32();
        }

        if (opcode is D3D10Opcode.DclResource or D3D10Opcode.DclUnorderedAccessViewTyped)
        {
            var resourceDimension = (ResourceDimension)((opcodeToken >> 11) & 0x1F);
            int sampleCount = (int)((opcodeToken >> 16) & 0x7F);
            return new D3D10Instruction(opcode, operandTokens, resourceDimension, _inputsAreVertexArrays)
            {
                ResourceSampleCount = sampleCount,
            };
        }

        var instruction = new D3D10Instruction(opcode, operandTokens, _inputsAreVertexArrays);
        instruction.Saturate = !opcode.IsDeclaration() && (opcodeToken & 0x2000) != 0;
        instruction.SampleOffsets = sampleOffsets;
        instruction.IndexableResourceDimension = indexableDimension;
        instruction.IndexableResourceStride = indexableStride;
        instruction.IndexableResourceReturnTypeToken = indexableReturnTypes;
        if (opcode == D3D10Opcode.DclUnorderedAccessViewStructured
            || opcode == D3D10Opcode.DclUnorderedAccessViewRaw)
        {
            // Bit 23, found from the bytecode rather than documentation.
            instruction.HasOrderPreservingCounter = (opcodeToken & 0x800000) != 0;
        }
        if (opcode.HasBooleanTest())
        {
            instruction.TestNonZero = (opcodeToken & 0x40000) != 0;
        }
        if (opcode == D3D10Opcode.ResInfo)
        {
            instruction.ResInfoReturnType = (D3D10ResInfoReturnType)((opcodeToken >> 11) & 0x3);
        }
        // sampleinfo has one bit: 1 means uint (2 in resinfo's two bits).
        if (opcode == D3D10Opcode.SampleInfo)
        {
            instruction.ResInfoReturnType = ((opcodeToken >> 11) & 0x1) != 0
                ? D3D10ResInfoReturnType.Uint
                : D3D10ResInfoReturnType.Float;
        }
        if (opcode == D3D10Opcode.DclTessDomain)
        {
            instruction.TessellatorDomain = (D3D10TessellatorDomain)((opcodeToken >> 11) & 0x3);
        }
        if (opcode is D3D10Opcode.DclInputControlPointCount
            or D3D10Opcode.DclOutputControlPointCount)
        {
            instruction.ControlPointCount = (int)((opcodeToken >> 11) & 0x7F);
        }
        if (opcode == D3D10Opcode.DclTessPartitioning)
        {
            instruction.TessellatorPartitioning =
                (D3D10TessellatorPartitioning)((opcodeToken >> 11) & 0x7);
        }
        if (opcode == D3D10Opcode.DclTessOutputPrimitive)
        {
            instruction.TessellatorOutputPrimitive =
                (D3D10TessellatorOutputPrimitive)((opcodeToken >> 11) & 0x7);
        }
        if (opcode == D3D10Opcode.DclConstantBuffer)
        {
            instruction.IsDynamicallyIndexed = ((opcodeToken >> 11) & 0x1) != 0;
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

        // Interface and struct type names, at the end of the SM5 record.
        string name = null;
        if (_typesHaveNames && variableClass is ParameterClass.InterfacePointer or ParameterClass.Struct)
        {
            BaseStream.Position = chunkOffset + typeOffset + 8 + 32;
            int nameOffset = ReadInt32();
            if (nameOffset != 0)
            {
                BaseStream.Position = chunkOffset + nameOffset + 8;
                name = ReadStringNullTerminated();
            }
        }

        List<ShaderStructMemberInfo> memberInfo = null;
        if (numStructMembers > 0)
        {
            memberInfo = [];
            for (int member = 0; member < numStructMembers; member++)
            {
                // Reading a member type moves the stream.
                BaseStream.Position = chunkOffset + firstMemberOffset + member * 12 + 8;
                int memberNameOffset = ReadInt32();
                int memberTypeOffset = ReadInt32();
                int memberByteOffset = ReadInt32();

                BaseStream.Position = chunkOffset + memberNameOffset + 8;
                string memberName = ReadStringNullTerminated();
                memberInfo.Add(new ShaderStructMemberInfo(
                    memberName, ReadShaderTypeInfo(chunkOffset, memberTypeOffset), memberByteOffset));
            }
        }

        return new ShaderTypeInfo(
            variableClass, variableType, rows, columns, numElements, memberInfo)
        {
            Name = name,
        };
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
            int stream = hasStream ? ReadInt32() : 0;
            RegisterSignature signature = ReadSignature(chunkOffset, operandType, stream);
            signatures.Add(signature);
        }
    }

    private RegisterSignature ReadSignature(int chunkOffset, OperandType operandType,
        int stream = 0)
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
        return new RegisterSignature(register, name, index, mask, valueType, componentType,
            readWriteMask)
        {
            Stream = stream,
        };
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
