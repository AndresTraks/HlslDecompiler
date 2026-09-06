using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HlslDecompiler;

public class HlslSimpleWriter : HlslWriter
{
    private int _loopVariableIndex = -1;
    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;
    private readonly IntegerOperandAnalysis _integerOperandAnalysis;

    public HlslSimpleWriter(ShaderModel shader)
        : base(shader)
    {
        _integerOperandAnalysis = new IntegerOperandAnalysis(shader);
    }

    protected override void WriteMethodBody()
    {
        if (_registers.MethodOutputRegisters.Count != 0)
        {
            if (_shader.Type == ShaderType.Geometry)
            {
                WriteLine("GS_OUT o;");
            }
            else
            {
                WriteLine("{0} o;", GetMethodReturnType());
            }
            WriteLine();
        }

        WriteTemporaryVariableDeclarations();
        foreach (Instruction instruction in _shader.Instructions)
        {
            if (instruction is D3D9Instruction d3d9Instruction)
            {
                WriteInstruction(d3d9Instruction);
            }
            else if (instruction is D3D10Instruction d9d10Instruction)
            {
                WriteInstruction(d9d10Instruction);
            }
        }

        if (_registers.MethodOutputRegisters.Count != 0 && _shader.Type != ShaderType.Geometry)
        {
            WriteLine();
            WriteLine("return o;");
        }
    }

    private void WriteTemporaryVariableDeclarations()
    {
        Dictionary<RegisterKey, int> registerWriteMasks = FindTemporaryRegisterAssignments(_shader.Instructions);
        foreach (var register in registerWriteMasks)
        {
            int writeMask = register.Value;
            // An array subscript has to be an integer, and the address register is
            // only ever used as one.
            bool isAddressRegister = register.Key is D3D9RegisterKey addressKey
                && addressKey.Type == RegisterType.Addr;
            string writeMaskName = isAddressRegister ? "int" : writeMask switch
            {
                0x1 => "float",
                0x3 => "float2",
                0x7 => "float3",
                0xF => "float4",
                _ => "float4",// TODO
            };
            WriteLine("{0} {1};", writeMaskName, GetTempRegisterName(register.Key));
        }
    }

    private Dictionary<RegisterKey, int> FindTemporaryRegisterAssignments(IList<Instruction> instructions)
    {
        var tempRegisters = new Dictionary<RegisterKey, int>();
        foreach (Instruction instruction in instructions.Where(i => i.HasDestination))
        {
            int destIndex = instruction.GetDestinationParamIndex().Value;
            if (IsDestinationTempRegister(instruction, destIndex))
            {
                int writeMask = instruction.GetDestinationWriteMask();

                var registerKey = instruction.GetParamRegisterKey(destIndex);
                if (!tempRegisters.TryAdd(registerKey, writeMask))
                {
                    tempRegisters[registerKey] |= writeMask;
                }
            }
        }
        return tempRegisters;
    }

    private bool IsDestinationTempRegister(Instruction instruction, int destIndex)
    {
        if (instruction is D3D9Instruction d3d9)
        {
            RegisterType type = d3d9.GetParamRegisterType(destIndex);
            // Addr and Texture are one register type number. In a vertex shader it is
            // the address register, which needs declaring like a temp; in a pixel
            // shader it is a texture coordinate input, which does not.
            return type == RegisterType.Temp
                || (type == RegisterType.Addr && _shader.Type != ShaderType.Pixel);
        }
        return instruction is D3D10Instruction d3d10 && d3d10.GetParamRegisterKey(destIndex).IsTempRegister;
    }

    private static String GetTempRegisterName(RegisterKey registerKey)
    {
        if (registerKey.IsTempRegister)
        {
            return "r" + registerKey.Number;
        }
        // `mova` writes the address register like a temp, so it is collected as
        // one and needs a name and a declaration for the same reason.
        if (registerKey is D3D9RegisterKey d3d9RegisterKey && d3d9RegisterKey.Type == RegisterType.Addr)
        {
            return "a" + registerKey.Number;
        }
        throw new NotImplementedException();
    }

    private static string GetModifier(D3D9Instruction instruction)
    {
        string source = "{1}";
        ResultModifier resultModifier = instruction.GetDestinationResultModifier();
        if (resultModifier.HasFlag(ResultModifier.Saturate))
        {
            source = $"saturate({source})";
        }
        if (resultModifier.HasFlag(ResultModifier.PartialPrecision))
        {
            string size = instruction.GetDestinationMaskLength().ToString();
            size = size == "1" ? "" : size;
            source = $"half{size}({source})";
        }
        return "{0} = " + source + ";";
    }

