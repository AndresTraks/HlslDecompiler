using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HlslDecompiler.DirectXShaderModel;

public class AsmWriter
{
    private ShaderModel shader;
    private StreamWriter asmWriter;
    private IDictionary<RegisterKey, int> _samplerDimensions = new Dictionary<RegisterKey, int>();

    public AsmWriter(ShaderModel shader)
    {
        this.shader = shader;
    }

    void WriteLine(string value)
    {
        asmWriter.WriteLine(value);
    }

    void WriteLine(string format, params object[] args)
    {
        asmWriter.WriteLine(format, args);
    }

    private string GetDestinationName(D3D9Instruction instruction)
    {
        int destIndex = instruction.GetDestinationParamIndex();
        string registerName = GetParamRegisterName(instruction, destIndex);
        int destinationLength = GetDestinationSemanticSize(instruction);
        string writeMaskName = instruction.GetDestinationWriteMaskName(destinationLength);
        return $"{registerName}{writeMaskName}";
    }

    private static int GetDestinationSemanticSize(D3D9Instruction instruction)
    {
        RegisterType registerType = instruction.GetParamRegisterType(instruction.GetDestinationParamIndex());
        if (registerType == RegisterType.DepthOut)
        {
            return 1;
        }
        return 4;
    }

    private static int GetDestinationSemanticSize(D3D10Instruction instruction)
    {
        if (instruction.GetOperandType(instruction.GetDestinationParamIndex()) == OperandType.OutputDepth)
        {
            return 1;
        }
        return 4;
    }

    private string GetSourceName(D3D9Instruction instruction, int srcIndex, int? destinationLength = null)
    {
        string sourceName = GetParamRegisterName(instruction, srcIndex);
        sourceName += instruction.GetSourceSwizzleName(srcIndex, destinationLength);
        sourceName = ApplyModifier(instruction.GetSourceModifier(srcIndex), sourceName);
        return sourceName;
    }

