using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HlslDecompiler.DirectXShaderModel.Xenos;

/// <summary>
/// Translates Xenos microcode into Direct3D 9 shader model 3 bytecode with the
/// same meaning, so the D3D9 path decompiles it. Most Xenos operations are a
/// D3D9 instruction; the rest are short sequences through scratch temps above
/// the shader's own registers. Interpolators and vertex fetches become inputs
/// moved into the registers the microcode reads them from, exports become
/// outputs, and the control flow program becomes structured if/else/loop, with
/// predicated instructions compared against a snapshot of what set the
/// predicate. What is not handled throws NotSupportedException.
/// </summary>
public sealed class XenosTranslator
{
    private readonly XenosShader _shader;
    private readonly List<uint> _code = [];
    private readonly bool _isPixel;

    // Scratch temps, past the shader's registers.
    private readonly int _scratch0;
    private readonly int _scratch1;
    private readonly int _scratch2;
    private readonly int _predicate;
    private readonly int _lod;
    private readonly int _gradientH;
    private readonly int _gradientV;
    private readonly int _previous;

    // The scalar result ps, kept in _previous.x where a *_prev operation reads it.
    private readonly bool _usesPrevious;

    // texCUBE compiles to a cube instruction, ALU work that turns its result
    // into face coordinates, and a tfetchCube of those: together a texld of the
    // direction the cube instruction was given. The lanes that derive from the
    // cube result, by register, until the fetch; what computes them is skipped.
    private readonly Dictionary<int, int> _cubeLanes = [];
    private uint[] _cubeDirection;

    // A float register holding (0, 1, 0, 0), defined if used.
    private readonly int _constantRegister;
    private bool _usesConstant;

    private readonly Dictionary<int, int> _vertexInputs = [];
    private readonly List<(DeclUsage Usage, int Index, int Register, int Mask, bool Centroid)> _inputDeclarations = [];
    private readonly Dictionary<int, int> _interpolatorOutputs = [];
    private readonly List<(DeclUsage Usage, int Index, int Register, int Mask)> _outputDeclarations = [];
    private int _positionOutput = -1;
    private int _pointSizeOutput = -1;
    private readonly HashSet<int> _samplers = [];

    // The predicate: p0 is (snapshot comparison 0), snapshot in _predicate.x.
    private IfComparison _predicateComparison = IfComparison.None;
    // The open predicated block: the condition it runs on, and whether its else is out.
    private bool? _openPredicate;
    private bool _openPredicateHasElse;
    // Set by a setp: what follows tests the new snapshot.
    private bool _predicateChanged;
    // With the predicate stack (setp_*_push, setp_pop) the snapshot is p0 itself, 1 or 0.
    private readonly bool _predicateIsBoolean;

    public XenosTranslator(XenosShader shader)
    {
        _shader = shader;
        _isPixel = shader.Type == ShaderType.Pixel;
        int first = Math.Max(shader.Config.MaxRegister + 1, (shader.Config.ParamGenRegister ?? 0) + 1);
        _scratch0 = first;
        _scratch1 = first + 1;
        _scratch2 = first + 2;
        _predicate = first + 3;
        _lod = first + 4;
        _gradientH = first + 5;
        _gradientV = first + 6;
        _previous = first + 7;
        _usesPrevious = UsesPrevious();
        _predicateIsBoolean = UsesPredicateStack();
        _constantRegister = FreeConstantRegister();
    }

