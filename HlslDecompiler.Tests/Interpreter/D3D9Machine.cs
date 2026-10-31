using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Tests.Interpreter;

/// <summary>
/// Runs a shader model 1 to 3 shader so two decompilations can be compared by what
/// they compute. fxc reallocates registers, so constants are named by declaration
/// and inputs and outputs by semantic.
/// </summary>
public class D3D9Machine
{
    // The caller skips the shader: an instruction that did not run is no evidence.
    public class UnsupportedException(string message) : Exception(message);

    private readonly ShaderModel _shader;
    private readonly float[][] _temp = NewFile(64);
    private readonly float[][] _input = NewFile(16);
    private readonly float[][] _texture = NewFile(16);
    private readonly float[][] _const = NewFile(256);
    private readonly float[][] _output = NewFile(32);
    private readonly int[][] _constInt = NewIntFile(16);
    private readonly bool[] _constBool = new bool[16];
    private readonly float[] _address = new float[4];
    private readonly Dictionary<int, string> _outputSemantics = [];
    private readonly Dictionary<string, float[]> _results = [];
    private readonly int _trial;
    private int _loopCounter;

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
    /// Returns the outputs by semantic, or none if the pixel was killed.
    /// </summary>
    public static Dictionary<string, float[]> Run(ShaderModel shader, int trial)
    {
        var machine = new D3D9Machine(shader, trial);
        machine.LoadConstants();
        machine.LoadDeclarations();
        if (machine.IsShaderModel1Pixel)
        {
            machine.LoadShaderModel1PixelInputs();
        }
        machine.Execute();
        if (machine.IsShaderModel1Pixel)
        {
            // There is no colour output register: the colour is r0, as it is left.
            machine._results["COLOR0"] = [.. machine._temp[0]];
        }
        return machine.Killed ? [] : Observed(machine._results);
    }

    // What the hardware takes from each output: the point size, fog and depth
    // are their x alone, and depth is clamped to [0, 1]. fxc fills o#.yzw of a
    // PSIZE; the Xbox 360 compiler saturates depth where fxc leaves it to this.
    private static Dictionary<string, float[]> Observed(Dictionary<string, float[]> results)
    {
        var observed = new Dictionary<string, float[]>();
        foreach ((string name, float[] value) in results)
        {
            if (name.StartsWith("PSIZE") || name.StartsWith("FOG"))
            {
                observed[name] = [value[0], 0, 0, 0];
            }
            else if (name.StartsWith("DEPTH"))
            {
                observed[name] = [float.IsNaN(value[0]) ? value[0] : Math.Clamp(value[0], 0, 1), 0, 0, 0];
            }
            else
            {
                observed[name] = value;
            }
        }
        return observed;
    }

    /// <summary>
    /// ps_1_x, run per the spec rather than via the decompiler's ps_2_0 rewrite, so
    /// the two check each other. v0/v1 are diffuse/specular, t# starts as texcoord #
    /// (read only in ps_1_4), and r0 is the output colour.
    /// </summary>
    private bool IsShaderModel1Pixel =>
        _shader.Type == ShaderType.Pixel && _shader.MajorVersion == 1;

    // Indexed by the number of the register a ps_1_x texture instruction writes.
    private readonly float[][] _coordinates = NewFile(8);

    private bool IsShaderModel14Pixel => IsShaderModel1Pixel && _shader.MinorVersion >= 4;

    private void LoadShaderModel1PixelInputs()
    {
        // Named as a ps_2_0 dcl names them, which is what the decompilation has.
        Array.Copy(PixelColour(Named("COLOR")), _input[0], 4);
        Array.Copy(PixelColour(Named("COLOR1")), _input[1], 4);
        for (int i = 0; i < _coordinates.Length; i++)
        {
            Array.Copy(Named(i == 0 ? "TEXCOORD" : "TEXCOORD" + i), _coordinates[i], 4);
            if (IsShaderModel14Pixel)
            {
                Array.Copy(_coordinates[i], _texture[i], 4);
            }
        }
    }

    // Colour inputs are in [0, 1]: the vertex pipeline clamps them.
    private static float[] PixelColour(float[] value) =>
        [.. value.Select(v => (v + 2) / 4)];

