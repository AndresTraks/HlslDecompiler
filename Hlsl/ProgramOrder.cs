using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// What became of a register component either side of an instruction: what last
/// wrote it, and what first reads it.
///
/// The instruction writer keeps one HLSL variable per register and has no notion of
/// a value, so every question about what a register is holding has to be answered by
/// reading the instructions around it. Three of those questions were being answered
/// by three walks of their own, each calling IndexOf to find where to start; this is
/// the walking, in one place, with the position looked up once.
///
/// Only the walk. What a reader makes of what it reads, and what a writer put there,
/// are questions about HLSL's own types that the writer answers - see GetConsumedKind
/// and GetProducedKind - and the component an operand reads is settled by the
/// signature of the intrinsic it is going to be written as, which is further into the
/// writer still. So the operand's components come in as a function and the kinds stay
/// out of here entirely: callers get the instruction back and ask their own question
/// of it.
///
/// In program order, straight through the branches, which is what it was before and
/// is sound only because of how it is used: the first reader of a value is nearly
/// always on the value's own path, since fxc puts a use next to what makes it and
/// flattens a small branch into a movc rather than jumping. A movc says nothing about
/// what it carries, so the rules built on this stay conservative where two paths
/// disagree. Anything added here that is not conservative in that direction wants a
/// real reaching-definitions pass instead.
/// </summary>
public sealed class ProgramOrder
{
    private readonly IList<Instruction> _instructions;

    /// <summary>The components a source operand reads, which only the writer knows:
    /// a load's address is as wide as the resource has dimensions whatever the
    /// destination mask says.</summary>
    private readonly Func<D3D10Instruction, int, int[]> _sourceComponents;

    /// <summary>Where each instruction sits, so that a walk starting at one does not
    /// have to find it first. First occurrence, the way IndexOf answered: a phase
    /// unrolled into a copy per run can hold the same instruction twice.</summary>
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
    /// The last instruction before this one to write a register component, or null
    /// where nothing did. What put a value in a register is the only thing that says
    /// what the value is, the register itself holding a texel address here and a
    /// colour there.
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
    /// The first instruction after this one to read a register component, with the
    /// operand it reads it as, or null where nothing reads it before something writes
    /// over it. What reads a value is what says what it was, where the instruction
    /// that made it does not - a dword out of a buffer is whatever was stored there.
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
            // The read comes first, because an instruction can write the register it
            // read: a mad over r0.x into r0.x is a read and then the end of what was
            // in it.
            if (Writes(next, register, component))
            {
                return null;
            }
        }
        return null;
    }

    /// <summary>
    /// Whether a double instruction reads a register pair as a double before anything
    /// writes over it. A pair is a double because something read it as one, there
    /// being no other mark of it: `asdouble` costs no instruction.
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
                // Which pair of the operand each value of the instruction reads. fxc
                // repeats a double operand's pair across the four swizzle slots, so
                // the nth double is at slot 2n.
                byte[] swizzle = next.GetSourceSwizzleComponents(operand);
                for (int value = 0; value < next.ValueCount; value++)
                {
                    if (swizzle[next.GetValuePair(value) * 2] / 2 == pair)
                    {
                        return true;
                    }
                }
            }
            // Either half ends the pair, as above: a dmul over r0.xy into r0.xy reads
            // it and then writes it.
            if (Writes(next, register, pair * 2) || Writes(next, register, pair * 2 + 1))
            {
                return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Where the instruction sits, or null for one that is not in this phase at all.
    ///
    /// Both walks answer "nothing found" for that rather than reading from the top of
    /// the shader, which is what looking the position up with IndexOf used to do: not
    /// finding it gave -1, and a walk from one past that starts at the first
    /// instruction and answers about a part of the program the caller never asked
    /// about. Nothing in the corpus reaches it either way. Saying nothing is the
    /// direction every rule built on this already leans.
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

    /// <summary>Whether an operand reads the register at all - a source operand, and
    /// one naming a register rather than an immediate.</summary>
    private static bool ReadsAt(D3D10Instruction instruction, int operandIndex, RegisterKey register)
    {
        return !instruction.IsDestinationOperand(operandIndex)
            && instruction.GetOperandType(operandIndex)
                is not (OperandType.Immediate32 or OperandType.Immediate64)
            && instruction.GetParamRegisterKey(operandIndex) is D3D10RegisterKey source
            && source.Equals(register);
    }
}
