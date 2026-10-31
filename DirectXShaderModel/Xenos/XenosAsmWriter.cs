using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HlslDecompiler.DirectXShaderModel.Xenos;

/// <summary>
/// Writes Xenos microcode in the syntax XNA Game Studio 3.1's assembler reads
/// back, laid out like its validator's listing: /* pair.slot */ before each
/// control flow instruction and /* address */ before each instruction it runs.
/// Formatting follows Xenia's disassembler, which round-trips through that
/// assembler (shader_translator_disasm.cc).
/// </summary>
public sealed class XenosAsmWriter
{
    private readonly XenosShader _shader;
    private readonly StringBuilder _out = new();

    public XenosAsmWriter(XenosShader shader)
    {
        _shader = shader;
    }

    private bool IsVertexShader => _shader.Type == ShaderType.Vertex;

    public string Write()
    {
        _out.Clear();
        _out.Append(IsVertexShader ? "xvs_3_0\n" : "xps_3_0\n");
        WriteConfig();
        WriteLiterals();
        WriteDeclarations();

        IReadOnlyList<uint> code = _shader.Microcode;
        int pairCount = code.Count / 3;
        var labels = new SortedSet<int>();
        // The control flow ends where the first exec's instructions start.
        for (int i = 0; i < pairCount; i++)
        {
            foreach (ControlFlowInstruction cf in Pair(i))
            {
                if (cf.IsExec)
                {
                    pairCount = Math.Min(pairCount, cf.ExecAddress);
                }
                switch (cf.Opcode)
                {
                    case ControlFlowOpcode.CondCall:
                    case ControlFlowOpcode.CondJmp:
                    case ControlFlowOpcode.LoopStart:
                    case ControlFlowOpcode.LoopEnd:
                        labels.Add(cf.TargetAddress);
                        break;
                }
            }
        }

        FetchInstruction previousFullFetch = default;
        for (int i = 0; i < pairCount; i++)
        {
            (ControlFlowInstruction a, ControlFlowInstruction b) = Unpack(i);
            for (int j = 0; j < 2; j++)
            {
                ControlFlowInstruction cf = j == 0 ? a : b;
                int index = i * 2 + j;
                if (labels.Contains(index))
                {
                    _out.Append($"                label L{index}\n");
                }
                _out.Append($"/* {i,4}.{j} */ ");
                WriteControlFlow(cf, ref previousFullFetch);
            }
        }
        return _out.ToString().Replace("\n", "\r\n");
    }

    // As XNA's listings start, then the program header's fields the assembler
    // takes as options. Without them it adds serialization and infers the
    // resource configs, where the microcode is already final.
    private static readonly string[] ExportModes =
        ["positionOnly", "1", "sprite", "edge", "kill", "spriteKill", "edgeKill"];

    private void WriteConfig()
    {
        XenosProgramConfig config = _shader.Config;
        string stage = IsVertexShader ? "Vs" : "Ps";
        _out.Append("config AutoSerialize=false\n");
        _out.Append("config AutoResource=false\n");
        _out.Append($"config {stage}MaxReg={config.MaxRegister}\n");
        if (config.Resource)
        {
            _out.Append($"config {stage}Resource=1\n");
        }
        if (config.ParamGenRegister is int paramGen)
        {
            _out.Append($"config PsParamGenReg={paramGen}\n");
        }
        if (config.GenScreenXY)
        {
            _out.Append("config PsGenScreenXY=true\n");
        }
        if (config.GenIndex)
        {
            _out.Append($"config {stage}GenIndex=true\n");
        }
        if (config.ExportMode != 0)
        {
            _out.Append("config VsExportMode=").Append(config.ExportMode < ExportModes.Length
                ? ExportModes[config.ExportMode] : $"{config.ExportMode}").Append('\n');
        }
    }

    // The literal constants, every register of them, as the assembler defines them.
    private void WriteLiterals()
    {
        IReadOnlyList<uint> literals = _shader.Literals;
        for (int i = 0; i + 3 < literals.Count; i += 4)
        {
            _out.Append($"def c{_shader.LiteralRegister + i / 4}");
            for (int j = 0; j < 4; j++)
            {
                _out.Append(", ").Append(FormatFloat(BitConverter.UInt32BitsToSingle(literals[i + j])));
            }
            _out.Append('\n');
        }
    }