    private void LoadConstants()
    {
        foreach (D3D9ConstantDeclaration declaration in ReadConstantTable())
        {
            for (int i = 0; i < declaration.RegisterCount; i++)
            {
                string name = $"{declaration.Name}[{i}]";
                float[] value = Named(name);
                int register = declaration.RegisterIndex + i;
                switch (declaration.RegisterSet)
                {
                    case RegisterSet.Float4:
                        Array.Copy(TypedValue(declaration.TypeInfo.ParameterType, name, value), _const[register], 4);
                        break;
                    case RegisterSet.Int4 when declaration.TypeInfo.Columns == 1 && declaration.TypeInfo.Rows == 1:
                        // A scalar int sets a loop's count, starting at 0 and stepping
                        // by 1, as the runtime writes it.
                        _constInt[register] = [Integer(name, value, 0), 0, 1, 0];
                        break;
                    case RegisterSet.Int4:
                        for (int c = 0; c < 4; c++)
                        {
                            _constInt[register][c] = Integer(name, value, c);
                        }
                        break;
                    case RegisterSet.Bool:
                        // The signed draw: odd trials' values are non-negative.
                        _constBool[register] = PseudoRandom.Vector(name, _trial)[0] > 0;
                        break;
                }
            }
        }

        // An fx_2_0 preshader fills registers the shader's table does not name.
        if (Preshader.Find(_shader) is Preshader preshader)
        {
            foreach (((PreshaderRegisterTable table, int offset), double value) in PreshaderMachine.Run(preshader,
                name => TypedValue(preshader.Inputs.Declarations
                    .FirstOrDefault(d => name.StartsWith(d.Name + "["))?.TypeInfo.ParameterType, name, Named(name))))
            {
                int register = offset / 4;
                int component = offset % 4;
                switch (table)
                {
                    case PreshaderRegisterTable.Output:
                        _const[register][component] = (float)value;
                        break;
                    case PreshaderRegisterTable.OutputInt:
                        _constInt[register][component] = (int)value;
                        break;
                    case PreshaderRegisterTable.OutputBool:
                        _constBool[register] = value != 0;
                        break;
                }
            }
        }
    }

    private List<D3D9ConstantDeclaration> _constantTable;

    private List<D3D9ConstantDeclaration> ConstantTable => _constantTable ??= [.. ReadConstantTable()];

    // A float register's value by the declared type. A bool fxc keeps in a float
    // register is 1 or 0, as the bool register it might have had (the Xbox 360
    // compiler uses b#, and a preshader reads it too); an int likewise holds the
    // integer an integer register would.
    private float[] TypedValue(ParameterType? type, string name, float[] value)
    {
        switch (type)
        {
            case ParameterType.Bool:
                float flag = PseudoRandom.Vector(name, _trial)[0] > 0 ? 1 : 0;
                return [flag, flag, flag, flag];
            case ParameterType.Int:
                return [Integer(name, value, 0), Integer(name, value, 1), Integer(name, value, 2), Integer(name, value, 3)];
            default:
                return value;
        }
    }

