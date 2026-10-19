using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace HlslDecompiler.DirectXShaderModel;

public class AsmWriter
{
    private ShaderModel shader;
    private StreamWriter asmWriter;

    // "+ " for the second instruction of a ps_1_x co-issued pair.
    private string _linePrefix = "";
    private IDictionary<RegisterKey, int> _samplerDimensions = new Dictionary<RegisterKey, int>();
    private readonly IntegerOperandAnalysis _integerOperandAnalysis;

    public AsmWriter(ShaderModel shader)
    {
        this.shader = shader;
        _integerOperandAnalysis = new IntegerOperandAnalysis(shader);
    }

    void WriteLine(string value)
    {
        asmWriter.WriteLine(_linePrefix + value);
    }

    void WriteLine(string format, params object[] args)
    {
        asmWriter.WriteLine(_linePrefix + string.Format(CultureInfo.InvariantCulture, format, args));
    }

    private string GetDestinationName(D3D9Instruction instruction)
    {
        int destIndex = instruction.GetDestinationParamIndex().Value;
        string registerName = GetParamRegisterName(instruction, destIndex);
        int destinationLength = GetDestinationSemanticSize(instruction);
        string writeMaskName = instruction.GetDestinationWriteMaskName(destinationLength);
        return $"{registerName}{writeMaskName}";
    }

    private static int GetDestinationSemanticSize(D3D9Instruction instruction)
    {
        RegisterType registerType = instruction.GetParamRegisterType(instruction.GetDestinationParamIndex().Value);
        if (registerType == RegisterType.DepthOut)
        {
            return 1;
        }
        return 4;
    }

    private static int GetDestinationSemanticSize(D3D10Instruction instruction)
    {
        OperandType operandType = instruction.GetOperandType(instruction.GetDestinationParamIndex().Value);
        if (operandType is OperandType.OutputDepth
            or OperandType.OutputCoverageMask
            or OperandType.InputCoverageMask
            or OperandType.OutputStencilRef
            or OperandType.InputInnerCoverage)
        {
            return 1;
        }
        return 4;
    }

    private string GetSourceName(D3D9Instruction instruction, int srcIndex, int? destinationLength = null)
    {
        // The modifier goes on the register, the swizzle last: -v0_abs.z.
        string sourceName = ApplyModifier(instruction.GetSourceModifier(srcIndex),
            GetParamRegisterName(instruction, srcIndex));
        sourceName += instruction.GetSourceSwizzleName(srcIndex, destinationLength);
        return sourceName;
    }

    public void Write(string asmFilename)
    {
        var stream = new FileStream(asmFilename, FileMode.Create, FileAccess.Write);
        Write(stream);
        stream.Dispose();
    }

    public void Write(Stream stream)
    {
        asmWriter = new StreamWriter(stream) { NewLine = "\r\n" };
        WriteLine(shader.Profile);

        foreach (Instruction instruction in shader.Instructions)
        {
            if (instruction is D3D10Instruction d3D10Instruction)
            {
                WriteD3D10Instruction(d3D10Instruction);
            }
            else
            {
                var d3D9Instruction = (D3D9Instruction)instruction;
                _linePrefix = d3D9Instruction.CoIssue ? "+ " : "";
                WriteInstruction(d3D9Instruction);
                _linePrefix = "";
            }
        }

        asmWriter.Dispose();
    }

