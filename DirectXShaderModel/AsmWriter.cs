using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.IO;

namespace HlslDecompiler.DirectXShaderModel
{
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

        static string GetDestinationName(Instruction instruction)
        {
            int destIndex = instruction.GetDestinationParamIndex();
            string registerName = instruction.GetParamRegisterName(destIndex);
            int destinationLength = instruction.GetDestinationSemanticSize();
            string writeMaskName = instruction.GetDestinationWriteMaskName(destinationLength);

            return $"{registerName}{writeMaskName}";
        }

        private static string GetSourceName(D3D9Instruction instruction, int srcIndex, int? destinationLength = null)
        {
            string sourceName = instruction.GetParamRegisterName(srcIndex);
            sourceName += instruction.GetSourceSwizzleName(srcIndex, destinationLength);
            sourceName = ApplyModifier(instruction.GetSourceModifier(srcIndex), sourceName);
            return sourceName;
        }

        private static string GetSourceName(D3D10Instruction instruction, int srcIndex)
        {
            var operandType = instruction.GetOperandType(srcIndex);
            if (operandType == OperandType.Immediate32)
            {
                var componentSelection = instruction.GetOperandComponentSelection(srcIndex);
                if (componentSelection == D3D10OperandNumComponents.Operand1Component)
                {
                    string immediate;
                    if (instruction.Opcode == D3D10Opcode.Discard)
                    {
                        immediate = instruction.GetParamInt(srcIndex).ToString();
                    }
                    else
                    {
                        immediate = ConstantFormatter.Format(instruction.GetParamSingle(srcIndex));
                    }
                    return $"l({immediate})";
                }
                else
                {
                    string immediate0 = ConstantFormatter.Format(instruction.GetParamSingle(srcIndex, 0));
                    string immediate1 = ConstantFormatter.Format(instruction.GetParamSingle(srcIndex, 1));
                    string immediate2 = ConstantFormatter.Format(instruction.GetParamSingle(srcIndex, 2));
                    string immediate3 = ConstantFormatter.Format(instruction.GetParamSingle(srcIndex, 3));
                    return $"l({immediate0}, {immediate1}, {immediate2}, {immediate3})";
                }
            }

            string sourceName = instruction.GetParamRegisterName(srcIndex);
            sourceName += instruction.GetSourceSwizzleName(srcIndex);
            sourceName = ApplyModifier(instruction.GetOperandModifier(srcIndex), sourceName);
            return sourceName;
        }