    // The vertex index register, then each interpolator: a vertex shader's o#,
    // a pixel shader's r#.
    private void WriteDeclarations()
    {
        if (_shader.Index is XenosIndexDeclaration index)
        {
            _out.Append($"dcl_index r{index.Register}.{"xyzw"[index.Component]}\n");
        }
        foreach (XenosDeclaration declaration in _shader.Declarations)
        {
            _out.Append($"dcl_{Semantic(declaration.Usage, declaration.UsageIndex)}");
            _out.Append(declaration.IsCentroid ? "_centroid " : " ");
            _out.Append(IsVertexShader ? 'o' : 'r').Append(declaration.Register);
            if (declaration.Mask != 0b1111)
            {
                _out.Append('.');
                for (int i = 0; i < 4; i++)
                {
                    if ((declaration.Mask & (1 << i)) != 0)
                    {
                        _out.Append("xyzw"[i]);
                    }
                }
            }
            _out.Append('\n');
        }
    }

    private static string Semantic(DeclUsage usage, int index) => $"{usage.ToString().ToLowerInvariant()}{index}";

    private IEnumerable<ControlFlowInstruction> Pair(int i)
    {
        (ControlFlowInstruction a, ControlFlowInstruction b) = Unpack(i);
        yield return a;
        yield return b;
    }

    private (ControlFlowInstruction, ControlFlowInstruction) Unpack(int i)
    {
        IReadOnlyList<uint> code = _shader.Microcode;
        return ControlFlowInstruction.Unpack(code[i * 3], code[i * 3 + 1], code[i * 3 + 2]);
    }

    private void WriteControlFlow(ControlFlowInstruction cf, ref FetchInstruction previousFullFetch)
    {
        switch (cf.Opcode)
        {
            case ControlFlowOpcode.Nop:
                _out.Append("      cnop\n");
                break;
            case ControlFlowOpcode.Exec:
            case ControlFlowOpcode.ExecEnd:
                _out.Append("      ").Append(cf.EndsShader ? "exece" : "exec");
                WriteExecTail(cf, false, cf.IsPredicateClean);
                WriteExecInstructions(cf, ref previousFullFetch);
                break;
            case ControlFlowOpcode.CondExec:
            case ControlFlowOpcode.CondExecEnd:
            case ControlFlowOpcode.CondExecPredClean:
            case ControlFlowOpcode.CondExecPredCleanEnd:
                _out.Append("      ").Append(cf.EndsShader ? "cexece" : "cexec")
                    .Append(cf.Condition ? " b" : " !b").Append(cf.BoolAddress);
                WriteExecTail(cf, true, cf.Opcode is ControlFlowOpcode.CondExecPredClean
                    or ControlFlowOpcode.CondExecPredCleanEnd);
                WriteExecInstructions(cf, ref previousFullFetch);
                break;
            case ControlFlowOpcode.CondExecPred:
            case ControlFlowOpcode.CondExecPredEnd:
                _out.Append(Predicate(cf.Condition)).Append(cf.EndsShader ? "exece" : "exec");
                WriteExecTail(cf, false, cf.IsPredicateClean);
                WriteExecInstructions(cf, ref previousFullFetch);
                break;
            case ControlFlowOpcode.LoopStart:
                _out.Append($"      loop i{cf.LoopId}, L{cf.TargetAddress}");
                if (cf.IsRepeat)
                {
                    _out.Append(", Repeat=true");
                }
                _out.Append('\n');
                break;
            case ControlFlowOpcode.LoopEnd:
                _out.Append(cf.IsPredicatedBreak ? Predicate(cf.Condition) : "      ");
                _out.Append($"endloop i{cf.LoopId}, L{cf.TargetAddress}\n");
                break;
            case ControlFlowOpcode.CondCall:
                WriteBranch(cf, "call", "ccall");
                break;
            case ControlFlowOpcode.Return:
                _out.Append("      ret\n");
                break;
            case ControlFlowOpcode.CondJmp:
                WriteBranch(cf, "jmp", "cjmp");
                break;
            case ControlFlowOpcode.Alloc:
                _out.Append("      alloc ");
                switch (cf.AllocType)
                {
                    case AllocType.VsPosition:
                        _out.Append("position");
                        break;
                    case AllocType.VsInterpolatorsOrPsColors:
                        _out.Append(IsVertexShader ? "interpolators" : "colors");
                        break;
                    case AllocType.Memory:
                        _out.Append($"export = {cf.AllocSize}");
                        break;
                }
                _out.Append('\n');
                break;
            case ControlFlowOpcode.MarkVsFetchDone:
                // Xenia writes nothing for it either; no shader has shown one.
                _out.Append('\n');
                break;
        }
    }