    private void WriteInstruction(D3D9Instruction instruction)
    {
        switch (instruction.Opcode)
        {
            case Opcode.Abs:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"abs({GetSourceName(instruction, 1)})");
                break;
            case Opcode.Add:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"{GetSourceName(instruction, 1)} + {GetSourceName(instruction, 2)}");
                break;
            case Opcode.BreakC:
                WriteLine("if ({0} {2} {1}) break;", GetSourceName(instruction, 0), GetSourceName(instruction, 1), instruction.Comparison.ToHlslString());
                break;
            case Opcode.Cmp:
                // TODO: should be per-component
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"({GetSourceName(instruction, 1)} >= 0) ? {GetSourceName(instruction, 2)} : {GetSourceName(instruction, 3)}");
                break;
            case Opcode.DP2Add:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"dot({GetSourceName(instruction, 1)}, {GetSourceName(instruction, 2)}) + {GetSourceName(instruction, 3)}");
                break;
            case Opcode.Dp3:
            case Opcode.Dp4:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"dot({GetSourceName(instruction, 1)}, {GetSourceName(instruction, 2)})");
                break;
            case Opcode.DSX:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"ddx({GetSourceName(instruction, 1)})");
                break;
            case Opcode.DSY:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"ddy({GetSourceName(instruction, 1)})");
                break;
            case Opcode.Else:
                indent = indent.Substring(0, indent.Length - 1);
                WriteLine("} else {");
                indent += "\t";
                break;
            case Opcode.Endif:
                indent = indent.Substring(0, indent.Length - 1);
                WriteLine("}");
                break;
            case Opcode.EndLoop:
            case Opcode.EndRep:
                indent = indent.Substring(0, indent.Length - 1);
                WriteLine("}");
                _loopVariableIndex--;
                break;
            case Opcode.Exp:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"exp2({GetSourceName(instruction, 1)})");
                break;
            case Opcode.Frc:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"frac({GetSourceName(instruction, 1)})");
                break;
            case Opcode.If:
                WriteLine("if ({0}) {{", GetSourceName(instruction, 0));
                indent += "\t";
                break;
            case Opcode.IfC:
                WriteLine("if ({0} {2} {1}) {{", GetSourceName(instruction, 0), GetSourceName(instruction, 1), instruction.Comparison.ToHlslString());
                indent += "\t";
                break;
            case Opcode.Log:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"log2({GetSourceName(instruction, 1)})");
                break;
            case Opcode.Loop:
                int loopRegisterNumber = instruction.GetParamRegisterNumber(1);
                ConstantIntRegister intRegister = _registers.FindConstantIntRegister(loopRegisterNumber);
                _loopVariableIndex++;
                string loopVariable = "i" + _loopVariableIndex;
                if (intRegister == null)
                {
                    // The trip count is a uniform, declared rather than defined by a defi.
                    string count = _registers.GetRegisterName(
                        new D3D9RegisterKey(RegisterType.ConstInt, loopRegisterNumber));
                    WriteLine("for (int {1} = 0; {1} < {0}; {1}++) {{", count, loopVariable);
                }
                else if (intRegister.Value[2] == 1)
                {
                    WriteLine("for (int {2} = {0}; {2} < {1}; {2}++) {{",
                        intRegister.Value[1], intRegister.Value[0], loopVariable);
                }
                else
                {
                    WriteLine("for (int {3} = {0}; {3} < {1}; {3} += {2}) {{",
                        intRegister.Value[1], intRegister.Value[0], intRegister.Value[2], loopVariable);
                }
                indent += "\t";
                break;
            case Opcode.Lrp:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"lerp({GetSourceName(instruction, 2)}, {GetSourceName(instruction, 3)}, {GetSourceName(instruction, 1)})");
                break;
            case Opcode.Mad:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"{GetSourceName(instruction, 1)} * {GetSourceName(instruction, 2)} + {GetSourceName(instruction, 3)}");
                break;
            case Opcode.Max:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"max({GetSourceName(instruction, 1)}, {GetSourceName(instruction, 2)})");
                break;
            case Opcode.Min:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"min({GetSourceName(instruction, 1)}, {GetSourceName(instruction, 2)})");
                break;
            case Opcode.Mov:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction), GetSourceName(instruction, 1));
                break;
            case Opcode.MovA:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction), GetSourceName(instruction, 1));
                break;
            case Opcode.Mul:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"{GetSourceName(instruction, 1)} * {GetSourceName(instruction, 2)}");
                break;
            case Opcode.Nrm:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"normalize({GetSourceName(instruction, 1)})");
                break;
            case Opcode.Pow:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"pow({GetSourceName(instruction, 1)}, {GetSourceName(instruction, 2)})");
                break;
            case Opcode.Rcp:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"1 / {GetSourceName(instruction, 1)}");
                break;
            case Opcode.Rep:
                int repRegisterNumber = instruction.GetParamRegisterNumber(0);
                ConstantIntRegister loopRegister = _registers.FindConstantIntRegister(repRegisterNumber);
                _loopVariableIndex++;
                // As with loop, the trip count is a uniform when there is no defi.
                object repCount = loopRegister != null
                    ? loopRegister[0]
                    : _registers.GetRegisterName(
                        new D3D9RegisterKey(RegisterType.ConstInt, repRegisterNumber));
                WriteLine("for (int {1} = 0; {1} < {0}; {1}++) {{", repCount, "i" + _loopVariableIndex);
                indent += "\t";
                break;
            case Opcode.Rsq:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"1 / sqrt({GetSourceName(instruction, 1)})");
                break;
            case Opcode.Sge:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"({GetSourceName(instruction, 1)} >= {GetSourceName(instruction, 2)}) ? 1 : 0");
                break;
            case Opcode.Slt:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"({GetSourceName(instruction, 1)} < {GetSourceName(instruction, 2)}) ? 1 : 0");
                break;
            case Opcode.SinCos:
                WriteLine("sincos({1}, {0}, {0});", GetDestinationName(instruction), GetSourceName(instruction, 1));
                break;
            case Opcode.Sub:
                WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                    $"{GetSourceName(instruction, 1)} - {GetSourceName(instruction, 2)}");
                break;
            case Opcode.Tex:
                if ((_shader.MajorVersion == 1 && _shader.MinorVersion >= 4) || (_shader.MajorVersion > 1))
                {
                    ConstantDeclaration sampler = _registers.FindConstant(RegisterSet.Sampler, instruction.GetParamRegisterNumber(2));
                    int samplerDimension = sampler.GetSamplerDimension();
                    string samplerType = sampler.TypeInfo.ParameterType == ParameterType.SamplerCube ? "CUBE" : (samplerDimension + "D");
                    if (instruction.TexldControls.HasFlag(TexldControls.Project))
                    {
                        WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                            $"tex{samplerType}proj({GetSourceName(instruction, 2)}, {GetSourceName(instruction, 1, 4)})");
                    }
                    else if (instruction.TexldControls.HasFlag(TexldControls.Bias))
                    {
                        WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                            $"tex{samplerType}bias({GetSourceName(instruction, 2)}, {GetSourceName(instruction, 1, 4)})");
                    }
                    else
                    {
                        WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                            $"tex{samplerType}({GetSourceName(instruction, 2)}, {GetSourceName(instruction, 1, samplerDimension)})");
                    }
                }
                else
                {
                    WriteLine(GetModifier(instruction), GetDestinationName(instruction), "tex2D()");
                }
                break;
            case Opcode.TexLDL:
                {
                    ConstantDeclaration sampler = _registers.FindConstant(RegisterSet.Sampler, instruction.GetParamRegisterNumber(2));
                    int samplerDimension = sampler.GetSamplerDimension();
                    string samplerType = sampler.TypeInfo.ParameterType == ParameterType.SamplerCube ? "CUBE" : (samplerDimension + "D");
                    WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                        $"tex{samplerType}lod({GetSourceName(instruction, 2)}, {GetSourceName(instruction, 1, 4)})");
                    break;
                }
            case Opcode.TexLDD:
                {
                    ConstantDeclaration sampler = _registers.FindConstant(RegisterSet.Sampler, instruction.GetParamRegisterNumber(2));
                    int samplerDimension = sampler.GetSamplerDimension();
                    string samplerType = sampler.TypeInfo.ParameterType == ParameterType.SamplerCube ? "CUBE" : (samplerDimension + "D");
                    WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                        $"tex{samplerType}grad({GetSourceName(instruction, 2)}, {GetSourceName(instruction, 1, samplerDimension)}, {GetSourceName(instruction, 3, samplerDimension)}, {GetSourceName(instruction, 4, samplerDimension)})");
                    break;
                }
            case Opcode.TexKill:
                WriteLine("clip({0});", GetDestinationName(instruction));
                break;
            case Opcode.Def:
            case Opcode.DefB:
            case Opcode.DefI:
            case Opcode.Dcl:
            case Opcode.Comment:
            case Opcode.End:
                break;
            default:
                throw new NotImplementedException(instruction.Opcode.ToString());
        }
    }

    private void WriteInstruction(D3D10Instruction instruction)
    {
        switch (instruction.Opcode)
        {
            case D3D10Opcode.Add:
            case D3D10Opcode.IAdd:
                WriteLine("{0} = {1} + {2};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.IShl:
                WriteLine("{0} = {1} << {2};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.BreakC:
                WriteLine("if ({0} != 0) break;", GetOperandName(instruction, 0));
                break;
            case D3D10Opcode.Cut:
                WriteLine("stream.RestartStrip();");
                break;
            case D3D10Opcode.DerivRtx:
                WriteLine("{0} = ddx({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.DerivRty:
                WriteLine("{0} = ddy({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.Discard:
                WriteLine("clip({0});", GetOperandName(instruction, 0));
                break;
            case D3D10Opcode.Dp2:
            case D3D10Opcode.Dp3:
            case D3D10Opcode.Dp4:
                WriteLine("{0} = dot({1}, {2});", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.Emit:
                WriteLine("stream.Append(o);");
                break;
            // Control flow was skipped entirely, so a DXBC loop with a guarded break
            // printed as `while (true)` with nothing to end it.
            case D3D10Opcode.If:
                WriteLine("if ({0} != 0) {{", GetOperandName(instruction, 0));
                indent += "\t";
                break;
            case D3D10Opcode.Else:
                indent = indent.Substring(0, indent.Length - 1);
                WriteLine("} else {");
                indent += "\t";
                break;
            case D3D10Opcode.EndIf:
                indent = indent.Substring(0, indent.Length - 1);
                WriteLine("}");
                break;
            case D3D10Opcode.Swtich:
                WriteLine("switch ({0}) {{", GetOperandName(instruction, 0));
                indent += "\t";
                break;
            case D3D10Opcode.Case:
                WriteLine("case {0}:", GetOperandName(instruction, 0));
                break;
            case D3D10Opcode.Default:
                WriteLine("default:");
                break;
            case D3D10Opcode.EndSwitch:
                indent = indent.Substring(0, indent.Length - 1);
                WriteLine("}");
                break;
            case D3D10Opcode.Break:
                WriteLine("break;");
                break;
            case D3D10Opcode.Continue:
                WriteLine("continue;");
                break;
            case D3D10Opcode.ContinueC:
                WriteLine("if ({0} != 0) continue;", GetOperandName(instruction, 0));
                break;
            case D3D10Opcode.Div:
                WriteLine("{0} = {1} / {2};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.Exp:
                WriteLine("{0} = exp2({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.Max:
            case D3D10Opcode.IMax:
                WriteLine("{0} = max({1}, {2});", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.IMin:
                WriteLine("{0} = min({1}, {2});", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.INeg:
                WriteLine("{0} = -{1};", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.IMul:
                // Two destinations, high and low halves; only the low one is modelled.
                WriteLine("{0} = {1} * {2};", GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3));
                break;
            case D3D10Opcode.IMad:
                WriteLine("{0} = {1} * {2} + {3};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3));
                break;
            case D3D10Opcode.RoundNi:
                WriteLine("{0} = floor({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.LT:
                WriteLine("{0} = ({1} < {2}) ? -1 : 0;", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.Ige:
            case D3D10Opcode.UGE:
                WriteLine("{0} = ({1} >= {2}) ? -1 : 0;", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.ULT:
                WriteLine("{0} = ({1} < {2}) ? -1 : 0;", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.EndLoop:
                indent = indent.Substring(0, indent.Length - 1);
                WriteLine("}");
                break;
            case D3D10Opcode.GE:
                WriteLine("{0} = ({1} >= {2}) ? -1 : 0;", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.Ilt:
                WriteLine("{0} = ({1} < {2}) ? -1 : 0;", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.IToF:
            case D3D10Opcode.UTof:
            case D3D10Opcode.Ftoi:
            case D3D10Opcode.Ftou:
                WriteLine("{0} = {1};", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.LdStructured:
                // TODO: consider offset
                WriteLine("{0} = {3}[{1}];", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3));
                break;
            case D3D10Opcode.Loop:
                WriteLine("while (true) {");
                indent += "\t";
                break;
            case D3D10Opcode.Mad:
                WriteLine("{0} = {1} * {2} + {3};", GetOperandName(instruction, 0),
                    GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3));
                break;
            case D3D10Opcode.Mov:
                WriteLine("{0} = {1};", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.MovC:
                WriteLine("{0} = ({1} != 0) ? {2} : {3};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3));
                break;
            case D3D10Opcode.Mul:
                WriteLine("{0} = {1} * {2};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.Rsq:
                WriteLine("{0} = 1 / sqrt({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.Sample:
                WriteLine("{0} = {2}.Sample({3}, {1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3));
                break;
            case D3D10Opcode.RoundZ:
                WriteLine("{0} = trunc({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.Frc:
                WriteLine("{0} = frac({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.RoundNe:
                WriteLine("{0} = round({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.Gather4:
                WriteLine("{0} = {2}.Gather({3}, {1});", GetOperandName(instruction, 0),
                    GetOperandName(instruction, 1), GetOperandName(instruction, 2),
                    GetOperandName(instruction, 3));
                break;
            case D3D10Opcode.SampleL:
                WriteLine("{0} = {2}.SampleLevel({3}, {1}, {4});", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3), GetOperandName(instruction, 4));
                break;
            case D3D10Opcode.SampleB:
                WriteLine("{0} = {2}.SampleBias({3}, {1}, {4});", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3), GetOperandName(instruction, 4));
                break;
            case D3D10Opcode.SampleC:
                WriteLine("{0} = {2}.SampleCmp({3}, {1}, {4});", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3), GetOperandName(instruction, 4));
                break;
            case D3D10Opcode.SampleCLZ:
                WriteLine("{0} = {2}.SampleCmpLevelZero({3}, {1}, {4});", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3), GetOperandName(instruction, 4));
                break;
            case D3D10Opcode.SampleD:
                WriteLine("{0} = {2}.SampleGrad({3}, {1}, {4}, {5});", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3), GetOperandName(instruction, 4), GetOperandName(instruction, 5));
                break;
            case D3D10Opcode.LD:
                WriteLine("{0} = {2}.Load({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.Log:
                WriteLine("{0} = log2({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.Min:
                WriteLine("{0} = min({1}, {2});", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.RoundPi:
                WriteLine("{0} = ceil({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.Ieq:
                WriteLine("{0} = ({1} == {2}) ? -1 : 0;", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.And:
                WriteLine("{0} = {1} & {2};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.Udiv:
                // Quotient and remainder, either of which may be null.
                if (instruction.GetOperandType(0) != OperandType.Null)
                {
                    WriteLine("{0} = {1} / {2};", GetOperandName(instruction, 0), GetOperandName(instruction, 2), GetOperandName(instruction, 3));
                }
                if (instruction.GetOperandType(1) != OperandType.Null)
                {
                    WriteLine("{0} = {1} % {2};", GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3));
                }
                break;
            case D3D10Opcode.Or:
                WriteLine("{0} = {1} | {2};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.Xor:
                WriteLine("{0} = {1} ^ {2};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.SinCos:
                WriteLine("{0} = sin({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 2));
                WriteLine("{0} = cos({1});", GetOperandName(instruction, 1), GetOperandName(instruction, 2));
                break;
            case D3D10Opcode.Sqrt:
                WriteLine("{0} = sqrt({1});", GetOperandName(instruction, 0), GetOperandName(instruction, 1));
                break;
            case D3D10Opcode.StoreStructured:
                // TODO: consider offset
                WriteLine("{0}[{1}] = {3};", GetOperandName(instruction, 0), GetOperandName(instruction, 1), GetOperandName(instruction, 2), GetOperandName(instruction, 3));
                break;
            case D3D10Opcode.DclConstantBuffer:
            case D3D10Opcode.DclGlobalFlags:
            case D3D10Opcode.DclGSInputPrimitive:
            case D3D10Opcode.DclGSMaxOutputVertexCount:
            case D3D10Opcode.DclInput:
            case D3D10Opcode.DclInputPS:
            case D3D10Opcode.DclInputPSSgv:
            case D3D10Opcode.DclInputPSSiv:
            case D3D10Opcode.DclInputSgv:
            case D3D10Opcode.DclOutputSgv:
            case D3D10Opcode.DclInputSiv:
            case D3D10Opcode.DclOutput:
            case D3D10Opcode.DclGSOutputPrimitiveTopology:
            case D3D10Opcode.DclOutputSiv:
            case D3D10Opcode.DclResource:
            case D3D10Opcode.DclResourceStructured:
            case D3D10Opcode.DclSampler:
            case D3D10Opcode.DclTemps:
            case D3D10Opcode.DclThreadGroup:
            case D3D10Opcode.DclUnorderedAccessViewStructured:
                break;
            case D3D10Opcode.RetC:
                WriteLine("if ({0} != 0) return{1};", GetOperandName(instruction, 0),
                    _registers.MethodOutputRegisters.Count != 0 && _shader.Type != ShaderType.Geometry
                        ? " o"
                        : "");
                break;
            case D3D10Opcode.Ret:
                // The last ret is the method returning, which is written after the
                // body. Anywhere else it is an early return, and dropping it lost the
                // branch that took it.
                if (!ReferenceEquals(instruction, _shader.Instructions[_shader.Instructions.Count - 1]))
                {
                    WriteLine(_registers.MethodOutputRegisters.Count != 0 && _shader.Type != ShaderType.Geometry
                        ? "return o;"
                        : "return;");
                }
                break;
            default:
                // Anything not listed above writes nothing, which silently drops
                // the instruction - sample_l left an empty method body.
                throw new NotImplementedException(instruction.Opcode.ToString());
        }
    }

    private string GetDestinationName(D3D9Instruction instruction)
    {
        int destIndex = instruction.GetDestinationParamIndex().Value;
        D3D9RegisterKey registerKey = instruction.GetParamRegisterKey(destIndex);

        string registerName;
        if (instruction.Opcode == Opcode.MovA && registerKey.Type == RegisterType.Addr)
        {
            registerName = "a0";
        }
        else
        {
            registerName = _registers.GetRegisterName(registerKey);
        }
        int registerLength = _registers.GetRegisterMaskedLength(registerKey);
        string writeMaskName = instruction.GetDestinationWriteMaskName(registerLength);

        return string.Format("{0}{1}", registerName, writeMaskName);
    }

    private string GetSourceName(D3D9Instruction instruction, int srcIndex, int? destinationLength = null)
    {
        string sourceRegisterName;

        var registerKey = instruction.GetParamRegisterKey(srcIndex);
        switch (registerKey.Type)
        {
            case RegisterType.Const:
            case RegisterType.Const2:
            case RegisterType.Const3:
            case RegisterType.Const4:
            case RegisterType.ConstBool:
            case RegisterType.ConstInt:
                // A relatively addressed def is the base of an array, not the value
                // being read - substituting its literal drops the subscript.
                if (!instruction.Params.HasRelativeAddressing(srcIndex))
                {
                    string constantValue = GetSourceConstantValue(instruction, srcIndex, destinationLength);
                    if (constantValue != null)
                    {
                        return constantValue;
                    }
                }

                if (_registers.FindConstantArray(registerKey) is ConstantArray literals)
                {
                    sourceRegisterName = literals.Name;
                    break;
                }

                ConstantDeclaration decl = _registers.FindConstant(registerKey);
                if (decl == null)
                {
                    // Constant register not found in def statements nor the constant table
                    throw new NotImplementedException();
                }

                if ((decl.TypeInfo.ParameterClass == ParameterClass.MatrixRows && _registers.ColumnMajorOrder) ||
                    (decl.TypeInfo.ParameterClass == ParameterClass.MatrixColumns && !_registers.ColumnMajorOrder))
                {
                    int row = registerKey.Number - decl.RegisterIndex;
                    sourceRegisterName = $"{decl.Name}[{row}]";
                }
                else if ((decl.TypeInfo.ParameterClass == ParameterClass.MatrixColumns && _registers.ColumnMajorOrder) ||
                    (decl.TypeInfo.ParameterClass == ParameterClass.MatrixRows && !_registers.ColumnMajorOrder))
                {
                    int column = registerKey.Number - decl.RegisterIndex;
                    sourceRegisterName = $"transpose({decl.Name})[{column}]";
                }
                else
                {
                    sourceRegisterName = decl.Name;
                }
                break;
            default:
                sourceRegisterName = _registers.GetRegisterName(registerKey);
                break;
        }

        sourceRegisterName += GetRelativeAddressingName(instruction, srcIndex);
        sourceRegisterName += instruction.GetSourceSwizzleName(srcIndex, destinationLength);
        return ApplyModifier(instruction.GetSourceModifier(srcIndex), sourceRegisterName);
    }

    private string GetDynamicOperandName(
        D3D10Instruction instruction,
        int operandIndex,
        D3D10OperandTokenCollection.OperandIndex[] operandIndices)
    {
        int relativeIndex = Array.FindIndex(operandIndices, i => i.IsRelative);
        (OperandType indexType, int indexNumber, byte indexComponent) =
            instruction.OperandTokens.GetRelativeIndexOperand(operandIndex, relativeIndex);
        if (indexType != OperandType.Temp)
        {
            throw new NotImplementedException(indexType.ToString());
        }
        string index = $"r{indexNumber}.{"xyzw"[indexComponent]}";

        OperandType operandType = instruction.GetOperandType(operandIndex);
        if (operandType == OperandType.Input)
        {
            // The vertex is the dynamic part; the second index names the register.
            var vertexKey = D3D10RegisterKey.CreateGSInput((int)operandIndices[1].Immediate, 0);
            return $"i[{index}].{_registers.RegisterDeclarations[vertexKey].Name}";
        }

        var registerKey = new D3D10RegisterKey(
            OperandType.ConstantBuffer,
            (int)operandIndices[0].Immediate,
            (int)operandIndices[1].Immediate);
        ConstantDeclaration declaration = _registers.FindConstant(registerKey, 0);
        int elementOffset = _registers.GetConstantBufferElementOffset(registerKey, declaration);
        return elementOffset == 0
            ? $"{declaration.Name}[{index}]"
            : $"{declaration.Name}[{index} + {elementOffset}]";
    }

    private string GetRelativeAddressingName(D3D9Instruction instruction, int srcIndex)
    {
        if (instruction.Params.HasRelativeAddressing(srcIndex))
        {
            // aL counts the enclosing loop; a0 is the address register.
            string index = instruction.GetRelativeParamRegisterType(srcIndex) == RegisterType.Loop
                ? $"i{_loopVariableIndex}"
                : $"a{instruction.GetRelativeParamRegisterNumber(srcIndex)}";

            // The subscripted register need not be the first of a def-defined run.
            var registerKey = instruction.GetParamRegisterKey(srcIndex);
            if (_registers.FindConstantArray(registerKey) is ConstantArray literals
                && registerKey.Number != literals.BaseRegisterIndex)
            {
                index += $" + {registerKey.Number - literals.BaseRegisterIndex}";
            }
            return $"[{index}]";
        }
        return string.Empty;
    }

    private string GetSourceConstantValue(D3D9Instruction instruction, int srcIndex, int? destinationLength = null)
    {
        var registerType = instruction.GetParamRegisterType(srcIndex);
        int registerNumber = instruction.GetParamRegisterNumber(srcIndex);
        byte[] swizzle = instruction.GetSourceSwizzleComponents(srcIndex);

        if (destinationLength == null)
        {
            if (instruction.HasDestination)
            {
                int writeMask = instruction.GetDestinationWriteMask();
                destinationLength = 0;
                for (int i = 0; i < 4; i++)
                {
                    if ((writeMask & (1 << i)) != 0)
                    {
                        destinationLength++;
                    }
                }
            }
            else
            {
                destinationLength = 4;
            }
        }

        switch (registerType)
        {
            case RegisterType.ConstBool:
                // Only defb gives a bool register a literal value, and nothing in the
                // codebase emits one. Otherwise the register names a uniform, which
                // the caller resolves through the constant table.
                return null;
            case RegisterType.ConstInt:
                {
                    var constantInt = _registers.ConstantIntDefinitions.FirstOrDefault(x => x.RegisterIndex == registerNumber);
                    if (constantInt == null)
                    {
                        return null;
                    }

                    uint[] constant = swizzle
                        .Take(destinationLength.Value)
                        .Select(s => constantInt[s]).ToArray();

                    switch (instruction.GetSourceModifier(srcIndex))
                    {
                        case SourceModifier.None:
                            break;
                        case SourceModifier.Negate:
                            for (int i = 0; i < constant.Length; i++)
                            {
                                throw new NotImplementedException();
                                //constantUint[i] = -constantUint[i];
                            }
                            break;
                        case SourceModifier.Abs:
                            for (int i = 0; i < constant.Length; i++)
                            {
                                throw new NotImplementedException();
                                //constantUint[i] = Math.Abs(constantUint[i]);
                            }
                            break;
                        case SourceModifier.AbsAndNegate:
                            for (int i = 0; i < constant.Length; i++)
                            {
                                throw new NotImplementedException();
                                //constantUint[i] = -Math.Abs(constantUint[i]);
                            }
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    if (constant.Skip(1).All(c => constant[0] == c))
                    {
                        return constant[0].ToString(_culture);
                    }
                    string size = constant.Length == 1 ? "" : constant.Length.ToString();
                    return $"int{size}({string.Join(", ", constant)})";
                }
            case RegisterType.Const:
            case RegisterType.Const2:
            case RegisterType.Const3:
            case RegisterType.Const4:
                {
                    var constantRegister = _registers.ConstantDefinitions.FirstOrDefault(x => x.RegisterIndex == registerNumber);
                    if (constantRegister == null)
                    {
                        return null;
                    }

                    float[] constant = swizzle
                        .Take(destinationLength.Value)
                        .Select(s => constantRegister[s]).ToArray();

                    switch (instruction.GetSourceModifier(srcIndex))
                    {
                        case SourceModifier.None:
                            break;
                        case SourceModifier.Negate:
                            for (int i = 0; i < constant.Length; i++)
                            {
                                constant[i] = -constant[i];
                            }
                            break;
                        case SourceModifier.Abs:
                            for (int i = 0; i < constant.Length; i++)
                            {
                                constant[i] = Math.Abs(constant[i]);
                            }
                            break;
                        case SourceModifier.AbsAndNegate:
                            for (int i = 0; i < constant.Length; i++)
                            {
                                constant[i] = -Math.Abs(constant[i]);
                            }
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    if (constant.Skip(1).All(c => constant[0] == c))
                    {
                        return constant[0].ToString(_culture);
                    }
                    string size = constant.Length == 1 ? "" : constant.Length.ToString();
                    return $"float{size}({string.Join(", ", constant.Select(c => c.ToString(_culture)))})";
                }
            default:
                throw new NotImplementedException();
        }
    }

    private string GetOperandName(D3D10Instruction instruction, int operandIndex)
    {
        D3D10RegisterKey registerKey = instruction.GetParamRegisterKey(operandIndex);

        if (registerKey.OperandType == OperandType.Immediate32)
        {
            // The 32 bits are typed by the instruction using them. Reading an integer
            // as a float printed its bit pattern - l(4) came out as 0.000000.
            bool isInteger = _integerOperandAnalysis.IsIntegerOperand(instruction);
            if (registerKey.ImmediateSingle.Length == 1)
            {
                return isInteger
                    ? instruction.GetParamInt(operandIndex, 0).ToString(_culture)
                    : ConstantFormatter.Format(registerKey.ImmediateSingle[0]);
            }
            int destinationLength = instruction.HasDestination ? instruction.GetDestinationMaskLength() : 4;
            byte[] swizzle = instruction.GetSourceSwizzleComponents(operandIndex);
            string[] constant = swizzle
                            .Take(destinationLength)
                            .Select(s => registerKey.ImmediateSingle[s])
                            .Select(ConstantFormatter.Format)
                            .ToArray();
            return $"float{destinationLength}(" + string.Join(", ", constant) + ")";
        }

        D3D10OperandModifier modifier = instruction.GetOperandModifier(operandIndex);
        // A relatively addressed operand decodes to a meaningless register number,
        // so its element has to be named from the index register instead.
        D3D10OperandTokenCollection.OperandIndex[] operandIndices =
            instruction.OperandTokens.GetOperandIndices(operandIndex);
        string registerName;
        bool isPackedScalar = false;
        if (operandIndices.Any(i => i.IsRelative))
        {
            registerName = GetDynamicOperandName(instruction, operandIndex, operandIndices);
        }
        else if (registerKey.OperandType == OperandType.ConstantBuffer)
        {
            // Several variables can share one register, so which is being read
            // depends on the component: cb0[0].y is `n`, not `mode.y`.
            byte component = instruction.GetSourceSwizzleComponents(operandIndex)[0];
            registerName = _registers.GetRegisterName(new RegisterComponentKey(registerKey, component));
            ConstantDeclaration packed = _registers.FindConstant(registerKey, component);
            isPackedScalar = packed != null
                && packed.TypeInfo.Rows == 1
                && packed.TypeInfo.Columns == 1;
        }
        else
        {
            registerName = _registers.GetRegisterName(registerKey);
        }
        string writeMaskName;
        if (operandIndex == instruction.GetDestinationParamIndex())
        {
            writeMaskName = instruction.GetDestinationWriteMaskName(_registers.GetRegisterMaskedLength(registerKey));
        }
        else if (instruction.Opcode == D3D10Opcode.LdStructured && operandIndex == 3)
        {
            writeMaskName = "";
        }
        else
        {
            int? maskedLength = null;
            if (instruction.Opcode == D3D10Opcode.Sample && operandIndex == 1)
            {
                maskedLength = _registers.ResourceDefinitions
                    .Where(d => d.ShaderInputType == D3DShaderInputType.Texture)
                    .First(d => d.BindPoint == instruction.GetParamRegisterNumber(2))
                    .GetDimensionSize();
            }
            // A scalar variable sharing a register has no component of its own to
            // name once the variable itself is named.
            writeMaskName = isPackedScalar
                ? ""
                : instruction.GetSourceSwizzleName(operandIndex, maskedLength);
        }

        return ApplyModifier(modifier, string.Format("{0}{1}", registerName, writeMaskName));
    }

    private static string ApplyModifier(SourceModifier modifier, string value)
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
            SourceModifier.X2 => $"(2 * {value})",
            SourceModifier.X2AndNegate => $"(-2 * {value})",
            SourceModifier.DivideByZ => $"{value}_dz",
            SourceModifier.DivideByW => $"{value}_dw",
            SourceModifier.Abs => $"abs({value})",
            SourceModifier.AbsAndNegate => $"-abs({value})",
            SourceModifier.Not => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };
    }

    private static string ApplyModifier(D3D10OperandModifier modifier, string value)
    {
        if (modifier.HasFlag(D3D10OperandModifier.Abs))
        {
            value = $"abs({value})";
        }
        if (modifier.HasFlag(D3D10OperandModifier.Neg))
        {
            value = $"-({value})";
        }
        return value;
    }
}