    private void WriteInstruction(D3D9Instruction instruction)
    {
        switch (instruction.Opcode)
        {
            case Opcode.Abs:
                WriteLine("abs{0} {1}, {2}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.Add:
                WriteLine("add{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                break;
            case Opcode.BreakC:
                WriteLine("break_{0} {1}, {2}", instruction.Comparison.ToString().ToLower(), GetSourceName(instruction, 0),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.Call:
                WriteLine("call {0}", GetSourceName(instruction, 0));
                break;
            case Opcode.CallNZ:
                WriteLine("callnz {0}, {1}", GetSourceName(instruction, 0),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.Cmp:
                WriteLine("cmp{0} {1}, {2}, {3}, {4}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                break;
            case Opcode.Cnd:
                WriteLine("cnd{0} {1}, {2}, {3}, {4}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                break;
            case Opcode.Crs:
                WriteLine("crs{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1, 3), GetSourceName(instruction, 2, 3));
                break;
            case Opcode.Break:
                WriteLine("break");
                break;
            case Opcode.Breakp:
                // fxc spells it `break p0`; the predicate is a destination token,
                // so it takes a write mask, not a swizzle.
                WriteLine("break {0}{1}", GetParamRegisterName(instruction, 0),
                    instruction.GetDestinationWriteMaskName(4));
                break;
            case Opcode.Dcl:
                string dclInstruction = "dcl";
                // ps_2_0 implies the semantic of t# and v#: `dcl t0.xy`, `dcl v0`.
                bool impliedSemantic = instruction.GetParamRegisterType(1) == RegisterType.Texture
                    || (instruction.HasImpliedInputSemantics
                        && instruction.GetParamRegisterType(1) == RegisterType.Input);
                if (instruction.GetParamRegisterType(1) != RegisterType.MiscType
                    && !impliedSemantic)
                {
                    dclInstruction += "_" + instruction.GetDeclSemantic().ToLower();
                }
                WriteLine("{0}{1} {2}", dclInstruction, GetModifier(instruction), GetDestinationName(instruction));
                if (instruction.GetParamRegisterType(1) == RegisterType.Sampler)
                {
                    var registerKey = instruction.GetParamRegisterKey(1);
                    switch (instruction.GetDeclSamplerTextureType())
                    {
                        case SamplerTextureType.TwoD:
                            _samplerDimensions[registerKey] = 2;
                            break;
                        case SamplerTextureType.Cube:
                        case SamplerTextureType.Volume:
                            _samplerDimensions[registerKey] = 3;
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
                break;
            case Opcode.Def:
                {
                    string constRegisterName = GetParamRegisterName(instruction, 0);
                    string constValue0 = ConstantFormatter.Format(instruction.GetParamSingle(1)[0]);
                    string constValue1 = ConstantFormatter.Format(instruction.GetParamSingle(2)[0]);
                    string constValue2 = ConstantFormatter.Format(instruction.GetParamSingle(3)[0]);
                    string constValue3 = ConstantFormatter.Format(instruction.GetParamSingle(4)[0]);
                    WriteLine("def {0}, {1}, {2}, {3}, {4}", constRegisterName, constValue0, constValue1, constValue2, constValue3);
                }
                break;
            case Opcode.DefB:
                WriteLine("defb {0}, {1}", GetParamRegisterName(instruction, 0),
                    instruction.Params[1] != 0 ? "true" : "false");
                break;
            case Opcode.DefI:
                {
                    string constRegisterName = GetParamRegisterName(instruction, 0);
                    WriteLine("defi {0}, {1}, {2}, {3}, {4}", constRegisterName,
                        instruction.Params[1], instruction.Params[2], instruction.Params[3], instruction.Params[4]);
                }
                break;
            case Opcode.DP2Add:
                WriteLine("dp2add {0}, {1}, {2}, {3}", GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                break;
            case Opcode.Dp3:
                WriteLine("dp3{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                break;
            case Opcode.Dp4:
                WriteLine("dp4{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                break;
            case Opcode.DSX:
                WriteLine("dsx {0}, {1}", GetDestinationName(instruction), GetSourceName(instruction, 1));
                break;
            case Opcode.DSY:
                WriteLine("dsy {0}, {1}", GetDestinationName(instruction), GetSourceName(instruction, 1));
                break;
            case Opcode.Dst:
                WriteLine("dst {0}, {1}, {2}", GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                break;
            case Opcode.Else:
                WriteLine("else");
                break;
            case Opcode.Endif:
                WriteLine("endif");
                break;
            case Opcode.EndLoop:
                WriteLine("endloop");
                break;
            case Opcode.EndRep:
                WriteLine("endrep");
                break;
            case Opcode.Exp:
                WriteLine("exp{0} {1}, {2}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.ExpP:
                WriteLine("expp{0} {1}, {2}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.Frc:
                WriteLine("frc {0}, {1}", GetDestinationName(instruction), GetSourceName(instruction, 1));
                break;
            case Opcode.If:
                WriteLine("if {0}", GetSourceName(instruction, 0));
                break;
            case Opcode.IfC:
                WriteLine("if_{0} {1}, {2}",
                    instruction.Comparison.ToString().ToLower(),
                    GetSourceName(instruction, 0), GetSourceName(instruction, 1));
                break;
            case Opcode.Label:
                WriteLine("label {0}", GetSourceName(instruction, 0));
                break;
            case Opcode.Log:
                WriteLine("log{0} {1}, {2}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.LogP:
                WriteLine("logp{0} {1}, {2}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.Loop:
                WriteLine("loop {0}, {1}", GetSourceName(instruction, 0),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.Lrp:
                WriteLine("lrp{0} {1}, {2}, {3}, {4}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                break;
            // Not componentwise: operands are as wide as the mnemonic's first number,
            // whatever the destination mask.
            case Opcode.M3x2:
                WriteLine("m3x2{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1, 3), GetSourceName(instruction, 2, 3));
                break;
            case Opcode.M3x3:
                WriteLine("m3x3{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1, 3), GetSourceName(instruction, 2, 3));
                break;
            case Opcode.M3x4:
                WriteLine("m3x4{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1, 3), GetSourceName(instruction, 2, 3));
                break;
            case Opcode.M4x3:
                WriteLine("m4x3{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1, 4), GetSourceName(instruction, 2, 4));
                break;
            case Opcode.M4x4:
                WriteLine("m4x4{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1, 4), GetSourceName(instruction, 2, 4));
                break;
            case Opcode.Mad:
                WriteLine("mad{0} {1}, {2}, {3}, {4}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                break;
            case Opcode.Max:
                WriteLine("max{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                break;
            case Opcode.Min:
                WriteLine("min{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                break;
            case Opcode.Mov:
                WriteLine("mov{0} {1}, {2}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.MovA:
                WriteLine("mova{0} {1}, {2}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.Mul:
                WriteLine("mul{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                break;
            case Opcode.Nop:
                WriteLine("nop");
                break;
            case Opcode.Lit:
                WriteLine("lit{0} {1}, {2}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.Nrm:
                WriteLine("nrm{0} {1}, {2}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.Pow:
                WriteLine("pow{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                break;
            case Opcode.Rep:
                WriteLine("rep {0}", GetSourceName(instruction, 0));
                break;
            case Opcode.Rcp:
                WriteLine("rcp{0} {1}, {2}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.Ret:
                WriteLine("ret");
                break;
            case Opcode.Rsq:
                WriteLine("rsq{0} {1}, {2}", GetModifier(instruction),GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.Sge:
                WriteLine("sge{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                break;
            case Opcode.Sgn:
                WriteLine("sgn{0} {1}, {2}, {3}, {4}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                break;
            case Opcode.SetP:
                WriteLine("setp_{0} {1}, {2}, {3}", instruction.Comparison.ToString().ToLower(),
                    GetDestinationName(instruction), GetSourceName(instruction, 1),
                    GetSourceName(instruction, 2));
                break;
            case Opcode.Slt:
                WriteLine("slt{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                break;
            case Opcode.SinCos:
                if (shader.MajorVersion >= 3)
                {
                    WriteLine("sincos {0}, {1}", GetDestinationName(instruction),
                        GetSourceName(instruction, 1));
                }
                else
                {
                    WriteLine("sincos {0}, {1}, {2}, {3}", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                }
                break;
            case Opcode.Sub:
                WriteLine("sub{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                break;
            case Opcode.Bem:
                // Not componentwise: the operands are two wide whatever the mask.
                WriteLine("bem {0}, {1}, {2}", GetDestinationName(instruction),
                    GetSourceName(instruction, 1, 2), GetSourceName(instruction, 2, 2));
                break;
            case Opcode.Phase:
                WriteLine("phase");
                break;
            case Opcode.TexCoord:
                // ps_1_4 renamed it texcrd and gave it a coordinate operand.
                if (shader.MajorVersion == 1 && shader.MinorVersion >= 4)
                {
                    // A projected coordinate also reads its divisor: t0_dw.xyww.
                    int? width = instruction.GetSourceModifier(1) switch
                    {
                        SourceModifier.DivideByZ => 3,
                        SourceModifier.DivideByW => 4,
                        _ => null,
                    };
                    WriteLine("texcrd {0}, {1}", GetDestinationName(instruction),
                        GetSourceName(instruction, 1, width));
                }
                else
                {
                    WriteLine("texcoord {0}", GetDestinationName(instruction));
                }
                break;
            case Opcode.TexDepth:
                WriteLine("texdepth {0}", GetDestinationName(instruction));
                break;
            case Opcode.Tex:
                if (shader.MajorVersion > 1)
                {
                    if (instruction.TexldControls.HasFlag(TexldControls.Project))
                    {
                        WriteLine("texldp{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                            GetSourceName(instruction, 1, 4), GetSourceName(instruction, 2));
                    }
                    else if (instruction.TexldControls.HasFlag(TexldControls.Bias))
                    {
                        WriteLine("texldb{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                            GetSourceName(instruction, 1, 4), GetSourceName(instruction, 2));
                    }
                    else
                    {
                        int texldSamplerDimension = _samplerDimensions[instruction.GetParamRegisterKey(2)];
                        WriteLine("texld{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                            GetSourceName(instruction, 1, texldSamplerDimension), GetSourceName(instruction, 2));
                    }
                }
                else if (shader.MinorVersion >= 4)
                {
                    // ps_1_4 texld has a coordinate operand but no sampler.
                    WriteLine("texld {0}, {1}", GetDestinationName(instruction),
                        GetSourceName(instruction, 1));
                }
                else
                {
                    WriteLine("tex {0}", GetDestinationName(instruction));
                }
                break;
            case Opcode.TexLDL:
                WriteLine("texldl{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1, 4), GetSourceName(instruction, 2));
                break;
            case Opcode.TexLDD:
                int samplerDimension = _samplerDimensions[instruction.GetParamRegisterKey(2)];
                WriteLine("texldd {0}, {1}, {2}, {3}, {4}",
                    GetDestinationName(instruction),
                    GetSourceName(instruction, 1, samplerDimension),
                    GetSourceName(instruction, 2),
                    GetSourceName(instruction, 3, samplerDimension),
                    GetSourceName(instruction, 4, samplerDimension));
                break;
            case Opcode.TexBem:
                WriteLine("texbem {0}, {1}", GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.TexBeml:
                WriteLine("texbeml {0}, {1}", GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.TexDP3:
                WriteLine("texdp3 {0}, {1}", GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.TexDP3Tex:
                WriteLine("texdp3tex {0}, {1}", GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.TexM3x2Depth:
                WriteLine("texm3x2depth {0}, {1}", GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.TeXM3x2Pad:
                WriteLine("texm3x2pad {0}, {1}", GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.TexM3x2Tex:
                WriteLine("texm3x2tex {0}, {1}", GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.TexM3x3:
                WriteLine("texm3x3 {0}, {1}", GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.TexM3x3Diff:
                WriteLine("texm3x3diff {0}, {1}", GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.TeXM3x3Pad:
                WriteLine("texm3x3pad {0}, {1}", GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.TexM3x3Tex:
                WriteLine("texm3x3tex {0}, {1}", GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.TexM3x3VSpec:
                WriteLine("texm3x3vspec {0}, {1}", GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.TexReg2AR:
                WriteLine("texreg2ar {0}, {1}", GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.TexReg2GB:
                WriteLine("texreg2gb {0}, {1}", GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.TexReg2RGB:
                WriteLine("texreg2rgb {0}, {1}", GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.TexM3x3Spec:
                WriteLine("texm3x3spec {0}, {1}, {2}", GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                break;
            case Opcode.TexKill:
                WriteLine("texkill {0}", GetDestinationName(instruction));
                break;
            case Opcode.Comment:
            case Opcode.End:
                break;
            default:
                // Throw rather than print the bare opcode name, which looks plausible.
                throw new NotImplementedException(instruction.Opcode.ToString());
        }
    }

    private void WriteD3D10Instruction(D3D10Instruction instruction)
    {
        switch (instruction.Opcode)
        {
            case D3D10Opcode.Add:
                WriteInstruction(instruction, "add", 3);
                break;
            case D3D10Opcode.BreakC:
                WriteInstruction(instruction, Conditional(instruction, "breakc"), 1);
                break;
            case D3D10Opcode.Cut:
                WriteInstruction(instruction, "cut", 0);
                break;
            case D3D10Opcode.DclConstantBuffer:
                // fxc writes CB# in the declaration and cb# elsewhere.
                WriteLine("dcl_constantbuffer CB{0}, {1}", FormatOperand(instruction, 0).Substring(2),
                    instruction.IsDynamicallyIndexed ? "dynamicIndexed" : "immediateIndexed");
                break;
            case D3D10Opcode.DclGlobalFlags:
                var setFlags = new List<string>();
                foreach (D3D10GlobalFlags flag in Enum.GetValues(typeof(D3D10GlobalFlags)))
                {
                    if (flag != D3D10GlobalFlags.None && instruction.GetGlobalFlags().HasFlag(flag))
                    {
                        setFlags.Add(GetGlobalFlagName(flag));
                    }
                }
                WriteLine("dcl_globalFlags {0}", string.Join(" | ", setFlags));
                break;
            case D3D10Opcode.DclInputPS:
                WriteLine("dcl_input_ps {0} {1}", instruction.GetInterpolationModeName(), FormatOperand(instruction, 0));
                break;
            case D3D10Opcode.DclInputPSSiv:
                WriteLine("dcl_input_ps_siv {0} {1}, {2}", instruction.GetInterpolationModeName(),
                    FormatOperand(instruction, 0), GetSystemValueName(instruction));
                break;
            case D3D10Opcode.DclInput:
                WriteLine("dcl_input {0}", FormatOperand(instruction, 0));
                break;
            case D3D10Opcode.DclGSInputPrimitive:
                WriteLine("dcl_inputprimitive {0}", instruction.GetPrimitive().ToHlslString());
                break;
            case D3D10Opcode.DclInputSiv:
                WriteLine("dcl_input_siv {0}, {1}", FormatOperand(instruction, 0),
                    GetSystemValueName(instruction));
                break;
            case D3D10Opcode.DclGSMaxOutputVertexCount:
                WriteLine("dcl_maxout {0}", instruction.GetParamInt(0));
                break;
            case D3D10Opcode.DclGSInstanceCount:
                WriteLine("dcl_gsinstances {0}", instruction.GetParamInt(0));
                break;
            case D3D10Opcode.DclGSOutputPrimitiveTopology:
                WriteLine("dcl_outputtopology {0}", instruction.GetPrimitiveTopology().ToString().ToLower());
                break;
            case D3D10Opcode.DclOutput:
                WriteInstruction(instruction, "dcl_output", 1);
                break;
            case D3D10Opcode.DclOutputSiv:
                WriteLine("dcl_output_siv {0}, {1}", FormatOperand(instruction, 0),
                    GetSystemValueName(instruction));
                break;
            case D3D10Opcode.DclOutputSgv:
                WriteLine("dcl_output_sgv {0}, {1}", FormatOperand(instruction, 0),
                    GetSystemValueName(instruction));
                break;
            case D3D10Opcode.DclResource:
                {
                    ResourceDimension resourceDimension = instruction.GetResourceDimension();
                    string dimension = GetResourceDimensionName(resourceDimension);
                    if (resourceDimension is ResourceDimension.Texture2Dms
                        or ResourceDimension.Texture2DmsArray)
                    {
                        dimension += $"({instruction.ResourceSampleCount})";
                    }
                    WriteInstruction(instruction,
                        $"dcl_resource_{dimension} ({GetResourceReturnTypes(instruction)})", 1);
                }
                break;
            case D3D10Opcode.DclUnorderedAccessViewTyped:
                WriteInstruction(instruction,
                    $"dcl_uav_typed_{GetResourceDimensionName(instruction.GetResourceDimension())}"
                        + (instruction.IsGloballyCoherent ? "_glc" : "")
                        + $" ({GetResourceReturnTypes(instruction)})", 1);
                break;
            case D3D10Opcode.DclResourceStructured:
                WriteLine("dcl_resource_structured {0}, {1}", FormatOperand(instruction, 0), instruction.GetParamIndexImmediate32(1, 0));
                break;
            case D3D10Opcode.DclResourceRaw:
                WriteLine("dcl_resource_raw {0}", FormatOperand(instruction, 0));
                break;
            case D3D10Opcode.DclUnorderedAccessViewRaw:
                WriteLine("dcl_uav_raw{0} {1}", instruction.IsGloballyCoherent ? "_glc" : "",
                    FormatOperand(instruction, 0));
                break;
            case D3D10Opcode.DclSampler:
                WriteLine("dcl_sampler {0}, mode_{1}", FormatOperand(instruction, 0),
                    instruction.SamplerMode.ToString().ToLowerInvariant());
                break;
            case D3D10Opcode.DclTemps:
                WriteLine("dcl_temps {0}", instruction.GetParamInt(0));
                break;
            case D3D10Opcode.DclIndexRange:
                WriteLine("dcl_indexrange {0} {1}", FormatOperand(instruction, 0),
                    instruction.IndexRangeCount);
                break;
            case D3D10Opcode.DclIndexableTemp:
                WriteLine("dcl_indexableTemp x{0}[{1}], {2}", instruction.IndexableTempRegister,
                    instruction.IndexableTempElementCount, instruction.IndexableTempComponentCount);
                break;
            case D3D10Opcode.DclInputControlPointCount:
                WriteLine("dcl_input_control_point_count {0}", instruction.ControlPointCount);
                break;
            case D3D10Opcode.DclOutputControlPointCount:
                WriteLine("dcl_output_control_point_count {0}", instruction.ControlPointCount);
                break;
            case D3D10Opcode.DclTessDomain:
                WriteLine("dcl_tessellator_domain domain_{0}",
                    instruction.TessellatorDomain switch
                    {
                        D3D10TessellatorDomain.Isoline => "isoline",
                        D3D10TessellatorDomain.Triangle => "tri",
                        D3D10TessellatorDomain.Quad => "quad",
                        _ => throw new NotImplementedException(
                            instruction.TessellatorDomain.ToString()),
                    });
                break;
            case D3D10Opcode.DclTessPartitioning:
                WriteLine("dcl_tessellator_partitioning partitioning_{0}",
                    instruction.TessellatorPartitioning switch
                    {
                        D3D10TessellatorPartitioning.Integer => "integer",
                        D3D10TessellatorPartitioning.Pow2 => "pow2",
                        D3D10TessellatorPartitioning.FractionalOdd => "fractional_odd",
                        D3D10TessellatorPartitioning.FractionalEven => "fractional_even",
                        _ => throw new NotImplementedException(
                            instruction.TessellatorPartitioning.ToString()),
                    });
                break;
            case D3D10Opcode.DclTessOutputPrimitive:
                WriteLine("dcl_tessellator_output_primitive output_{0}",
                    instruction.TessellatorOutputPrimitive switch
                    {
                        D3D10TessellatorOutputPrimitive.Point => "point",
                        D3D10TessellatorOutputPrimitive.Line => "line",
                        D3D10TessellatorOutputPrimitive.TriangleClockwise => "triangle_cw",
                        D3D10TessellatorOutputPrimitive.TriangleCounterClockwise => "triangle_ccw",
                        _ => throw new NotImplementedException(
                            instruction.TessellatorOutputPrimitive.ToString()),
                    });
                break;
            case D3D10Opcode.DclHSMaxTessFactor:
                // The token is a raw float, not an operand.
                WriteLine("dcl_hs_max_tessfactor l({0})", ConstantFormatter.Format(
                    BitConverter.Int32BitsToSingle(instruction.GetParamInt(0))));
                break;
            case D3D10Opcode.DclHSForkPhaseInstanceCount:
                WriteLine("dcl_hs_fork_phase_instance_count {0}", instruction.GetParamInt(0));
                break;
            case D3D10Opcode.DclHSJoinPhaseInstanceCount:
                WriteLine("dcl_hs_join_phase_instance_count {0}", instruction.GetParamInt(0));
                break;
            // Hull shader phase markers.
            case D3D10Opcode.HsDecls:
                WriteLine("hs_decls");
                break;
            case D3D10Opcode.HsControlPointPhase:
                WriteLine("hs_control_point_phase");
                break;
            case D3D10Opcode.HsForkPhase:
                WriteLine("hs_fork_phase");
                break;
            case D3D10Opcode.HsJoinPhase:
                WriteLine("hs_join_phase");
                break;
            case D3D10Opcode.DclThreadGroup:
                WriteLine("dcl_thread_group {0}, {1}, {2}", instruction.GetParamIndexImmediate32(0, 0), instruction.GetParamIndexImmediate32(0, 1), instruction.GetParamIndexImmediate32(0, 2));
                break;
            case D3D10Opcode.DclUnorderedAccessViewStructured:
                WriteLine("dcl_uav_structured{0}{1} {2}, {3}",
                    instruction.IsGloballyCoherent ? "_glc" : "",
                    instruction.HasOrderPreservingCounter ? "_opc" : "",
                    FormatOperand(instruction, 0), instruction.GetParamIndexImmediate32(1, 0));
                break;
            // Dynamic linkage declarations carry plain dwords, not operands.
            case D3D10Opcode.DclFunctionBody:
                WriteLine("dcl_function_body fb{0}", instruction.OperandTokens.Tokens[0]);
                break;
            case D3D10Opcode.DclFunctionTable:
                {
                    uint[] tokens = instruction.OperandTokens.Tokens;
                    WriteLine("dcl_function_table ft{0} = {{{1}}}", tokens[0],
                        string.Join(", ", tokens.Skip(2).Take((int)tokens[1]).Select(b => $"fb{b}")));
                    break;
                }
            case D3D10Opcode.DclInterface:
                {
                    // fp0[array length][function count] = {every bindable table}.
                    uint[] tokens = instruction.OperandTokens.Tokens;
                    uint arrayLength = tokens[2] >> 16;
                    uint tableCount = tokens[2] & 0xFFFF;
                    WriteLine("dcl_interface fp{0}[{1}][{2}] = {{{3}}}",
                        tokens[0], arrayLength, tokens[1],
                        string.Join(", ", tokens.Skip(3).Take((int)tableCount).Select(t => $"ft{t}")));
                    break;
                }
            case D3D10Opcode.InterfaceCall:
                {
                    // The function index dword precedes the interface operand.
                    uint[] tokens = instruction.OperandTokens.Tokens;
                    WriteLine("fcall fp{0}[{1}][{2}]", tokens[2], tokens[3], tokens[0]);
                    break;
                }
            case D3D10Opcode.Label:
                WriteLine("label {0}", FormatOperand(instruction, 0));
                break;
            // Subroutine calls: fxc always inlines, so only hand-assembled
            // bytecode holds them. The label operand names the routine.
            case D3D10Opcode.Call:
                WriteInstruction(instruction, "call", 1);
                break;
            case D3D10Opcode.CallC:
                WriteInstruction(instruction, Conditional(instruction, "callc"), 2);
                break;
            case D3D10Opcode.Nop:
                WriteLine("nop");
                break;
            case D3D10Opcode.DclThreadGroupSharedMemoryRaw:
                WriteLine("dcl_tgsm_raw {0}, {1}", FormatOperand(instruction, 0),
                    instruction.GetParamIndexImmediate32(1, 0));
                break;
            case D3D10Opcode.DclThreadGroupSharedMemoryStructured:
                WriteLine("dcl_tgsm_structured {0}, {1}, {2}", FormatOperand(instruction, 0),
                    instruction.GetThreadGroupSharedMemoryStride(), instruction.GetThreadGroupSharedMemoryCount());
                break;
            case D3D10Opcode.DerivRtx:
                WriteInstruction(instruction, "deriv_rtx", 2);
                break;
            case D3D10Opcode.DerivRty:
                WriteInstruction(instruction, "deriv_rty", 2);
                break;
            case D3D10Opcode.DerivRtxCoarse:
                WriteInstruction(instruction, "deriv_rtx_coarse", 2);
                break;
            case D3D10Opcode.DerivRtxFine:
                WriteInstruction(instruction, "deriv_rtx_fine", 2);
                break;
            case D3D10Opcode.DerivRtyCoarse:
                WriteInstruction(instruction, "deriv_rty_coarse", 2);
                break;
            case D3D10Opcode.DerivRtyFine:
                WriteInstruction(instruction, "deriv_rty_fine", 2);
                break;
            case D3D10Opcode.Rcp:
                WriteInstruction(instruction, "rcp", 2);
                break;
            case D3D10Opcode.UBFE:
                WriteInstruction(instruction, "ubfe", 4);
                break;
            case D3D10Opcode.IBFE:
                WriteInstruction(instruction, "ibfe", 4);
                break;
            case D3D10Opcode.BFI:
                WriteInstruction(instruction, "bfi", 5);
                break;
            case D3D10Opcode.Discard:
                WriteInstruction(instruction, Conditional(instruction, "discard"), 1);
                break;
            case D3D10Opcode.Dp2:
                WriteInstruction(instruction, "dp2", 3);
                break;
            case D3D10Opcode.Dp3:
                WriteInstruction(instruction, "dp3", 3);
                break;
            case D3D10Opcode.Dp4:
                WriteInstruction(instruction, "dp4", 3);
                break;
            case D3D10Opcode.Emit:
                WriteInstruction(instruction, "emit", 0);
                break;
            case D3D10Opcode.EndLoop:
                WriteInstruction(instruction, "endloop", 0);
                break;
            case D3D10Opcode.Div:
                WriteInstruction(instruction, "div", 3);
                break;
            case D3D10Opcode.Eq:
                WriteInstruction(instruction, "eq", 3);
                break;
            case D3D10Opcode.GE:
                WriteInstruction(instruction, "ge", 3);
                break;
            case D3D10Opcode.LT:
                WriteInstruction(instruction, "lt", 3);
                break;
            case D3D10Opcode.Ne:
                WriteInstruction(instruction, "ne", 3);
                break;
            case D3D10Opcode.And:
                WriteInstruction(instruction, "and", 3);
                break;
            case D3D10Opcode.Udiv:
                WriteInstruction(instruction, "udiv", 4);
                break;
            case D3D10Opcode.Or:
                WriteInstruction(instruction, "or", 3);
                break;
            case D3D10Opcode.Xor:
                WriteInstruction(instruction, "xor", 3);
                break;
            case D3D10Opcode.Not:
                WriteInstruction(instruction, "not", 2);
                break;
            case D3D10Opcode.IAdd:
                WriteInstruction(instruction, "iadd", 3);
                break;
            case D3D10Opcode.IShl:
                WriteInstruction(instruction, "ishl", 3);
                break;
            case D3D10Opcode.IShr:
                WriteInstruction(instruction, "ishr", 3);
                break;
            case D3D10Opcode.UShr:
                WriteInstruction(instruction, "ushr", 3);
                break;
            case D3D10Opcode.IMul:
                WriteInstruction(instruction, "imul", 4);
                break;
            case D3D10Opcode.IMad:
                WriteInstruction(instruction, "imad", 4);
                break;
            case D3D10Opcode.MSAD:
                WriteInstruction(instruction, "msad", 4);
                break;
            case D3D10Opcode.IMax:
                WriteInstruction(instruction, "imax", 3);
                break;
            case D3D10Opcode.IMin:
                WriteInstruction(instruction, "imin", 3);
                break;
            case D3D10Opcode.Umad:
                WriteInstruction(instruction, "umad", 4);
                break;
            case D3D10Opcode.UMul:
                WriteInstruction(instruction, "umul", 4);
                break;
            case D3D10Opcode.UAddC:
                WriteInstruction(instruction, "uaddc", 4);
                break;
            case D3D10Opcode.USubB:
                WriteInstruction(instruction, "usubb", 4);
                break;
            case D3D10Opcode.SwapC:
                WriteInstruction(instruction, "swapc", 5);
                break;
            case D3D10Opcode.UMax:
                WriteInstruction(instruction, "umax", 3);
                break;
            case D3D10Opcode.UMin:
                WriteInstruction(instruction, "umin", 3);
                break;
            case D3D10Opcode.INeg:
                WriteInstruction(instruction, "ineg", 2);
                break;
            case D3D10Opcode.Ine:
                WriteInstruction(instruction, "ine", 3);
                break;
            case D3D10Opcode.RoundNe:
                WriteInstruction(instruction, "round_ne", 2);
                break;
            case D3D10Opcode.RoundNi:
                WriteInstruction(instruction, "round_ni", 2);
                break;
            case D3D10Opcode.RoundPi:
                WriteInstruction(instruction, "round_pi", 2);
                break;
            case D3D10Opcode.RoundZ:
                WriteInstruction(instruction, "round_z", 2);
                break;
            case D3D10Opcode.Ieq:
                WriteInstruction(instruction, "ieq", 3);
                break;
            case D3D10Opcode.Ige:
                WriteInstruction(instruction, "ige", 3);
                break;
            case D3D10Opcode.UGE:
                WriteInstruction(instruction, "uge", 3);
                break;
            case D3D10Opcode.ULT:
                WriteInstruction(instruction, "ult", 3);
                break;
            case D3D10Opcode.Ilt:
                WriteInstruction(instruction, "ilt", 3);
                break;
            case D3D10Opcode.Ftoi:
                WriteInstruction(instruction, "ftoi", 2);
                break;
            case D3D10Opcode.Ftou:
                WriteInstruction(instruction, "ftou", 2);
                break;
            case D3D10Opcode.IToF:
                WriteInstruction(instruction, "itof", 2);
                break;
            case D3D10Opcode.UTof:
                WriteInstruction(instruction, "utof", 2);
                break;
            case D3D10Opcode.LD:
                WriteInstruction(instruction, "ld", 3);
                break;
            case D3D10Opcode.LDMS:
                WriteInstruction(instruction, "ldms", 4);
                break;
            case D3D10Opcode.SamplePos:
                WriteInstruction(instruction, "samplepos", 3);
                break;
            case D3D10Opcode.SampleInfo:
                WriteInstruction(instruction, instruction.ResInfoReturnType == D3D10ResInfoReturnType.Uint
                    ? "sampleinfo_uint"
                    : "sampleinfo", 2);
                break;
            case D3D10Opcode.ResInfo:
                WriteInstruction(instruction, instruction.ResInfoReturnType switch
                {
                    D3D10ResInfoReturnType.Uint => "resinfo_uint",
                    D3D10ResInfoReturnType.RcpFloat => "resinfo_rcpFloat",
                    _ => "resinfo",
                }, 3);
                break;
            case D3D10Opcode.LdStructured:
                WriteInstruction(instruction, "ld_structured", 4);
                break;
            case D3D10Opcode.LdRaw:
                WriteInstruction(instruction, "ld_raw", 3);
                break;
            case D3D10Opcode.Loop:
                WriteInstruction(instruction, "loop", 0);
                break;
            case D3D10Opcode.Min:
                WriteInstruction(instruction, "min", 3);
                break;
            case D3D10Opcode.Max:
                WriteInstruction(instruction, "max", 3);
                break;
            case D3D10Opcode.Log:
                WriteInstruction(instruction, "log", 2);
                break;
            case D3D10Opcode.Exp:
                WriteInstruction(instruction, "exp", 2);
                break;
            case D3D10Opcode.Frc:
                WriteInstruction(instruction, "frc", 2);
                break;
            case D3D10Opcode.SampleL:
                WriteInstruction(instruction, "sample_l", 5);
                break;
            case D3D10Opcode.SampleB:
                WriteInstruction(instruction, "sample_b", 5);
                break;
            case D3D10Opcode.SampleD:
                WriteInstruction(instruction, "sample_d", 6);
                break;
            case D3D10Opcode.SampleCLZ:
                WriteInstruction(instruction, "sample_c_lz", 5);
                break;
            case D3D10Opcode.Gather4C:
                WriteInstruction(instruction, "gather4_c", 5);
                break;
            case D3D10Opcode.Gather4Po:
                WriteInstruction(instruction, "gather4_po", 5);
                break;
            case D3D10Opcode.Gather4PoC:
                WriteInstruction(instruction, "gather4_po_c", 6);
                break;
            case D3D10Opcode.SampleC:
                WriteInstruction(instruction, "sample_c", 5);
                break;
            case D3D10Opcode.RetC:
                WriteInstruction(instruction, Conditional(instruction, "retc"), 1);
                break;
            case D3D10Opcode.DclInputSgv:
                WriteLine("dcl_input_sgv {0}, {1}", FormatOperand(instruction, 0),
                    GetSystemValueName(instruction));
                break;
            case D3D10Opcode.DclInputPSSgv:
                WriteLine("dcl_input_ps_sgv {0} {1}, {2}", instruction.GetInterpolationModeName(),
                    FormatOperand(instruction, 0), GetSystemValueName(instruction));
                break;
            case D3D10Opcode.CustomData:
                {
                    // On one line, unlike fxc's one row per line.
                    uint[] data = instruction.CustomData ?? [];
                    var rows = new List<string>();
                    for (int row = 0; row + 3 < data.Length; row += 4)
                    {
                        var values = new List<string>();
                        for (int i = 0; i < 4; i++)
                        {
                            values.Add(ConstantFormatter.Format(
                                BitConverter.Int32BitsToSingle((int)data[row + i])));
                        }
                        rows.Add("{ " + string.Join(", ", values) + " }");
                    }
                    WriteLine("dcl_immediateConstantBuffer { " + string.Join(", ", rows) + " }");
                    break;
                }
            case D3D10Opcode.If:
                WriteInstruction(instruction, Conditional(instruction, "if"), 1);
                break;
            case D3D10Opcode.Else:
                WriteInstruction(instruction, "else", 0);
                break;
            case D3D10Opcode.EndIf:
                WriteInstruction(instruction, "endif", 0);
                break;
            case D3D10Opcode.Break:
                WriteInstruction(instruction, "break", 0);
                break;
            case D3D10Opcode.Swtich:
                WriteInstruction(instruction, "switch", 1);
                break;
            case D3D10Opcode.Case:
                WriteInstruction(instruction, "case", 1);
                break;
            case D3D10Opcode.Default:
                WriteInstruction(instruction, "default", 0);
                break;
            case D3D10Opcode.EndSwitch:
                WriteInstruction(instruction, "endswitch", 0);
                break;
            case D3D10Opcode.Continue:
                WriteInstruction(instruction, "continue", 0);
                break;
            case D3D10Opcode.ContinueC:
                WriteInstruction(instruction, Conditional(instruction, "continuec"), 1);
                break;
            case D3D10Opcode.Mad:
                WriteInstruction(instruction, "mad", 4);
                break;
            case D3D10Opcode.Mov:
                WriteInstruction(instruction, "mov", 2);
                break;
            case D3D10Opcode.MovC:
                WriteInstruction(instruction, "movc", 4);
                break;
            case D3D10Opcode.Mul:
                WriteInstruction(instruction, "mul", 3);
                break;
            case D3D10Opcode.Ret:
                WriteInstruction(instruction, "ret", 0);
                break;
            case D3D10Opcode.Rsq:
                WriteInstruction(instruction, "rsq", 2);
                break;
            case D3D10Opcode.Gather4:
                WriteInstruction(instruction, "gather4", 4);
                break;
            case D3D10Opcode.Sample:
                WriteInstruction(instruction, "sample", 4);
                break;
            case D3D10Opcode.Lod:
                WriteInstruction(instruction, "lod", 4);
                break;
            case D3D10Opcode.SinCos:
                WriteInstruction(instruction, "sincos", 3);
                break;
            case D3D10Opcode.Sqrt:
                WriteInstruction(instruction, "sqrt", 2);
                break;
            case D3D10Opcode.CountBits:
                WriteInstruction(instruction, "countbits", 2);
                break;
            case D3D10Opcode.FirstBitLo:
                WriteInstruction(instruction, "firstbit_lo", 2);
                break;
            case D3D10Opcode.FirstBitHi:
                WriteInstruction(instruction, "firstbit_hi", 2);
                break;
            case D3D10Opcode.FirstBitSHi:
                WriteInstruction(instruction, "firstbit_shi", 2);
                break;
            case D3D10Opcode.BFRev:
                WriteInstruction(instruction, "bfrev", 2);
                break;
            case D3D10Opcode.F32ToF16:
                WriteInstruction(instruction, "f32tof16", 2);
                break;
            case D3D10Opcode.F16ToF32:
                WriteInstruction(instruction, "f16tof32", 2);
                break;
            case D3D10Opcode.AtomicIAdd:
                WriteInstruction(instruction, "atomic_iadd", 3);
                break;
            case D3D10Opcode.AtomicAnd:
                WriteInstruction(instruction, "atomic_and", 3);
                break;
            case D3D10Opcode.AtomicOr:
                WriteInstruction(instruction, "atomic_or", 3);
                break;
            case D3D10Opcode.AtomicXor:
                WriteInstruction(instruction, "atomic_xor", 3);
                break;
            case D3D10Opcode.AtomicIMax:
                WriteInstruction(instruction, "atomic_imax", 3);
                break;
            case D3D10Opcode.AtomicIMin:
                WriteInstruction(instruction, "atomic_imin", 3);
                break;
            case D3D10Opcode.AtomicUMax:
                WriteInstruction(instruction, "atomic_umax", 3);
                break;
            case D3D10Opcode.AtomicUMin:
                WriteInstruction(instruction, "atomic_umin", 3);
                break;
            case D3D10Opcode.AtomicCmpStore:
                WriteInstruction(instruction, "atomic_cmp_store", 4);
                break;
            case D3D10Opcode.ImmAtomicAlloc:
                WriteInstruction(instruction, "imm_atomic_alloc", 2);
                break;
            case D3D10Opcode.ImmAtomicConsume:
                WriteInstruction(instruction, "imm_atomic_consume", 2);
                break;
            case D3D10Opcode.ImmAtomicIAdd:
                WriteInstruction(instruction, "imm_atomic_iadd", 4);
                break;
            case D3D10Opcode.ImmAtomicAnd:
                WriteInstruction(instruction, "imm_atomic_and", 4);
                break;
            case D3D10Opcode.ImmAtomicOr:
                WriteInstruction(instruction, "imm_atomic_or", 4);
                break;
            case D3D10Opcode.ImmAtomicXor:
                WriteInstruction(instruction, "imm_atomic_xor", 4);
                break;
            case D3D10Opcode.ImmAtomicIMax:
                WriteInstruction(instruction, "imm_atomic_imax", 4);
                break;
            case D3D10Opcode.ImmAtomicIMin:
                WriteInstruction(instruction, "imm_atomic_imin", 4);
                break;
            case D3D10Opcode.ImmAtomicUMax:
                WriteInstruction(instruction, "imm_atomic_umax", 4);
                break;
            case D3D10Opcode.ImmAtomicUMin:
                WriteInstruction(instruction, "imm_atomic_umin", 4);
                break;
            case D3D10Opcode.ImmAtomicExch:
                WriteInstruction(instruction, "imm_atomic_exch", 4);
                break;
            case D3D10Opcode.ImmAtomicCmpExch:
                WriteInstruction(instruction, "imm_atomic_cmp_exch", 5);
                break;
            case D3D10Opcode.StoreStructured:
                WriteInstruction(instruction, "store_structured", 4);
                break;
            case D3D10Opcode.EvalSampleIndex:
                WriteInstruction(instruction, "eval_sample_index", 3);
                break;
            case D3D10Opcode.EvalSnapped:
                WriteInstruction(instruction, "eval_snapped", 3);
                break;
            case D3D10Opcode.EvalCentroid:
                WriteInstruction(instruction, "eval_centroid", 2);
                break;
            case D3D10Opcode.DclStream:
                WriteLine("dcl_stream {0}", FormatOperand(instruction, 0));
                break;
            case D3D10Opcode.EmitStream:
                WriteInstruction(instruction, "emit_stream", 1);
                break;
            case D3D10Opcode.CutStream:
                WriteInstruction(instruction, "cut_stream", 1);
                break;
            case D3D10Opcode.EmitThenCutStream:
                WriteInstruction(instruction, "emit_then_cut_stream", 1);
                break;
            case D3D10Opcode.EmitThenCut:
                WriteLine("emit_then_cut");
                break;
            case D3D10Opcode.BufInfo:
                WriteInstruction(instruction, "bufinfo", 2);
                break;
            case D3D10Opcode.StoreUAVTyped:
                WriteInstruction(instruction, "store_uav_typed", 3);
                break;
            case D3D10Opcode.LdUAVTyped:
                WriteInstruction(instruction, "ld_uav_typed", 3);
                break;
            case D3D10Opcode.StoreRaw:
                WriteInstruction(instruction, "store_raw", 3);
                break;
            case D3D10Opcode.Sync:
                WriteLine("sync" + GetSyncSuffix(instruction.SyncFlags));
                break;
            // Double precision. fxc writes a pair's swizzle four wide (r0.xyxy);
            // this writer trims it like any other swizzle.
            case D3D10Opcode.DAdd:
                WriteInstruction(instruction, "dadd", 3);
                break;
            case D3D10Opcode.DMul:
                WriteInstruction(instruction, "dmul", 3);
                break;
            case D3D10Opcode.DDiv:
                WriteInstruction(instruction, "ddiv", 3);
                break;
            case D3D10Opcode.DMax:
                WriteInstruction(instruction, "dmax", 3);
                break;
            case D3D10Opcode.DMin:
                WriteInstruction(instruction, "dmin", 3);
                break;
            case D3D10Opcode.DEq:
                WriteInstruction(instruction, "deq", 3);
                break;
            case D3D10Opcode.DGe:
                WriteInstruction(instruction, "dge", 3);
                break;
            case D3D10Opcode.DLt:
                WriteInstruction(instruction, "dlt", 3);
                break;
            case D3D10Opcode.DNe:
                WriteInstruction(instruction, "dne", 3);
                break;
            case D3D10Opcode.DMov:
                WriteInstruction(instruction, "dmov", 2);
                break;
            case D3D10Opcode.DMovC:
                WriteInstruction(instruction, "dmovc", 4);
                break;
            case D3D10Opcode.DFMA:
                WriteInstruction(instruction, "dfma", 4);
                break;
            case D3D10Opcode.DRCP:
                WriteInstruction(instruction, "drcp", 2);
                break;
            case D3D10Opcode.DToF:
                WriteInstruction(instruction, "dtof", 2);
                break;
            case D3D10Opcode.FToD:
                WriteInstruction(instruction, "ftod", 2);
                break;
            case D3D10Opcode.DToI:
                WriteInstruction(instruction, "dtoi", 2);
                break;
            case D3D10Opcode.DToU:
                WriteInstruction(instruction, "dtou", 2);
                break;
            case D3D10Opcode.IToD:
                WriteInstruction(instruction, "itod", 2);
                break;
            case D3D10Opcode.UToD:
                WriteInstruction(instruction, "utod", 2);
                break;
            default:
                throw new NotImplementedException(instruction.Opcode.ToString());
        }
    }

    /// <summary>
    /// fxc's name for a global flag: the enum name with a lowered first letter,
    /// except the two 11.1 extensions.
    /// </summary>
    private static string GetGlobalFlagName(D3D10GlobalFlags flag)
    {
        if (flag == D3D10GlobalFlags.EnableDoubleExtensions)
        {
            return "enable11_1DoubleExtensions";
        }
        if (flag == D3D10GlobalFlags.EnableShaderExtensions)
        {
            return "enable11_1ShaderExtensions";
        }
        string name = flag.ToString();
        return char.ToLower(name[0]) + name.Substring(1);
    }

    // In the order fxc writes them: sync_uglobal_g_t.
    private static string GetSyncSuffix(D3D10SyncFlags flags)
    {
        string suffix = "";
        if (flags.HasFlag(D3D10SyncFlags.UavMemoryGlobal)) suffix += "_uglobal";
        if (flags.HasFlag(D3D10SyncFlags.UavMemoryGroup)) suffix += "_ugroup";
        if (flags.HasFlag(D3D10SyncFlags.ThreadGroupSharedMemory)) suffix += "_g";
        if (flags.HasFlag(D3D10SyncFlags.ThreadsInGroup)) suffix += "_t";
        return suffix;
    }

    /// <summary>
    /// fxc's name for a system value. Spelled out: SM4 names use underscores and
    /// later ones camel case, so no rule derives them.
    /// </summary>
    private static string GetSystemValueName(D3D10Instruction instruction)
    {
        var name = (D3D10Name)instruction.GetParamIndexImmediate32(1, 0);
        return name switch
        {
            D3D10Name.Position => "position",
            D3D10Name.ClipDistance => "clip_distance",
            D3D10Name.CullDistance => "cull_distance",
            D3D10Name.RenderTargetArrayIndex => "rendertarget_array_index",
            D3D10Name.ViewportArrayIndex => "viewport_array_index",
            D3D10Name.VertexID => "vertex_id",
            D3D10Name.PrimitiveID => "primitive_id",
            D3D10Name.InstanceID => "instance_id",
            D3D10Name.IsFrontFace => "is_front_face",
            D3D10Name.SampleIndex => "sampleIndex",
            D3D10Name.FinalQuadUEq0EdgeTessFactor => "finalQuadUeq0EdgeTessFactor",
            D3D10Name.FinalQuadVEq0EdgeTessFactor => "finalQuadVeq0EdgeTessFactor",
            D3D10Name.FinalQuadUEq1EdgeTessFactor => "finalQuadUeq1EdgeTessFactor",
            D3D10Name.FinalQuadVEq1EdgeTessFactor => "finalQuadVeq1EdgeTessFactor",
            D3D10Name.FinalQuadUInsideTessFactor => "finalQuadUInsideTessFactor",
            D3D10Name.FinalQuadVInsideTessFactor => "finalQuadVInsideTessFactor",
            D3D10Name.FinalTriUEq0EdgeTessFactor => "finalTriUeq0EdgeTessFactor",
            D3D10Name.FinalTriVEq0EdgeTessFactor => "finalTriVeq0EdgeTessFactor",
            D3D10Name.FinalTriWEq0EdgeTessFactor => "finalTriWeq0EdgeTessFactor",
            D3D10Name.FinalTriInsideTessFactor => "finalTriInsideTessFactor",
            D3D10Name.FinalLineDetailTessFactor => "finalLineDetailTessFactor",
            D3D10Name.FinalLineDensityTessFactor => "finalLineDensityTessFactor",
            _ => throw new NotImplementedException(name.ToString()),
        };
    }

    // fxc emits only _nz, but the bit is printed as found.
    private static string Conditional(D3D10Instruction instruction, string mnemonic)
    {
        return instruction.TestNonZero ? mnemonic + "_nz" : mnemonic + "_z";
    }

    private void WriteInstruction(D3D10Instruction instruction, string mnemonic, int operandCount)
    {
        // Suffixes first, then their arguments in the same order:
        // gather4_aoffimmi_indexable(1,-1,0)(texture2d)(float,float,float,float).
        string arguments = "";
        if (instruction.SampleOffsets != null)
        {
            mnemonic += "_aoffimmi";
            arguments += $"({string.Join(",", instruction.SampleOffsets.Select(o => o.ToString(CultureInfo.InvariantCulture)))})";
        }
        if (instruction.IndexableResourceDimension != null)
        {
            mnemonic += "_indexable";
            arguments += GetIndexableResourceArguments(instruction);
        }
        mnemonic += arguments;
        string line = instruction.Saturate ? mnemonic + "_sat" : mnemonic;
        if (instruction.PreciseMask != 0)
        {
            // Bare when it covers every written lane.
            line += instruction.PreciseMask == instruction.GetWriteMask(0)
                ? " [precise]"
                : $" [precise({string.Concat(Enumerable.Range(0, 4).Where(c => (instruction.PreciseMask & (1 << c)) != 0).Select(c => "xyzw"[c]))})]";
        }
        for (int i = 0; i < operandCount; i++)
        {
            line += " " + FormatOperand(instruction, i);
            if (i != operandCount - 1)
            {
                line += ",";
            }
        }
        WriteLine(line);
    }

    private static string GetModifier(D3D9Instruction instruction)
    {
        // A ps_1_x result scale precedes the saturate: add_x2_sat.
        string result = instruction.GetDestinationResultShift() switch
        {
            0 => "",
            int shift when shift > 0 => $"_x{1 << shift}",
            int shift => $"_d{1 << -shift}",
        };
        ResultModifier modifier = instruction.GetDestinationResultModifier();
        if ((modifier & ResultModifier.Saturate) != 0)
        {
            result += "_sat";
        }
        if ((modifier & ResultModifier.PartialPrecision) != 0)
        {
            result += "_pp";
        }
        if ((modifier & ResultModifier.Centroid) != 0)
        {
            result += "_centroid";
        }
        return result;
    }

    private static string ApplyModifier(D3D10OperandModifier modifier, string value)
    {
        return modifier switch
        {
            D3D10OperandModifier.None => value,
            D3D10OperandModifier.Neg => $"-{value}",
            D3D10OperandModifier.Abs => $"|{value}|",
            D3D10OperandModifier.Neg | D3D10OperandModifier.Abs => $"-|{value}|",
            _ => throw new NotSupportedException("Not supported operand modifier " + modifier),
        };
    }

    static string ApplyModifier(SourceModifier modifier, string value)
    {
        return modifier switch
        {
            SourceModifier.None => value,
            SourceModifier.Negate => $"-{value}",
            SourceModifier.Bias => $"{value}_bias",
            SourceModifier.BiasAndNegate => $"-{value}_bias",
            SourceModifier.Sign => $"{value}_bx2",
            SourceModifier.SignAndNegate => $"-{value}_bx2",
            SourceModifier.Complement => $"1-{value}",
            SourceModifier.X2 => $"{value}_x2",
            SourceModifier.X2AndNegate => $"-{value}_x2",
            SourceModifier.DivideByZ => $"{value}_dz",
            SourceModifier.DivideByW => $"{value}_dw",
            SourceModifier.Abs => $"{value}_abs",
            SourceModifier.AbsAndNegate => $"-{value}_abs",
            SourceModifier.Not => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };
    }

    private string GetParamRegisterName(D3D9Instruction instruction, int index)
    {
        var registerType = instruction.GetParamRegisterType(index);
        int registerNumber = instruction.GetParamRegisterNumber(index);

        string registerTypeName;
        switch (registerType)
        {
            case RegisterType.Addr:
                // Addr and Texture share a number: a# in vertex, t# in pixel shaders.
                registerTypeName = shader.Type == ShaderType.Pixel ? "t" : "a";
                break;
            case RegisterType.AttrOut:
                registerTypeName = "oD";
                break;
            case RegisterType.Const:
                registerTypeName = "c";
                break;
            case RegisterType.Const2:
                registerTypeName = "c";
                registerNumber += 2048;
                break;
            case RegisterType.Const3:
                registerTypeName = "c";
                registerNumber += 4096;
                break;
            case RegisterType.Const4:
                registerTypeName = "c";
                registerNumber += 6144;
                break;
            case RegisterType.ConstBool:
                registerTypeName = "b";
                break;
            case RegisterType.ConstInt:
                registerTypeName = "i";
                break;
            case RegisterType.Input:
                registerTypeName = "v";
                break;
            case RegisterType.Label:
                registerTypeName = "l";
                break;
            case RegisterType.Predicate:
                registerTypeName = "p";
                break;
            case RegisterType.Output:
                // oT# up to vs_2_0; vs_3_0 declares o# registers.
                registerTypeName = shader.MajorVersion <= 2 ? "oT" : "o";
                break;
            case RegisterType.RastOut:
                if (registerNumber == 0)
                {
                    return "oPos";
                }
                else if (registerNumber == 1)
                {
                    return "oFog";
                }
                else if (registerNumber == 2)
                {
                    return "oPts";
                }
                throw new NotImplementedException();
            case RegisterType.Temp:
                registerTypeName = "r";
                break;
            case RegisterType.Sampler:
                registerTypeName = "s";
                break;
            case RegisterType.ColorOut:
                registerTypeName = "oC";
                break;
            case RegisterType.DepthOut:
                return "oDepth";
            case RegisterType.MiscType:
                if (registerNumber == 0)
                {
                    return "vPos";
                }
                else if (registerNumber == 1)
                {
                    return "vFace";
                }
                else
                {
                    throw new NotImplementedException();
                }
            case RegisterType.Loop:
                return "aL";
            default:
                throw new NotImplementedException();
        }

        if (instruction.Params.HasRelativeAddressing(index))
        {
            RegisterType relativeType = instruction.GetRelativeParamRegisterType(index);
            switch (relativeType)
            {
                // The base register is always named, even c0: `c0[a0.x]`.
                case RegisterType.Loop:
                    return $"{registerTypeName}{registerNumber}[aL]";
                case RegisterType.Addr:
                    char component = "xyzw"[instruction.GetRelativeParamComponent(index)];
                    return $"{registerTypeName}{registerNumber}[a0.{component}]";
                default:
                    throw new NotSupportedException(relativeType.ToString());
            }
        }

        return registerTypeName + registerNumber;
    }

    // cb0[2], cb0[r0.x], cb0[r0.x + 2].
    private static string FormatOperandIndex(
        D3D10Instruction instruction, int operandIndex, int index,
        D3D10OperandTokenCollection.OperandIndex operand)
    {
        if (!operand.IsRelative)
        {
            return operand.Immediate.ToString();
        }

        (OperandType type, int number, byte component) =
            instruction.OperandTokens.GetRelativeIndexOperand(operandIndex, index);
        string register = type switch
        {
            OperandType.Temp => "r" + number,
            OperandType.Input => "v" + number,
            _ => throw new NotImplementedException(type.ToString()),
        };
        register += "." + "xyzw"[component];
        return operand.Representation == D3D10OperandIndexRepresentation.Relative
            ? register
            : $"{register} + {operand.Immediate}";
    }

    // fxc's spelling of a double immediate: six decimals and an `l` suffix.
    private static string FormatDoubleOperand(D3D10Instruction instruction, int index, int number)
    {
        double value = instruction.GetParamDouble(index, number);
        return double.IsNaN(value) || double.IsInfinity(value)
            ? ConstantFormatter.Format(value) + "l"
            : value.ToString("F6", CultureInfo.InvariantCulture) + "l";
    }

    private string FormatOperand(D3D10Instruction instruction, int index)
    {
        var operandType = instruction.GetOperandType(index);
        string registerNumber;
        if (operandType == OperandType.ConstantBuffer)
        {
            D3D10OperandTokenCollection.OperandIndex[] indices =
                instruction.OperandTokens.GetOperandIndices(index);
            // A class's data is in the buffer `this` names: cb[r1.y + 0][r1.x + 0].
            string buffer = indices[0].IsRelative
                ? "[" + FormatOperandIndex(instruction, index, 0, indices[0]) + "]"
                : indices[0].Immediate.ToString();
            registerNumber = buffer + "[" + FormatOperandIndex(instruction, index, 1, indices[1]) + "]";
        }
        else if (operandType == OperandType.ThisPointer)
        {
            registerNumber = $"[{instruction.GetParamRegisterNumber(index)}]";
        }
        else if (D3D10Instruction.IsThreadRegister(operandType))
        {
            registerNumber = "";
        }
        else if (operandType == OperandType.Immediate32)
        {
            bool isInteger = _integerOperandAnalysis.IsIntegerOperand(instruction)
                || IsIntegerIndexOperand(instruction, index);
            // A mov or movc immediate is typed by the readers of its destination.
            if (instruction.Opcode is D3D10Opcode.Mov or D3D10Opcode.MovC)
            {
                ValueKind readAs = _integerOperandAnalysis.GetImmediateKindByReaders(instruction);
                if (readAs != ValueKind.Unknown)
                {
                    isInteger = readAs == ValueKind.Integer;
                }
            }
            // A store into an integer groupshared array stores an integer.
            if (instruction.Opcode == D3D10Opcode.StoreStructured && index == 3
                && instruction.GetOperandType(0) == OperandType.ThreadGroupSharedMemory
                && _integerOperandAnalysis.IsIntegerThreadGroupSharedMemory(
                    instruction.GetParamRegisterNumber(0)))
            {
                isInteger = true;
            }
            // A bitwise operator over a float's bits prints hex: l(0x3f800000).
            bool isBits = !isInteger && instruction.Opcode is D3D10Opcode.And or D3D10Opcode.Or
                or D3D10Opcode.Xor or D3D10Opcode.Not;
            var componentSelection = instruction.GetOperandComponentSelection(index);
            if (componentSelection == D3D10OperandNumComponents.Operand1Component)
            {
                if (isBits)
                {
                    return $"l(0x{instruction.GetParamInt(index):x8})";
                }
                if (isInteger)
                {
                    return $"l({instruction.GetParamInt(index).ToString(CultureInfo.InvariantCulture)})";
                }
                else
                {
                    return $"l({ConstantFormatter.Format(instruction.GetParamSingle(index)[0])})";
                }
            }
            else
            {
                if (isBits)
                {
                    return "l(" + string.Join(", ", Enumerable.Range(0, 4)
                        .Select(c => $"0x{instruction.GetParamInt(index, c):x8}")) + ")";
                }
                if (isInteger)
                {
                    string immediate0 = instruction.GetParamInt(index, 0).ToString(CultureInfo.InvariantCulture);
                    string immediate1 = instruction.GetParamInt(index, 1).ToString(CultureInfo.InvariantCulture);
                    string immediate2 = instruction.GetParamInt(index, 2).ToString(CultureInfo.InvariantCulture);
                    string immediate3 = instruction.GetParamInt(index, 3).ToString(CultureInfo.InvariantCulture);
                    return $"l({immediate0}, {immediate1}, {immediate2}, {immediate3})";
                }
                else
                {
                    string immediate0 = ConstantFormatter.Format(instruction.GetParamSingle(index, 0));
                    string immediate1 = ConstantFormatter.Format(instruction.GetParamSingle(index, 1));
                    string immediate2 = ConstantFormatter.Format(instruction.GetParamSingle(index, 2));
                    string immediate3 = ConstantFormatter.Format(instruction.GetParamSingle(index, 3));
                    return $"l({immediate0}, {immediate1}, {immediate2}, {immediate3})";
                }
            }
        }
        else if (operandType == OperandType.Immediate64)
        {
            return $"d({FormatDoubleOperand(instruction, index, 0)}, {FormatDoubleOperand(instruction, index, 1)})";
        }
        else if (operandType == OperandType.IndexableTemp)
        {
            // x0[3], x0[r0.x + 1].
            D3D10OperandTokenCollection.OperandIndex[] indices =
                instruction.OperandTokens.GetOperandIndices(index);
            registerNumber = indices[0].Immediate + "[" + FormatOperandIndex(instruction, index, 1, indices[1]) + "]";
        }
        else
        {
            D3D10OperandTokenCollection.OperandIndex[] indices =
                instruction.OperandTokens.GetOperandIndices(index);
            if (indices.Length == 1 && !indices[0].IsRelative)
            {
                registerNumber = instruction.GetParamRegisterNumber(index).ToString();
            }
            else
            {
                registerNumber = "";
                for (int i = 0; i < indices.Length; i++)
                {
                    registerNumber += "[" + FormatOperandIndex(instruction, index, i, indices[i]) + "]";
                }
            }
        }

        string registerTypeName = operandType switch
        {
            OperandType.Input => "v",
            OperandType.Output => "o",
            OperandType.Temp => "r",
            OperandType.IndexableTemp => "x",
            OperandType.ConstantBuffer => "cb",
            OperandType.ImmediateConstantBuffer => "icb",
            OperandType.Resource => "t",
            OperandType.Sampler => "s",
            OperandType.InputThreadID => "vThreadID",
            OperandType.InputThreadGroupID => "vThreadGroupID",
            OperandType.InputThreadIDInGroup => "vThreadIDInGroup",
            OperandType.InputThreadIDInGroupFlattened => "vThreadIDInGroupFlattened",
            OperandType.InputPrimitiveID => "vPrim",
            OperandType.InputGSInstanceID => "vGSInstanceID",
            OperandType.InputDomainPoint => "vDomain",
            OperandType.InputControlPoint => "vicp",
            OperandType.OutputControlPoint => "vocp",
            OperandType.InputPatchConstant => "vpc",
            OperandType.OutputControlPointID => "vOutputControlPointID",
            OperandType.InputForkInstanceID => "vForkInstanceID",
            OperandType.InputJoinInstanceID => "vJoinInstanceID",
            OperandType.UnorderedAccessView => "u",
            OperandType.ThreadGroupSharedMemory => "g",
            // fxc writes `call label2`; the D3D9 label register is l#, but not this one.
            OperandType.Label => "label",
            OperandType.FunctionBody => "fb",
            OperandType.FunctionTable => "ft",
            OperandType.Interface => "fp",
            OperandType.ThisPointer => "this",
            OperandType.Stream => "m",
            // These carry no register number of their own.
            OperandType.OutputDepth => "oDepth",
            OperandType.OutputDepthGreaterEqual => "oDepthGE",
            OperandType.OutputDepthLessEqual => "oDepthLE",
            OperandType.OutputCoverageMask => "oMask",
            OperandType.InputCoverageMask => "vCoverage",
            OperandType.OutputStencilRef => "oStencilRef",
            OperandType.InputInnerCoverage => "vInnerCoverage",
            OperandType.Null => "null",
            // The render target, as sampleinfo and samplepos read it.
            OperandType.Rasterizer => "rasterizer",
            _ => throw new NotImplementedException(operandType.ToString()),
        };

        string swizzle = instruction.GetOperandComponentSelection(index) == D3D10OperandNumComponents.Operand0Component
            ? ""
            : index == instruction.GetDestinationParamIndex()
                ? instruction.GetDestinationWriteMaskName(GetDestinationSemanticSize(instruction))
                // The second of two destinations carries its own write mask, which
                // a swapc may make differ from the first's.
                : instruction.IsDestinationOperand(index)
                    ? instruction.GetWriteMaskName(index, 4)
                    : instruction.GetSourceSwizzleName(index);

        var modifier = instruction.GetOperandModifier(index);
        return ApplyModifier(modifier, $"{registerTypeName}{registerNumber}{swizzle}");
    }

    private static string GetResourceDimensionName(ResourceDimension dimension)
    {
        return dimension switch
        {
            ResourceDimension.Buffer => "buffer",
            ResourceDimension.Texture1D => "texture1d",
            ResourceDimension.Texture2D => "texture2d",
            ResourceDimension.Texture2Dms => "texture2dms",
            ResourceDimension.Texture3D => "texture3d",
            ResourceDimension.TextureCube => "texturecube",
            ResourceDimension.Texture1DArray => "texture1darray",
            ResourceDimension.Texture2DArray => "texture2darray",
            ResourceDimension.Texture2DmsArray => "texture2dmsarray",
            ResourceDimension.TextureCubeArray => "texturecubearray",
            _ => throw new NotImplementedException(dimension.ToString()),
        };
    }

    /// <summary>
    /// Whether an operand is an integer address or sample index, whatever the
    /// instruction's own type (samplepos returns a float2).
    /// </summary>
    private static bool IsIntegerIndexOperand(D3D10Instruction instruction, int index)
    {
        return instruction.Opcode switch
        {
            D3D10Opcode.LdStructured or D3D10Opcode.StoreStructured => index is 1 or 2,
            D3D10Opcode.LdRaw or D3D10Opcode.StoreRaw => index == 1,
            D3D10Opcode.EvalSampleIndex or D3D10Opcode.EvalSnapped => index == 2,
            D3D10Opcode.SamplePos => index == 2,
            _ => false,
        };
    }

    // (structured_buffer, stride=16)(mixed,mixed,mixed,mixed)
    private static string GetIndexableResourceArguments(D3D10Instruction instruction)
    {
        ResourceDimension dimension = instruction.IndexableResourceDimension.Value;
        string name = dimension switch
        {
            ResourceDimension.StructuredBuffer =>
                $"structured_buffer, stride={instruction.IndexableResourceStride}",
            ResourceDimension.RawBuffer => "raw_buffer",
            _ => GetResourceDimensionName(dimension),
        };
        string returnTypes = instruction.IndexableResourceReturnTypeToken is int token
            ? GetResourceReturnTypes(token)
            : "";
        return $"({name})({returnTypes})";
    }

    private static string GetResourceReturnTypes(D3D10Instruction instruction)
    {
        return GetResourceReturnTypes(instruction.GetResourceReturnTypeToken());
    }

    // Four nibbles, one D3D_RESOURCE_RETURN_TYPE per component.
    private static string GetResourceReturnTypes(int token)
    {
        return string.Join(",", Enumerable.Range(0, 4).Select(i =>
            (D3DResourceReturnType)((token >> (4 * i)) & 0xF) switch
            {
                D3DResourceReturnType.UNorm => "unorm",
                D3DResourceReturnType.SNorm => "snorm",
                D3DResourceReturnType.SInt => "sint",
                D3DResourceReturnType.UInt => "uint",
                D3DResourceReturnType.Float => "float",
                D3DResourceReturnType.Mixed => "mixed",
                D3DResourceReturnType.Double => "double",
                _ => "float",
            }));
    }
}