    private void WriteExecTail(ControlFlowInstruction cf, bool isConditional, bool isPredicateClean)
    {
        if (cf.IsYield)
        {
            // `exec, Yield=true` is a syntax error; `cexec b0, Yield=true` needs it.
            _out.Append(isConditional ? ", Yield=true" : " Yield=true");
        }
        if (!isPredicateClean)
        {
            _out.Append("    // PredicateClean=false");
        }
        _out.Append('\n');
    }

    private void WriteBranch(ControlFlowInstruction cf, string name, string conditionalName)
    {
        if (cf.IsUnconditional)
        {
            _out.Append($"      {name} ");
        }
        else if (cf.IsPredicated)
        {
            _out.Append(Predicate(cf.Condition)).Append($"{name} ");
        }
        else
        {
            _out.Append($"      {conditionalName} ").Append(cf.Condition ? "" : "!").Append($"b{cf.BoolAddress}, ");
        }
        _out.Append($"L{cf.TargetAddress}\n");
    }

    private static string Predicate(bool condition) => condition ? " (p0) " : "(!p0) ";

    private void WriteExecInstructions(ControlFlowInstruction cf, ref FetchInstruction previousFullFetch)
    {
        IReadOnlyList<uint> code = _shader.Microcode;
        int sequence = cf.Sequence;
        for (int address = cf.ExecAddress; address < cf.ExecAddress + cf.ExecCount; address++, sequence >>= 2)
        {
            _out.Append($"/* {address,4}   */ ");
            if ((sequence & 0b10) != 0)
            {
                _out.Append("         serialize\n             ");
            }
            uint d0 = code[address * 3], d1 = code[address * 3 + 1], d2 = code[address * 3 + 2];
            if ((sequence & 0b01) != 0)
            {
                var fetch = new FetchInstruction(d0, d1, d2);
                if (fetch.Opcode == FetchOpcode.VertexFetch && FindVertexInput(address) is XenosVertexInput input)
                {
                    WriteSemanticVertexFetch(fetch, input);
                    previousFullFetch = fetch;
                }
                else if (fetch.Opcode == FetchOpcode.VertexFetch)
                {
                    WriteVertexFetch(fetch, previousFullFetch);
                    if (!fetch.IsMiniFetch)
                    {
                        previousFullFetch = fetch;
                    }
                }
                else
                {
                    WriteTextureFetch(fetch);
                }
            }
            else
            {
                WriteAlu(new AluInstruction(d0, d1, d2));
            }
        }
    }

    private void WriteFetchPrefix(FetchInstruction fetch, string name)
    {
        _out.Append("   ").Append(fetch.IsPredicated ? Predicate(fetch.PredicateCondition) : "      ");
        _out.Append(name).Append(' ');
    }

    // r#, a mask with constants, or ._ for none: x, y, z, w, 0, 1, or 7 to keep.
    private void WriteFetchResult(FetchInstruction fetch)
    {
        _out.Append(fetch.IsDestinationRelative ? $"r[{fetch.Destination}+aL]" : $"r{fetch.Destination}");
        var components = new StringBuilder();
        bool isStandard = true;
        int written = 0;
        for (int i = 0; i < 4; i++)
        {
            int swizzle = (fetch.DestinationSwizzle >> (3 * i)) & 0b111;
            char component = swizzle switch
            {
                0 => 'x', 1 => 'y', 2 => 'z', 3 => 'w', 5 => '1', 7 => '_', _ => '0',
            };
            if (component != '_')
            {
                written++;
            }
            if (component != "xyzw"[i])
            {
                isStandard = false;
            }
            components.Append(component);
        }
        if (written == 0)
        {
            _out.Append("._");
        }
        else if (!isStandard)
        {
            _out.Append('.').Append(components);
        }
    }