    public void Write(string asmFilename)
    {
        var asmFile = new FileStream(asmFilename, FileMode.Create, FileAccess.Write);
        asmWriter = new StreamWriter(asmFile);
        string shaderType = shader.Type switch
        {
            ShaderType.Vertex => "vs",
            ShaderType.Pixel => "ps",
            ShaderType.Geometry => "gs",
            ShaderType.Compute => "cs",
            _ => throw new NotImplementedException(shader.Type.ToString()),
        };
        WriteLine($"{shaderType}_{shader.MajorVersion}_{shader.MinorVersion}");

        foreach (Instruction instruction in shader.Instructions)
        {
            if (instruction is D3D10Instruction d3D10Instruction)
            {
                WriteD3D10Instruction(d3D10Instruction);
            }
            else
            {
                WriteInstruction(instruction as D3D9Instruction);
            }
        }

        asmWriter.Dispose();
        asmFile.Dispose();
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
            case Opcode.Cmp:
                WriteLine("cmp{0} {1}, {2}, {3}, {4}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                break;
            case Opcode.Dcl:
                string dclInstruction = "dcl";
                if (instruction.GetParamRegisterType(1) != RegisterType.MiscType)
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
            case Opcode.Log:
                WriteLine("log{0} {1}, {2}", GetModifier(instruction), GetDestinationName(instruction),
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
            case Opcode.Rsq:
                WriteLine("rsq{0} {1}, {2}", GetModifier(instruction),GetDestinationName(instruction),
                    GetSourceName(instruction, 1));
                break;
            case Opcode.Sge:
                WriteLine("sge{0} {1}, {2}, {3}", GetModifier(instruction), GetDestinationName(instruction),
                    GetSourceName(instruction, 1), GetSourceName(instruction, 2));
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
            case Opcode.Tex:
                if ((shader.MajorVersion == 1 && shader.MinorVersion >= 4) || (shader.MajorVersion > 1))
                {
                    if (instruction.TexldControls.HasFlag(TexldControls.Project))
                    {
                        WriteLine("texldp {0}, {1}, {2}", GetDestinationName(instruction),
                            GetSourceName(instruction, 1, 4), GetSourceName(instruction, 2));
                    }
                    else if (instruction.TexldControls.HasFlag(TexldControls.Bias))
                    {
                        WriteLine("texldb {0}, {1}, {2}", GetDestinationName(instruction),
                            GetSourceName(instruction, 1, 4), GetSourceName(instruction, 2));
                    }
                    else
                    {
                        int texldSamplerDimension = _samplerDimensions[instruction.GetParamRegisterKey(2)];
                        WriteLine("texld {0}, {1}, {2}", GetDestinationName(instruction),
                            GetSourceName(instruction, 1, texldSamplerDimension), GetSourceName(instruction, 2));
                    }
                }
                else
                {
                    WriteLine("tex {0}", GetDestinationName(instruction));
                }
                break;
            case Opcode.TexLDL:
                WriteLine("texldl {0}, {1}, {2}", GetDestinationName(instruction),
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
            case Opcode.TexKill:
                WriteLine("texkill {0}", GetDestinationName(instruction));
                break;
            case Opcode.Comment:
            case Opcode.End:
                break;
            default:
                WriteLine(instruction.Opcode.ToString());
                Console.WriteLine(instruction.Opcode);
                //throw new NotImplementedException();
                break;
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
                WriteInstruction(instruction, "breakc_nz", 1);
                break;
            case D3D10Opcode.Cut:
                WriteInstruction(instruction, "cut", 0);
                break;
            case D3D10Opcode.DclConstantBuffer:
                WriteLine("dcl_constantbuffer {0}, {1}", FormatOperand(instruction, 0), "immediateIndexed"); // TODO: AccessPattern
                break;
            case D3D10Opcode.DclGlobalFlags:
                string globalFlags = "";
                foreach (D3D10GlobalFlags flag in Enum.GetValues(typeof(D3D10GlobalFlags)))
                {
                    if (flag != D3D10GlobalFlags.None && instruction.GetGlobalFlags().HasFlag(flag))
                    {
                        string flagString = flag.ToString();
                        globalFlags += " " + char.ToLower(flagString[0]) + flagString.Substring(1);
                    }
                }
                WriteLine("dcl_globalFlags{0}", globalFlags);
                break;
            case D3D10Opcode.DclInputPS:
                WriteLine("dcl_input_ps {0} {1}", instruction.GetInterpolationModeName(), FormatOperand(instruction, 0));
                break;
            case D3D10Opcode.DclInputPSSiv:
                WriteLine("dcl_input_sv {0} {1}", instruction.GetInterpolationModeName(), FormatOperand(instruction, 0));
                break;
            case D3D10Opcode.DclInput:
                WriteLine("dcl_input {0}", FormatOperand(instruction, 0));
                break;
            case D3D10Opcode.DclGSInputPrimitive:
                WriteLine("dcl_inputprimitive {0}", instruction.GetPrimitive().ToHlslString());
                break;
            case D3D10Opcode.DclInputSiv:
                {
                    string name = ((D3D10Name)instruction.GetParamIndexImmediate32(1, 0)).ToString();
                    name = name[0].ToString().ToLower() + name.Substring(1).ToString();
                    WriteLine("dcl_input_siv {0}, {1}", FormatOperand(instruction, 0), name);
                    break;
                }
            case D3D10Opcode.DclGSMaxOutputVertexCount:
                WriteLine("dcl_maxout {0}", instruction.GetParamInt(0));
                break;
            case D3D10Opcode.DclGSOutputPrimitiveTopology:
                WriteLine("dcl_outputtopology {0}", instruction.GetPrimitiveTopology().ToString().ToLower());
                break;
            case D3D10Opcode.DclOutput:
                WriteInstruction(instruction, "dcl_output", 1);
                break;
            case D3D10Opcode.DclOutputSiv:
                {
                    string name = ((D3D10Name)instruction.GetParamIndexImmediate32(1, 0)) switch
                    {
                        D3D10Name.Position => "position",
                        D3D10Name.ClipDistance => "clip_distance",
                        D3D10Name.CullDistance => "cull_distance",
                        D3D10Name.RenderTargetArrayIndex => "render_target_array_index",
                        D3D10Name.ViewportArrayIndex => "viewport_array_index",
                        D3D10Name.VertexID => "vertex_id",
                        D3D10Name.PrimitiveID => "primitive_id",
                        D3D10Name.InstanceID => "instance_id",
                        D3D10Name.IsFrontFace => "is_front_face",
                        D3D10Name.SampleIndex => "sample_index",
                        _ => throw new NotImplementedException(((D3D10Name)instruction.GetParamIndexImmediate32(1, 0)).ToString()),
                    };
                    WriteLine("dcl_output_siv {0}, {1}", FormatOperand(instruction, 0), name);
                    break;
                }
            case D3D10Opcode.DclResource:
                WriteInstruction(instruction, "dcl_resource_texture2d (float,float,float,float)", 1);
                break;
            case D3D10Opcode.DclResourceStructured:
                WriteLine("dcl_resource_structured {0}, {1}", FormatOperand(instruction, 0), instruction.GetParamIndexImmediate32(1, 0));
                break;
            case D3D10Opcode.DclSampler:
                WriteLine("dcl_sampler {0}, mode_default", FormatOperand(instruction, 0)); // TODO: mode
                break;
            case D3D10Opcode.DclTemps:
                WriteLine("dcl_temps {0}", instruction.GetParamInt(0));
                break;
            case D3D10Opcode.DclThreadGroup:
                WriteLine("dcl_thread_group {0}, {1}, {2}", instruction.GetParamIndexImmediate32(0, 0), instruction.GetParamIndexImmediate32(0, 1), instruction.GetParamIndexImmediate32(0, 2));
                break;
            case D3D10Opcode.DclUnorderedAccessViewStructured:
                WriteLine("dcl_uav_structured {0}, {1}", FormatOperand(instruction, 0), instruction.GetParamIndexImmediate32(1, 0));
                break;
            case D3D10Opcode.DerivRtx:
                WriteInstruction(instruction, "deriv_rtx", 2);
                break;
            case D3D10Opcode.DerivRty:
                WriteInstruction(instruction, "deriv_rty", 2);
                break;
            case D3D10Opcode.Discard:
                WriteInstruction(instruction, "discard_nz", 1);
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
            case D3D10Opcode.GE:
                WriteInstruction(instruction, "ge", 3);
                break;
            case D3D10Opcode.IAdd:
                WriteInstruction(instruction, "iadd", 3);
                break;
            case D3D10Opcode.Ilt:
                WriteInstruction(instruction, "ilt", 2);
                break;
            case D3D10Opcode.IToF:
                WriteInstruction(instruction, "itof", 2);
                break;
            case D3D10Opcode.LdStructured:
                WriteInstruction(instruction, "ld_structured", 4);
                break;
            case D3D10Opcode.Loop:
                WriteInstruction(instruction, "loop", 0);
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
            case D3D10Opcode.Sample:
                WriteInstruction(instruction, "sample", 4);
                break;
            case D3D10Opcode.SinCos:
                WriteInstruction(instruction, "sincos", 3);
                break;
            case D3D10Opcode.Sqrt:
                WriteInstruction(instruction, "sqrt", 2);
                break;
            case D3D10Opcode.StoreStructured:
                WriteInstruction(instruction, "store_structured", 4);
                break;
            default:
                WriteLine(instruction.Opcode.ToString());
                Console.WriteLine(instruction.Opcode);
                //throw new NotImplementedException();
                break;
        }
    }

    private void WriteInstruction(D3D10Instruction instruction, string mnemonic, int operandCount)
    {
        string line = mnemonic;
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
        string result = "";
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
            SourceModifier.Complement => throw new NotImplementedException(),
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
                registerTypeName = "a";
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
            case RegisterType.Output:
                if (shader.MajorVersion == 1)
                {
                    registerTypeName = "oT";
                }
                else
                {
                    registerTypeName = "o";
                }
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
                case RegisterType.Loop:
                    if (registerNumber != 0)
                    {
                        return $"{registerTypeName}[{registerNumber} + aL]";
                    }
                    return $"{registerTypeName}[aL]";
                case RegisterType.Addr:
                    if (registerNumber != 0)
                    {
                        return $"{registerTypeName}[{registerNumber} + a0.x]";
                    }
                    return $"{registerTypeName}[a0.x]";
                default:
                    throw new NotSupportedException(relativeType.ToString());
            }
        }

        return registerTypeName + registerNumber;
    }

