using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HlslDecompiler
{
    public class HlslSimpleWriter : HlslWriter
    {
        private int _loopVariableIndex = -1;
        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;
        private Dictionary<RegisterKey, int> _registerWriteMasks;

        public HlslSimpleWriter(ShaderModel shader)
            : base(shader)
        {
        }

        protected override void WriteMethodBody()
        {
            WriteLine("{0} o;", GetMethodReturnType());
            WriteLine();

            _registerWriteMasks = FindTemporaryRegisterAssignments(_shader.Instructions);
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

            WriteLine();
            WriteLine("return o;");
        }

        private void WriteTemporaryVariableDeclarations()
        {
            foreach (var register in _registerWriteMasks)
            {
                int writeMask = register.Value;
                string writeMaskName;
                switch (writeMask)
                {
                    case 0x1:
                        writeMaskName = "float";
                        break;
                    case 0x3:
                        writeMaskName = "float2";
                        break;
                    case 0x7:
                        writeMaskName = "float3";
                        break;
                    case 0xF:
                        writeMaskName = "float4";
                        break;
                    default:
                        // TODO
                        writeMaskName = "float4";
                        break;
                        //throw new NotImplementedException();
                }
                WriteLine("{0} {1};", writeMaskName, GetTempRegisterName(register.Key));
            }
        }

        private static Dictionary<RegisterKey, int> FindTemporaryRegisterAssignments(IList<Instruction> instructions)
        {
            var tempRegisters = new Dictionary<RegisterKey, int>();
            foreach (Instruction instruction in instructions.Where(i => i.HasDestination))
            {
                int destIndex = instruction.GetDestinationParamIndex();
                if (instruction is D3D9Instruction d3D9Instruction
                    && (d3D9Instruction.GetParamRegisterType(destIndex) == RegisterType.Temp
                    || d3D9Instruction.GetParamRegisterType(destIndex) == RegisterType.Addr))
                {
                    int writeMask = instruction.GetDestinationWriteMask();

                    var registerKey = instruction.GetParamRegisterKey(destIndex);
                    if (!tempRegisters.TryAdd(registerKey, writeMask))
                    {
                        tempRegisters[registerKey] |= writeMask;
                    }
                }
                else if (instruction is D3D10Instruction d3D10Instruction
                    && d3D10Instruction.GetParamRegisterKey(destIndex).IsTempRegister)
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

        private static String GetTempRegisterName(RegisterKey registerKey)
        {
            if (registerKey.IsTempRegister)
            {
                return "r" + registerKey.Number;
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
                    string ifComparisonBreak;
                    switch (instruction.Comparison)
                    {
                        case IfComparison.GT:
                            ifComparisonBreak = ">";
                            break;
                        case IfComparison.EQ:
                            ifComparisonBreak = "==";
                            break;
                        case IfComparison.GE:
                            ifComparisonBreak = ">=";
                            break;
                        case IfComparison.LE:
                            ifComparisonBreak = "<=";
                            break;
                        case IfComparison.NE:
                            ifComparisonBreak = "!=";
                            break;
                        case IfComparison.LT:
                            ifComparisonBreak = "<";
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                    WriteLine("if ({0} {2} {1}) break;", GetSourceName(instruction, 0), GetSourceName(instruction, 1), ifComparisonBreak);
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
                    string ifComparison;
                    switch (instruction.Comparison)
                    {
                        case IfComparison.GT:
                            ifComparison = ">";
                            break;
                        case IfComparison.EQ:
                            ifComparison = "==";
                            break;
                        case IfComparison.GE:
                            ifComparison = ">=";
                            break;
                        case IfComparison.LE:
                            ifComparison = "<=";
                            break;
                        case IfComparison.NE:
                            ifComparison = "!=";
                            break;
                        case IfComparison.LT:
                            ifComparison = "<";
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                    WriteLine("if ({0} {2} {1}) {{", GetSourceName(instruction, 0), GetSourceName(instruction, 1), ifComparison);
                    indent += "\t";
                    break;
                case Opcode.Log:
                    WriteLine(GetModifier(instruction), GetDestinationName(instruction),
                        $"log2({GetSourceName(instruction, 1)})");
                    break;
                case Opcode.Loop:
                    ConstantIntRegister intRegister = _registers.FindConstantIntRegister(instruction.GetParamRegisterNumber(1));
                    uint end = intRegister.Value[0];
                    uint start = intRegister.Value[1];
                    uint stride = intRegister.Value[2];
                    _loopVariableIndex++;
                    string loopVariable = "i" + _loopVariableIndex;
                    if (stride == 1)
                    {
                        WriteLine("for (int {2} = {0}; {2} < {1}; {2}++) {{", start, end, loopVariable);
                    }
                    else
                    {
                        WriteLine("for (int {3} = {0}; {3} < {1}; {3} += {2}) {{", start, end, stride, loopVariable);
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
                    ConstantIntRegister loopRegister = _registers.FindConstantIntRegister(instruction.GetParamRegisterNumber(0));
                    _loopVariableIndex++;
                    WriteLine("for (int {1} = 0; {1} < {0}; {1}++) {{", loopRegister[0], "i" + _loopVariableIndex);
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
                    break;
            }
        }

        private void WriteInstruction(D3D10Instruction instruction)
        {
            switch (instruction.Opcode)
            {
                case D3D10Opcode.Add:
                    WriteLine("{0} = {1} + {2};", GetDestinationName(instruction), GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case D3D10Opcode.DerivRtx:
                    WriteLine("{0} = ddx({1});", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case D3D10Opcode.DerivRty:
                    WriteLine("{0} = ddy({1});", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case D3D10Opcode.Discard:
                    WriteLine("clip({0});", GetSourceName(instruction, 0));
                    break;
                case D3D10Opcode.Dp2:
                case D3D10Opcode.Dp3:
                case D3D10Opcode.Dp4:
                    WriteLine("{0} = dot({1}, {2});", GetDestinationName(instruction), GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case D3D10Opcode.GE:
                    WriteLine("{0} = ({1} >= {2}) ? -1 : 0;", GetDestinationName(instruction), GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case D3D10Opcode.Mad:
                    WriteLine("{0} = {1} * {2} + {3};", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                    break;
                case D3D10Opcode.Mov:
                    WriteLine("{0} = {1};", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case D3D10Opcode.MovC:
                    WriteLine("{0} = ({1} != 0) ? {2} : {3};", GetDestinationName(instruction), GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                    break;
                case D3D10Opcode.Mul:
                    WriteLine("{0} = {1} * {2};", GetDestinationName(instruction), GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case D3D10Opcode.Rsq:
                    WriteLine("{0} = 1 / sqrt({1});", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case D3D10Opcode.Sample:
                    var samplerKey = instruction.GetParamRegisterKey(2);
                    int texCoordLength = _registers.ResourceDefinitions
                        .Where(d => d.ShaderInputType == D3DShaderInputType.Texture)
                        .First(d => d.BindPoint == samplerKey.Number)
                        .GetDimensionSize();
                    WriteLine("{0} = {2}.Sample({3}, {1});", GetDestinationName(instruction), GetSourceName(instruction, 1, texCoordLength), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                    break;
                case D3D10Opcode.Sqrt:
                    WriteLine("{0} = sqrt({1});", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case D3D10Opcode.DclConstantBuffer:
                case D3D10Opcode.DclInput:
                case D3D10Opcode.DclInputPS:
                case D3D10Opcode.DclInputPSSiv:
                case D3D10Opcode.DclOutput:
                case D3D10Opcode.DclResource:
                case D3D10Opcode.DclSampler:
                case D3D10Opcode.DclTemps:
                case D3D10Opcode.Ret:
                    break;
                default:
                    break;
            }
        }

        private string GetDestinationName(Instruction instruction)
        {
            int destIndex = instruction.GetDestinationParamIndex();
            RegisterKey registerKey = instruction.GetParamRegisterKey(destIndex);

            string registerName;
            if (instruction is D3D9Instruction d3D9Instruction && d3D9Instruction.Opcode == Opcode.MovA
                && (registerKey as D3D9RegisterKey).Type == RegisterType.Addr)
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

            var registerKey = instruction.GetParamRegisterKey(srcIndex) as D3D9RegisterKey;
            switch (registerKey.Type)
            {
                case RegisterType.Const:
                case RegisterType.Const2:
                case RegisterType.Const3:
                case RegisterType.Const4:
                case RegisterType.ConstBool:
                case RegisterType.ConstInt:
                    string constantValue = GetSourceConstantValue(instruction, srcIndex, destinationLength);
                    if (constantValue != null)
                    {
                        return constantValue;
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

        private string GetSourceName(D3D10Instruction instruction, int srcIndex, int? destinationLength = null)
        {
            string sourceRegisterName;

            var registerKey = instruction.GetParamRegisterKey(srcIndex) as D3D10RegisterKey;
            switch (registerKey.OperandType)
            {
                case OperandType.Immediate32:
                    return GetSourceConstantValue(instruction, srcIndex, destinationLength);
                default:
                    sourceRegisterName = _registers.GetRegisterName(registerKey);
                    break;
            }
            sourceRegisterName += instruction.GetSourceSwizzleName(srcIndex, destinationLength);
            return ApplyModifier(instruction.GetOperandModifier(srcIndex), sourceRegisterName);
        }

        private static string GetRelativeAddressingName(Instruction instruction, int srcIndex)
        {
            if (instruction is D3D9Instruction d3D9Instruction && d3D9Instruction.Params.HasRelativeAddressing(srcIndex))
            {
                return "[a0]";
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
                    if (instruction is D3D9Instruction d3D9Instruction
                        && (d3D9Instruction.Opcode == Opcode.If || d3D9Instruction.Opcode == Opcode.IfC))
                    {
                        // TODO
                    }
                    destinationLength = 4;
                }
            }

            switch (registerType)
            {
                case RegisterType.ConstBool:
                    throw new NotImplementedException();
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

        private static string GetSourceConstantValue(D3D10Instruction instruction, int srcIndex, int? destinationLength = null)
        {
            D3D10RegisterKey registerKey = instruction.GetParamRegisterKey(srcIndex) as D3D10RegisterKey;
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

            if (registerKey.ImmediateSingle.Length == 1)
            {
                return ConstantFormatter.Format(registerKey.ImmediateSingle[0]);
            }
            string[] constant = swizzle
                            .Take(destinationLength.Value)
                            .Select(s => registerKey.ImmediateSingle[s])
                            .Select(ConstantFormatter.Format)
                            .ToArray();
            return $"float{destinationLength.Value}(" + string.Join(", ", constant) + ")";
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
}