    // Small, so a loop count terminates; in even trials cycled from a fixed
    // offset, so every count 0-3 is reached.
    private int Integer(string name, float[] value, int component)
    {
        float[] offset = PseudoRandom.Vector(name, 0);
        return _trial % 2 == 0
            ? (_trial / 2 + (int)(Math.Abs(offset[component]) * 1000)) % 4
            : (int)Math.Abs(value[component] * 4) % 4;
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
                    {
                        string semantic = instruction.GetDeclSemantic();
                        float[] value = Named(semantic);
                        // Integral, so rounding and truncating into a0 agree.
                        if (semantic.StartsWith("BLENDINDICES", StringComparison.OrdinalIgnoreCase))
                        {
                            value = [.. value.Select(v => (float)((int)Math.Abs(v * 4) % 8))];
                        }
                        if (_shader.Type == ShaderType.Pixel
                            && semantic.StartsWith("COLOR", StringComparison.OrdinalIgnoreCase))
                        {
                            value = PixelColour(value);
                        }
                        Array.Copy(value, _input[number], 4);
                    }
                    break;
                case RegisterType.Texture:
                    // A ps_2_x t# is texture coordinate set #: named so, as the ps_1_x
                    // coordinates and the decompilation's TEXCOORD# are. Its dcl
                    // carries no usage index to say it.
                    Array.Copy(Named(_shader.Type == ShaderType.Pixel
                            ? (number == 0 ? "TEXCOORD" : "TEXCOORD" + number)
                            : instruction.GetDeclSemantic()),
                        _texture[number], 4);
                    break;
                case RegisterType.Sampler:
                    // A sampler1D is declared 2D: its v is the rest of the register.
                    _samplerDimensions[number] = instruction.GetDeclSamplerTextureType() switch
                    {
                        SamplerTextureType.TwoD when ReadConstantTable().Any(d => d.RegisterSet == RegisterSet.Sampler
                            && d.ContainsIndex(number) && d.TypeInfo.ParameterType == ParameterType.Sampler1D) => 1,
                        SamplerTextureType.TwoD => 2,
                        _ => 3,
                    };
                    break;
                case RegisterType.Output:
                    _outputSemantics[number] = instruction.GetDeclSemantic();
                    break;
            }
        }
    }

    // Even trials signed, odd trials non-negative, as in D3D10Machine: a value
    // behind saturate or max(x, 0) was otherwise zero half the time.
    private float[] Named(string name)
    {
        float[] value = PseudoRandom.Vector(name, _trial);
        // As the D3D10 machine's: 1 everywhere every eighth trial, rounded to
        // -2..2 in the ones between, so sge and slt are tested where they part.
        if (_trial % 8 == 2)
        {
            return [1, 1, 1, 1];
        }
        if (_trial % 8 == 6)
        {
            return [.. value.Select(PseudoRandom.Rounded)];
        }
        // Small, so a product stays inside a narrow test, as the D3D10 machine's.
        if (_trial % 8 == 4)
        {
            return [.. value.Select(v => v / 8)];
        }
        // Large, as the D3D10 machine's.
        if (_trial % 8 == 0)
        {
            return [.. value.Select(v => v * 16)];
        }
        return _trial % 2 == 0 ? value : [.. value.Select(MathF.Abs)];
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
            if (IsShaderModel1Pixel && !IsShaderModel14Pixel && AddressTexture(instruction))
            {
                pc++;
                continue;
            }
            switch (instruction.Opcode)
            {
                case Opcode.Comment:
                case Opcode.Dcl:
                case Opcode.End:
                case Opcode.Nop:
                case Opcode.Phase:
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
                    // Its operand is a destination token, whose mask bits Source would
                    // read as a swizzle (xyzw as .wwxx), so the register is read whole.
                    // ps_1_1-1_3 test the coordinate set (not what a tex left in t#) and
                    // ps_1_4 its register, on xyz; from ps_2_0 all four count - fxc
                    // fills w with a copy (mov r0.w, t2.z) for a clip of a float3.
                    {
                        int killed = instruction.GetParamRegisterNumber(0);
                        float[] tested = IsShaderModel1Pixel && !IsShaderModel14Pixel
                            ? _coordinates[killed]
                            : Read(instruction.GetParamRegisterType(0), killed);
                        Killed |= tested.Take(IsShaderModel1Pixel ? 3 : 4).Any(c => c < 0);
                    }
                    pc++;
                    continue;
                // ps_1_4: texld samples the stage its destination is numbered for.
                case Opcode.Tex when IsShaderModel14Pixel:
                    {
                        int stage = instruction.GetParamRegisterNumber(0);
                        Store(instruction, Texture.Sample(stage, Source(instruction, 1),
                            ShaderModel1SamplerDimension(stage)));
                        pc++;
                        continue;
                    }
                case Opcode.TexCoord when IsShaderModel14Pixel:
                    Store(instruction, Source(instruction, 1));
                    pc++;
                    continue;
                case Opcode.TexDepth:
                    {
                        // r5.r / r5.g, and 1 where r5.g is 0.
                        float[] ratio = _temp[instruction.GetParamRegisterNumber(0)];
                        _results["DEPTH"] = Broadcast(ratio[1] == 0 ? 1 : ratio[0] / ratio[1]);
                        pc++;
                        continue;
                    }
                case Opcode.Tex when IsShaderModel1Pixel:
                    {
                        int stage = instruction.GetParamRegisterNumber(0);
                        Array.Copy(Texture.Sample(stage, _coordinates[stage], ShaderModel1SamplerDimension(stage)),
                            _texture[stage], 4);
                        pc++;
                        continue;
                    }
                case Opcode.TexCoord when IsShaderModel1Pixel:
                    {
                        // The coordinate as a colour: clamped, with an alpha of one.
                        int stage = instruction.GetParamRegisterNumber(0);
                        float[] c = _coordinates[stage];
                        _texture[stage][0] = Math.Clamp(c[0], 0, 1);
                        _texture[stage][1] = Math.Clamp(c[1], 0, 1);
                        _texture[stage][2] = Math.Clamp(c[2], 0, 1);
                        _texture[stage][3] = 1;
                        pc++;
                        continue;
                    }
            }

            // A co-issued pair reads both sources before either writes.
            if (pc + 1 < instructions.Count && instructions[pc + 1].CoIssue)
            {
                float[] first = Evaluate(instruction);
                float[] second = Evaluate(instructions[pc + 1]);
                Store(instruction, first);
                Store(instructions[pc + 1], second);
                pc += 2;
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
            case Opcode.ExpP when _shader.MajorVersion != 1:
                // expp from vs_2_0 up is exp. (In vs_1_1 it is the partial form below.)
                return Broadcast(MathF.Pow(2, Source(instruction, 1)[0]));
            case Opcode.ExpP:
                {
                    // vs_1_1's expp is the partial exponential: the parts of 2^v
                    // laid out whole and split.
                    float value = Source(instruction, 1)[0];
                    float whole = MathF.Floor(value);
                    return [MathF.Pow(2, whole), value - whole,
                        MathF.Pow(2, value), 1.0f];
                }
            case Opcode.Log:
            case Opcode.LogP:
                // logp is log at every version.
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
            case Opcode.Bem:
                {
                    // ps_1_4: uses the bump matrix of the destination's stage.
                    float[] value = Source(instruction, 1);
                    float[] offset = BumpOffset(instruction.GetParamRegisterNumber(0), Source(instruction, 2));
                    return
                    [
                        value[0] + offset[0],
                        value[1] + offset[1],
                        0,
                        0,
                    ];
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
                // No neighbouring pixels: zero.
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
        // No mips, so bias and explicit level are ignored.
        return Texture.Sample(sampler, coordinates, _samplerDimensions.GetValueOrDefault(sampler, 4));
    }

    private readonly Dictionary<int, int> _samplerDimensions = [];

    // Rows computed so far by texm3x2pad/texm3x3pad.
    private readonly List<float> _matrixRows = [];

    /// <summary>
    /// ps_1_1-1_3 texture addressing: reads t(n) to find where stage m samples and
    /// writes t(m). Bump matrix and luminance are named as the uniforms the
    /// decompilation declares. False for any other opcode.
    /// </summary>
    private bool AddressTexture(D3D9Instruction instruction)
    {
        if (instruction.Opcode is not (Opcode.TexBem or Opcode.TexBeml or Opcode.TexReg2AR
            or Opcode.TexReg2GB or Opcode.TexReg2RGB or Opcode.TeXM3x2Pad or Opcode.TeXM3x3Pad
            or Opcode.TexM3x2Tex or Opcode.TexM3x2Depth or Opcode.TexM3x3Tex or Opcode.TexM3x3
            or Opcode.TexM3x3Spec or Opcode.TexM3x3VSpec or Opcode.TexDP3 or Opcode.TexDP3Tex
            or Opcode.TexM3x3Diff))
        {
            return false;
        }
        int m = instruction.GetParamRegisterNumber(0);
        switch (instruction.Opcode)
        {
            case Opcode.TexBem:
            case Opcode.TexBeml:
                {
                    float[] t = Source(instruction, 1);
                    float[] offset = BumpOffset(m, t);
                    float u = _coordinates[m][0] + offset[0];
                    float v = _coordinates[m][1] + offset[1];
                    float[] colour = SampleStage(m, [u, v, 0, 0]);
                    if (instruction.Opcode == Opcode.TexBeml)
                    {
                        float[] luminance = Named($"bumpEnvLum{m}[0]");
                        float scale = Math.Clamp(t[2] * luminance[0] + luminance[1], 0, 1);
                        colour = [colour[0] * scale, colour[1] * scale, colour[2] * scale, colour[3]];
                    }
                    Array.Copy(colour, _texture[m], 4);
                    return true;
                }
            case Opcode.TexReg2AR:
                {
                    float[] t = Source(instruction, 1);
                    Array.Copy(SampleStage(m, [t[3], t[0], 0, 0]), _texture[m], 4);
                    return true;
                }
            case Opcode.TexReg2GB:
                {
                    float[] t = Source(instruction, 1);
                    Array.Copy(SampleStage(m, [t[1], t[2], 0, 0]), _texture[m], 4);
                    return true;
                }
            case Opcode.TexReg2RGB:
                {
                    float[] t = Source(instruction, 1);
                    Array.Copy(SampleStage(m, [t[0], t[1], t[2], 0]), _texture[m], 4);
                    return true;
                }
            case Opcode.TeXM3x2Pad:
            case Opcode.TeXM3x3Pad:
                _matrixRows.Add(MatrixRow(instruction));
                return true;
            case Opcode.TexM3x2Tex:
                Array.Copy(SampleStage(m, [_matrixRows[0], MatrixRow(instruction), 0, 0]), _texture[m], 4);
                _matrixRows.Clear();
                return true;
            case Opcode.TexM3x2Depth:
                {
                    float z = _matrixRows[0];
                    float w = MatrixRow(instruction);
                    _results["DEPTH"] = Broadcast(w == 0 ? 1 : z / w);
                    _matrixRows.Clear();
                    return true;
                }
            case Opcode.TexM3x3Tex:
                Array.Copy(SampleStage(m, [.. MatrixVector(instruction), 0]), _texture[m], 4);
                return true;
            case Opcode.TexM3x3:
                Array.Copy((float[])[.. MatrixVector(instruction), 1], _texture[m], 4);
                return true;
            case Opcode.TexM3x3Spec:
            case Opcode.TexM3x3VSpec:
                {
                    float[] n = MatrixVector(instruction);
                    float[] eye = instruction.Opcode == Opcode.TexM3x3Spec
                        ? _const[instruction.GetParamRegisterNumber(2)]
                        : [_coordinates[m - 2][3], _coordinates[m - 1][3], _coordinates[m][3]];
                    // The eye reflected about the normal: 2N(N.E)/(N.N) - E.
                    float scale = 2 * Dot(n, eye, 3) / Dot(n, n, 3);
                    Array.Copy(SampleStage(m, [n[0] * scale - eye[0], n[1] * scale - eye[1],
                        n[2] * scale - eye[2], 0]), _texture[m], 4);
                    return true;
                }
            case Opcode.TexDP3:
                Array.Copy(Broadcast(MatrixRow(instruction)), _texture[m], 4);
                return true;
            case Opcode.TexDP3Tex:
                Array.Copy(SampleStage(m, [MatrixRow(instruction), 0, 0, 0]), _texture[m], 4);
                return true;
            case Opcode.TexM3x3Diff:
                throw new UnsupportedException("texm3x3diff");
            default:
                return false;
        }
    }

    /// <summary>
    /// (M00 r + M10 g, M01 r + M11 g). Named as the decompilation's column-major
    /// float2x2: bumpEnvMat#[0] = (M00, M10), bumpEnvMat#[1] = (M01, M11).
    /// </summary>
    private float[] BumpOffset(int stage, float[] value)
    {
        float[] column0 = Named($"bumpEnvMat{stage}[0]");
        float[] column1 = Named($"bumpEnvMat{stage}[1]");
        return
        [
            column0[0] * value[0] + column0[1] * value[1],
            column1[0] * value[0] + column1[1] * value[1],
        ];
    }

    // Texture coordinate m dotted with texture n: one row of a texture matrix.
    private float MatrixRow(D3D9Instruction instruction)
    {
        return Dot(_coordinates[instruction.GetParamRegisterNumber(0)], Source(instruction, 1), 3);
    }

    private float[] MatrixVector(D3D9Instruction instruction)
    {
        float[] vector = [_matrixRows[0], _matrixRows[1], MatrixRow(instruction)];
        _matrixRows.Clear();
        return vector;
    }

    private float[] SampleStage(int stage, float[] coordinates)
    {
        return Texture.Sample(stage, coordinates, ShaderModel1SamplerDimension(stage));
    }

    // ps_1_x declares no samplers. A texture is 2D unless the constant table says
    // it is a cube or a volume.
    private int ShaderModel1SamplerDimension(int stage)
    {
        D3D9ConstantDeclaration sampler = ReadConstantTable().FirstOrDefault(d =>
            d.RegisterSet == RegisterSet.Sampler && d.ContainsIndex(stage));
        return sampler == null ? 2 : sampler.GetSamplerDimension();
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

    private float[] Source(D3D9Instruction instruction, int index)
    {
        RegisterType type = instruction.GetParamRegisterType(index);
        int number = instruction.GetParamRegisterNumber(index);
        if (instruction.Params.HasRelativeAddressing(index))
        {
            int indexed = number + RelativeOffset(instruction, index);
            // An index past its array is undefined: code from another compiler need
            // not agree there. Elsewhere, made up indices clamp alike in both.
            if (type == RegisterType.Const && ConstantTable.FirstOrDefault(d => d.RegisterSet == RegisterSet.Float4
                    && d.ContainsIndex(number)) is D3D9ConstantDeclaration array && !array.ContainsIndex(indexed))
            {
                throw new D3D10Machine.UndefinedTrialException($"{array.Name}[{indexed - array.RegisterIndex}] is past the array");
            }
            number = Math.Clamp(indexed, 0, 255);
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
            // ps_1_x, after the swizzle like the rest.
            case SourceModifier.Bias:
                value = [.. value.Select(v => v - 0.5f)];
                break;
            case SourceModifier.BiasAndNegate:
                value = [.. value.Select(v => -(v - 0.5f))];
                break;
            case SourceModifier.Sign:
                value = [.. value.Select(v => 2 * v - 1)];
                break;
            case SourceModifier.SignAndNegate:
                value = [.. value.Select(v => -(2 * v - 1))];
                break;
            case SourceModifier.Complement:
                value = [.. value.Select(v => 1 - v)];
                break;
            case SourceModifier.X2:
                value = [.. value.Select(v => 2 * v)];
                break;
            case SourceModifier.X2AndNegate:
                value = [.. value.Select(v => -2 * v)];
                break;
            // ps_1_4 _dz/_dw projection.
            case SourceModifier.DivideByZ:
                value = [value[0] / value[2], value[1] / value[2], value[2], value[3]];
                break;
            case SourceModifier.DivideByW:
                value = [value[0] / value[3], value[1] / value[3], value[2], value[3]];
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
                // vPos has two components, never negative (the Xbox 360 packs the
                // face into its sign); vFace is one value, broadcast.
                float[] misc = Named(number == 0 ? "VPOS" : "VFACE");
                return number == 0
                    ? [Math.Abs(misc[0]), Math.Abs(misc[1]), 0, 0]
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

        // A ps_1_x result scale, applied before the saturate.
        int shift = instruction.GetDestinationResultShift();
        if (shift != 0)
        {
            float scale = MathF.Pow(2, shift);
            value = [.. value.Select(v => v * scale)];
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
            // The address register rounds to nearest.
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

    // All output register kinds share one file, each in slots of its own: o#
    // (oT# below vs_3_0) had shared oPos's, so a masked write to oT0 kept the
    // position in its other components.
    private int OutputSlot(RegisterType type, int number)
    {
        return type switch
        {
            RegisterType.RastOut => number,
            RegisterType.AttrOut => 4 + number,
            RegisterType.ColorOut => 8 + number,
            RegisterType.DepthOut => 12,
            _ => 16 + number,
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
                // Below vs_3_0 the same register type is oT#, undeclared: texture
                // coordinate #. Unnamed, every one went uncompared.
                if (_outputSemantics.TryGetValue(number, out string semantic))
                {
                    return semantic;
                }
                return _shader.Type == ShaderType.Vertex && _shader.MajorVersion < 3 ? "TEXCOORD" + number : null;
            default:
                return null;
        }
    }
}