    private static string FormatOperand(D3D10Instruction instruction, int index)
    {
        var operandType = instruction.GetOperandType(index);
        string registerNumber;
        if (operandType == OperandType.ConstantBuffer)
        {
            registerNumber = instruction.GetParamRegisterNumber(index) + "[" + instruction.GetParamConstantBufferOffset(index) + "]";
        }
        else if (operandType == OperandType.InputThreadID)
        {
            registerNumber = "";
        }
        else if (operandType == OperandType.Immediate32)
        {
            var componentSelection = instruction.GetOperandComponentSelection(index);
            if (componentSelection == D3D10OperandNumComponents.Operand1Component)
            {
                string immediate;
                if (instruction.Opcode == D3D10Opcode.Discard)
                {
                    immediate = instruction.GetParamInt(index).ToString();
                }
                else
                {
                    immediate = ConstantFormatter.Format(instruction.GetParamSingle(index)[0]);
                }
                return $"l({immediate})";
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
        else
        {
            D3D10OperandIndexRepresentation[] indexRepresentation = instruction.GetOperandIndexRepresentation(index);
            if (indexRepresentation.Length != 0 && !indexRepresentation.Any(i => i == D3D10OperandIndexRepresentation.Immediate32))
            {
                throw new NotImplementedException();
            }
            if (indexRepresentation.Length == 1)
            {
                registerNumber = instruction.GetParamRegisterNumber(index).ToString();
            }
            else
            {
                registerNumber = "";
                for (int i = 0; i < indexRepresentation.Length; i++)
                {
                    D3D10OperandIndexRepresentation representation = indexRepresentation[i];
                    if (representation == D3D10OperandIndexRepresentation.Immediate32)
                    {
                        registerNumber += "[" + instruction.GetParamIndexImmediate32(index, i + 1) + "]";
                    }
                    else
                    {
                        throw new NotImplementedException(representation.ToString());
                    }
                }
            }
        }

        string registerTypeName = operandType switch
        {
            OperandType.Input => "v",
            OperandType.Output => "o",
            OperandType.Temp => "r",
            OperandType.ConstantBuffer => "cb",
            OperandType.Resource => "t",
            OperandType.Sampler => "s",
            OperandType.InputThreadID => "vThreadID",
            OperandType.UnorderedAccessView => "u",
            _ => throw new NotImplementedException(),
        };

        string swizzle = index == instruction.GetDestinationParamIndex()
            ? instruction.GetDestinationWriteMaskName(GetDestinationSemanticSize(instruction))
            : instruction.GetSourceSwizzleName(index);

        var modifier = instruction.GetOperandModifier(index);
        return ApplyModifier(modifier, $"{registerTypeName}{registerNumber}{swizzle}");
    }
}