    private XenosVertexInput? FindVertexInput(int address)
    {
        foreach (XenosVertexInput input in _shader.VertexInputs)
        {
            if (input.Address == address)
            {
                return input;
            }
        }
        return null;
    }

    // A fetch the runtime binds to a vertex element: the semantic names the
    // element, and its fetch constant and data format are placeholders.
    private void WriteSemanticVertexFetch(FetchInstruction fetch, XenosVertexInput input)
    {
        WriteFetchPrefix(fetch, "vfetch");
        WriteFetchResult(fetch);
        _out.Append(", ");
        _out.Append(fetch.IsSourceRelative ? $"r[{fetch.Source}+aL]" : $"r{fetch.Source}");
        _out.Append('.').Append("xyzw"[fetch.VertexSourceSwizzle]);
        _out.Append(", ").Append(Semantic(input.Usage, input.UsageIndex)).Append('\n');
    }

    private static readonly Dictionary<int, string> VertexFormats = new()
    {
        [6] = "FMT_8_8_8_8",
        [7] = "FMT_2_10_10_10",
        [16] = "FMT_10_11_11",
        [17] = "FMT_11_11_10",
        [25] = "FMT_16_16",
        [26] = "FMT_16_16_16_16",
        [31] = "FMT_16_16_FLOAT",
        [32] = "FMT_16_16_16_16_FLOAT",
        [33] = "FMT_32",
        [34] = "FMT_32_32",
        [35] = "FMT_32_32_32_32",
        [36] = "FMT_32_FLOAT",
        [37] = "FMT_32_32_FLOAT",
        [38] = "FMT_32_32_32_32_FLOAT",
        [57] = "FMT_32_32_32_FLOAT",
    };

    private void WriteVertexFetch(FetchInstruction fetch, FetchInstruction previousFullFetch)
    {
        // A mini fetch reuses the last full fetch's address.
        FetchInstruction full = fetch.IsMiniFetch ? previousFullFetch : fetch;
        WriteFetchPrefix(fetch, fetch.IsMiniFetch ? "vfetch_mini" : "vfetch_full");
        WriteFetchResult(fetch);
        if (!fetch.IsMiniFetch)
        {
            _out.Append(", ");
            _out.Append(full.IsSourceRelative ? $"r[{full.Source}+aL]" : $"r{full.Source}");
            _out.Append('.').Append("xyzw"[full.VertexSourceSwizzle]);
            _out.Append($", vf{95 - full.VertexFetchConstant}");
            if (full.IsIndexRounded)
            {
                _out.Append(", RoundIndex=true");
            }
        }
        if (fetch.ExpAdjust != 0)
        {
            _out.Append($", ExpAdjust={fetch.ExpAdjust}");
        }
        if (fetch.Offset != 0)
        {
            _out.Append($", Offset={fetch.Offset}");
        }
        if (fetch.DataFormat != 0)
        {
            _out.Append(", DataFormat=").Append(VertexFormats.TryGetValue(fetch.DataFormat, out string format)
                ? format : $"{fetch.DataFormat}");
        }
        if (!fetch.IsMiniFetch && full.Stride != 0)
        {
            _out.Append($", Stride={full.Stride}");
        }
        if (fetch.IsSigned)
        {
            _out.Append(", Signed=true");
        }
        if (fetch.IsInteger)
        {
            _out.Append(", NumFormat=integer");
        }
        if (fetch.PrefetchCount != 0)
        {
            _out.Append($", PrefetchCount={fetch.PrefetchCount + 1}");
        }
        _out.Append('\n');
    }

    private static readonly string[] TextureFilters = ["point", "linear", "basemap", "keep"];
    private static readonly string[] AnisoFilters = ["disabled", "max1to1", "max2to1", "max4to1", "max8to1", "max16to1", "keep", "keep"];

