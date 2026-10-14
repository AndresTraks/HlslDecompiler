using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// For the instruction writer, which has no notion of a value: the last writer and
/// first reader of a register component around an instruction. Only the walk; callers
/// classify what they get back.
///
/// Walks in program order straight through branches. Sound only because fxc keeps a
/// use near its definition and flattens small branches to movc, and the callers stay
/// conservative; anything less conservative needs real reaching definitions.
/// </summary>
public sealed class ProgramOrder
{
    private readonly IList<Instruction> _instructions;

    /// <summary>Supplied by the writer: e.g. a load's address width follows the
    /// resource, not the destination mask.</summary>
    private readonly Func<D3D10Instruction, int, int[]> _sourceComponents;

    /// <summary>First occurrence: an unrolled phase can hold an instruction twice.</summary>
    private readonly Dictionary<D3D10Instruction, int> _positions;

    public ProgramOrder(ShaderModel shader, Func<D3D10Instruction, int, int[]> sourceComponents)
    {
        _instructions = shader.Instructions;
        _sourceComponents = sourceComponents;
        _positions = new Dictionary<D3D10Instruction, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < _instructions.Count; i++)
        {
            if (_instructions[i] is D3D10Instruction d3d10)
            {
                _positions.TryAdd(d3d10, i);
            }
        }
    }

    /// <summary>
    /// The last earlier instruction to write a register component, or null.
    /// </summary>
    public D3D10Instruction LastWriterOf(D3D10Instruction before, RegisterKey register, int component)
    {
        if (PositionOf(before) is not int position)
        {
            return null;
        }
        for (int i = position - 1; i >= 0; i--)
        {
            if (_instructions[i] is D3D10Instruction previous
                && Writes(previous, register, component))
            {
                return previous;
            }
        }
        return null;
    }

    /// <summary>
    /// The first later reader of a register component and its operand, or null if it
    /// is overwritten first.
    /// </summary>
    public (D3D10Instruction Reader, int OperandIndex)? FirstReaderOf(
        D3D10Instruction after, RegisterKey register, int component)
    {
        if (PositionOf(after) is not int position)
        {
            return null;
        }
        for (int i = position + 1; i < _instructions.Count; i++)
        {
            if (_instructions[i] is not D3D10Instruction next)
            {
                continue;
            }
            for (int operand = 0; operand < next.OperandTokens.OperandCount; operand++)
            {
                if (!ReadsAt(next, operand, register)
                    || !_sourceComponents(next, operand).Contains(component))
                {
                    continue;
                }
                return (next, operand);
            }
            // Reads checked first: `mad r0.x, r0.x, ...` reads, then overwrites.
            if (Writes(next, register, component))
            {
                return null;
            }
        }
        return null;
    }

    /// <summary>
    /// Whether a register pair is read as a double before being overwritten; nothing
    /// else marks a double, since `asdouble` costs no instruction.
    /// </summary>
    public bool IsReadAsDouble(D3D10Instruction after, RegisterKey register, int pair)
    {
        if (PositionOf(after) is not int position)
        {
            return false;
        }
        for (int i = position + 1; i < _instructions.Count; i++)
        {
            if (_instructions[i] is not D3D10Instruction next)
            {
                continue;
            }
            for (int operand = 1; operand < next.OperandTokens.OperandCount; operand++)
            {
                if (!next.IsDoubleOperand(operand) || !ReadsAt(next, operand, register))
                {
                    continue;
                }
                // fxc repeats a double's pair in the swizzle: the nth double is at slot 2n.
                byte[] swizzle = next.GetSourceSwizzleComponents(operand);
                for (int value = 0; value < next.ValueCount; value++)
                {
                    if (swizzle[next.GetValuePair(value) * 2] / 2 == pair)
                    {
                        return true;
                    }
                }
            }
            // Either half ends the pair, after the reads.
            if (Writes(next, register, pair * 2) || Writes(next, register, pair * 2 + 1))
            {
                return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Null for an instruction outside this phase; the walks then find nothing.
    /// </summary>
    private int? PositionOf(D3D10Instruction instruction)
    {
        return _positions.TryGetValue(instruction, out int position) ? position : null;
    }

    private static bool Writes(D3D10Instruction instruction, RegisterKey register, int component)
    {
        return instruction.GetDestinationParamIndex() is int destination
            && instruction.GetParamRegisterKey(destination) is D3D10RegisterKey written
            && written.Equals(register)
            && (instruction.GetWriteMask(destination) & (1 << component)) != 0;
    }

    /// <summary>A source operand naming the register, not an immediate.</summary>
    private static bool ReadsAt(D3D10Instruction instruction, int operandIndex, RegisterKey register)
    {
        return !instruction.IsDestinationOperand(operandIndex)
            && instruction.GetOperandType(operandIndex)
                is not (OperandType.Immediate32 or OperandType.Immediate64)
            && instruction.GetParamRegisterKey(operandIndex) is D3D10RegisterKey source
            && source.Equals(register);
    }
}