        public void Write(string asmFilename)
        {
            var asmFile = new FileStream(asmFilename, FileMode.Create, FileAccess.Write);
            asmWriter = new StreamWriter(asmFile);

            string shaderType = (shader.Type == ShaderType.Vertex) ? "vs" : "ps";
            WriteLine("{0}_{1}_{2}", shaderType, shader.MajorVersion, shader.MinorVersion);

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
                    WriteLine("{0} {1}", dclInstruction, GetDestinationName(instruction));
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
                        string constRegisterName = instruction.GetParamRegisterName(0);
                        string constValue0 = ConstantFormatter.Format(instruction.GetParamSingle(1));
                        string constValue1 = ConstantFormatter.Format(instruction.GetParamSingle(2));
                        string constValue2 = ConstantFormatter.Format(instruction.GetParamSingle(3));
                        string constValue3 = ConstantFormatter.Format(instruction.GetParamSingle(4));
                        WriteLine("def {0}, {1}, {2}, {3}, {4}", constRegisterName, constValue0, constValue1, constValue2, constValue3);
                    }
                    break;
                case Opcode.DefI:
                    {
                        string constRegisterName = instruction.GetParamRegisterName(0);
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
                    WriteLine("frac {0}, {1}", GetDestinationName(instruction), GetSourceName(instruction, 1));
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
                    WriteLine("add {0}, {1}, {2}", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case D3D10Opcode.DclConstantBuffer:
                    WriteLine("dcl_constantbuffer {0}, {1}", GetDestinationName(instruction), "immediateIndexed"); // TODO: AccessPattern
                    break;
                case D3D10Opcode.DclInputPS:
                    WriteLine("dcl_input_ps {0} {1}", instruction.GetInterpolationModeName(), GetDestinationName(instruction));
                    break;
                case D3D10Opcode.DclInputPSSiv:
                    WriteLine("dcl_input_sv {0} {1}", instruction.GetInterpolationModeName(), GetDestinationName(instruction));
                    break;
                case D3D10Opcode.DclInput:
                    WriteLine("dcl_input {0}", GetDestinationName(instruction));
                    break;
                case D3D10Opcode.DclOutput:
                    WriteLine("dcl_output {0}", GetDestinationName(instruction));
                    break;
                case D3D10Opcode.DclTemps:
                    WriteLine("dcl_temps {0}", instruction.GetParamInt(0));
                    break;
                case D3D10Opcode.DerivRtx:
                    WriteLine("deriv_rtx {0}, {1}", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case D3D10Opcode.DerivRty:
                    WriteLine("deriv_rty {0}, {1}", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case D3D10Opcode.Discard:
                    WriteLine("discard_nz {0}", GetSourceName(instruction, 0));
                    break;
                case D3D10Opcode.Dp2:
                    WriteLine("dp2 {0}, {1}, {2}", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case D3D10Opcode.Dp3:
                    WriteLine("dp3 {0}, {1}, {2}", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case D3D10Opcode.Dp4:
                    WriteLine("dp4 {0}, {1}, {2}", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case D3D10Opcode.Mad:
                    WriteLine("mad {0}, {1}, {2}, {3}", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                    break;
                case D3D10Opcode.Mov:
                    WriteLine("mov {0}, {1}", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case D3D10Opcode.Mul:
                    WriteLine("mul {0}, {1}, {2}", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case D3D10Opcode.Ret:
                    WriteLine("ret");
                    break;
                case D3D10Opcode.Rsq:
                    WriteLine("rsq {0}, {1}", GetDestinationName(instruction),
                        GetSourceName(instruction, 1));
                    break;
                case D3D10Opcode.Sqrt:
                    WriteLine("sqrt {0}, {1}", GetDestinationName(instruction),
                        GetSourceName(instruction, 1));
                    break;
                default:
                    WriteLine(instruction.Opcode.ToString());
                    Console.WriteLine(instruction.Opcode);
                    //throw new NotImplementedException();
                    break;
            }
        }

        private static string GetModifier(D3D9Instruction instruction)
        {
            string result = "";
            var modifier = instruction.GetDestinationResultModifier();
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
            switch (modifier)
            {
                case D3D10OperandModifier.None:
                    return value;
                case D3D10OperandModifier.Neg:
                    return $"-{value}";
                case D3D10OperandModifier.Abs:
                    return $"|{value}|";
                case D3D10OperandModifier.Neg | D3D10OperandModifier.Abs:
                    return $"-|{value}|";
                default:
                    throw new NotSupportedException("Not supported operand modifier " + modifier);
            }
        }

        static string ApplyModifier(SourceModifier modifier, string value)
        {
            switch (modifier)
            {
                case SourceModifier.None:
                    return value;
                case SourceModifier.Negate:
                    return $"-{value}";
                case SourceModifier.Bias:
                    return $"{value}_bias";
                case SourceModifier.BiasAndNegate:
                    return $"-{value}_bias";
                case SourceModifier.Sign:
                    return $"{value}_bx2";
                case SourceModifier.SignAndNegate:
                    return $"-{value}_bx2";
                case SourceModifier.Complement:
                    throw new NotImplementedException();
                case SourceModifier.X2:
                    return $"{value}_x2";
                case SourceModifier.X2AndNegate:
                    return $"-{value}_x2";
                case SourceModifier.DivideByZ:
                    return $"{value}_dz";
                case SourceModifier.DivideByW:
                    return $"{value}_dw";
                case SourceModifier.Abs:
                    return $"{value}_abs";
                case SourceModifier.AbsAndNegate:
                    return $"-{value}_abs";
                case SourceModifier.Not:
                    throw new NotImplementedException();
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
