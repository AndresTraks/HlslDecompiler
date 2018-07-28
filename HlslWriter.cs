using HlslDecompiler.Hlsl;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace HlslDecompiler
{
    public class HlslWriter
    {
        private readonly ShaderModel _shader;
        private readonly bool doAstAnalysis;

        FileStream hlslFile;
        StreamWriter hlslWriter;
        string indent = "";

        public ICollection<Constant> ConstantDefinitions { get; private set; }
        public ICollection<ConstantInt> ConstantIntDefinitions { get; private set; }
        public ICollection<ConstantDeclaration> ConstantDeclarations { get; private set; }

        private ICollection<RegisterDeclaration> _registerDeclarations;
        private IList<RegisterDeclaration> _inputRegisters;
        private IList<RegisterDeclaration> _outputRegisters;
        private HashSet<string> _pixelShaderOutputRegisters;

        public HlslWriter(ShaderModel shader, bool doAstAnalysis = false)
        {
            _shader = shader;
            this.doAstAnalysis = doAstAnalysis;
        }

        void WriteLine()
        {
            hlslWriter.WriteLine();
        }

        void WriteLine(string value)
        {
            hlslWriter.Write(indent);
            hlslWriter.WriteLine(value);
        }

        void WriteLine(string format, params object[] args)
        {
            hlslWriter.Write(indent);
            hlslWriter.WriteLine(format, args);
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
                    return $"(2 * {value})";
                case SourceModifier.X2AndNegate:
                    return $"(-2 * {value})";
                case SourceModifier.DivideByZ:
                    return $"{value}_dz";
                case SourceModifier.DivideByW:
                    return $"{value}_dw";
                case SourceModifier.Abs:
                    return $"abs({value})";
                case SourceModifier.AbsAndNegate:
                    return $"-abs({value})";
                case SourceModifier.Not:
                    throw new NotImplementedException();
                default:
                    throw new NotImplementedException();
            }
        }

        string GetRegisterName(Instruction instruction, int paramIndex)
        {
            RegisterType registerType = instruction.GetParamRegisterType(paramIndex);
            int registerNumber = instruction.GetParamRegisterNumber(paramIndex);
            return GetRegisterName(registerType, registerNumber);
        }

        private string GetRegisterName(RegisterType registerType, int registerNumber)
        {
            var decl = _registerDeclarations.FirstOrDefault(x => x.RegisterType == registerType && x.RegisterNumber == registerNumber);
            if (decl != null)
            {
                switch (registerType)
                {
                    case RegisterType.Texture:
                        return decl.Name;
                    case RegisterType.Input:
                        return (_inputRegisters.Count == 1) ? decl.Name : ("i." + decl.Name);
                    case RegisterType.Output:
                        return (_outputRegisters.Count == 1) ? "o" : ("o." + decl.Name);
                    case RegisterType.Sampler:
                        var samplerDecl = ConstantDeclarations.FirstOrDefault(x => x.RegisterSet == RegisterSet.Sampler && x.RegisterIndex == registerNumber);
                        if (samplerDecl != null)
                        {
                            return samplerDecl.Name;
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    case RegisterType.MiscType:
                        if (registerNumber == 0)
                        {
                            return "vFace";
                        }
                        else if (registerNumber == 1)
                        {
                            return "vPos";
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    default:
                        throw new NotImplementedException();
                }
            }

            switch (registerType)
            {
                case RegisterType.Const:
                    var constDecl = ConstantDeclarations.FirstOrDefault(x => x.ContainsIndex(registerNumber));
                    if (constDecl != null)
                    {
                        return constDecl.Name;
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                case RegisterType.ColorOut:
                    return "o";
            }

            return null;
        }

        int GetRegisterFullLength(RegisterDeclaration decl)
        {
            if (decl != null)
            {
                switch (decl.TypeName)
                {
                    case "float":
                        return 1;
                    case "float2":
                        return 2;
                    case "float3":
                        return 3;
                }
            }
            return 4;
        }

        int GetRegisterFullLength(Instruction instruction, int paramIndex)
        {
            RegisterType registerType = instruction.GetParamRegisterType(paramIndex);
            int registerNumber = instruction.GetParamRegisterNumber(paramIndex);
            var decl = _registerDeclarations.FirstOrDefault(x => x.RegisterType == registerType && x.RegisterNumber == registerNumber);
            return GetRegisterFullLength(decl);
        }

        string GetDestinationName(Instruction instruction)
        {
            int destIndex = instruction.GetDestinationParamIndex();

            string registerName = GetRegisterName(instruction, destIndex);
            registerName = registerName ?? instruction.GetParamRegisterName(destIndex);
            int registerLength = GetRegisterFullLength(instruction, destIndex);
            string writeMaskName = instruction.GetDestinationWriteMaskName(registerLength, true);

            return string.Format("{0}{1}", registerName, writeMaskName);
        }

        string GetSourceConstantName(Instruction instruction, int srcIndex)
        {
            var registerType = instruction.GetParamRegisterType(srcIndex);
            int registerNumber = instruction.GetParamRegisterNumber(srcIndex);

            switch (registerType)
            {
                case RegisterType.ConstBool:
                    //throw new NotImplementedException();
                    return null;
                case RegisterType.ConstInt:
                    {
                        var constantInt = ConstantIntDefinitions.FirstOrDefault(x => x.RegisterIndex == registerNumber);
                        if (constantInt == null)
                        {
                            return null;
                        }
                        byte[] swizzle = instruction.GetSourceSwizzleComponents(srcIndex);
                        uint[] constant = {
                                constantInt[swizzle[0]],
                                constantInt[swizzle[1]],
                                constantInt[swizzle[2]],
                                constantInt[swizzle[3]] };

                        switch (instruction.GetSourceModifier(srcIndex))
                        {
                            case SourceModifier.None:
                                break;
                            case SourceModifier.Negate:
                                throw new NotImplementedException();
                                /*
                                for (int i = 0; i < 4; i++)
                                {
                                    constant[i] = -constant[i];
                                }*/
                                break;
                            default:
                                throw new NotImplementedException();
                        }

                        int destLength = instruction.GetDestinationMaskLength();
                        switch (destLength)
                        {
                            case 1:
                                return constant[0].ToString();
                            case 2:
                                if (constant[0] == constant[1])
                                {
                                    return constant[0].ToString();
                                }
                                return $"int2({constant[0]}, {constant[1]})";
                            case 3:
                                if (constant[0] == constant[1] && constant[0] == constant[2])
                                {
                                    return constant[0].ToString();
                                }
                                return $"int3({constant[0]}, {constant[1]}, {constant[2]})";
                            case 4:
                                if (constant[0] == constant[1] && constant[0] == constant[2] && constant[0] == constant[3])
                                {
                                    return constant[0].ToString();
                                }
                                return $"int4({constant[0]}, {constant[1]}, {constant[2]}, {constant[3]})";
                            default:
                                throw new InvalidOperationException();
                        }
                    }

                case RegisterType.Const:
                case RegisterType.Const2:
                case RegisterType.Const3:
                case RegisterType.Const4:
                    {
                        var constantRegister = ConstantDefinitions.FirstOrDefault(x => x.RegisterIndex == registerNumber);
                        if (constantRegister == null)
                        {
                            return null;
                        }

                        byte[] swizzle = instruction.GetSourceSwizzleComponents(srcIndex);
                        float[] constant = {
                            constantRegister[swizzle[0]],
                            constantRegister[swizzle[1]],
                            constantRegister[swizzle[2]],
                            constantRegister[swizzle[3]] };

                        switch (instruction.GetSourceModifier(srcIndex))
                        {
                            case SourceModifier.None:
                                break;
                            case SourceModifier.Negate:
                                for (int i = 0; i < 4; i++)
                                {
                                    constant[i] = -constant[i];
                                }
                                break;
                            default:
                                throw new NotImplementedException();
                        }

                        int destLength;
                        if (instruction.HasDestination)
                        {
                            destLength = instruction.GetDestinationMaskLength();
                        }
                        else
                        {
                            if (instruction.Opcode == Opcode.If ||instruction.Opcode == Opcode.IfC)
                            {
                                // TODO
                            }
                            destLength = 4;
                        }
                        switch (destLength)
                        {
                            case 1:
                                return constant[0].ToString(CultureInfo.InvariantCulture);
                            case 2:
                                if (constant[0] == constant[1])
                                {
                                    return constant[0].ToString(CultureInfo.InvariantCulture);
                                }
                                return string.Format("float2({0}, {1})",
                                    constant[0].ToString(CultureInfo.InvariantCulture),
                                    constant[1].ToString(CultureInfo.InvariantCulture));
                            case 3:
                                if (constant[0] == constant[1] && constant[0] == constant[2])
                                {
                                    return constant[0].ToString(CultureInfo.InvariantCulture);
                                }
                                return string.Format("float3({0}, {1}, {2})",
                                    constant[0].ToString(CultureInfo.InvariantCulture),
                                    constant[1].ToString(CultureInfo.InvariantCulture),
                                    constant[2].ToString(CultureInfo.InvariantCulture));
                            case 4:
                                if (constant[0] == constant[1] && constant[0] == constant[2] && constant[0] == constant[3])
                                {
                                    return constant[0].ToString(CultureInfo.InvariantCulture);
                                }
                                return string.Format("float4({0}, {1}, {2}, {3})",
                                    constant[0].ToString(CultureInfo.InvariantCulture),
                                    constant[1].ToString(CultureInfo.InvariantCulture),
                                    constant[2].ToString(CultureInfo.InvariantCulture),
                                    constant[3].ToString(CultureInfo.InvariantCulture));
                            default:
                                throw new InvalidOperationException();
                        }
                    }
                default:
                    return null;
            }
        }

        string GetSourceName(Instruction instruction, int srcIndex)
        {
            string sourceRegisterName;

            var registerType = instruction.GetParamRegisterType(srcIndex);
            switch (registerType)
            {
                case RegisterType.Const:
                case RegisterType.Const2:
                case RegisterType.Const3:
                case RegisterType.Const4:
                case RegisterType.ConstBool:
                case RegisterType.ConstInt:
                    sourceRegisterName = GetSourceConstantName(instruction, srcIndex);
                    if (sourceRegisterName != null)
                    {
                        return sourceRegisterName;
                    }

                    ParameterType parameterType;
                    switch (registerType)
                    {
                        case RegisterType.Const:
                        case RegisterType.Const2:
                        case RegisterType.Const3:
                        case RegisterType.Const4:
                            parameterType = ParameterType.Float;
                            break;
                        case RegisterType.ConstBool:
                            parameterType = ParameterType.Bool;
                            break;
                        case RegisterType.ConstInt:
                            parameterType = ParameterType.Int;
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                    int registerNumber = instruction.GetParamRegisterNumber(srcIndex);
                    var decl = ConstantDeclarations.FirstOrDefault(
                        x => x.ParameterType == parameterType && x.ContainsIndex(registerNumber));
                    if (decl == null)
                    {
                        // Constant register not found in def statements nor the constant table
                        throw new NotImplementedException();
                    }

                    if (decl.ParameterClass == ParameterClass.MatrixRows)
                    {
                        sourceRegisterName = string.Format("{0}[{1}]", decl.Name, registerNumber - decl.RegisterIndex);
                    }
                    else
                    {
                        sourceRegisterName = decl.Name;
                    }
                    break;
                default:
                    sourceRegisterName = GetRegisterName(instruction, srcIndex);
                    break;
            }

            sourceRegisterName = sourceRegisterName ?? instruction.GetParamRegisterName(srcIndex);

            sourceRegisterName += instruction.GetSourceSwizzleName(srcIndex);
            return ApplyModifier(instruction.GetSourceModifier(srcIndex), sourceRegisterName);
        }

        string GetTypeName(ConstantDeclaration declaration)
        {
            switch (declaration.ParameterClass)
            {
                case ParameterClass.Scalar:
                    return declaration.ParameterType.ToString().ToLower();
                case ParameterClass.Vector:
                    if (declaration.ParameterType == ParameterType.Float)
                    {
                        return "float" + declaration.Columns;
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                case ParameterClass.MatrixColumns:
                case ParameterClass.MatrixRows:
                    if (declaration.ParameterType == ParameterType.Float)
                    {
                        return $"float{declaration.Rows}x{declaration.Columns}";
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                case ParameterClass.Object:
                    switch (declaration.ParameterType)
                    {
                        case ParameterType.Sampler2D:
                        case ParameterType.Sampler3D:
                            return "sampler";
                        default:
                            throw new NotImplementedException();
                    }
            }
            throw new NotImplementedException();
        }

        void WriteInstruction(Instruction instruction)
        {
            switch (instruction.Opcode)
            {
                case Opcode.Abs:
                    WriteLine("{0} = abs({1});", GetDestinationName(instruction),
                        GetSourceName(instruction, 1));
                    break;
                case Opcode.Add:
                    WriteLine("{0} = {1} + {2};", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case Opcode.Cmp:
                    // TODO: should be per-component
                    WriteLine("{0} = ({1} >= 0) ? {2} : {3};", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                    break;
                case Opcode.Def:
                    var c = new Constant(
                        instruction.GetParamRegisterNumber(0),
                        instruction.GetParamSingle(1),
                        instruction.GetParamSingle(2),
                        instruction.GetParamSingle(3),
                        instruction.GetParamSingle(4));
                    ConstantDefinitions.Add(c);
                    break;
                case Opcode.DefI:
                    var ci = new ConstantInt(instruction.GetParamRegisterNumber(0),
                        instruction.Params[1],
                        instruction.Params[2],
                        instruction.Params[3],
                        instruction.Params[4]);
                    ConstantIntDefinitions.Add(ci);
                    break;
                case Opcode.DP2Add:
                    WriteLine("{0} = dot({1}, {2}) + {3};", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                    break;
                case Opcode.Dp3:
                    WriteLine("{0} = dot({1}, {2});", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case Opcode.Dp4:
                    WriteLine("{0} = dot({1}, {2});", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
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
                case Opcode.Exp:
                    WriteLine("{0} = exp2({1});", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case Opcode.Frc:
                    WriteLine("{0} = frac({1});", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case Opcode.If:
                    WriteLine("if ({0}) {{", GetSourceName(instruction, 0));
                    indent += "\t";
                    break;
                case Opcode.IfC:
                    if ((IfComparison)instruction.Modifier == IfComparison.GE &&
                        instruction.GetSourceModifier(0) == SourceModifier.AbsAndNegate &&
                        instruction.GetSourceModifier(1) == SourceModifier.Abs &&
                        instruction.GetParamRegisterName(0) + instruction.GetSourceSwizzleName(0) ==
                        instruction.GetParamRegisterName(1) + instruction.GetSourceSwizzleName(1))
                    {
                        WriteLine("if ({0} == 0) {{", instruction.GetParamRegisterName(0) + instruction.GetSourceSwizzleName(0));
                    }
                    else if ((IfComparison)instruction.Modifier == IfComparison.LT &&
                        instruction.GetSourceModifier(0) == SourceModifier.AbsAndNegate &&
                        instruction.GetSourceModifier(1) == SourceModifier.Abs &&
                        instruction.GetParamRegisterName(0) + instruction.GetSourceSwizzleName(0) ==
                        instruction.GetParamRegisterName(1) + instruction.GetSourceSwizzleName(1))
                    {
                        WriteLine("if ({0} != 0) {{", instruction.GetParamRegisterName(0) + instruction.GetSourceSwizzleName(0));
                    }
                    else
                    {
                        string ifComparison;
                        switch ((IfComparison)instruction.Modifier)
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
                    }
                    indent += "\t";
                    break;
                case Opcode.Log:
                    WriteLine("{0} = log2({1});", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case Opcode.Lrp:
                    WriteLine("{0} = lerp({2}, {3}, {1});", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                    break;
                case Opcode.Mad:
                    WriteLine("{0} = {1} * {2} + {3};", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                    break;
                case Opcode.Max:
                    WriteLine("{0} = max({1}, {2});", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case Opcode.Min:
                    WriteLine("{0} = min({1}, {2});", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case Opcode.Mov:
                    WriteLine("{0} = {1};", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case Opcode.MovA:
                    WriteLine("{0} = {1};", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case Opcode.Mul:
                    WriteLine("{0} = {1} * {2};", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case Opcode.Nrm:
                    WriteLine("{0} = normalize({1});", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case Opcode.Pow:
                    WriteLine("{0} = pow({1}, {2});", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case Opcode.Rcp:
                    WriteLine("{0} = 1 / {1};", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case Opcode.Rsq:
                    WriteLine("{0} = 1 / sqrt({1});", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case Opcode.Sge:
                    if (instruction.GetSourceModifier(1) == SourceModifier.AbsAndNegate &&
                        instruction.GetSourceModifier(2) == SourceModifier.Abs &&
                        instruction.GetParamRegisterName(1) + instruction.GetSourceSwizzleName(1) ==
                        instruction.GetParamRegisterName(2) + instruction.GetSourceSwizzleName(2))
                    {
                        WriteLine("{0} = ({1} == 0) ? 1 : 0;", GetDestinationName(instruction),
                            instruction.GetParamRegisterName(1) + instruction.GetSourceSwizzleName(1));
                    }
                    else
                    {
                        WriteLine("{0} = ({1} >= {2}) ? 1 : 0;", GetDestinationName(instruction), GetSourceName(instruction, 1),
                            GetSourceName(instruction, 2));
                    }
                    break;
                case Opcode.Slt:
                    WriteLine("{0} = ({1} < {2}) ? 1 : 0;", GetDestinationName(instruction), GetSourceName(instruction, 1),
                        GetSourceName(instruction, 2));
                    break;
                case Opcode.SinCos:
                    WriteLine("sincos({1}, {0}, {0});", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case Opcode.Sub:
                    WriteLine("{0} = {1} - {2};", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case Opcode.Tex:
                    if ((_shader.MajorVersion == 1 && _shader.MinorVersion >= 4) || (_shader.MajorVersion > 1))
                    {
                        WriteLine("{0} = tex2D({2}, {1});", GetDestinationName(instruction),
                            GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    }
                    else
                    {
                        WriteLine("{0} = tex2D();", GetDestinationName(instruction));
                    }
                    break;
                case Opcode.TexLDL:
                    WriteLine("{0} = tex2Dlod({2}, {1});", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case Opcode.Comment:
                case Opcode.End:
                    break;
            }
        }

        public void Write(string hlslFilename)
        {
            hlslFile = new FileStream(hlslFilename, FileMode.Create, FileAccess.Write);
            hlslWriter = new StreamWriter(hlslFile);

            ConstantDefinitions = new List<Constant>();
            ConstantIntDefinitions = new List<ConstantInt>();
            ConstantDeclarations = _shader.ParseConstantTable();

            ParseRegisterDeclarations();

            WriteConstantDeclarations();

            if (_inputRegisters.Count > 1)
            {
                WriteInputStructureDeclaration();
            }

            if (_outputRegisters.Count > 1)
            {
                WriteOutputStructureDeclaration();
            }

            string methodReturnType = GetMethodReturnType();
            string methodParameters = GetMethodParameters();
            string methodSemantic = GetMethodSemantic();
            WriteLine("{0} main({1}){2}", methodReturnType, methodParameters, methodSemantic);
            WriteLine("{");
            indent = "\t";

            HlslAst ast = null;
            if (doAstAnalysis)
            {
                var parser = new BytecodeParser();
                ast = parser.Parse(_shader);
                ast.ReduceTree();
            }
            if (ast != null)
            {
                WriteAst(ast);
            }
            else
            {
                WriteLine("{0} o;", methodReturnType);
                WriteLine();

                // Find all assignments to temporary variables
                // and declare the variables.
                var tempRegisters = new Dictionary<string, int>();
                foreach (Instruction instruction in _shader.Instructions)
                {
                    if (!instruction.HasDestination)
                    {
                        continue;
                    }

                    int destIndex = instruction.GetDestinationParamIndex();
                    if (instruction.GetParamRegisterType(destIndex) == RegisterType.Temp)
                    {
                        string registerName = instruction.GetParamRegisterName(destIndex);
                        if (!tempRegisters.ContainsKey(registerName))
                        {
                            tempRegisters.Add(registerName, 0);
                        }
                        tempRegisters[registerName] |= instruction.GetDestinationWriteMask();
                    }
                }

                foreach (var registerName in tempRegisters.Keys)
                {
                    int writeMask = tempRegisters[registerName];
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
                    WriteLine("{0} {1};", writeMaskName, registerName);
                }

                foreach (Instruction instruction in _shader.Instructions)
                {
                    WriteInstruction(instruction);
                }

                WriteLine();
                WriteLine("return o;");
            }
            indent = "";
            WriteLine("}");

            hlslWriter.Dispose();
            hlslFile.Dispose();
        }

        private void ParseRegisterDeclarations()
        {
            _registerDeclarations = new List<RegisterDeclaration>();
            _inputRegisters = new List<RegisterDeclaration>();
            _outputRegisters = new List<RegisterDeclaration>();
            foreach (var declInstruction in _shader.Instructions.Where(x => x.Opcode == Opcode.Dcl))
            {
                var reg = new RegisterDeclaration(declInstruction);
                _registerDeclarations.Add(reg);

                switch (reg.RegisterType)
                {
                    case RegisterType.Input:
                    case RegisterType.MiscType:
                        _inputRegisters.Add(reg);
                        break;
                    case RegisterType.Output:
                    case RegisterType.ColorOut:
                        _outputRegisters.Add(reg);
                        break;
                }
            }

            if (_shader.Type == ShaderType.Pixel)
            {
                // Find all assignments to color outputs, because these outputs are not declared.
                _pixelShaderOutputRegisters = new HashSet<string>();
                foreach (Instruction instruction in _shader.Instructions.Where(i => i.HasDestination))
                {
                    int destIndex = instruction.GetDestinationParamIndex();
                    if (instruction.GetParamRegisterType(destIndex) == RegisterType.ColorOut)
                    {
                        string registerName = "oC" + instruction.GetParamRegisterNumber(destIndex).ToString();
                        _pixelShaderOutputRegisters.Add(registerName);
                    }
                }
            }
        }

        private void WriteConstantDeclarations()
        {
            if (ConstantDeclarations.Count != 0)
            {
                foreach (ConstantDeclaration declaration in ConstantDeclarations)
                {
                    string typeName = GetTypeName(declaration);
                    WriteLine("{0} {1};", typeName, declaration.Name);
                }

                WriteLine();
            }
        }

        private void WriteInputStructureDeclaration()
        {
            if (_shader.Type == ShaderType.Pixel)
            {
                WriteLine("struct VS_OUT");
            }
            else
            {
                WriteLine("struct VS_IN");
            }
            WriteLine("{");
            indent = "\t";
            foreach (var dclInput in _inputRegisters)
            {
                WriteLine("{0} {1} : {2};",
                    dclInput.TypeName, dclInput.Name, dclInput.Semantic);
            }
            indent = "";
            WriteLine("};");
            WriteLine();
        }

        private void WriteOutputStructureDeclaration()
        {
            WriteLine("struct VS_OUT");
            WriteLine("{");
            indent = "\t";
            foreach (var output in _outputRegisters)
            {
                WriteLine($"{output.TypeName} {output.Name} : {output.Semantic};");
            }
            indent = "";
            WriteLine("};");
            WriteLine();
        }

        private string GetMethodReturnType()
        {
            if (_shader.Type == ShaderType.Pixel)
            {
                switch (_pixelShaderOutputRegisters.Count)
                {
                    case 0:
                        throw new InvalidOperationException();
                    case 1:
                        return "float4";
                    default:
                        return "PS_OUT";
                }
            }

            switch (_outputRegisters.Count)
            {
                case 0:
                    throw new InvalidOperationException();
                case 1:
                    return _outputRegisters[0].TypeName;
                default:
                    return "VS_OUT";
            }
        }

        private string GetMethodSemantic()
        {
            if (_shader.Type == ShaderType.Pixel)
            {
                switch (_pixelShaderOutputRegisters.Count)
                {
                    case 0:
                        throw new InvalidOperationException();
                    case 1:
                        return " : COLOR";
                    default:
                        return string.Empty;
                }
            }

            switch (_outputRegisters.Count)
            {
                case 0:
                    throw new InvalidOperationException();
                case 1:
                    string semantic = _outputRegisters[0].Semantic;
                    return $" : {semantic}";
                default:
                    return string.Empty;
            }
        }

        private string GetMethodParameters()
        {
            if (_inputRegisters.Count == 0)
            {
                return string.Empty;
            }
            else if (_inputRegisters.Count == 1)
            {
                var input = _inputRegisters[0];
                return $"{input.TypeName} {input.Name} : {input.Semantic}";
            }

            return _shader.Type == ShaderType.Pixel
                    ? "VS_OUT i"
                    : "VS_IN i";
        }

        private string GetAstSourceSwizzleName(IEnumerable<IHasComponentIndex> inputs, int registerSize)
        {
            string swizzleName = "";
            foreach (int swizzle in inputs.Select(i => i.ComponentIndex))
            {
                swizzleName += "xyzw"[swizzle];
            }

            if (swizzleName.Equals("xyzw".Substring(0, registerSize)))
            {
                return "";
            }

            if (swizzleName.Distinct().Count() == 1)
            {
                return "." + swizzleName.First();
            }

            return "." + swizzleName;
        }

        private void WriteAst(HlslAst ast)
        {
            var roots = ast.Roots.OrderBy(r => r.Key.ComponentIndex).Select(r => r.Value).ToList();

            string statement = Compile(roots);

            WriteLine($"return {statement};");

        }

        private string Compile(IEnumerable<HlslTreeNode> group)
        {
            List<HlslTreeNode> groupList = group.ToList();
            int componentCount = groupList.Count;

            var subGroups = GroupComponents(groupList);
            if (subGroups.Count == 0)
            {
                throw new InvalidOperationException();
            }

            if (subGroups.Count > 1)
            {
                // In float4(x, float), x cannot be promoted from float to float3
                // In float4(x, y), x cannot be promoted to float2 and y to float2
                var constructorParts = subGroups.Select(Compile);
                return $"float{componentCount}({string.Join(", ", constructorParts)})";
            }

            var first = group.First();

            if (first is ConstantNode constant)
            {
                return CompileConstant(constant);
            }

            if (first is Operation operation)
            {
                if (operation is AbsoluteOperation)
                {
                    return string.Format("abs({0})",
                        Compile(group.Select(g => g.Children[0])));
                }

                if (operation is AddOperation)
                {
                    return string.Format("{0} + {1}",
                        Compile(group.Select(g => g.Children[0])),
                        Compile(group.Select(g => g.Children[1])));
                }

                if (operation is CosineOperation)
                {
                    return string.Format("cos({0})",
                        Compile(group.Select(g => g.Children[0])));
                }

                if (operation is FractionalOperation)
                {
                    return string.Format("{0} - floor({0})",
                        Compile(group.Select(g => g.Children[0])));
                }

                if (operation is NegateOperation)
                {
                    return string.Format("-{0}",
                        Compile(group.Select(g => g.Children[0])));
                }

                if (operation is ReciprocalOperation)
                {
                    return string.Format("rcp({0})",
                        Compile(group.Select(g => g.Children[0])));
                }

                if (operation is SubtractOperation)
                {
                    return string.Format("{0} - {1}",
                        Compile(group.Select(g => g.Children[0])),
                        Compile(group.Select(g => g.Children[1])));
                }

                if (operation is MaximumOperation)
                {
                    var value1 = group.Select(g => g.Children[0]);
                    var value2 = group.Select(g => g.Children[1]);

                    return string.Format("max({0}, {1})",
                        Compile(value1),
                        Compile(value2));
                }

                if (operation is MinimumOperation)
                {
                    var value1 = group.Select(g => g.Children[0]);
                    var value2 = group.Select(g => g.Children[1]);

                    return string.Format("min({0}, {1})",
                        Compile(value1),
                        Compile(value2));
                }

                if (operation is MultiplyOperation)
                {
                    var multiplicand1 = group.Select(g => g.Children[0]);
                    var multiplicand2 = group.Select(g => g.Children[1]);

                    if (multiplicand2.First() is ConstantNode)
                    {
                        return string.Format("{0} * {1}",
                            Compile(multiplicand2),
                            Compile(multiplicand1));
                    }

                    return string.Format("{0} * {1}",
                        Compile(multiplicand1),
                        Compile(multiplicand2));
                }

                if (operation is ReciprocalSquareRootOperation)
                {
                    return string.Format("rsqrt({0})",
                        Compile(group.Select(g => g.Children[0])));
                }

                if (operation is SignGreaterOrEqualOperation)
                {
                    var value1 = group.Select(g => g.Children[0]);
                    var value2 = group.Select(g => g.Children[1]);
                    return $"({Compile(value1)} >= {Compile(value2)}) ? 1 : 0";
                }

                if (operation is SignLessOperation)
                {
                    var value1 = group.Select(g => g.Children[0]);
                    var value2 = group.Select(g => g.Children[1]);
                    return $"({Compile(value1)} < {Compile(value2)}) ? 1 : 0";
                }
            }


            if (first is IHasComponentIndex component)
            {
                var components = group.Cast<IHasComponentIndex>();

                if (first is RegisterInputNode shaderInput)
                {
                    RegisterType registerType = shaderInput.RegisterComponentKey.Type;
                    int registerNumber = shaderInput.RegisterComponentKey.Number;

                    string swizzle = "";
                    if (registerType != RegisterType.Sampler)
                    {
                        var decl = _registerDeclarations.FirstOrDefault(x =>
                                x.RegisterType == registerType &&
                                x.RegisterNumber == registerNumber);
                        swizzle = GetAstSourceSwizzleName(components, GetRegisterFullLength(decl));
                    }

                    string name = GetRegisterName(registerType, registerNumber);
                    return $"{name}{swizzle}";
                }

                if (first is TextureLoadOutputNode textureLoad)
                {
                    string swizzle = GetAstSourceSwizzleName(components, 4);

                    string sampler = Compile(new[] { textureLoad.SamplerInput });
                    string texcoords = Compile(textureLoad.TextureCoordinateInputs);
                    return $"tex2D({sampler}, {texcoords}){swizzle}";
                }

                if (first is DotProductOutputNode dotProductOutputNode)
                {
                    string input1 = Compile(dotProductOutputNode.Inputs1);
                    string input2 = Compile(dotProductOutputNode.Inputs2);
                    string swizzle = GetAstSourceSwizzleName(components, 4);
                    return $"dot({input1}, {input2}){swizzle}";
                }

                throw new NotImplementedException();
            }

            throw new NotImplementedException();
        }

        private static string CompileConstant(ConstantNode firstConstant)
        {
            return firstConstant.Value.ToString(CultureInfo.InvariantCulture);
        }

        private IList<IList<HlslTreeNode>> GroupComponents(List<HlslTreeNode> nodes)
        {
            switch (nodes.Count)
            {
                case 0:
                case 1:
                    return new List<IList<HlslTreeNode>> { nodes };
            }

            var groups = new List<IList<HlslTreeNode>>();
            int n, groupStart = 0;
            for (n = 1; n < nodes.Count; n++)
            {
                if (!CanGroupComponents(nodes[groupStart], nodes[n]))
                {
                    groups.Add(nodes.GetRange(groupStart, n - groupStart));
                    groupStart = n;
                }
            }
            groups.Add(nodes.GetRange(groupStart, n - groupStart));
            return groups;
        }

        private bool CanGroupComponents(HlslTreeNode node1, HlslTreeNode node2)
        {
            if (node1 is ConstantNode constant1 &&
                node2 is ConstantNode constant2)
            {
                return constant1.Value == constant2.Value;
            }

            if (node1 is RegisterInputNode input1 &&
                node2 is RegisterInputNode input2)
            {
                return input1.RegisterComponentKey.Type == input2.RegisterComponentKey.Type &&
                       input1.RegisterComponentKey.Number == input2.RegisterComponentKey.Number;
            }

            if (node1 is Operation operation1 &&
                node2 is Operation operation2)
            {
                if (operation1 is AddOperation add1 &&
                    operation2 is AddOperation add2)
                {
                    return add1.Children.Any(c1 => add2.Children.Any(c2 => CanGroupComponents(c1, c2)));
                }
                else if (
                    (operation1 is AbsoluteOperation && operation2 is AbsoluteOperation)
                    || (operation1 is CosineOperation && operation2 is CosineOperation)
                    || (operation1 is FractionalOperation && operation2 is FractionalOperation)
                    || (operation1 is NegateOperation && operation2 is NegateOperation)
                    || (operation1 is ReciprocalOperation && operation2 is ReciprocalOperation)
                    || (operation1 is ReciprocalSquareRootOperation && operation2 is ReciprocalSquareRootOperation))
                {
                    return CanGroupComponents(operation1.Children[0], operation2.Children[0]);
                }
                else if (
                    (operation1 is MinimumOperation && operation2 is MinimumOperation) ||
                    (operation1 is SignGreaterOrEqualOperation && operation2 is SignGreaterOrEqualOperation) ||
                    (operation1 is SignLessOperation && operation2 is SignLessOperation))
                {
                    return CanGroupComponents(operation1.Children[0], operation2.Children[0])
                        && CanGroupComponents(operation1.Children[1], operation2.Children[1]);
                }
                else if (operation1 is MultiplyOperation multiply1 &&
                    operation2 is MultiplyOperation multiply2)
                {
                    return multiply1.Children.Any(c1 => multiply2.Children.Any(c2 => CanGroupComponents(c1, c2)));
                }
                else if (operation1 is SubtractOperation subtract1 &&
                    operation2 is SubtractOperation subtract2)
                {
                    return subtract1.Subtrahend.Equals(subtract2.Subtrahend);
                }
            }

            if (node1 is IHasComponentIndex &&
                node2 is IHasComponentIndex)
            {
                if (node1.Children.Count == node2.Children.Count)
                {
                    for (int i = 0; i < node1.Children.Count; i++)
                    {
                        if (node1.Children[i].Equals(node2.Children[i]) == false)
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }

            return false;
        }
    }
}