    private void WriteTextureFetch(FetchInstruction fetch)
    {
        string dimension = fetch.Dimension switch
        {
            FetchDimension.D1 => "1D",
            FetchDimension.D2 => "2D",
            FetchDimension.D3OrStacked => "3D",
            _ => "Cube",
        };
        (string name, bool hasResult, bool hasConstant, bool hasAttributes, int componentCount) = fetch.Opcode switch
        {
            FetchOpcode.TextureFetch => ("tfetch" + dimension, true, true, true, 0),
            FetchOpcode.GetTextureBorderColorFrac => ("getBCF" + dimension, true, true, true, 0),
            FetchOpcode.GetTextureComputedLod => ("getCompTexLOD" + dimension, true, true, true, 0),
            FetchOpcode.GetTextureGradients => ("getGradients", true, true, true, 2),
            FetchOpcode.GetTextureWeights => ("getWeights" + dimension, true, true, true, 0),
            FetchOpcode.SetTextureLod => ("setTexLOD", false, false, false, 1),
            FetchOpcode.SetTextureGradientsHorz => ("setGradientH", false, false, false, 3),
            FetchOpcode.SetTextureGradientsVert => ("setGradientV", false, false, false, 3),
            _ => throw new NotSupportedException($"Fetch opcode {(int)fetch.Opcode}"),
        };
        if (componentCount == 0)
        {
            componentCount = fetch.Dimension switch { FetchDimension.D1 => 1, FetchDimension.D2 => 2, _ => 3 };
        }

        WriteFetchPrefix(fetch, name);
        if (hasResult)
        {
            WriteFetchResult(fetch);
            _out.Append(", ");
        }
        _out.Append(fetch.IsSourceRelative ? $"r[{fetch.Source}+aL]" : $"r{fetch.Source}");
        _out.Append('.');
        // Components past the operation's are in the microcode too: the
        // assembler repeats the last, the compiler leaves x. Exact either way,
        // except that setTexLOD takes one component only.
        int swizzle = fetch.TextureSourceSwizzle;
        int written = fetch.Opcode == FetchOpcode.SetTextureLod ? 1 : 3;
        while (written > componentCount
            && ((swizzle >> (2 * (written - 1))) & 3) == ((swizzle >> (2 * (componentCount - 1))) & 3))
        {
            written--;
        }
        for (int i = 0; i < written; i++)
        {
            _out.Append("xyzw"[(swizzle >> (2 * i)) & 3]);
        }
        if (hasConstant)
        {
            _out.Append($", tf{fetch.TextureFetchConstant}");
        }
        if (!hasAttributes)
        {
            _out.Append('\n');
            return;
        }

        if (!fetch.FetchValidOnly)
        {
            _out.Append(", FetchValidOnly=false");
        }
        if (fetch.UnnormalizedCoordinates)
        {
            _out.Append(", UnnormalizedTextureCoords=true");
        }
        WriteFilter("MagFilter", fetch.MagFilter);
        WriteFilter("MinFilter", fetch.MinFilter);
        WriteFilter("MipFilter", fetch.MipFilter);
        if (fetch.AnisoFilter != 7)
        {
            _out.Append($", AnisoFilter={AnisoFilters[fetch.AnisoFilter]}");
        }
        WriteFilter("VolMagFilter", fetch.VolMagFilter);
        WriteFilter("VolMinFilter", fetch.VolMinFilter);
        if (!fetch.UseComputedLod)
        {
            _out.Append(", UseComputedLOD=false");
        }
        if (fetch.UseRegisterLod)
        {
            _out.Append(", UseRegisterLOD=true");
        }
        if (fetch.UseRegisterGradients)
        {
            _out.Append(", UseRegisterGradients=true");
        }
        if (fetch.LodBias != 0)
        {
            _out.Append(", LODBias=").Append(FormatFloat(fetch.LodBias / 16.0f));
        }
        int dimensionComponents = fetch.Dimension switch { FetchDimension.D1 => 1, FetchDimension.D2 => 2, _ => 3 };
        if (fetch.OffsetX != 0)
        {
            _out.Append(", OffsetX=").Append(FormatFloat(fetch.OffsetX * 0.5f));
        }
        if (dimensionComponents > 1 && fetch.OffsetY != 0)
        {
            _out.Append(", OffsetY=").Append(FormatFloat(fetch.OffsetY * 0.5f));
        }
        if (dimensionComponents > 2 && fetch.OffsetZ != 0)
        {
            _out.Append(", OffsetZ=").Append(FormatFloat(fetch.OffsetZ * 0.5f));
        }
        _out.Append('\n');
    }

