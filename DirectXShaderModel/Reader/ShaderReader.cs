using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HlslDecompiler.DirectXShaderModel;

public class ShaderReader : BinaryReader
{
    public ShaderReader(Stream input, bool leaveOpen = false)
        : base(input, new UTF8Encoding(false, true), leaveOpen)
    {
    }

    virtual public ShaderModel ReadShader()
    {
        // Version token
        byte minorVersion = ReadByte();
        byte majorVersion = ReadByte();
        ShaderType shaderType = (ShaderType)ReadUInt16();

        var instructions = new List<Instruction>();
        while (true)
        {
            D3D9Instruction instruction = (majorVersion == 1)
                ? ReadFixedSizeInstruction()
                : ReadDynamicSizeInstruction();
            instructions.Add(instruction);
            if (instruction.Opcode == Opcode.End) break;
        }

        return new ShaderModel(majorVersion, minorVersion, shaderType, instructions);
    }

    private D3D9Instruction ReadDynamicSizeInstruction()
    {
        uint instructionToken = ReadUInt32();
        Opcode opcode = (Opcode)(instructionToken & 0xffff);

        int size;
        if (opcode == Opcode.Comment)
        {
            size = (int)((instructionToken >> 16) & 0x7FFF);
        }
        else
        {
            size = (int)((instructionToken >> 24) & 0x0f);
        }

        uint[] paramTokens = new uint[size];
        for (int i = 0; i < size; i++)
        {
            paramTokens[i] = ReadUInt32();
        }
        var instruction = new D3D9Instruction(instructionToken, paramTokens);
        InstructionVerifier.Verify(instruction);
        return instruction;
    }

    private D3D9Instruction ReadFixedSizeInstruction()
    {
        uint instructionToken = ReadUInt32();
        Opcode opcode = (Opcode)(instructionToken & 0xffff);

        int size;
        switch (opcode)
        {
            case Opcode.Comment:
                size = (int)((instructionToken >> 16) & 0x7FFF);
                break;
            default:
                size = GetOperationFixedSize(opcode);
                break;
        }

        uint[] paramTokens = new uint[size];
        for (int i = 0; i < size; i++)
        {
            paramTokens[i] = ReadUInt32();
        }
        var instruction = new D3D9Instruction(instructionToken, paramTokens);
        InstructionVerifier.Verify(instruction);
        return instruction;
    }

    private static int GetOperationFixedSize(Opcode opcode)
    {
        switch (opcode)
        {
            case Opcode.End:
            case Opcode.Nop:
                return 0;
            case Opcode.Dcl:
            case Opcode.Exp:
            case Opcode.ExpP:
            case Opcode.Frc:
            case Opcode.Lit:
            case Opcode.Log:
            case Opcode.LogP:
            case Opcode.Mov:
            case Opcode.Rcp:
            case Opcode.Rsq:
                return 2;
            case Opcode.Add:
            case Opcode.Dp3:
            case Opcode.Dp4:
            case Opcode.Dst:
            case Opcode.M3x2:
            case Opcode.M3x3:
            case Opcode.M3x4:
            case Opcode.M4x3:
            case Opcode.M4x4:
            case Opcode.Max:
            case Opcode.Min:
            case Opcode.Mul:
            case Opcode.Sge:
            case Opcode.Slt:
            case Opcode.Sub:
                return 3;
            case Opcode.Mad:
                return 4;
            case Opcode.Def:
                return 5;
            default:
                throw new NotImplementedException(opcode.ToString());
        }
    }
}
