using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;

namespace HlslDecompiler.Tests.Interpreter;

/// <summary>
/// Pairs each block opening instruction with the one that closes it, so that the
/// machine can jump without rescanning.
/// </summary>
public static class ControlFlow
{
    /// <summary>
    /// Maps if to its else or, lacking one, its endif; else to its endif; and rep
    /// or loop to its endrep or endloop.
    /// </summary>
    public static Dictionary<int, int> Match(IReadOnlyList<D3D9Instruction> instructions)
    {
        var matches = new Dictionary<int, int>();
        var open = new Stack<int>();
        for (int i = 0; i < instructions.Count; i++)
        {
            switch (instructions[i].Opcode)
            {
                case Opcode.If:
                case Opcode.IfC:
                case Opcode.Rep:
                case Opcode.Loop:
                    open.Push(i);
                    break;
                case Opcode.Else:
                    // The if now ends at this else; the else ends where the if would
                    // have, so it takes the if's place on the stack.
                    matches[open.Pop()] = i;
                    open.Push(i);
                    break;
                case Opcode.Endif:
                case Opcode.EndRep:
                case Opcode.EndLoop:
                    matches[open.Pop()] = i;
                    break;
            }
        }
        return matches;
    }
}
