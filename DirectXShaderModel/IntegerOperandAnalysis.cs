using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.DirectXShaderModel;

public sealed class IntegerOperandAnalysis
{
    private HashSet<RegisterComponentKey> _integerRegisters;
    private readonly ShaderModel _shader;

    public IntegerOperandAnalysis(ShaderModel shader)
    {
        _shader = shader;
    }

    public bool IsIntegerOperand(D3D10Instruction instruction)
    {
        if (instruction.Opcode.IsInteger())
        {
            return true;
        }

        _integerRegisters ??= FindIntegerRegisters(_shader);
        return IsIntegerMove(instruction);
    }

    private static HashSet<RegisterComponentKey> FindIntegerRegisters(ShaderModel shader)
    {
        var integerRegisters = new HashSet<RegisterComponentKey>();

        foreach (D3D10Instruction instruction in shader.Instructions.OfType<D3D10Instruction>())
        {
            if (instruction.Opcode != D3D10Opcode.IToF
                && instruction.Opcode != D3D10Opcode.UTof
                && GetSourceCount(instruction.Opcode) != 0)
            {
                AddDestinationComponents(instruction, integerRegisters);
            }
            if (instruction.Opcode != D3D10Opcode.Ftoi && GetSourceCount(instruction.Opcode) != 0)
            {
                AddSourceComponents(instruction, integerRegisters);
            }
        }

        return integerRegisters;
    }

    private bool IsIntegerMove(D3D10Instruction instruction)
    {
        if (instruction.Opcode != D3D10Opcode.Mov || _integerRegisters.Count == 0)
        {
            return false;
        }

        var components = new HashSet<RegisterComponentKey>();
        AddDestinationComponents(instruction, components);
        return components.Overlaps(_integerRegisters);
    }

    private static void AddDestinationComponents(D3D10Instruction instruction, HashSet<RegisterComponentKey> components)
    {
        int? destinationIndex = instruction.GetDestinationParamIndex();
        if (destinationIndex == null)
        {
            return;
        }

        RegisterKey registerKey = instruction.GetParamRegisterKey(destinationIndex.Value);
        int writeMask = instruction.GetDestinationWriteMask();
        for (int component = 0; component < 4; component++)
        {
            if ((writeMask & (1 << component)) != 0)
            {
                components.Add(new RegisterComponentKey(registerKey, component));
            }
        }
    }

    private static void AddSourceComponents(D3D10Instruction instruction, HashSet<RegisterComponentKey> components)
    {
        int sourceCount = GetSourceCount(instruction.Opcode);
        for (int source = 1; source <= sourceCount; source++)
        {
            if (instruction.GetOperandType(source) == OperandType.Immediate32)
            {
                continue;
            }

            RegisterKey registerKey = instruction.GetParamRegisterKey(source);
            foreach (byte component in instruction.GetSourceSwizzleComponents(source))
            {
                components.Add(new RegisterComponentKey(registerKey, component));
            }
        }
    }

    private static int GetSourceCount(D3D10Opcode opcode)
    {
        switch (opcode)
        {
            case D3D10Opcode.Ftoi:
            case D3D10Opcode.IToF:
            case D3D10Opcode.UTof:
            case D3D10Opcode.INeg:
                return 1;
            case D3D10Opcode.IAdd:
            case D3D10Opcode.IShl:
            case D3D10Opcode.Ieq:
            case D3D10Opcode.Ige:
            case D3D10Opcode.UGE:
            case D3D10Opcode.ULT:
            case D3D10Opcode.Ilt:
            case D3D10Opcode.IMin:
            case D3D10Opcode.IMax:
            case D3D10Opcode.IMul:
            case D3D10Opcode.Ine:
                return 2;
            case D3D10Opcode.IMad:
                return 3;
            default:
                return 0;
        }
    }
}