    public byte[] Translate()
    {
        DeclareInterface();
        TranslateControlFlow();
        ClosePredicate();

        var tokens = new List<uint> { (_isPixel ? 0xFFFF0000u : 0xFFFE0000u) | 0x0300 };
        tokens.AddRange(ConstantTableComment());
        WriteDefinitions(tokens);
        WriteDeclarations(tokens);
        tokens.AddRange(_code);
        tokens.Add(0x0000FFFF);

        var bytes = new byte[tokens.Count * 4];
        for (int i = 0; i < tokens.Count; i++)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 4), tokens[i]);
        }
        return bytes;
    }

    /// <summary>The translation, read as the D3D9 path reads bytecode.</summary>
    public ShaderModel TranslateToShaderModel()
    {
        using var stream = new MemoryStream(Translate());
        using var reader = new ShaderReader(stream);
        return reader.ReadShader();
    }

    private int IntegerBase => _isPixel ? 16 : 0;
    private int BoolBase => _isPixel ? 128 : 0;

    // The highest float register neither the constant table nor the literals use.
    private int FreeConstantRegister()
    {
        var used = new HashSet<int>();
        foreach (D3D9ConstantDeclaration declaration in _shader.Constants.Declarations)
        {
            if (declaration.RegisterSet == RegisterSet.Float4)
            {
                for (int i = 0; i < declaration.RegisterCount; i++)
                {
                    used.Add(declaration.RegisterIndex + i);
                }
            }
        }
        for (int i = 0; i < _shader.Literals.Count / 4; i++)
        {
            used.Add(_shader.LiteralRegister + i);
        }
        for (int register = 255; register >= 0; register--)
        {
            if (!used.Contains(register))
            {
                return register;
            }
        }
        throw new NotSupportedException("No free float constant register");
    }

    // Inputs, then the moves into the registers the microcode reads them from.
    private void DeclareInterface()
    {
        if (_isPixel)
        {
            int input = 0;
            foreach (XenosDeclaration declaration in _shader.Declarations)
            {
                _inputDeclarations.Add((declaration.Usage, declaration.UsageIndex, input, declaration.Mask,
                    declaration.IsCentroid));
                Emit(Opcode.Mov, Destination(RegisterType.Temp, declaration.Register, declaration.Mask),
                    Source(RegisterType.Input, input));
                input++;
            }
            if (_shader.Config.ParamGenRegister is int paramGen)
            {
                // abs(xy) is the pixel's position, x's sign the face (Xenia's
                // translators do the same).
                Emit(Opcode.Mov, Destination(RegisterType.Temp, paramGen, 0b0010),
                    Source(RegisterType.MiscType, 0, Swizzle(1, 1, 1, 1)));
                Emit(Opcode.Cmp, Destination(RegisterType.Temp, paramGen, 0b0001),
                    Source(RegisterType.MiscType, 1, Swizzle(0, 0, 0, 0)),
                    Source(RegisterType.MiscType, 0, Swizzle(0, 0, 0, 0)),
                    Source(RegisterType.MiscType, 0, Swizzle(0, 0, 0, 0), SourceModifier.Negate));
                Emit(Opcode.Mov, Destination(RegisterType.Temp, paramGen, 0b1100), Zero());
            }
        }
        else
        {
            int input = 0;
            foreach (XenosVertexInput vertexInput in _shader.VertexInputs)
            {
                _inputDeclarations.Add((vertexInput.Usage, vertexInput.UsageIndex, input, 0b1111, false));
                _vertexInputs[vertexInput.Address] = input++;
            }
            int output = 0;
            foreach (XenosDeclaration declaration in _shader.Declarations)
            {
                _outputDeclarations.Add((declaration.Usage, declaration.UsageIndex, output, declaration.Mask));
                _interpolatorOutputs[declaration.Register] = output++;
            }
            _positionOutput = output++;
            _outputDeclarations.Add((DeclUsage.Position, 0, _positionOutput, 0b1111));
            if (_shader.Config.ExportMode == 2)
            {
                _pointSizeOutput = output++;
                _outputDeclarations.Add((DeclUsage.PSize, 0, _pointSizeOutput, 0b0001));
            }
            else if (_shader.Config.ExportMode != 0)
            {
                throw new NotSupportedException($"VsExportMode {_shader.Config.ExportMode}");
            }
        }
    }

    private void WriteDefinitions(List<uint> tokens)
    {
        IReadOnlyList<uint> literals = _shader.Literals;
        for (int i = 0; i + 3 < literals.Count; i += 4)
        {
            tokens.Add(InstructionToken(Opcode.Def, 5));
            tokens.Add(Destination(RegisterType.Const, _shader.LiteralRegister + i / 4, 0b1111));
            tokens.AddRange(literals.Skip(i).Take(4));
        }
        foreach ((int register, uint value) in _shader.IntegerLiterals)
        {
            tokens.Add(InstructionToken(Opcode.DefI, 5));
            tokens.Add(Destination(RegisterType.ConstInt, register - IntegerBase, 0b1111));
            tokens.AddRange([value & 0xFF, (value >> 8) & 0xFF, (uint)(sbyte)((value >> 16) & 0xFF), 0]);
        }
        if (_usesConstant)
        {
            tokens.Add(InstructionToken(Opcode.Def, 5));
            tokens.Add(Destination(RegisterType.Const, _constantRegister, 0b1111));
            tokens.AddRange([BitConverter.SingleToUInt32Bits(0), BitConverter.SingleToUInt32Bits(1), 0, 0]);
        }
    }

    private void WriteDeclarations(List<uint> tokens)
    {
        foreach (var input in _inputDeclarations)
        {
            tokens.Add(InstructionToken(Opcode.Dcl, 2));
            tokens.Add(0x80000000 | (uint)input.Usage | ((uint)input.Index << 16));
            uint destination = Destination(RegisterType.Input, input.Register, input.Mask);
            if (input.Centroid)
            {
                destination |= 0x00400000;
            }
            tokens.Add(destination);
        }
        if (_isPixel && _shader.Config.ParamGenRegister != null)
        {
            tokens.Add(InstructionToken(Opcode.Dcl, 2));
            tokens.Add(0x80000000);
            tokens.Add(Destination(RegisterType.MiscType, 0, 0b0011));
            tokens.Add(InstructionToken(Opcode.Dcl, 2));
            tokens.Add(0x80000000);
            tokens.Add(Destination(RegisterType.MiscType, 1, 0b0001));
        }
        foreach (var output in _outputDeclarations)
        {
            tokens.Add(InstructionToken(Opcode.Dcl, 2));
            tokens.Add(0x80000000 | (uint)output.Usage | ((uint)output.Index << 16));
            tokens.Add(Destination(RegisterType.Output, output.Register, output.Mask));
        }
        foreach (int sampler in _samplers.OrderBy(s => s))
        {
            ParameterType type = _shader.Constants.Declarations
                .FirstOrDefault(d => d.RegisterSet == RegisterSet.Sampler && d.ContainsIndex(sampler))
                ?.TypeInfo.ParameterType ?? ParameterType.Sampler2D;
            SamplerTextureType textureType = type switch
            {
                ParameterType.SamplerCube => SamplerTextureType.Cube,
                ParameterType.Sampler3D => SamplerTextureType.Volume,
                _ => SamplerTextureType.TwoD,
            };
            tokens.Add(InstructionToken(Opcode.Dcl, 2));
            tokens.Add(0x80000000 | ((uint)textureType << 27));
            tokens.Add(Destination(RegisterType.Sampler, sampler, 0b1111));
        }
    }

    // Control flow.

    private enum BlockKind { If, Loop }

    private sealed class Block
    {
        public BlockKind Kind;
        // The control flow index an if closes at.
        public int End;
        public bool HasElse;
    }

    // The control flow program: pairs from the start, up to the first exec's instructions.
    private List<ControlFlowInstruction> ControlFlowInstructions()
    {
        IReadOnlyList<uint> code = _shader.Microcode;
        var instructions = new List<ControlFlowInstruction>();
        int pairCount = code.Count / 3;
        for (int i = 0; i < pairCount; i++)
        {
            (ControlFlowInstruction a, ControlFlowInstruction b) =
                ControlFlowInstruction.Unpack(code[i * 3], code[i * 3 + 1], code[i * 3 + 2]);
            foreach (ControlFlowInstruction cf in new[] { a, b })
            {
                if (cf.IsExec)
                {
                    pairCount = Math.Min(pairCount, cf.ExecAddress);
                }
            }
        }
        for (int i = 0; i < pairCount; i++)
        {
            (ControlFlowInstruction a, ControlFlowInstruction b) =
                ControlFlowInstruction.Unpack(code[i * 3], code[i * 3 + 1], code[i * 3 + 2]);
            instructions.Add(a);
            instructions.Add(b);
        }
        return instructions;
    }

    private void TranslateControlFlow()
    {
        List<ControlFlowInstruction> instructions = ControlFlowInstructions();
        var blocks = new Stack<Block>();
        for (int index = 0; index < instructions.Count; index++)
        {
            while (blocks.Count != 0 && blocks.Peek().Kind == BlockKind.If && blocks.Peek().End == index)
            {
                ClosePredicate();
                Emit(Opcode.Endif);
                blocks.Pop();
            }

            ControlFlowInstruction cf = instructions[index];
            switch (cf.Opcode)
            {
                case ControlFlowOpcode.Nop:
                case ControlFlowOpcode.Alloc:
                case ControlFlowOpcode.MarkVsFetchDone:
                    break;
                case ControlFlowOpcode.Exec:
                case ControlFlowOpcode.ExecEnd:
                    TranslateExec(cf, null);
                    break;
                case ControlFlowOpcode.CondExecPred:
                case ControlFlowOpcode.CondExecPredEnd:
                    TranslateExec(cf, cf.Condition);
                    break;
                case ControlFlowOpcode.CondExec:
                case ControlFlowOpcode.CondExecEnd:
                case ControlFlowOpcode.CondExecPredClean:
                case ControlFlowOpcode.CondExecPredCleanEnd:
                    ClosePredicate();
                    EmitBoolIf(cf.BoolAddress, cf.Condition);
                    TranslateExec(cf, null);
                    ClosePredicate();
                    Emit(Opcode.Endif);
                    break;
                case ControlFlowOpcode.CondJmp:
                    TranslateJump(cf, index, blocks);
                    break;
                case ControlFlowOpcode.LoopStart:
                    ClosePredicate();
                    if (cf.IsRepeat)
                    {
                        throw new NotSupportedException("loop with Repeat=true");
                    }
                    Emit(Opcode.Loop, Source(RegisterType.Loop, 0),
                        Source(RegisterType.ConstInt, cf.LoopId - IntegerBase));
                    blocks.Push(new Block { Kind = BlockKind.Loop });
                    break;
                case ControlFlowOpcode.LoopEnd:
                    ClosePredicate();
                    if (cf.IsPredicatedBreak)
                    {
                        EmitPredicateBreak(cf.Condition);
                    }
                    Emit(Opcode.EndLoop);
                    if (blocks.Count == 0 || blocks.Peek().Kind != BlockKind.Loop)
                    {
                        throw new NotSupportedException("endloop out of a loop");
                    }
                    blocks.Pop();
                    break;
                default:
                    throw new NotSupportedException($"Control flow {cf.Opcode}");
            }

            if (cf.EndsShader)
            {
                if (blocks.Count != 0 || cf.Opcode != ControlFlowOpcode.ExecEnd)
                {
                    throw new NotSupportedException("A shader end inside control flow");
                }
                return;
            }
        }
    }

    // A forward jump: past an if's body (cjmp on the condition's opposite) or
    // past an else (an unconditional jmp at the body's end).
    private void TranslateJump(ControlFlowInstruction cf, int index, Stack<Block> blocks)
    {
        ClosePredicate();
        int target = cf.TargetAddress;
        if (target <= index)
        {
            throw new NotSupportedException("A backward jump");
        }
        if (cf.IsUnconditional)
        {
            if (blocks.Count != 0 && blocks.Peek().Kind == BlockKind.If && blocks.Peek().End == index + 1
                && !blocks.Peek().HasElse)
            {
                Emit(Opcode.Else);
                blocks.Peek().HasElse = true;
                blocks.Peek().End = target;
                return;
            }
            throw new NotSupportedException("An unconditional jump that is not an else");
        }
        if (cf.IsPredicated)
        {
            // Jumping when p0 == condition runs the body when it is not.
            EmitPredicateIf(!cf.Condition);
        }
        else
        {
            EmitBoolIf(cf.BoolAddress, !cf.Condition);
        }
        blocks.Push(new Block { Kind = BlockKind.If, End = target });
    }

    private void EmitBoolIf(int boolAddress, bool condition)
    {
        Emit(Opcode.If, Source(RegisterType.ConstBool, boolAddress - BoolBase));
        if (!condition)
        {
            Emit(Opcode.Else);
        }
    }

    private void TranslateExec(ControlFlowInstruction cf, bool? predicate)
    {
        IReadOnlyList<uint> code = _shader.Microcode;
        int sequence = cf.Sequence;
        for (int address = cf.ExecAddress; address < cf.ExecAddress + cf.ExecCount; address++, sequence >>= 2)
        {
            uint d0 = code[address * 3], d1 = code[address * 3 + 1], d2 = code[address * 3 + 2];
            if ((sequence & 1) != 0)
            {
                var fetch = new FetchInstruction(d0, d1, d2);
                SetPredicate(fetch.IsPredicated ? fetch.PredicateCondition : predicate);
                TranslateFetch(fetch, address);
            }
            else
            {
                var alu = new AluInstruction(d0, d1, d2);
                SetPredicate(alu.IsPredicated ? alu.PredicateCondition : predicate);
                TranslateAlu(alu);
                if (_predicateChanged)
                {
                    ClosePredicate();
                    _predicateChanged = false;
                }
            }
        }
    }

    private bool UsesPredicateStack() => ExecutedAlu().Any(alu =>
        alu.VectorOpcode is >= AluVectorOpcode.SetpEqPush and <= AluVectorOpcode.SetpGePush
            || alu.ScalarOpcode is AluScalarOpcode.SetpPop);

    private IEnumerable<AluInstruction> ExecutedAlu()
    {
        IReadOnlyList<uint> code = _shader.Microcode;
        foreach (ControlFlowInstruction cf in ControlFlowInstructions().Where(cf => cf.IsExec))
        {
            for (int k = 0; k < cf.ExecCount; k++)
            {
                if (((cf.Sequence >> (2 * k)) & 1) == 0)
                {
                    int s = (cf.ExecAddress + k) * 3;
                    yield return new AluInstruction(code[s], code[s + 1], code[s + 2]);
                }
            }
        }
    }

    // Whether a *_prev operation reads ps, which every other scalar operation then keeps.
    private bool UsesPrevious()
    {
        IReadOnlyList<uint> code = _shader.Microcode;
        foreach (ControlFlowInstruction cf in ControlFlowInstructions().Where(cf => cf.IsExec))
        {
            for (int k = 0; k < cf.ExecCount; k++)
            {
                if (((cf.Sequence >> (2 * k)) & 1) != 0)
                {
                    continue;
                }
                int s = (cf.ExecAddress + k) * 3;
                var alu = new AluInstruction(code[s], code[s + 1], code[s + 2]);
                if (alu.ScalarOpcode is AluScalarOpcode.AddsPrev or AluScalarOpcode.MulsPrev
                    or AluScalarOpcode.SubsPrev or AluScalarOpcode.RetainPrev && alu.ScalarWriteMask != 0)
                {
                    return true;
                }
            }
        }
        return false;
    }

    // Predication: one ifc around a run of instructions on the same condition,
    // its else around the opposite run.
    private void SetPredicate(bool? condition)
    {
        if (condition == _openPredicate)
        {
            return;
        }
        if (condition != null && _openPredicate != null && !_openPredicateHasElse)
        {
            Emit(Opcode.Else);
            _openPredicate = condition;
            _openPredicateHasElse = true;
            return;
        }
        ClosePredicate();
        if (condition is bool open)
        {
            EmitPredicateIf(open);
            _openPredicate = open;
            _openPredicateHasElse = false;
        }
    }

    private void ClosePredicate()
    {
        if (_openPredicate != null)
        {
            Emit(Opcode.Endif);
            _openPredicate = null;
        }
    }

    private void EmitPredicateIf(bool condition)
    {
        if (_predicateComparison == IfComparison.None)
        {
            throw new NotSupportedException("A predicate read before it is set");
        }
        Emit(Opcode.IfC, (int)(condition ? _predicateComparison : Opposite(_predicateComparison)),
            Source(RegisterType.Temp, _predicate, Swizzle(0, 0, 0, 0)), Zero());
    }

    private void EmitPredicateBreak(bool condition)
    {
        if (_predicateComparison == IfComparison.None)
        {
            throw new NotSupportedException("A predicate read before it is set");
        }
        Emit(Opcode.BreakC, (int)(condition ? _predicateComparison : Opposite(_predicateComparison)),
            Source(RegisterType.Temp, _predicate, Swizzle(0, 0, 0, 0)), Zero());
    }

    private static IfComparison Opposite(IfComparison comparison) => comparison switch
    {
        IfComparison.GT => IfComparison.LE,
        IfComparison.EQ => IfComparison.NE,
        IfComparison.GE => IfComparison.LT,
        IfComparison.LT => IfComparison.GE,
        IfComparison.NE => IfComparison.EQ,
        IfComparison.LE => IfComparison.GT,
        _ => throw new InvalidOperationException(),
    };

    // Fetches.

    private void TranslateFetch(FetchInstruction fetch, int address)
    {
        if (fetch.IsDestinationRelative || fetch.IsSourceRelative)
        {
            throw new NotSupportedException("A relatively addressed fetch");
        }
        switch (fetch.Opcode)
        {
            case FetchOpcode.VertexFetch:
                if (!_vertexInputs.TryGetValue(address, out int input))
                {
                    throw new NotSupportedException("A vfetch the runtime does not bind");
                }
                WriteFetchResult(fetch, Source(RegisterType.Input, input));
                break;
            case FetchOpcode.SetTextureLod:
                Emit(Opcode.Mov, Destination(RegisterType.Temp, _lod, 0b0001),
                    Source(RegisterType.Temp, fetch.Source, Replicate(fetch.TextureSourceSwizzle & 3)));
                break;
            case FetchOpcode.SetTextureGradientsHorz:
            case FetchOpcode.SetTextureGradientsVert:
                Emit(Opcode.Mov, Destination(RegisterType.Temp,
                        fetch.Opcode == FetchOpcode.SetTextureGradientsHorz ? _gradientH : _gradientV, 0b0111),
                    Source(RegisterType.Temp, fetch.Source, FetchSourceSwizzle(fetch, 3)));
                break;
            case FetchOpcode.TextureFetch:
                TranslateTextureFetch(fetch);
                break;
            case FetchOpcode.GetTextureGradients:
                {
                    // x, z: ddx of the source's x, y; y, w: their ddy.
                    uint source = Source(RegisterType.Temp, fetch.Source, PairSwizzle(fetch));
                    Emit(Opcode.DSX, Destination(RegisterType.Temp, _scratch2, 0b0101), source);
                    Emit(Opcode.DSY, Destination(RegisterType.Temp, _scratch2, 0b1010), source);
                    WriteFetchResult(fetch, Source(RegisterType.Temp, _scratch2));
                }
                break;
            default:
                throw new NotSupportedException($"Fetch {fetch.Opcode}");
        }
    }

    private uint PairSwizzle(FetchInstruction fetch)
    {
        int x = fetch.TextureSourceSwizzle & 3, y = (fetch.TextureSourceSwizzle >> 2) & 3;
        return Swizzle(x, x, y, y);
    }

    private static uint FetchSourceSwizzle(FetchInstruction fetch, int count)
    {
        var components = new int[4];
        for (int i = 0; i < 4; i++)
        {
            components[i] = (fetch.TextureSourceSwizzle >> (2 * Math.Min(i, count - 1))) & 3;
        }
        return Swizzle(components[0], components[1], components[2], components[3]);
    }

    private void TranslateTextureFetch(FetchInstruction fetch)
    {
        if (fetch.LodBias != 0 || fetch.OffsetX != 0 || fetch.OffsetY != 0 || fetch.OffsetZ != 0)
        {
            throw new NotSupportedException("A texture fetch with an LOD bias or offset");
        }
        // A vertex shader's texture fetch constants start at 16.
        int sampler = fetch.TextureFetchConstant - (_isPixel ? 0 : 16);
        _samplers.Add(sampler);
        int count = fetch.Dimension switch { FetchDimension.D1 => 1, FetchDimension.D2 => 2, _ => 3 };
        uint[] coordinates = [Source(RegisterType.Temp, fetch.Source, FetchSourceSwizzle(fetch, count))];
        if (fetch.Dimension == FetchDimension.Cube)
        {
            if (!_cubeLanes.ContainsKey(fetch.Source))
            {
                throw new NotSupportedException("A cube fetch not from a cube instruction");
            }
            coordinates = _cubeDirection;
            _cubeLanes.Clear();
        }
        else
        {
            CheckNotFromCube(fetch.Source, FetchLanes(fetch, count));
        }
        uint samplerSource = Source(RegisterType.Sampler, sampler);
        uint result = Destination(RegisterType.Temp, _scratch2, 0b1111);

        if (fetch.UseRegisterGradients)
        {
            Emit(Opcode.TexLDD, [result, .. coordinates, samplerSource,
                Source(RegisterType.Temp, _gradientH), Source(RegisterType.Temp, _gradientV)]);
        }
        else if (fetch.UseRegisterLod)
        {
            // The LOD, or with the computed one the bias, goes in w.
            Emit(Opcode.Mov, [Destination(RegisterType.Temp, _scratch1, 0b0111), .. coordinates]);
            Emit(Opcode.Mov, Destination(RegisterType.Temp, _scratch1, 0b1000),
                Source(RegisterType.Temp, _lod, Swizzle(0, 0, 0, 0)));
            if (fetch.UseComputedLod)
            {
                Emit(Opcode.Tex, (int)TexldControls.Bias, result, Source(RegisterType.Temp, _scratch1), samplerSource);
            }
            else
            {
                Emit(Opcode.TexLDL, result, Source(RegisterType.Temp, _scratch1), samplerSource);
            }
        }
        else if (!fetch.UseComputedLod || !_isPixel)
        {
            // Neither LOD, or a vertex shader's, which has no derivatives: LOD 0.
            Emit(Opcode.Mov, [Destination(RegisterType.Temp, _scratch1, 0b0111), .. coordinates]);
            Emit(Opcode.Mov, Destination(RegisterType.Temp, _scratch1, 0b1000), Zero());
            Emit(Opcode.TexLDL, result, Source(RegisterType.Temp, _scratch1), samplerSource);
        }
        else
        {
            Emit(Opcode.Tex, [result, .. coordinates, samplerSource]);
        }
        WriteFetchResult(fetch, Source(RegisterType.Temp, _scratch2));
    }

    // A fetch's destination swizzle picks x, y, z, w, 0 or 1 per lane, or keeps it.
    private void WriteFetchResult(FetchInstruction fetch, uint value)
    {
        int swizzle = fetch.DestinationSwizzle;
        int written = 0;
        for (int i = 0; i < 4; i++)
        {
            written |= ((swizzle >> (3 * i)) & 0b111) != 7 ? 1 << i : 0;
        }
        MarkCubeLanes(new Target(RegisterType.Temp, fetch.Destination, false), written, true, false);
        var lanes = new int[4];
        int fromValue = 0, zero = 0, one = 0;
        for (int i = 0; i < 4; i++)
        {
            lanes[i] = (swizzle >> (3 * i)) & 0b111;
            switch (lanes[i])
            {
                case < 4:
                    fromValue |= 1 << i;
                    break;
                case 5:
                    one |= 1 << i;
                    break;
                case 7:
                    break;
                default:
                    zero |= 1 << i;
                    break;
            }
        }
        if (fromValue != 0)
        {
            var components = new int[4];
            for (int i = 0; i < 4; i++)
            {
                components[i] = lanes[i] < 4 ? lanes[i] : 0;
            }
            Emit(Opcode.Mov, Destination(RegisterType.Temp, fetch.Destination, fromValue),
                WithSwizzle(value, Swizzle(components[0], components[1], components[2], components[3])));
        }
        if (zero != 0)
        {
            Emit(Opcode.Mov, Destination(RegisterType.Temp, fetch.Destination, zero), Zero());
        }
        if (one != 0)
        {
            Emit(Opcode.Mov, Destination(RegisterType.Temp, fetch.Destination, one), One());
        }
    }

    // ALU.

    private readonly record struct Target(RegisterType Type, int Register, bool IsExport);

    private void TranslateAlu(AluInstruction alu)
    {
        Target target = ResolveTarget(alu);

        int constant0Mask = alu.IsExport && alu.IsScalarDestinationRelative
            ? 0b1111 & ~(alu.VectorWriteMask | alu.ScalarWriteMask) : 0;
        int constant1Mask = alu.IsExport ? alu.VectorWriteMask & alu.ScalarWriteMask : 0;
        int vectorMask = alu.IsExport ? alu.VectorWriteMask & ~alu.ScalarWriteMask : alu.VectorWriteMask;
        int scalarMask = alu.IsExport ? alu.ScalarWriteMask & ~alu.VectorWriteMask : alu.ScalarWriteMask;
        if (!alu.IsExport && (alu.IsVectorDestinationRelative || alu.IsScalarDestinationRelative))
        {
            throw new NotSupportedException("A relatively addressed ALU destination");
        }

        Target vectorTarget = target;
        Target scalarTarget = alu.IsExport ? target : new Target(RegisterType.Temp, alu.ScalarDestination, false);

        // Co-issued halves read before either writes: a vector result the scalar
        // half reads goes through scratch.
        bool scalarReadsVectorResult = vectorMask != 0 && !target.IsExport
            && ScalarReadsTemp(alu, target.Register);
        Target vectorWrite = scalarReadsVectorResult ? new Target(RegisterType.Temp, _scratch0, false) : vectorTarget;

        bool vectorRuns = vectorMask != 0 || ChangesState(alu.VectorOpcode);
        bool scalarRuns = scalarMask != 0 || ChangesState(alu.ScalarOpcode)
            || _usesPrevious && alu.ScalarOpcode != AluScalarOpcode.RetainPrev;
        if (_cubeLanes.Count != 0 || alu.VectorOpcode == AluVectorOpcode.Cube && vectorRuns)
        {
            // What reads lanes from the cube result only feeds the cube fetch: it
            // is skipped, and the lanes it writes derive from the cube in turn.
            bool vectorFromCube = vectorRuns && (alu.VectorOpcode == AluVectorOpcode.Cube
                || ReadsCubeLanes(alu, vectorMask));
            bool scalarFromCube = scalarRuns && ScalarReadsCubeLanes(alu);
            if (vectorFromCube && alu.VectorOpcode == AluVectorOpcode.Cube)
            {
                TranslateVector(alu, vectorTarget, vectorMask);
            }
            MarkCubeLanes(vectorTarget, vectorMask, vectorRuns, vectorFromCube);
            MarkCubeLanes(scalarTarget, scalarMask, scalarRuns, scalarFromCube);
            vectorRuns &= !vectorFromCube;
            scalarRuns &= !scalarFromCube;
            scalarReadsVectorResult &= vectorRuns && scalarRuns;
            vectorWrite = scalarReadsVectorResult ? vectorWrite : vectorTarget;
        }
        if (vectorRuns)
        {
            TranslateVector(alu, vectorWrite, vectorMask);
        }
        if (scalarRuns)
        {
            TranslateScalar(alu, scalarTarget, scalarMask);
        }
        if (scalarReadsVectorResult && vectorRuns)
        {
            Emit(Opcode.Mov, Destination(target.Type, target.Register, vectorMask),
                Source(RegisterType.Temp, _scratch0));
        }
        if (constant0Mask != 0)
        {
            Emit(Opcode.Mov, Destination(target.Type, target.Register, constant0Mask), Zero());
        }
        if (constant1Mask != 0)
        {
            Emit(Opcode.Mov, Destination(target.Type, target.Register, constant1Mask), One());
        }
    }

    private static bool ChangesState(AluVectorOpcode opcode) =>
        opcode is >= AluVectorOpcode.SetpEqPush and <= AluVectorOpcode.KillNe or AluVectorOpcode.MaxA;

    private static bool ChangesState(AluScalarOpcode opcode) =>
        opcode is >= AluScalarOpcode.SetpEq and <= AluScalarOpcode.KillsOne
            or AluScalarOpcode.MaxAs or AluScalarOpcode.MaxAsf;

    private Target ResolveTarget(AluInstruction alu)
    {
        if (!alu.IsExport)
        {
            return new Target(RegisterType.Temp, alu.VectorDestination, false);
        }
        int export = alu.VectorDestination;
        if (_isPixel)
        {
            if (export <= 3)
            {
                return new Target(RegisterType.ColorOut, export, true);
            }
            if (export == 61)
            {
                return new Target(RegisterType.DepthOut, 0, true);
            }
        }
        else if (export <= 15 && _interpolatorOutputs.TryGetValue(export, out int output))
        {
            return new Target(RegisterType.Output, output, true);
        }
        else if (export == 62)
        {
            return new Target(RegisterType.Output, _positionOutput, true);
        }
        else if (export == 63 && _pointSizeOutput >= 0)
        {
            return new Target(RegisterType.Output, _pointSizeOutput, true);
        }
        throw new NotSupportedException($"Export {export}");
    }

    private void MarkCubeLanes(Target target, int mask, bool runs, bool fromCube)
    {
        if (!runs || mask == 0)
        {
            return;
        }
        if (fromCube && target.IsExport)
        {
            throw new NotSupportedException("A cube result exported");
        }
        int lanes = _cubeLanes.GetValueOrDefault(target.Register);
        lanes = fromCube ? lanes | mask : lanes & ~mask;
        if (lanes == 0)
        {
            _cubeLanes.Remove(target.Register);
        }
        else
        {
            _cubeLanes[target.Register] = lanes;
        }
    }

    // Whether the vector half reads lanes of the cube result.
    private bool ReadsCubeLanes(AluInstruction alu, int mask)
    {
        for (int i = 1; i <= VectorOperandCount(alu.VectorOpcode); i++)
        {
            if (alu.SourceIsTemp(i) && _cubeLanes.TryGetValue(alu.SourceRegister(i) & 0x3F, out int lanes)
                && (VectorLanesRead(alu, i, mask) & lanes) != 0)
            {
                return true;
            }
        }
        return false;
    }

    private bool ScalarReadsCubeLanes(AluInstruction alu)
    {
        int a = ScalarA(alu), b = ScalarB(alu);
        switch (ScalarOperandForm(alu.ScalarOpcode))
        {
            case 1:
            case 2:
                return alu.SourceIsTemp(3) && _cubeLanes.TryGetValue(alu.SourceRegister(3) & 0x3F, out int lanes)
                    && ((1 << a | (ScalarOperandForm(alu.ScalarOpcode) == 2 ? 1 << b : 0)) & lanes) != 0;
            case 3:
                return _cubeLanes.TryGetValue(alu.ScalarConstantOperationTemp, out int tempLanes)
                    && ((1 << b) & tempLanes) != 0;
            default:
                return false;
        }
    }

    private static int VectorLanesRead(AluInstruction alu, int i, int mask)
    {
        int components = alu.VectorOpcode switch
        {
            AluVectorOpcode.Dp4 or AluVectorOpcode.Max4 or AluVectorOpcode.Cube => 0b1111,
            AluVectorOpcode.Dp3 => 0b0111,
            AluVectorOpcode.Dp2Add => i == 3 ? 0b0001 : 0b0011,
            _ => mask,
        };
        int lanes = 0;
        for (int j = 0; j < 4; j++)
        {
            if ((components & (1 << j)) != 0)
            {
                lanes |= 1 << VectorComponent(alu, i, j);
            }
        }
        return lanes;
    }

    private static int FetchLanes(FetchInstruction fetch, int count)
    {
        int lanes = 0;
        for (int i = 0; i < count; i++)
        {
            lanes |= 1 << ((fetch.TextureSourceSwizzle >> (2 * i)) & 3);
        }
        return lanes;
    }

    private void CheckNotFromCube(int register, int lanes)
    {
        if (_cubeLanes.TryGetValue(register, out int cube) && (cube & lanes) != 0)
        {
            throw new NotSupportedException("A cube result read other than by its fetch");
        }
    }

    private static bool VectorReadsTemp(AluInstruction alu, int register)
    {
        for (int i = 1; i <= VectorOperandCount(alu.VectorOpcode); i++)
        {
            if (alu.SourceIsTemp(i) && (alu.SourceRegister(i) & 0x3F) == register)
            {
                return true;
            }
        }
        return false;
    }

    private static int VectorOperandCount(AluVectorOpcode opcode) => opcode switch
    {
        AluVectorOpcode.Frc or AluVectorOpcode.Trunc or AluVectorOpcode.Floor or AluVectorOpcode.Max4 => 1,
        AluVectorOpcode.Mad or AluVectorOpcode.CndEq or AluVectorOpcode.CndGe or AluVectorOpcode.CndGt
            or AluVectorOpcode.Dp2Add => 3,
        _ => 2,
    };

    private static bool ScalarReadsTemp(AluInstruction alu, int register)
    {
        int form = ScalarOperandForm(alu.ScalarOpcode);
        if (form == 3)
        {
            return alu.ScalarConstantOperationTemp == register;
        }
        return form != 0 && alu.SourceIsTemp(3) && (alu.SourceRegister(3) & 0x3F) == register;
    }

    // 0: none; 1: W; 2: W and X; 3: c#.w and r#.x.
    private static int ScalarOperandForm(AluScalarOpcode opcode) => opcode switch
    {
        AluScalarOpcode.Adds or AluScalarOpcode.Muls or AluScalarOpcode.MulsPrev2 or AluScalarOpcode.Maxs
            or AluScalarOpcode.Mins or AluScalarOpcode.MaxAs or AluScalarOpcode.MaxAsf or AluScalarOpcode.Subs => 2,
        AluScalarOpcode.SetpClr or AluScalarOpcode.RetainPrev => 0,
        >= AluScalarOpcode.Mulsc0 and <= AluScalarOpcode.Subsc1 => 3,
        _ => 1,
    };

    // A source operand as D3D9 tokens: the parameter, then a relative register if any.
    private uint[] AluSource(AluInstruction alu, int i, uint swizzle, bool negate = false, bool abs = false)
    {
        int register = alu.SourceRegister(i);
        bool isNegated = alu.SourceNegate(i) ^ negate;
        if (alu.SourceIsTemp(i))
        {
            if ((register & 0x40) != 0)
            {
                throw new NotSupportedException("A relatively addressed temp");
            }
            bool isAbsolute = (register & 0x80) != 0 || abs;
            return [Source(RegisterType.Temp, register & 0x3F, swizzle, Modifier(isNegated, isAbsolute))];
        }
        bool constantAbsolute = alu.AbsConstants || abs;
        uint source = Source(RegisterType.Const, register, swizzle, Modifier(isNegated, constantAbsolute));
        if (!alu.IsSourceConstantRelative(i))
        {
            return [source];
        }
        uint relative = alu.IsConstantAddressRegisterRelative
            ? Source(RegisterType.Addr, 0, Swizzle(0, 0, 0, 0))
            : Source(RegisterType.Loop, 0, Swizzle(0, 0, 0, 0));
        return [source | 0x2000, relative];
    }

    private static SourceModifier Modifier(bool negate, bool abs) =>
        abs ? (negate ? SourceModifier.AbsAndNegate : SourceModifier.Abs)
            : (negate ? SourceModifier.Negate : SourceModifier.None);

    private static uint VectorSwizzle(AluInstruction alu, int i)
    {
        int swizzle = alu.SourceSwizzle(i);
        var components = new int[4];
        for (int j = 0; j < 4; j++)
        {
            components[j] = ((swizzle >> (2 * j)) + j) & 3;
        }
        return Swizzle(components[0], components[1], components[2], components[3]);
    }

    // The scalar operand's a (from W) and b (from X).
    private static int ScalarA(AluInstruction alu) => ((alu.SourceSwizzle(3) >> 6) + 3) & 3;
    private static int ScalarB(AluInstruction alu) => alu.SourceSwizzle(3) & 3;

    private void TranslateVector(AluInstruction alu, Target target, int mask)
    {
        uint destination = Destination(target.Type, target.Register, mask, alu.VectorClamp);
        uint[] S(int i) => AluSource(alu, i, VectorSwizzle(alu, i));
        uint[] SNeg(int i) => AluSource(alu, i, VectorSwizzle(alu, i), negate: true);
        switch (alu.VectorOpcode)
        {
            case AluVectorOpcode.Add:
                Emit(Opcode.Add, [destination, .. S(1), .. S(2)]);
                break;
            case AluVectorOpcode.Mul:
                Emit(Opcode.Mul, [destination, .. S(1), .. S(2)]);
                break;
            case AluVectorOpcode.Max when SameOperand(alu, 1, 2):
                // The Xenos move.
                Emit(Opcode.Mov, [destination, .. S(1)]);
                break;
            case AluVectorOpcode.Max:
                Emit(Opcode.Max, [destination, .. S(1), .. S(2)]);
                break;
            case AluVectorOpcode.Cube:
                {
                    // cube r, v.zzxy, v.yxzz: the direction is the first operand's z, w, x.
                    if (target.Type != RegisterType.Temp)
                    {
                        throw new NotSupportedException("A cube exported");
                    }
                    int x = VectorComponent(alu, 1, 2), y = VectorComponent(alu, 1, 3), z = VectorComponent(alu, 1, 0);
                    _cubeDirection = AluSource(alu, 1, Swizzle(x, y, z, z));
                }
                break;
            case AluVectorOpcode.MaxA:
                if (_isPixel)
                {
                    // ps_3_0 has no address register: its register type 3 is t#.
                    throw new NotSupportedException("maxa in a pixel shader");
                }
                Emit(Opcode.MovA, [Destination(RegisterType.Addr, 0, 0b0001),
                    .. AluSource(alu, 1, Replicate(VectorComponent(alu, 1, 3)))]);
                if (mask != 0)
                {
                    Emit(Opcode.Max, [destination, .. S(1), .. S(2)]);
                }
                break;
            case AluVectorOpcode.Min:
                Emit(Opcode.Min, [destination, .. S(1), .. S(2)]);
                break;
            case AluVectorOpcode.Mad:
                Emit(Opcode.Mad, [destination, .. S(1), .. S(2), .. S(3)]);
                break;
            case AluVectorOpcode.Dp4:
                Emit(Opcode.Dp4, [destination, .. S(1), .. S(2)]);
                break;
            case AluVectorOpcode.Dp3:
                Emit(Opcode.Dp3, [destination, .. S(1), .. S(2)]);
                break;
            case AluVectorOpcode.Dp2Add:
                {
                    int x = VectorComponent(alu, 3, 0);
                    Emit(Opcode.DP2Add, [destination, .. S(1), .. S(2), .. AluSource(alu, 3, Swizzle(x, x, x, x))]);
                }
                break;
            case AluVectorOpcode.Frc:
                Emit(Opcode.Frc, [destination, .. S(1)]);
                break;
            case AluVectorOpcode.Floor:
                Emit(Opcode.Frc, [Destination(RegisterType.Temp, _scratch1, 0b1111), .. S(1)]);
                Emit(Opcode.Add, [destination, .. S(1), Source(RegisterType.Temp, _scratch1, Identity, SourceModifier.Negate)]);
                break;
            case AluVectorOpcode.Trunc:
                // floor(|x|) with x's sign.
                Emit(Opcode.Frc, [Destination(RegisterType.Temp, _scratch1, 0b1111), .. AluSource(alu, 1, VectorSwizzle(alu, 1), abs: true)]);
                Emit(Opcode.Add, [Destination(RegisterType.Temp, _scratch1, 0b1111), .. AluSource(alu, 1, VectorSwizzle(alu, 1), abs: true),
                    Source(RegisterType.Temp, _scratch1, Identity, SourceModifier.Negate)]);
                Emit(Opcode.Cmp, [destination, .. S(1), Source(RegisterType.Temp, _scratch1),
                    Source(RegisterType.Temp, _scratch1, Identity, SourceModifier.Negate)]);
                break;
            case AluVectorOpcode.Sge:
                Emit(Opcode.Sge, [destination, .. S(1), .. S(2)]);
                break;
            case AluVectorOpcode.Sgt:
                Emit(Opcode.Slt, [destination, .. S(2), .. S(1)]);
                break;
            case AluVectorOpcode.Seq:
            case AluVectorOpcode.Sne:
                Emit(Opcode.Sge, [Destination(RegisterType.Temp, _scratch1, 0b1111), .. S(1), .. S(2)]);
                Emit(Opcode.Sge, [Destination(RegisterType.Temp, _scratch2, 0b1111), .. S(2), .. S(1)]);
                if (alu.VectorOpcode == AluVectorOpcode.Seq)
                {
                    Emit(Opcode.Mul, destination, Source(RegisterType.Temp, _scratch1), Source(RegisterType.Temp, _scratch2));
                }
                else
                {
                    Emit(Opcode.Mad, destination, Source(RegisterType.Temp, _scratch1, Identity, SourceModifier.Negate),
                        Source(RegisterType.Temp, _scratch2), One());
                }
                break;
            case AluVectorOpcode.CndEq:
                Emit(Opcode.Cmp, [destination, .. AluSource(alu, 1, VectorSwizzle(alu, 1), negate: !alu.SourceNegate(1), abs: true),
                    .. S(2), .. S(3)]);
                break;
            case AluVectorOpcode.CndGe:
                Emit(Opcode.Cmp, [destination, .. S(1), .. S(2), .. S(3)]);
                break;
            case AluVectorOpcode.CndGt:
                Emit(Opcode.Cmp, [destination, .. SNeg(1), .. S(3), .. S(2)]);
                break;
            case AluVectorOpcode.Max4:
                {
                    uint s = VectorSwizzle(alu, 1);
                    int[] c = [(int)(s & 3), (int)((s >> 2) & 3), (int)((s >> 4) & 3), (int)((s >> 6) & 3)];
                    Emit(Opcode.Max, [Destination(RegisterType.Temp, _scratch1, 0b0011),
                        .. AluSource(alu, 1, Swizzle(c[0], c[1], c[1], c[1])), .. AluSource(alu, 1, Swizzle(c[2], c[3], c[3], c[3]))]);
                    Emit(Opcode.Max, destination, Source(RegisterType.Temp, _scratch1, Swizzle(0, 0, 0, 0)),
                        Source(RegisterType.Temp, _scratch1, Swizzle(1, 1, 1, 1)));
                }
                break;
            case AluVectorOpcode.Dst:
                Emit(Opcode.Dst, [destination, .. S(1), .. S(2)]);
                break;
            case AluVectorOpcode.SetpEqPush:
            case AluVectorOpcode.SetpNePush:
            case AluVectorOpcode.SetpGtPush:
            case AluVectorOpcode.SetpGePush:
                {
                    if (!_predicateIsBoolean || _openPredicate != null)
                    {
                        throw new NotSupportedException("A predicate push under a predicate");
                    }
                    IfComparison comparison = alu.VectorOpcode switch
                    {
                        AluVectorOpcode.SetpEqPush => IfComparison.EQ,
                        AluVectorOpcode.SetpNePush => IfComparison.NE,
                        AluVectorOpcode.SetpGtPush => IfComparison.GT,
                        _ => IfComparison.GE,
                    };
                    // x: the counter, from the first operand's x, 0 where it is 0 and
                    // the second's x passes, else one more. w likewise sets p0.
                    if (mask != 0)
                    {
                        EmitComparison(Destination(RegisterType.Temp, _scratch1, 0b0001),
                            AluSource(alu, 1, Replicate(VectorComponent(alu, 1, 0))), IfComparison.EQ);
                        EmitComparison(Destination(RegisterType.Temp, _scratch2, 0b0001),
                            AluSource(alu, 2, Replicate(VectorComponent(alu, 2, 0))), comparison);
                        Emit(Opcode.Mul, Destination(RegisterType.Temp, _scratch1, 0b0001),
                            Source(RegisterType.Temp, _scratch1, Swizzle(0, 0, 0, 0)), Source(RegisterType.Temp, _scratch2, Swizzle(0, 0, 0, 0)));
                        Emit(Opcode.Add, [Destination(RegisterType.Temp, _scratch2, 0b0001),
                            .. AluSource(alu, 1, Replicate(VectorComponent(alu, 1, 0))), One()]);
                    }
                    EmitComparison(Destination(RegisterType.Temp, _scratch1, 0b0010),
                        AluSource(alu, 1, Replicate(VectorComponent(alu, 1, 3))), IfComparison.EQ);
                    EmitComparison(Destination(RegisterType.Temp, _scratch2, 0b0010),
                        AluSource(alu, 2, Replicate(VectorComponent(alu, 2, 3))), comparison);
                    if (mask != 0)
                    {
                        // (x + 1) * (1 - passed): before p0 is written, which may be the destination.
                        Emit(Opcode.Mad, destination, Source(RegisterType.Temp, _scratch1, Swizzle(0, 0, 0, 0), SourceModifier.Negate),
                            Source(RegisterType.Temp, _scratch2, Swizzle(0, 0, 0, 0)), Source(RegisterType.Temp, _scratch2, Swizzle(0, 0, 0, 0)));
                    }
                    Emit(Opcode.Mul, Destination(RegisterType.Temp, _predicate, 0b0001),
                        Source(RegisterType.Temp, _scratch1, Swizzle(1, 1, 1, 1)), Source(RegisterType.Temp, _scratch2, Swizzle(1, 1, 1, 1)));
                    _predicateComparison = IfComparison.NE;
                    _predicateChanged = true;
                }
                break;
            case AluVectorOpcode.KillGt:
            case AluVectorOpcode.KillGe:
            case AluVectorOpcode.KillEq:
            case AluVectorOpcode.KillNe:
                if (mask != 0)
                {
                    throw new NotSupportedException("A kill's result");
                }
                switch (alu.VectorOpcode)
                {
                    case AluVectorOpcode.KillGt:
                        Emit(Opcode.Slt, [Destination(RegisterType.Temp, _scratch1, 0b1111), .. S(2), .. S(1)]);
                        break;
                    case AluVectorOpcode.KillGe:
                        Emit(Opcode.Sge, [Destination(RegisterType.Temp, _scratch1, 0b1111), .. S(1), .. S(2)]);
                        break;
                    default:
                        Emit(Opcode.Sge, [Destination(RegisterType.Temp, _scratch1, 0b1111), .. S(1), .. S(2)]);
                        Emit(Opcode.Sge, [Destination(RegisterType.Temp, _scratch2, 0b1111), .. S(2), .. S(1)]);
                        if (alu.VectorOpcode == AluVectorOpcode.KillEq)
                        {
                            Emit(Opcode.Mul, Destination(RegisterType.Temp, _scratch1, 0b1111),
                                Source(RegisterType.Temp, _scratch1), Source(RegisterType.Temp, _scratch2));
                        }
                        else
                        {
                            Emit(Opcode.Mad, Destination(RegisterType.Temp, _scratch1, 0b1111),
                                Source(RegisterType.Temp, _scratch1, Identity, SourceModifier.Negate),
                                Source(RegisterType.Temp, _scratch2), One());
                        }
                        break;
                }
                EmitKillIfAnyPositive(_scratch1);
                break;
            default:
                throw new NotSupportedException($"Vector {alu.VectorOpcode}");
        }
    }

    private static bool SameOperand(AluInstruction alu, int i, int j) =>
        alu.SourceIsTemp(i) == alu.SourceIsTemp(j) && alu.SourceRegister(i) == alu.SourceRegister(j)
            && alu.SourceSwizzle(i) == alu.SourceSwizzle(j) && alu.SourceNegate(i) == alu.SourceNegate(j)
            && (alu.SourceIsTemp(i) || alu.IsSourceConstantRelative(i) == alu.IsSourceConstantRelative(j));

    private static int VectorComponent(AluInstruction alu, int i, int component) =>
        ((alu.SourceSwizzle(i) >> (2 * component)) + component) & 3;

    // 1 where the value compares so against 0, else 0.
    private void EmitComparison(uint destination, uint[] value, IfComparison comparison)
    {
        uint[] Modified(SourceModifier modifier) => [(value[0] & ~0x0F000000u) | ((uint)Combine((SourceModifier)((value[0] >> 24) & 0xF), modifier) << 24), .. value.Skip(1)];
        switch (comparison)
        {
            case IfComparison.EQ:
                Emit(Opcode.Cmp, [destination, .. Modified(SourceModifier.AbsAndNegate), One(), Zero()]);
                break;
            case IfComparison.NE:
                Emit(Opcode.Cmp, [destination, .. Modified(SourceModifier.AbsAndNegate), Zero(), One()]);
                break;
            case IfComparison.GT:
                Emit(Opcode.Cmp, [destination, .. Modified(SourceModifier.Negate), Zero(), One()]);
                break;
            default:
                Emit(Opcode.Cmp, [destination, .. value, One(), Zero()]);
                break;
        }
    }

    // A modifier applied over another: -(-x) is x, -|x| stays.
    private static SourceModifier Combine(SourceModifier inner, SourceModifier outer)
    {
        bool abs = inner is SourceModifier.Abs or SourceModifier.AbsAndNegate || outer is SourceModifier.Abs or SourceModifier.AbsAndNegate;
        bool innerNegate = inner is SourceModifier.Negate or SourceModifier.AbsAndNegate;
        bool outerNegate = outer is SourceModifier.Negate or SourceModifier.AbsAndNegate;
        bool negate = outer is SourceModifier.AbsAndNegate ? true : (abs && outer is SourceModifier.Abs ? false : innerNegate ^ outerNegate);
        return Modifier(negate, abs);
    }

    // texkill kills where a component is negative: where any of the four is 1.
    private void EmitKillIfAnyPositive(int register)
    {
        Emit(Opcode.Mov, Destination(RegisterType.Temp, register, 0b1111),
            Source(RegisterType.Temp, register, Identity, SourceModifier.Negate));
        Emit(Opcode.TexKill, Destination(RegisterType.Temp, register, 0b1111));
    }

    private void TranslateScalar(AluInstruction alu, Target target, int mask)
    {
        uint destination = Destination(target.Type, target.Register, mask, alu.ScalarClamp);
        // Where ps is read, the result goes to it first, then to the destination.
        bool keepsPrevious = _usesPrevious && !ChangesState(alu.ScalarOpcode)
            && alu.ScalarOpcode != AluScalarOpcode.RetainPrev;
        uint finalDestination = destination;
        if (keepsPrevious)
        {
            destination = Destination(RegisterType.Temp, _previous, 0b0001);
        }
        uint previous = Source(RegisterType.Temp, _previous, Swizzle(0, 0, 0, 0));
        int a = ScalarA(alu), b = ScalarB(alu);
        uint[] A(bool negate = false, bool abs = false) => AluSource(alu, 3, Swizzle(a, a, a, a), negate, abs);
        uint[] B(bool negate = false) => AluSource(alu, 3, Swizzle(b, b, b, b), negate);
        uint scratch = Destination(RegisterType.Temp, _scratch1, 0b1111);
        uint scratchSource = Source(RegisterType.Temp, _scratch1, Swizzle(0, 0, 0, 0));

        switch (alu.ScalarOpcode)
        {
            case AluScalarOpcode.Adds:
                Emit(Opcode.Add, [destination, .. A(), .. B()]);
                break;
            case AluScalarOpcode.Subs:
                Emit(Opcode.Add, [destination, .. A(), .. B(negate: true)]);
                break;
            case AluScalarOpcode.Muls:
                Emit(Opcode.Mul, [destination, .. A(), .. B()]);
                break;
            case AluScalarOpcode.Maxs when a == b:
                Emit(Opcode.Mov, [destination, .. A()]);
                break;
            case AluScalarOpcode.Maxs:
                Emit(Opcode.Max, [destination, .. A(), .. B()]);
                break;
            case AluScalarOpcode.Mins:
                Emit(Opcode.Min, [destination, .. A(), .. B()]);
                break;
            case AluScalarOpcode.Mulsc0:
            case AluScalarOpcode.Mulsc1:
            case AluScalarOpcode.Addsc0:
            case AluScalarOpcode.Addsc1:
            case AluScalarOpcode.Subsc0:
            case AluScalarOpcode.Subsc1:
                {
                    // c#.w (src3's register as a constant) and a temp's x, src3's
                    // negation and abs_constants on both.
                    bool negate = alu.SourceNegate(3);
                    SourceModifier modifier = Modifier(negate, alu.AbsConstants);
                    uint constant = Source(RegisterType.Const, alu.SourceRegister(3), Swizzle(a, a, a, a), modifier);
                    if (alu.IsSourceConstantRelative(3))
                    {
                        throw new NotSupportedException("A relative scalar constant operand");
                    }
                    uint temp = Source(RegisterType.Temp, alu.ScalarConstantOperationTemp, Swizzle(b, b, b, b), modifier);
                    uint negatedTemp = Source(RegisterType.Temp, alu.ScalarConstantOperationTemp, Swizzle(b, b, b, b),
                        Modifier(!negate, alu.AbsConstants));
                    switch (alu.ScalarOpcode)
                    {
                        case AluScalarOpcode.Mulsc0 or AluScalarOpcode.Mulsc1:
                            Emit(Opcode.Mul, destination, constant, temp);
                            break;
                        case AluScalarOpcode.Addsc0 or AluScalarOpcode.Addsc1:
                            Emit(Opcode.Add, destination, constant, temp);
                            break;
                        default:
                            Emit(Opcode.Add, destination, constant, negatedTemp);
                            break;
                    }
                }
                break;
            case AluScalarOpcode.Seqs:
                Emit(Opcode.Cmp, [destination, .. A(negate: true, abs: true), One(), Zero()]);
                break;
            case AluScalarOpcode.Snes:
                Emit(Opcode.Cmp, [destination, .. A(negate: true, abs: true), Zero(), One()]);
                break;
            case AluScalarOpcode.Sges:
                Emit(Opcode.Cmp, [destination, .. A(), One(), Zero()]);
                break;
            case AluScalarOpcode.Sgts:
                Emit(Opcode.Cmp, [destination, .. A(negate: true), Zero(), One()]);
                break;
            case AluScalarOpcode.Frcs:
                Emit(Opcode.Frc, [destination, .. A()]);
                break;
            case AluScalarOpcode.Floors:
                Emit(Opcode.Frc, [scratch, .. A()]);
                Emit(Opcode.Add, [destination, .. A(), Source(RegisterType.Temp, _scratch1, Swizzle(0, 0, 0, 0), SourceModifier.Negate)]);
                break;
            case AluScalarOpcode.Truncs:
                Emit(Opcode.Frc, [scratch, .. A(abs: true)]);
                Emit(Opcode.Add, [scratch, .. A(abs: true), Source(RegisterType.Temp, _scratch1, Swizzle(0, 0, 0, 0), SourceModifier.Negate)]);
                Emit(Opcode.Cmp, [destination, .. A(), scratchSource,
                    Source(RegisterType.Temp, _scratch1, Swizzle(0, 0, 0, 0), SourceModifier.Negate)]);
                break;
            case AluScalarOpcode.Exp:
                Emit(Opcode.Exp, [destination, .. A()]);
                break;
            case AluScalarOpcode.Log:
            case AluScalarOpcode.Logc:
                Emit(Opcode.Log, [destination, .. A()]);
                break;
            case AluScalarOpcode.Rcp:
            case AluScalarOpcode.Rcpc:
            case AluScalarOpcode.Rcpf:
                Emit(Opcode.Rcp, [destination, .. A()]);
                break;
            case AluScalarOpcode.Rsq:
            case AluScalarOpcode.Rsqc:
            case AluScalarOpcode.Rsqf:
                Emit(Opcode.Rsq, [destination, .. A()]);
                break;
            case AluScalarOpcode.Sqrt:
                Emit(Opcode.Rsq, [scratch, .. A()]);
                Emit(Opcode.Rcp, destination, scratchSource);
                break;
            case AluScalarOpcode.Sin:
            case AluScalarOpcode.Cos:
                {
                    bool sin = alu.ScalarOpcode == AluScalarOpcode.Sin;
                    Emit(Opcode.SinCos, [Destination(RegisterType.Temp, _scratch1, sin ? 0b0010 : 0b0001), .. A()]);
                    int lane = sin ? 1 : 0;
                    Emit(Opcode.Mov, destination, Source(RegisterType.Temp, _scratch1, Swizzle(lane, lane, lane, lane)));
                }
                break;
            case AluScalarOpcode.SetpEq:
            case AluScalarOpcode.SetpNe:
            case AluScalarOpcode.SetpGt:
            case AluScalarOpcode.SetpGe:
                {
                    IfComparison comparison = alu.ScalarOpcode switch
                    {
                        AluScalarOpcode.SetpEq => IfComparison.EQ,
                        AluScalarOpcode.SetpNe => IfComparison.NE,
                        AluScalarOpcode.SetpGt => IfComparison.GT,
                        _ => IfComparison.GE,
                    };
                    // Under a predicate, the snapshot changes only where the setp
                    // runs, which works only while the comparison stays the same.
                    if (_openPredicate != null && comparison != _predicateComparison)
                    {
                        throw new NotSupportedException("A predicate set under a different predicate");
                    }
                    if (_predicateIsBoolean)
                    {
                        EmitComparison(Destination(RegisterType.Temp, _predicate, 0b0001), A(), comparison);
                        comparison = IfComparison.NE;
                    }
                    else
                    {
                        Emit(Opcode.Mov, [Destination(RegisterType.Temp, _predicate, 0b0001), .. A()]);
                    }
                    _predicateComparison = comparison;
                    _predicateChanged = true;
                }
                if (mask != 0)
                {
                    // 0 where the predicate is set, 1 where it is not.
                    throw new NotSupportedException("A setp's result");
                }
                break;
            case AluScalarOpcode.SetpPop:
                {
                    // a - 1 <= 0: p0 set and 0; else a - 1.
                    if (!_predicateIsBoolean || _openPredicate != null)
                    {
                        throw new NotSupportedException("setp_pop under a predicate");
                    }
                    uint decremented = Destination(RegisterType.Temp, _scratch1, 0b0001);
                    Emit(Opcode.Add, [decremented, .. A(), Source(RegisterType.Const, _constantRegister, Swizzle(1, 1, 1, 1), SourceModifier.Negate)]);
                    _usesConstant = true;
                    uint value = Source(RegisterType.Temp, _scratch1, Swizzle(0, 0, 0, 0));
                    uint test = Source(RegisterType.Temp, _scratch1, Swizzle(0, 0, 0, 0), SourceModifier.Negate);
                    if (mask != 0)
                    {
                        Emit(Opcode.Cmp, destination, test, Zero(), value);
                    }
                    Emit(Opcode.Cmp, Destination(RegisterType.Temp, _predicate, 0b0001), test, One(), Zero());
                    _predicateComparison = IfComparison.NE;
                    _predicateChanged = true;
                }
                break;
            case AluScalarOpcode.KillsGt:
                Emit(Opcode.Mov, [Destination(RegisterType.Temp, _scratch1, 0b1111), .. A(negate: true)]);
                Emit(Opcode.TexKill, Destination(RegisterType.Temp, _scratch1, 0b1111));
                break;
            case AluScalarOpcode.KillsNe:
                Emit(Opcode.Mov, [Destination(RegisterType.Temp, _scratch1, 0b1111), .. A(negate: true, abs: true)]);
                Emit(Opcode.TexKill, Destination(RegisterType.Temp, _scratch1, 0b1111));
                break;
            case AluScalarOpcode.KillsGe:
                Emit(Opcode.Cmp, [Destination(RegisterType.Temp, _scratch1, 0b1111), .. A(), One(), Zero()]);
                EmitKillIfAnyPositive(_scratch1);
                break;
            case AluScalarOpcode.KillsEq:
                Emit(Opcode.Cmp, [Destination(RegisterType.Temp, _scratch1, 0b1111), .. A(negate: true, abs: true), One(), Zero()]);
                EmitKillIfAnyPositive(_scratch1);
                break;
            case AluScalarOpcode.RetainPrev when mask == 0:
                break;
            case AluScalarOpcode.RetainPrev:
                Emit(Opcode.Mov, destination, previous);
                break;
            case AluScalarOpcode.AddsPrev:
                Emit(Opcode.Add, [destination, .. A(), previous]);
                break;
            case AluScalarOpcode.SubsPrev:
                Emit(Opcode.Add, [destination, .. A(), Source(RegisterType.Temp, _previous, Swizzle(0, 0, 0, 0), SourceModifier.Negate)]);
                break;
            case AluScalarOpcode.MulsPrev:
                Emit(Opcode.Mul, [destination, .. A(), previous]);
                break;
            case AluScalarOpcode.MaxAs:
            case AluScalarOpcode.MaxAsf:
                if (_isPixel)
                {
                    // ps_3_0 has no address register: its register type 3 is t#.
                    throw new NotSupportedException("maxas in a pixel shader");
                }
                if (alu.ScalarOpcode == AluScalarOpcode.MaxAs)
                {
                    // mova rounds to the nearest, as maxas does.
                    Emit(Opcode.MovA, [Destination(RegisterType.Addr, 0, 0b0001), .. A()]);
                }
                else
                {
                    Emit(Opcode.Frc, [scratch, .. A()]);
                    Emit(Opcode.Add, [scratch, .. A(), Source(RegisterType.Temp, _scratch1, Swizzle(0, 0, 0, 0), SourceModifier.Negate)]);
                    Emit(Opcode.MovA, Destination(RegisterType.Addr, 0, 0b0001), scratchSource);
                }
                if (mask != 0)
                {
                    Emit(a == b ? Opcode.Mov : Opcode.Max, a == b ? [destination, .. A()] : [destination, .. A(), .. B()]);
                }
                break;
            default:
                throw new NotSupportedException($"Scalar {alu.ScalarOpcode}");
        }
        if (keepsPrevious && mask != 0)
        {
            Emit(Opcode.Mov, finalDestination, previous);
        }
        if (alu.ScalarOpcode is >= AluScalarOpcode.KillsEq and <= AluScalarOpcode.KillsOne && mask != 0)
        {
            throw new NotSupportedException("A kill's result");
        }
    }

    // Tokens.

    private static readonly uint Identity = Swizzle(0, 1, 2, 3);

    private static uint Swizzle(int x, int y, int z, int w) => (uint)(x | (y << 2) | (z << 4) | (w << 6));

    private static uint Replicate(int component) => Swizzle(component, component, component, component);

    private static uint RegisterTypeBits(RegisterType type) =>
        (((uint)type & 7) << 28) | ((((uint)type >> 3) & 3) << 11);

    private static uint Destination(RegisterType type, int register, int mask, bool saturate = false) =>
        0x80000000 | RegisterTypeBits(type) | (uint)register | ((uint)mask << 16) | (saturate ? 0x00100000u : 0);

    private static uint Source(RegisterType type, int register, uint swizzle = 0xE4,
        SourceModifier modifier = SourceModifier.None) =>
        0x80000000 | RegisterTypeBits(type) | (uint)register | (swizzle << 16) | ((uint)modifier << 24);

    private static uint WithSwizzle(uint source, uint swizzle) => (source & ~0x00FF0000u) | (swizzle << 16);

    private uint Zero()
    {
        _usesConstant = true;
        return Source(RegisterType.Const, _constantRegister, Swizzle(0, 0, 0, 0));
    }

    private uint One()
    {
        _usesConstant = true;
        return Source(RegisterType.Const, _constantRegister, Swizzle(1, 1, 1, 1));
    }

    private static uint InstructionToken(Opcode opcode, int length, int controls = 0) =>
        (uint)opcode | ((uint)controls << 16) | ((uint)length << 24);

    private void Emit(Opcode opcode, params uint[] parameters) => Emit(opcode, 0, parameters);

    private void Emit(Opcode opcode, int controls, params uint[] parameters)
    {
        _code.Add(InstructionToken(opcode, parameters.Length, controls));
        _code.AddRange(parameters);
    }

    // The constant table as a CTAB comment.
    private uint[] ConstantTableComment()
    {
        ConstantTable table = _shader.Constants;
        IList<D3D9ConstantDeclaration> declarations = table.Declarations;
        var data = new List<byte>();
        void Int(int value) => data.AddRange(BitConverter.GetBytes(value));
        void Short(int value) => data.AddRange(BitConverter.GetBytes((short)value));

        // Header, then the constant infos; types and strings after, patched in.
        var strings = new List<(int At, string Text)>();
        Int(28);
        strings.Add((data.Count, table.CompilerInfo ?? "HlslDecompiler"));
        Int(0);
        Int((_isPixel ? unchecked((int)0xFFFF0000) : unchecked((int)0xFFFE0000)) | 0x0300);
        Int(declarations.Count);
        Int(28);
        Int((int)table.ShaderFlags);
        strings.Add((data.Count, _isPixel ? "ps_3_0" : "vs_3_0"));
        Int(0);

        var typeFixups = new List<(int At, ShaderTypeInfo Type)>();
        foreach (D3D9ConstantDeclaration declaration in declarations)
        {
            strings.Add((data.Count, declaration.Name));
            Int(0);
            Short((int)declaration.RegisterSet);
            Short(declaration.RegisterIndex);
            Short(declaration.RegisterCount);
            Short(0);
            typeFixups.Add((data.Count, declaration.TypeInfo));
            Int(0);
            Int(0);
        }

        var pending = new Queue<(int At, ShaderTypeInfo Type)>(typeFixups);
        var patches = new List<(int At, int Value)>();
        while (pending.Count != 0)
        {
            (int at, ShaderTypeInfo type) = pending.Dequeue();
            patches.Add((at, data.Count));
            Short((int)type.ParameterClass);
            Short((int)type.ParameterType);
            Short(type.Rows);
            Short(type.Columns);
            Short(type.NumElements);
            Short(type.MemberInfo?.Count ?? 0);
            int membersAt = data.Count;
            Int(0);
            if (type.MemberInfo is { Count: > 0 } members)
            {
                patches.Add((membersAt, data.Count));
                foreach (ShaderStructMemberInfo member in members)
                {
                    strings.Add((data.Count, member.Name));
                    Int(0);
                    pending.Enqueue((data.Count, member.TypeInfo));
                    Int(0);
                }
            }
        }
        foreach ((int at, string text) in strings)
        {
            patches.Add((at, data.Count));
            data.AddRange(Encoding.ASCII.GetBytes(text));
            data.Add(0);
        }
        while (data.Count % 4 != 0)
        {
            data.Add(0xAB);
        }
        byte[] bytes = data.ToArray();
        foreach ((int at, int value) in patches)
        {
            BitConverter.GetBytes(value).CopyTo(bytes, at);
        }

        var tokens = new List<uint> { 0xFFFE | ((uint)(bytes.Length / 4 + 1) << 16), 0x42415443 /* CTAB */ };
        for (int i = 0; i < bytes.Length; i += 4)
        {
            tokens.Add(BitConverter.ToUInt32(bytes, i));
        }
        return [.. tokens];
    }
}