    // 3 is "use the fetch constant's".
    private void WriteFilter(string name, int filter)
    {
        if (filter != 3)
        {
            _out.Append($", {name}={TextureFilters[filter]}");
        }
    }

    // The shortest text that reads back as the same float.
    private static string FormatFloat(float value) => value.ToString(CultureInfo.InvariantCulture);

    private static readonly string[] VectorNames =
    [
        "add", "mul", "max", "min", "seq", "sgt", "sge", "sne", "frc", "trunc", "floor", "mad",
        "cndeq", "cndge", "cndgt", "dp4", "dp3", "dp2add", "cube", "max4",
        "setp_eq_push", "setp_ne_push", "setp_gt_push", "setp_ge_push",
        "kill_eq", "kill_gt", "kill_ge", "kill_ne", "dst", "maxa", "opcode_30", "opcode_31",
    ];

    private static int VectorOperandCount(AluVectorOpcode opcode) => opcode switch
    {
        AluVectorOpcode.Frc or AluVectorOpcode.Trunc or AluVectorOpcode.Floor or AluVectorOpcode.Max4 => 1,
        AluVectorOpcode.Mad or AluVectorOpcode.CndEq or AluVectorOpcode.CndGe or AluVectorOpcode.CndGt
            or AluVectorOpcode.Dp2Add => 3,
        (AluVectorOpcode)30 or (AluVectorOpcode)31 => 0,
        _ => 2,
    };

    private static readonly string[] ScalarNames =
    [
        "adds", "adds_prev", "muls", "muls_prev", "muls_prev2", "maxs", "mins", "seqs", "sgts", "sges", "snes",
        "frcs", "truncs", "floors", "exp", "logc", "log", "rcpc", "rcpf", "rcp", "rsqc", "rsqf", "rsq",
        "maxas", "maxasf", "subs", "subs_prev", "setp_eq", "setp_ne", "setp_gt", "setp_ge", "setp_inv",
        "setp_pop", "setp_clr", "setp_rstr", "kills_eq", "kills_gt", "kills_ge", "kills_ne", "kills_one",
        "sqrt", "opcode_41", "mulsc", "mulsc", "addsc", "addsc", "subsc", "subsc", "sin", "cos", "retain_prev",
    ];

    // 0: none; 1: one operand, W; 2: one operand, W and X; 3: c#.w and r#.x.
    private static int ScalarOperandForm(AluScalarOpcode opcode) => opcode switch
    {
        AluScalarOpcode.Adds or AluScalarOpcode.Muls or AluScalarOpcode.MulsPrev2 or AluScalarOpcode.Maxs
            or AluScalarOpcode.Mins or AluScalarOpcode.MaxAs or AluScalarOpcode.MaxAsf or AluScalarOpcode.Subs => 2,
        AluScalarOpcode.SetpClr or AluScalarOpcode.RetainPrev => 0,
        >= AluScalarOpcode.Mulsc0 and <= AluScalarOpcode.Subsc1 => 3,
        > AluScalarOpcode.RetainPrev or (AluScalarOpcode)41 => 0,
        _ => 1,
    };

    private string ScalarName(AluScalarOpcode opcode) =>
        (int)opcode < ScalarNames.Length ? ScalarNames[(int)opcode] : $"opcode_{(int)opcode}";

    private enum Target { Register, Interpolator, Position, PointSize, ExportAddress, ExportData, Color, Depth, None }

