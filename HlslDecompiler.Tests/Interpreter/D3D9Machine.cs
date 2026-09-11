using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Tests.Interpreter;

/// <summary>
/// Runs a shader model 1 to 3 shader, so that two decompilations of one shader can
/// be compared by what they compute and not only by whether they compile.
///
/// Register numbers do not survive a round trip - fxc allocates its own - so
/// everything crossing the boundary is named instead: constants by their
/// declaration, inputs and outputs by their semantic.
/// </summary>
public class D3D9Machine
{
    /// <summary>
    /// Thrown for anything not modelled. The caller skips the shader rather than
    /// reporting a difference, since an instruction that did not run is not
    /// evidence either way.
    /// </summary>
    public class UnsupportedException(string message) : Exception(message);

    private readonly ShaderModel _shader;
    private readonly float[][] _temp = NewFile(64);
    private readonly float[][] _input = NewFile(16);
    private readonly float[][] _texture = NewFile(16);
    private readonly float[][] _const = NewFile(256);
    private readonly float[][] _output = NewFile(16);
    private readonly int[][] _constInt = NewIntFile(16);
    private readonly bool[] _constBool = new bool[16];
    private readonly float[] _address = new float[4];
    private readonly Dictionary<int, string> _outputSemantics = [];
    private readonly Dictionary<string, float[]> _results = [];
    private readonly int _trial;
    private int _loopCounter;

    /// <summary>Set by texkill: the pixel is discarded, whatever was written.</summary>
    public bool Killed { get; private set; }

    private D3D9Machine(ShaderModel shader, int trial)
    {
        _shader = shader;
        _trial = trial;
    }

    private static float[][] NewFile(int count)
    {
        var file = new float[count][];
        for (int i = 0; i < count; i++)
        {
            file[i] = new float[4];
        }
        return file;
    }

    private static int[][] NewIntFile(int count)
    {
        var file = new int[count][];
        for (int i = 0; i < count; i++)
        {
            file[i] = new int[4];
        }
        return file;
    }

    /// <summary>
    /// Runs the shader and returns what it wrote, by semantic. A shader that
    /// kills the pixel returns no results. Every constant and input is derived
    /// from its own name and the trial number, so that two programs holding the
    /// same value in different registers still agree on it.
    /// </summary>
    public static Dictionary<string, float[]> Run(ShaderModel shader, int trial)
    {
        var machine = new D3D9Machine(shader, trial);
        machine.LoadConstants();
        machine.LoadDeclarations();
        machine.Execute();
        return machine.Killed ? [] : machine._results;
    }

    private void LoadConstants()
    {
        foreach (D3D9ConstantDeclaration declaration in ReadConstantTable())
        {
            for (int i = 0; i < declaration.RegisterCount; i++)
            {
                float[] value = Named($"{declaration.Name}[{i}]");
                int register = declaration.RegisterIndex + i;
                switch (declaration.RegisterSet)
                {
                    case RegisterSet.Float4:
                        Array.Copy(value, _const[register], 4);
                        break;
                    case RegisterSet.Int4:
                        for (int c = 0; c < 4; c++)
                        {
                            // Whole numbers in a small range. A loop count taken
                            // from a pseudo-random float would run for ever.
                            _constInt[register][c] = (int)Math.Abs(value[c] * 4) % 4;
                        }
                        break;
                    case RegisterSet.Bool:
                        _constBool[register] = value[0] > 0;
                        break;
                }
            }
        }
    }

    private IEnumerable<D3D9ConstantDeclaration> ReadConstantTable()
    {
        foreach (D3D9Instruction instruction in _shader.Instructions.OfType<D3D9Instruction>())
        {
            if (instruction.Opcode != Opcode.Comment)
            {
                continue;
            }

            using var reader = new ConstantTableCommentReader(instruction);
            foreach (D3D9ConstantDeclaration declaration in reader.ReadTable().Declarations)
            {
                yield return declaration;
            }
        }
    }

    private void LoadDeclarations()
    {
        foreach (D3D9Instruction instruction in _shader.Instructions.OfType<D3D9Instruction>())
        {
            if (instruction.Opcode != Opcode.Dcl)
            {
                continue;
            }

            RegisterType type = instruction.GetParamRegisterType(1);
            int number = instruction.GetParamRegisterNumber(1);
            switch (type)
            {
                case RegisterType.Input:
                    Array.Copy(Named(instruction.GetDeclSemantic()), _input[number], 4);
                    break;
                case RegisterType.Texture:
                    Array.Copy(Named(instruction.GetDeclSemantic()), _texture[number], 4);
                    break;
                case RegisterType.Output:
                    _outputSemantics[number] = instruction.GetDeclSemantic();
                    break;
            }
        }
    }

