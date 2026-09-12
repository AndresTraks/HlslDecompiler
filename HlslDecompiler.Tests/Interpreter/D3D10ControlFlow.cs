using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;

namespace HlslDecompiler.Tests.Interpreter;

/// <summary>
/// Where each block ends, and which case a switch jumps to, so that the machine
/// does not rescan to find out.
/// </summary>
public class D3D10ControlFlow
{
    /// <summary>
    /// if to its else or, lacking one, its endif; else to its endif; loop to its
    /// endloop; switch to its endswitch.
    /// </summary>
    public Dictionary<int, int> Blocks { get; } = [];

    /// <summary>Which of the matched blocks are loops rather than switches.</summary>
    private readonly HashSet<int> _loops = [];

    public bool IsLoop(int index) => _loops.Contains(index);

    /// <summary>Each switch, to the value each of its cases matches.</summary>
    private readonly Dictionary<int, Dictionary<uint, int>> _cases = [];

    /// <summary>Each switch, to its default, when it has one.</summary>
    private readonly Dictionary<int, int> _defaults = [];

    public static D3D10ControlFlow Match(IReadOnlyList<D3D10Instruction> instructions)
    {
        var flow = new D3D10ControlFlow();
        var open = new Stack<int>();
        var switches = new Stack<int>();

        for (int i = 0; i < instructions.Count; i++)
        {
            D3D10Instruction instruction = instructions[i];
            switch (instruction.Opcode)
            {
                case D3D10Opcode.Loop:
                    flow._loops.Add(i);
                    open.Push(i);
                    break;
                case D3D10Opcode.If:
                    open.Push(i);
                    break;
                case D3D10Opcode.Swtich:
                    open.Push(i);
                    switches.Push(i);
                    flow._cases[i] = [];
                    break;
                case D3D10Opcode.Else:
                    // The if ends at this else; the else ends where the if would
                    // have, so it takes the if's place.
                    flow.Blocks[open.Pop()] = i;
                    open.Push(i);
                    break;
                case D3D10Opcode.Case:
                    flow._cases[switches.Peek()]
                        [unchecked((uint)instruction.GetParamInt(0, 0))] = i;
                    break;
                case D3D10Opcode.Default:
                    flow._defaults[switches.Peek()] = i;
                    break;
                case D3D10Opcode.EndSwitch:
                    switches.Pop();
                    flow.Blocks[open.Pop()] = i;
                    break;
                case D3D10Opcode.EndIf:
                case D3D10Opcode.EndLoop:
                    flow.Blocks[open.Pop()] = i;
                    break;
            }
        }
        return flow;
    }

    /// <summary>
    /// Where a switch on this value continues: the matching case, the default, or
    /// past the end when it has neither.
    /// </summary>
    public int SwitchTarget(int switchIndex, uint value)
    {
        if (_cases[switchIndex].TryGetValue(value, out int match))
        {
            return match + 1;
        }
        if (_defaults.TryGetValue(switchIndex, out int fallback))
        {
            return fallback + 1;
        }
        return Blocks[switchIndex] + 1;
    }
}
