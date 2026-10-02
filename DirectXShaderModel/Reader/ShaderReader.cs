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

        bool impliedInputSemantics = shaderType == ShaderType.Pixel && majorVersion < 3;
        var instructions = new List<Instruction>();
        while (true)
        {
            D3D9Instruction instruction = (majorVersion == 1)
                ? ReadFixedSizeInstruction(minorVersion)
                : ReadDynamicSizeInstruction();
            instruction.HasImpliedInputSemantics = impliedInputSemantics;
            instruction.HasSeparateRelativeToken = majorVersion > 1;
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

    private D3D9Instruction ReadFixedSizeInstruction(byte minorVersion)
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
                size = GetOperationFixedSize(opcode, minorVersion);
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

    // Shader model 1 has no length field in the instruction token, so the operand
    // count is the opcode's own. Only the two instructions ps_1_4 renamed take a
    // different number there: `tex t0` becomes `texld r0, t0` and `texcoord t0`
    // becomes `texcrd r0, t0`, both reading the coordinate they used to imply.
    private static int GetOperationFixedSize(Opcode opcode, byte minorVersion)
    {
        switch (opcode)
        {
            case Opcode.Tex:
            case Opcode.TexCoord:
                return minorVersion >= 4 ? 2 : 1;
            case Opcode.End:
            case Opcode.Nop:
            case Opcode.Phase:
                return 0;
            case Opcode.TexDepth:
            case Opcode.TexKill:
                return 1;
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
            // The ps_1_x texture addressing set, every one of which takes the
            // register it writes and the one it reads the coordinates from.
            case Opcode.TexBem:
            case Opcode.TexBeml:
            case Opcode.TexDP3:
            case Opcode.TexDP3Tex:
            case Opcode.TexM3x2Depth:
            case Opcode.TeXM3x2Pad:
            case Opcode.TexM3x2Tex:
            case Opcode.TexM3x3:
            case Opcode.TexM3x3Diff:
            case Opcode.TeXM3x3Pad:
            case Opcode.TexM3x3Tex:
            case Opcode.TexM3x3VSpec:
            case Opcode.TexReg2AR:
            case Opcode.TexReg2GB:
            case Opcode.TexReg2RGB:
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
            // texm3x3spec reflects an eye ray, which is the second source; bem
            // takes the two it adds.
            case Opcode.Bem:
            case Opcode.TexM3x3Spec:
                return 3;
            case Opcode.Cmp:
            case Opcode.Cnd:
            case Opcode.Lrp:
            case Opcode.Mad:
                return 4;
            case Opcode.Def:
                return 5;
            default:
                throw new NotImplementedException(opcode.ToString());
        }
    }
}