    // The same name gives the same value in either program, whichever register it
    // happened to land in there.
    private float[] Named(string name)
    {
        return PseudoRandom.Vector(name, _trial);
    }

    private void Execute()
    {
        List<D3D9Instruction> instructions = [.. _shader.Instructions.OfType<D3D9Instruction>()];
        Dictionary<int, int> blocks = ControlFlow.Match(instructions);
        var loops = new Stack<LoopState>();
        int pc = 0;
        int steps = 0;

        while (pc < instructions.Count)
        {
            if (++steps > 200000)
            {
                throw new UnsupportedException("the shader did not terminate");
            }

            D3D9Instruction instruction = instructions[pc];
            switch (instruction.Opcode)
            {
                case Opcode.Comment:
                case Opcode.Dcl:
                case Opcode.End:
                case Opcode.Nop:
                case Opcode.Endif:
                    pc++;
                    continue;
                case Opcode.Def:
                    // Four separate parameters, one per component.
                    for (int i = 0; i < 4; i++)
                    {
                        _const[instruction.GetParamRegisterNumber(0)][i] =
                            instruction.GetParamSingle(1 + i)[0];
                    }
                    pc++;
                    continue;
                case Opcode.DefI:
                    for (int i = 0; i < 4; i++)
                    {
                        _constInt[instruction.GetParamRegisterNumber(0)][i] = instruction.GetParamInt(1 + i);
                    }
                    pc++;
                    continue;
                case Opcode.DefB:
                    _constBool[instruction.GetParamRegisterNumber(0)] = instruction.GetParamInt(1) != 0;
                    pc++;
                    continue;
                case Opcode.If:
                case Opcode.IfC:
                    pc = TakeBranch(instruction) ? pc + 1 : blocks[pc] + 1;
                    continue;
                case Opcode.Else:
                    // Only reached by running off the end of the taken branch.
                    pc = blocks[pc] + 1;
                    continue;
                case Opcode.Rep:
                case Opcode.Loop:
                    pc = EnterLoop(instruction, blocks, loops, pc);
                    continue;
                case Opcode.EndRep:
                case Opcode.EndLoop:
                    pc = RepeatLoop(loops, pc);
                    continue;
                case Opcode.Break:
                    pc = LeaveLoop(loops);
                    continue;
                case Opcode.BreakC:
                    pc = Compare(instruction.Comparison, Source(instruction, 0)[0], Source(instruction, 1)[0])
                        ? LeaveLoop(loops)
                        : pc + 1;
                    continue;
                case Opcode.TexKill:
                    Killed |= Source(instruction, 0).Take(3).Any(c => c < 0);
                    pc++;
                    continue;
            }

            Store(instruction, Evaluate(instruction));
            pc++;
        }
    }

    private bool TakeBranch(D3D9Instruction instruction)
    {
        if (instruction.Opcode == Opcode.IfC)
        {
            return Compare(instruction.Comparison, Source(instruction, 0)[0], Source(instruction, 1)[0]);
        }
        // A plain if tests a boolean register rather than a value.
        if (instruction.GetParamRegisterType(0) == RegisterType.ConstBool)
        {
            return _constBool[instruction.GetParamRegisterNumber(0)];
        }
        return Source(instruction, 0)[0] != 0;
    }

    private sealed class LoopState
    {
        public int Start;
        public int End;
        public int Remaining;
        public int Step;
        public int PreviousCounter;
    }

    private int EnterLoop(
        D3D9Instruction instruction,
        Dictionary<int, int> blocks,
        Stack<LoopState> loops,
        int pc)
    {
        int end = blocks[pc];
        int count;
        int start = 0;
        int step = 1;
        if (instruction.Opcode == Opcode.Rep)
        {
            count = _constInt[instruction.GetParamRegisterNumber(0)][0];
        }
        else
        {
            int[] counter = _constInt[instruction.GetParamRegisterNumber(1)];
            count = counter[0];
            start = counter[1];
            step = counter[2];
        }

        if (count <= 0)
        {
            return end + 1;
        }

        loops.Push(new LoopState
        {
            Start = pc + 1,
            End = end,
            Remaining = count,
            Step = step,
            PreviousCounter = _loopCounter,
        });
        _loopCounter = start;
        return pc + 1;
    }