    private void WriteAlu(AluInstruction alu)
    {
        // Exports: both operations write the export the vector destination names.
        Target target = Target.Register;
        int exportIndex = 0;
        if (alu.IsExport)
        {
            int export = alu.VectorDestination;
            target = Target.None;
            if (export == 32)
            {
                target = Target.ExportAddress;
            }
            else if (export >= 33 && export <= 37)
            {
                target = Target.ExportData;
                exportIndex = export - 33;
            }
            else if (IsVertexShader)
            {
                if (export <= 15)
                {
                    target = Target.Interpolator;
                    exportIndex = export;
                }
                else if (export == 62)
                {
                    target = Target.Position;
                }
                else if (export == 63)
                {
                    target = Target.PointSize;
                }
            }
            else if (export <= 3)
            {
                target = Target.Color;
                exportIndex = export;
            }
            else if (export == 61)
            {
                target = Target.Depth;
            }
            if (target == Target.None)
            {
                throw new NotSupportedException($"Export {export}");
            }
        }

        // An export's components: written by neither operation are 0 if the
        // scalar destination's relative bit is set, by both are 1.
        int constant0Mask = alu.IsExport && alu.IsScalarDestinationRelative
            ? 0b1111 & ~(alu.VectorWriteMask | alu.ScalarWriteMask) : 0;
        int constant1Mask = alu.IsExport ? alu.VectorWriteMask & alu.ScalarWriteMask : 0;
        int vectorMask = alu.IsExport ? alu.VectorWriteMask & ~alu.ScalarWriteMask : alu.VectorWriteMask;
        int scalarMask = alu.IsExport ? alu.ScalarWriteMask & ~alu.VectorWriteMask : alu.ScalarWriteMask;

        var vectorResult = new Result(target,
            alu.IsExport ? exportIndex : alu.VectorDestination,
            !alu.IsExport && alu.IsVectorDestinationRelative,
            vectorMask | constant0Mask | constant1Mask, constant0Mask, constant1Mask, alu.VectorClamp);
        var scalarResult = new Result(target,
            alu.IsExport ? exportIndex : alu.ScalarDestination,
            !alu.IsExport && alu.IsScalarDestinationRelative,
            scalarMask, 0, 0, alu.ScalarClamp);

        bool isScalarNop = alu.ScalarOpcode == AluScalarOpcode.RetainPrev && scalarResult.Mask == 0 && !scalarResult.Clamp
            && (target != Target.Register || (scalarResult.Index == 0 && !scalarResult.IsRelative));
        bool isVectorNop = IsVectorNop(alu, vectorResult, isScalarNop);

        string predicate = alu.IsPredicated ? Predicate(alu.PredicateCondition) : "      ";
        if (isVectorNop && isScalarNop)
        {
            _out.Append("   ").Append(predicate).Append("nop\n");
            return;
        }
        if (!isVectorNop)
        {
            _out.Append("   ").Append(predicate).Append(VectorNames[(int)alu.VectorOpcode]);
            if (alu.VectorClamp)
            {
                _out.Append("_sat");
            }
            _out.Append(' ');
            WriteResult(vectorResult);
            for (int i = 1; i <= VectorOperandCount(alu.VectorOpcode); i++)
            {
                _out.Append(", ");
                WriteSource(alu, i, 4);
            }
            _out.Append('\n');
        }
        if (!isScalarNop)
        {
            _out.Append(isVectorNop ? "   " : "              + ").Append(predicate).Append(ScalarName(alu.ScalarOpcode));
            if (alu.ScalarClamp)
            {
                _out.Append("_sat");
            }
            _out.Append(' ');
            WriteResult(scalarResult);
            switch (ScalarOperandForm(alu.ScalarOpcode))
            {
                case 1:
                    _out.Append(", ");
                    WriteSource(alu, 3, 1);
                    break;
                case 2:
                    _out.Append(", ");
                    WriteSource(alu, 3, 2);
                    break;
                case 3:
                    WriteScalarConstantOperands(alu);
                    break;
            }
            _out.Append('\n');
        }
    }

    private readonly record struct Result(Target Target, int Index, bool IsRelative, int Mask,
        int Constant0Mask, int Constant1Mask, bool Clamp);

    // max r0._, r0, r0 is the vector half of a lone scalar operation. An export
    // keeps it when the scalar half is a nop too, to say it is an export.
    private static bool IsVectorNop(AluInstruction alu, Result result, bool isScalarNop)
    {
        if (alu.VectorOpcode != AluVectorOpcode.Max || result.Mask != 0 || result.Clamp)
        {
            return false;
        }
        for (int i = 1; i <= 2; i++)
        {
            int register = alu.SourceRegister(i);
            if (!alu.SourceIsTemp(i) || (register & 0x3F) != 0 || (register & 0xC0) != 0
                || alu.SourceNegate(i) || alu.SourceSwizzle(i) != 0)
            {
                return false;
            }
        }
        if (result.Target == Target.Register)
        {
            return result.Index == 0 && !result.IsRelative;
        }
        return !isScalarNop;
    }

