using HlslDecompiler.Util;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HlslDecompiler.DirectXShaderModel;

public class ConstantTableCommentReader : BinaryReader
{
    public ConstantTableCommentReader(D3D9Instruction instruction)
        : base(ReadRawTable(instruction))
    {
    }

    public ConstantTable ReadTable()
    {
        if (BaseStream.Length == 0)
        {
            return new ConstantTable();
        }

        // D3DXSHADER_CONSTANTTABLE
        int ctabSize = ReadInt32();
        System.Diagnostics.Debug.Assert(ctabSize == 0x1C);
        long creatorPosition = ReadInt32();

        int minorVersion = ReadByte();
        int majorVersion = ReadByte();
        var shaderType = (ShaderType)ReadUInt16();

        int numConstants = ReadInt32();
        long constantInfoPosition = ReadInt32();
        ShaderFlags shaderFlags = (ShaderFlags)ReadInt32();

        long shaderModelPosition = ReadInt32();

        BaseStream.Position = creatorPosition;
        string compilerInfo = ReadStringNullTerminated();

        BaseStream.Position = shaderModelPosition;
        string shaderModel = ReadStringNullTerminated();

        IList<D3D9ConstantDeclaration> declarations = [];
        for (int i = 0; i < numConstants; i++)
        {
            BaseStream.Position = constantInfoPosition + i * 20;
            D3D9ConstantDeclaration constant = ReadConstantDeclaration();
            declarations.Add(constant);
        }

        return new ConstantTable(minorVersion, majorVersion, shaderType, shaderFlags, compilerInfo, shaderModel, declarations);
    }

    private D3D9ConstantDeclaration ReadConstantDeclaration()
    {
        // D3DXSHADER_CONSTANTINFO
        int nameOffset = ReadInt32();
        RegisterSet registerSet = (RegisterSet)ReadInt16();
        short registerIndex = ReadInt16();
        short registerCount = ReadInt16();
        BaseStream.Position += sizeof(short); // Reserved
        int typeInfoOffset = ReadInt32();
        int defaultValueOffset = ReadInt32();

        BaseStream.Position = nameOffset;
        string name = ReadStringNullTerminated();

        BaseStream.Position = typeInfoOffset;
        ShaderTypeInfo typeInfo = ReadTypeInfo();

        if (defaultValueOffset != 0)
        {
            BaseStream.Position = defaultValueOffset;
            // TODO
        }

        return new D3D9ConstantDeclaration(name, registerSet, registerIndex, registerCount, typeInfo);
    }

    private ShaderTypeInfo ReadTypeInfo()
    {
        // D3DXSHADER_TYPEINFO
        ParameterClass cl = (ParameterClass)ReadInt16();
        ParameterType type = (ParameterType)ReadInt16();
        short rows = ReadInt16();
        short columns = ReadInt16();
        short numElements = ReadInt16();
        short numStructMembers = ReadInt16();
        int structMemberInfoOffset = ReadInt32();

        IList<ShaderStructMemberInfo> memberInfo = null;
        if (cl == ParameterClass.Struct)
        {
            // D3DXSHADER_STRUCTMEMBERINFO
            memberInfo = new List<ShaderStructMemberInfo>();
            for (int i = 0; i < numStructMembers; i++)
            {
                BaseStream.Position = structMemberInfoOffset + i * 8;

                int structMemberNameOffset = ReadInt32();
                int structMemberTypeInfoOffset = ReadInt32();

                BaseStream.Position = structMemberNameOffset;
                string structMemberName = ReadStringNullTerminated();

                BaseStream.Position = structMemberTypeInfoOffset;
                ShaderTypeInfo typeInfo = ReadTypeInfo();
                memberInfo.Add(new ShaderStructMemberInfo(structMemberName, typeInfo));
            }
        }

        return new ShaderTypeInfo(cl, type, rows, columns, numElements, memberInfo);
    }

    private static MemoryStream ReadRawTable(D3D9Instruction instruction)
    {
        System.Diagnostics.Debug.Assert(instruction.Opcode == Opcode.Comment);

        if (instruction.Params[0] != FourCC.Make("CTAB"))
        {
            return new MemoryStream();
        }

        byte[] constantTable = new byte[instruction.Params.Count * 4];
        for (int i = 1; i < instruction.Params.Count; i++)
        {
            constantTable[i * 4 - 4] = (byte)(instruction.Params[i] & 0xFF);
            constantTable[i * 4 - 3] = (byte)((instruction.Params[i] >> 8) & 0xFF);
            constantTable[i * 4 - 2] = (byte)((instruction.Params[i] >> 16) & 0xFF);
            constantTable[i * 4 - 1] = (byte)((instruction.Params[i] >> 24) & 0xFF);
        }

        return new MemoryStream(constantTable);
    }

    private string ReadStringNullTerminated()
    {
        var builder = new StringBuilder();
        char b;
        while ((b = (char)BaseStream.ReadByte()) != 0)
        {
            builder.Append(b);
        }
        return builder.ToString();
    }
}