    private int RepeatLoop(Stack<LoopState> loops, int pc)
    {
        LoopState state = loops.Peek();
        state.Remaining--;
        if (state.Remaining > 0)
        {
            _loopCounter += state.Step;
            return state.Start;
        }
        _loopCounter = state.PreviousCounter;
        loops.Pop();
        return pc + 1;
    }

    private int LeaveLoop(Stack<LoopState> loops)
    {
        LoopState state = loops.Pop();
        _loopCounter = state.PreviousCounter;
        return state.End + 1;
    }

    private static bool Compare(IfComparison comparison, float left, float right)
    {
        return comparison switch
        {
            IfComparison.GT => left > right,
            IfComparison.EQ => left == right,
            IfComparison.GE => left >= right,
            IfComparison.LT => left < right,
            IfComparison.NE => left != right,
            IfComparison.LE => left <= right,
            _ => throw new UnsupportedException($"comparison {comparison}"),
        };
    }

    private float[] Evaluate(D3D9Instruction instruction)
    {
        switch (instruction.Opcode)
        {
            case Opcode.Mov:
            case Opcode.MovA:
                return Source(instruction, 1);
            case Opcode.Add:
                return Zip(instruction, (a, b) => a + b);
            case Opcode.Sub:
                return Zip(instruction, (a, b) => a - b);
            case Opcode.Mul:
                return Zip(instruction, (a, b) => a * b);
            case Opcode.Min:
                return Zip(instruction, Math.Min);
            case Opcode.Max:
                return Zip(instruction, Math.Max);
            case Opcode.Slt:
                return Zip(instruction, (a, b) => a < b ? 1 : 0);
            case Opcode.Sge:
                return Zip(instruction, (a, b) => a >= b ? 1 : 0);
            case Opcode.Pow:
                return Zip(instruction, (a, b) => MathF.Pow(Math.Abs(a), b));
            case Opcode.Crs:
                return Cross(Source(instruction, 1), Source(instruction, 2));
            case Opcode.Mad:
                {
                    float[] a = Source(instruction, 1);
                    float[] b = Source(instruction, 2);
                    float[] c = Source(instruction, 3);
                    return [.. Enumerable.Range(0, 4).Select(i => a[i] * b[i] + c[i])];
                }
            case Opcode.Lrp:
                {
                    float[] s = Source(instruction, 1);
                    float[] a = Source(instruction, 2);
                    float[] b = Source(instruction, 3);
                    return [.. Enumerable.Range(0, 4).Select(i => b[i] + s[i] * (a[i] - b[i]))];
                }
            case Opcode.Cmp:
                {
                    float[] a = Source(instruction, 1);
                    float[] b = Source(instruction, 2);
                    float[] c = Source(instruction, 3);
                    return [.. Enumerable.Range(0, 4).Select(i => a[i] >= 0 ? b[i] : c[i])];
                }
            case Opcode.Cnd:
                {
                    float[] a = Source(instruction, 1);
                    float[] b = Source(instruction, 2);
                    float[] c = Source(instruction, 3);
                    return [.. Enumerable.Range(0, 4).Select(i => a[i] > 0.5f ? b[i] : c[i])];
                }
            case Opcode.Rcp:
                return Broadcast(Reciprocal(Source(instruction, 1)[0]));
            case Opcode.Rsq:
                return Broadcast(Reciprocal(MathF.Sqrt(Math.Abs(Source(instruction, 1)[0]))));
            case Opcode.Exp:
                return Broadcast(MathF.Pow(2, Source(instruction, 1)[0]));
            case Opcode.Log:
                {
                    float value = Math.Abs(Source(instruction, 1)[0]);
                    return Broadcast(value == 0 ? float.NegativeInfinity : MathF.Log2(value));
                }
            case Opcode.Frc:
                return Map(instruction, v => v - MathF.Floor(v));
            case Opcode.Abs:
                return Map(instruction, Math.Abs);
            case Opcode.Sgn:
                return Map(instruction, v => MathF.Sign(v));
            case Opcode.Nrm:
                {
                    float[] v = Source(instruction, 1);
                    float length = MathF.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
                    float scale = Reciprocal(length);
                    return [v[0] * scale, v[1] * scale, v[2] * scale, v[3] * scale];
                }
            case Opcode.Dp3:
                return Broadcast(Dot(Source(instruction, 1), Source(instruction, 2), 3));
            case Opcode.Dp4:
                return Broadcast(Dot(Source(instruction, 1), Source(instruction, 2), 4));
            case Opcode.DP2Add:
                return Broadcast(Dot(Source(instruction, 1), Source(instruction, 2), 2)
                    + Source(instruction, 3)[0]);
            case Opcode.SinCos:
                {
                    float angle = Source(instruction, 1)[0];
                    return [MathF.Cos(angle), MathF.Sin(angle), 0, 0];
                }
            case Opcode.Dst:
                {
                    float[] a = Source(instruction, 1);
                    float[] b = Source(instruction, 2);
                    return [1, a[1] * b[1], a[2], b[3]];
                }
            case Opcode.Lit:
                {
                    float[] v = Source(instruction, 1);
                    float specular = v[0] > 0 && v[1] > 0 ? MathF.Pow(v[1], v[3]) : 0;
                    return [1, Math.Max(v[0], 0), specular, 1];
                }
            case Opcode.M4x4:
                return Multiply(instruction, 4, 4);
            case Opcode.M4x3:
                return Multiply(instruction, 4, 3);
            case Opcode.M3x4:
                return Multiply(instruction, 3, 4);
            case Opcode.M3x3:
                return Multiply(instruction, 3, 3);
            case Opcode.M3x2:
                return Multiply(instruction, 3, 2);
            case Opcode.DSX:
            case Opcode.DSY:
                // No neighbouring pixel to difference against. Both programs get
                // zero, so a shader using one is compared on everything else.
                return [0, 0, 0, 0];
            case Opcode.Tex:
            case Opcode.TexLDL:
            case Opcode.TexLDD:
                return Sample(instruction);
            default:
                throw new UnsupportedException($"opcode {instruction.Opcode}");
        }
    }