    private void WriteResult(Result result)
    {
        switch (result.Target)
        {
            case Target.Register:
                _out.Append('r');
                break;
            case Target.Interpolator:
                _out.Append('o');
                break;
            case Target.Position:
                _out.Append("oPos");
                break;
            case Target.PointSize:
                _out.Append("oPts");
                break;
            case Target.ExportAddress:
                _out.Append("eA");
                break;
            case Target.ExportData:
                _out.Append("eM");
                break;
            case Target.Color:
                _out.Append("oC");
                break;
            case Target.Depth:
                _out.Append("oDepth");
                break;
        }
        if (result.Target is Target.Register or Target.Interpolator or Target.ExportData or Target.Color)
        {
            _out.Append(result.IsRelative ? $"[{result.Index}+aL]" : $"{result.Index}");
        }
        if (result.Mask == 0)
        {
            _out.Append("._");
        }
        else if (result.Mask != 0b1111 || result.Constant0Mask != 0 || result.Constant1Mask != 0)
        {
            _out.Append('.');
            for (int i = 0; i < 4; i++)
            {
                if ((result.Mask & (1 << i)) == 0)
                {
                    _out.Append('_');
                }
                else if ((result.Constant0Mask & (1 << i)) != 0)
                {
                    _out.Append('0');
                }
                else if ((result.Constant1Mask & (1 << i)) != 0)
                {
                    _out.Append('1');
                }
                else
                {
                    _out.Append("xyzw"[i]);
                }
            }
        }
    }

    // componentCount 4: a vector operand; 1: a scalar's W; 2: a scalar's W and X.
    private void WriteSource(AluInstruction alu, int i, int componentCount)
    {
        if (alu.SourceNegate(i))
        {
            _out.Append('-');
        }
        int register = alu.SourceRegister(i);
        bool isAbsolute;
        string index;
        if (alu.SourceIsTemp(i))
        {
            _out.Append('r');
            isAbsolute = (register & 0x80) != 0;
            index = (register & 0x40) != 0 ? $"[{register & 0x3F}+aL]" : null;
            register &= 0x3F;
        }
        else
        {
            _out.Append('c');
            isAbsolute = alu.AbsConstants;
            index = !alu.IsSourceConstantRelative(i) ? null
                : alu.IsConstantAddressRegisterRelative ? $"[{register}+a0]" : $"[{register}+aL]";
        }
        if (isAbsolute)
        {
            _out.Append("_abs");
        }
        _out.Append(index ?? (isAbsolute ? $"[{register}]" : $"{register}"));

        int swizzle = alu.SourceSwizzle(i);
        if (componentCount == 4)
        {
            if (swizzle != 0)
            {
                _out.Append('.');
                for (int j = 0; j < 4; j++)
                {
                    _out.Append(SwizzledComponent(swizzle, j));
                }
            }
        }
        else
        {
            _out.Append('.').Append(SwizzledComponent(swizzle, 3));
            if (componentCount == 2)
            {
                _out.Append(SwizzledComponent(swizzle, 0));
            }
        }
    }

    private static char SwizzledComponent(int swizzle, int component) =>
        "xyzw"[((swizzle >> (2 * component)) + component) & 3];

    // mulsc, addsc, subsc: c#.w, then a temp's .x, both negated by src3's bit.
    private void WriteScalarConstantOperands(AluInstruction alu)
    {
        bool negate = alu.SourceNegate(3);
        int swizzle = alu.SourceSwizzle(3);
        string abs = alu.AbsConstants ? "_abs" : "";

        _out.Append(", ").Append(negate ? "-" : "").Append('c').Append(abs);
        int constant = alu.SourceRegister(3);
        if (alu.IsSourceConstantRelative(3))
        {
            _out.Append(alu.IsConstantAddressRegisterRelative ? $"[{constant}+a0]" : $"[{constant}+aL]");
        }
        else
        {
            _out.Append(alu.AbsConstants ? $"[{constant}]" : $"{constant}");
        }
        _out.Append('.').Append(SwizzledComponent(swizzle, 3));

        int temp = alu.ScalarConstantOperationTemp;
        _out.Append(", ").Append(negate ? "-" : "").Append('r').Append(abs);
        _out.Append(alu.AbsConstants ? $"[{temp}]" : $"{temp}");
        _out.Append('.').Append(SwizzledComponent(swizzle, 0));
    }
}