    private float[] Sample(D3D9Instruction instruction)
    {
        float[] coordinates = Source(instruction, 1);
        int sampler = instruction.GetParamRegisterNumber(2);
        if (instruction.TexldControls == TexldControls.Project)
        {
            float scale = Reciprocal(coordinates[3]);
            coordinates = [coordinates[0] * scale, coordinates[1] * scale, coordinates[2] * scale, 1];
        }
        // A bias or an explicit level changes which mip is read, and there are no
        // mips here. Both programs read the same one.
        return Texture.Sample(sampler, coordinates);
    }

    private float[] Multiply(D3D9Instruction instruction, int columns, int rows)
    {
        float[] vector = Source(instruction, 1);
        int register = instruction.GetParamRegisterNumber(2);
        RegisterType type = instruction.GetParamRegisterType(2);
        var result = new float[4];
        for (int row = 0; row < rows; row++)
        {
            float[] matrixRow = Read(type, register + row);
            result[row] = Dot(vector, matrixRow, columns);
        }
        return result;
    }

    private static float[] Cross(float[] a, float[] b)
    {
        return
        [
            a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0],
            0,
        ];
    }

    private static float Dot(float[] a, float[] b, int length)
    {
        float sum = 0;
        for (int i = 0; i < length; i++)
        {
            sum += a[i] * b[i];
        }
        return sum;
    }

    private static float Reciprocal(float value)
    {
        return value == 0 ? float.PositiveInfinity : 1 / value;
    }

    private static float[] Broadcast(float value)
    {
        return [value, value, value, value];
    }

    private float[] Zip(D3D9Instruction instruction, Func<float, float, float> combine)
    {
        float[] a = Source(instruction, 1);
        float[] b = Source(instruction, 2);
        return [.. Enumerable.Range(0, 4).Select(i => combine(a[i], b[i]))];
    }

    private float[] Map(D3D9Instruction instruction, Func<float, float> transform)
    {
        return [.. Source(instruction, 1).Select(transform)];
    }

    /// <summary>
    /// One source operand, swizzled and with its modifier applied, always four
    /// components wide.
    /// </summary>
    private float[] Source(D3D9Instruction instruction, int index)
    {
        RegisterType type = instruction.GetParamRegisterType(index);
        int number = instruction.GetParamRegisterNumber(index);
        if (instruction.Params.HasRelativeAddressing(index))
        {
            // A real index is in range because the shader put it there; one made
            // up here need not be. Both programs derive it from the same named
            // value, so clamping keeps them reading the same element.
            number = Math.Clamp(number + RelativeOffset(instruction, index), 0, 255);
        }

        float[] register = Read(type, number);
        byte[] swizzle = instruction.GetSourceSwizzleComponents(index);
        var value = new float[4];
        for (int i = 0; i < 4; i++)
        {
            value[i] = register[swizzle[i]];
        }

        switch (instruction.GetSourceModifier(index))
        {
            case SourceModifier.None:
                break;
            case SourceModifier.Negate:
                value = [.. value.Select(v => -v)];
                break;
            case SourceModifier.Abs:
                value = [.. value.Select(Math.Abs)];
                break;
            case SourceModifier.AbsAndNegate:
                value = [.. value.Select(v => -Math.Abs(v))];
                break;
            default:
                throw new UnsupportedException($"source modifier {instruction.GetSourceModifier(index)}");
        }
        return value;
    }

    private int RelativeOffset(D3D9Instruction instruction, int index)
    {
        RegisterType type = instruction.GetRelativeParamRegisterType(index);
        if (type == RegisterType.Loop)
        {
            return _loopCounter;
        }
        return (int)_address[instruction.GetRelativeParamComponent(index)];
    }

    private float[] Read(RegisterType type, int number)
    {
        switch (type)
        {
            case RegisterType.Temp:
                return _temp[number];
            case RegisterType.Input:
                return _input[number];
            case RegisterType.Const:
                return _const[number];
            case RegisterType.Const2:
                return _const[number + 2048];
            case RegisterType.Const3:
                return _const[number + 4096];
            case RegisterType.Const4:
                return _const[number + 6144];
            case RegisterType.Texture:
                return _texture[number];
            case RegisterType.Loop:
                return Broadcast(_loopCounter);
            case RegisterType.MiscType:
                // vPos carries a pixel position in two components; vFace is one
                // value the hardware broadcasts. Giving either four independent
                // components made a four wide cmp on vFace disagree with a one
                // wide one, which is a difference this machine invented.
                float[] misc = Named(number == 0 ? "VPOS" : "VFACE");
                return number == 0
                    ? [misc[0], misc[1], 0, 0]
                    : Broadcast(misc[0]);
            default:
                throw new UnsupportedException($"reading register type {type}");
        }
    }

    private void Store(D3D9Instruction instruction, float[] value)
    {
        int? destinationIndex = instruction.GetDestinationParamIndex();
        if (destinationIndex == null)
        {
            return;
        }

        ResultModifier modifier = instruction.GetDestinationResultModifier();
        if (modifier.HasFlag(ResultModifier.Saturate))
        {
            value = [.. value.Select(v => Math.Clamp(v, 0, 1))];
        }

        RegisterType type = instruction.GetParamRegisterType(destinationIndex.Value);
        int number = instruction.GetParamRegisterNumber(destinationIndex.Value);
        if (instruction.Opcode == Opcode.MovA)
        {
            // The address register rounds to nearest, which is what makes c[a0.x]
            // land on a row rather than between two.
            value = [.. value.Select(v => MathF.Round(v, MidpointRounding.AwayFromZero))];
        }

        float[] destination = Write(type, number);
        string outputName = OutputName(type, number);
        if (outputName != null)
        {
            _results[outputName] = destination;
        }

        int writeMask = instruction.GetDestinationWriteMask();
        for (int i = 0; i < 4; i++)
        {
            if ((writeMask & (1 << i)) != 0)
            {
                destination[i] = value[i];
            }
        }
    }

    private float[] Write(RegisterType type, int number)
    {
        switch (type)
        {
            case RegisterType.Temp:
                return _temp[number];
            case RegisterType.Addr when _shader.Type == ShaderType.Vertex:
                return _address;
            case RegisterType.Texture:
                return _texture[number];
            case RegisterType.Output:
            case RegisterType.RastOut:
            case RegisterType.AttrOut:
            case RegisterType.ColorOut:
            case RegisterType.DepthOut:
                return _output[OutputSlot(type, number)];
            case RegisterType.Predicate:
                throw new UnsupportedException("a predicate register");
            default:
                throw new UnsupportedException($"writing register type {type}");
        }
    }

    // Output registers of different kinds share one file here, keyed by the name
    // they are reported under.
    private int OutputSlot(RegisterType type, int number)
    {
        return type switch
        {
            RegisterType.RastOut => number,
            RegisterType.AttrOut => 4 + number,
            RegisterType.ColorOut => 8 + number,
            RegisterType.DepthOut => 12,
            _ => number,
        };
    }

    private string OutputName(RegisterType type, int number)
    {
        switch (type)
        {
            case RegisterType.ColorOut:
                return "COLOR" + number;
            case RegisterType.DepthOut:
                return "DEPTH";
            case RegisterType.RastOut:
                return number switch { 0 => "POSITION", 1 => "FOG", _ => "PSIZE" };
            case RegisterType.AttrOut:
                return "COLOR" + number;
            case RegisterType.Output:
                // A vs_3_0 output register carries whatever semantic its dcl gave it.
                return _outputSemantics.TryGetValue(number, out string semantic) ? semantic : null;
            default:
                return null;
        }
    }
}
