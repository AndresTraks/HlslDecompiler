using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public sealed class RegisterState
    {
        public readonly bool ColumnMajorOrder = true;

        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

        private ICollection<ConstantRegister> _constantDefinitions { get; } = new List<ConstantRegister>();
        private ICollection<ConstantIntRegister> _constantIntDefinitions { get; } = new List<ConstantIntRegister>();
        private readonly IDictionary<RegisterKey, RegisterDeclaration> _registerDeclarations = new Dictionary<RegisterKey, RegisterDeclaration>();

        public RegisterState(ShaderModel shader)
        {
            Load(shader);
        }

        public ICollection<ConstantDeclaration> ConstantDeclarations { get; private set; }

        public IDictionary<RegisterKey, RegisterDeclaration> MethodInputRegisters { get; } = new Dictionary<RegisterKey, RegisterDeclaration>();
        public IDictionary<RegisterKey, RegisterDeclaration> MethodOutputRegisters { get; } = new Dictionary<RegisterKey, RegisterDeclaration>();

        public string GetDestinationName(Instruction instruction)
        {
            int destIndex = instruction.GetDestinationParamIndex();
            RegisterKey registerKey = instruction.GetParamRegisterKey(destIndex);

            string registerName = GetRegisterName(registerKey);
            registerName = registerName ?? instruction.GetParamRegisterName(destIndex);
            int registerLength = GetRegisterFullLength(registerKey);
            string writeMaskName = instruction.GetDestinationWriteMaskName(registerLength, true);

            return string.Format("{0}{1}", registerName, writeMaskName);
        }

        public string GetSourceName(D3D9Instruction instruction, int srcIndex)
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
                    ConstantDeclaration decl = FindConstant(parameterType, registerNumber);
                    if (decl == null)
                    {
                        // Constant register not found in def statements nor the constant table
                        throw new NotImplementedException();
                    }

                    if ((decl.ParameterClass == ParameterClass.MatrixRows && ColumnMajorOrder) ||
                        (decl.ParameterClass == ParameterClass.MatrixColumns && !ColumnMajorOrder))
                    {
                        int row = registerNumber - decl.RegisterIndex;
                        sourceRegisterName = $"{decl.Name}[{row}]";
                    }
                    else if ((decl.ParameterClass == ParameterClass.MatrixColumns && ColumnMajorOrder) ||
                        (decl.ParameterClass == ParameterClass.MatrixRows && !ColumnMajorOrder))
                    {
                        int column = registerNumber - decl.RegisterIndex;
                        sourceRegisterName = $"transpose({decl.Name})[{column}]";
                    }
                    else
                    {
                        sourceRegisterName = decl.Name;
                    }
                    break;
                default:
                    RegisterKey registerKey = instruction.GetParamRegisterKey(srcIndex);
                    sourceRegisterName = GetRegisterName(registerKey);
                    break;
            }

            sourceRegisterName = sourceRegisterName ?? instruction.GetParamRegisterName(srcIndex);

            sourceRegisterName += GetRelativeAddressingName(instruction, srcIndex);
            sourceRegisterName += instruction.GetSourceSwizzleName(srcIndex);
            return ApplyModifier(instruction.GetSourceModifier(srcIndex), sourceRegisterName);
        }

        private string GetRelativeAddressingName(Instruction instruction, int srcIndex)
        {
            if (instruction is D3D9Instruction d3D9Instruction  && d3D9Instruction.Params.HasRelativeAddressing(srcIndex))
            {
                return "[i]";
            }
            return string.Empty;
        }

        public int GetRegisterFullLength(RegisterKey registerKey)
        {
            if (registerKey is D3D9RegisterKey d3D9RegisterKey && d3D9RegisterKey.Type == RegisterType.Const)
            {
                var constant = FindConstant(ParameterType.Float, registerKey.Number);
                return constant.Columns;
            }

            RegisterDeclaration decl = _registerDeclarations[registerKey];
            switch (decl.TypeName)
            {
                case "float":
                    return 1;
                case "float2":
                    return 2;
                case "float3":
                    return 3;
                case "float4":
                    return 4;
                default:
                    throw new InvalidOperationException();
            }
        }

        public string GetRegisterName(RegisterKey registerKey)
        {
            if (registerKey is D3D9RegisterKey d3D9RegisterKey)
            {
                var decl = _registerDeclarations[registerKey];
                switch (d3D9RegisterKey.Type)
                {
                    case RegisterType.Texture:
                        return decl.Name;
                    case RegisterType.Input:
                        return (MethodInputRegisters.Count == 1) ? decl.Name : ("i." + decl.Name);
                    case RegisterType.Output:
                    case RegisterType.ColorOut:
                        return (MethodOutputRegisters.Count == 1) ? "o" : ("o." + decl.Name);
                    case RegisterType.Const:
                        var constDecl = FindConstant(ParameterType.Float, registerKey.Number);
                        if (constDecl.Rows == 1)
                        {
                            return constDecl.Name;
                        }
                        if (ColumnMajorOrder)
                        {
                            int column = registerKey.Number - constDecl.RegisterIndex;
                            return $"transpose({constDecl.Name})[{column}]";
                        }
                        string row = (registerKey.Number - constDecl.RegisterIndex).ToString();
                        return constDecl.Name + $"[{row}]";
                    case RegisterType.Temp:
                        return "r" + registerKey.Number;
                    case RegisterType.Sampler:
                        ConstantDeclaration samplerDecl = FindConstant(RegisterSet.Sampler, registerKey.Number);
                        if (samplerDecl != null)
                        {
                            return samplerDecl.Name;
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    case RegisterType.MiscType:
                        switch (registerKey.Number)
                        {
                            case 0:
                                return "vFace";
                            case 1:
                                return "vPos";
                            default:
                                throw new NotImplementedException();
                        }
                    case RegisterType.Loop:
                        return "aL";
                    default:
                        throw new NotImplementedException();
                }
            }
            else if (registerKey is D3D10RegisterKey d3D10RegisterKey)
            {
                var decl = _registerDeclarations[registerKey];
                switch (d3D10RegisterKey.OperandType)
                {
                    case OperandType.Input:
                        return (MethodInputRegisters.Count == 1) ? decl.Name : ("i." + decl.Name);
                    default:
                        throw new NotImplementedException();
                }
            }
            throw new NotImplementedException();
        }

        public ConstantDeclaration FindConstant(RegisterInputNode register)
        {
            if (register.RegisterComponentKey.RegisterKey is D3D9RegisterKey d3D9RegisterKey && d3D9RegisterKey.Type != RegisterType.Const)
            {
                return null;
            }
            return FindConstant(ParameterType.Float, register.RegisterComponentKey.Number);
        }

        public ConstantDeclaration FindConstant(RegisterSet set, int index)
        {
            return ConstantDeclarations.FirstOrDefault(c =>
                c.RegisterSet == set &&
                c.ContainsIndex(index));
        }

        public ConstantDeclaration FindConstant(ParameterType type, int index)
        {
            return ConstantDeclarations.FirstOrDefault(c =>
                c.ParameterType == type &&
                c.ContainsIndex(index));
        }

        public ConstantIntRegister FindConstantIntRegister(int index)
        {
            return _constantIntDefinitions.FirstOrDefault(c => c.RegisterIndex == index);
        }

        private void Load(ShaderModel shader)
        {
            ConstantDeclarations = shader.ParseConstantTable();
            foreach (var constantDeclaration in ConstantDeclarations)
            {
                RegisterType registerType;
                switch (constantDeclaration.RegisterSet)
                {
                    case RegisterSet.Bool:
                        registerType = RegisterType.ConstBool;
                        break;
                    case RegisterSet.Float4:
                        registerType = RegisterType.Const;
                        break;
                    case RegisterSet.Int4:
                        registerType = RegisterType.Input;
                        break;
                    case RegisterSet.Sampler:
                        registerType = RegisterType.Sampler;
                        break;
                    default:
                        throw new InvalidOperationException();
                }
                if (registerType == RegisterType.Sampler)
                {
                    // Use declaration from declaration instruction instead
                    continue;
                }
                for (int r = 0; r < constantDeclaration.RegisterCount; r++)
                {
                    var registerKey = new D3D9RegisterKey(registerType, constantDeclaration.RegisterIndex + r);
                    var registerDeclaration = CreateRegisterDeclarationFromD3D9RegistryKey(registerKey);
                    _registerDeclarations.Add(registerKey, registerDeclaration);
                }
            }

            if (shader.MajorVersion <= 3)
            {
                LoadD3D9State(shader);
            }
            else
            {
                LoadD3D10State(shader);
            }
        }

        private void LoadD3D9State(ShaderModel shader)
        {
            foreach (D3D9Instruction instruction in shader.Instructions)
            {
                if (!instruction.HasDestination)
                {
                    if (instruction.Opcode == Opcode.Loop)
                    {
                        D3D9RegisterKey registerKey = new D3D9RegisterKey(RegisterType.Loop, 0);
                        if (!_registerDeclarations.TryGetValue(registerKey, out _))
                        {
                            var registerDeclaration = CreateRegisterDeclarationFromD3D9RegistryKey(registerKey);
                            _registerDeclarations.Add(registerKey, registerDeclaration);
                        }
                    }
                    continue;
                }

                if (instruction.Opcode == Opcode.Dcl)
                {
                    RegisterDeclaration registerDeclaration = CreateRegisterDeclarationFromD3D9Dcl(instruction);
                    RegisterKey registerKey = registerDeclaration.RegisterKey;

                    _registerDeclarations.Add(registerKey, registerDeclaration);

                    if (registerKey is D3D9RegisterKey d3d9RegisterKey)
                    {
                        switch (d3d9RegisterKey.Type)
                        {
                            case RegisterType.Input:
                            case RegisterType.MiscType:
                                MethodInputRegisters.Add(registerKey, registerDeclaration);
                                break;
                            case RegisterType.Output:
                            case RegisterType.ColorOut:
                                MethodOutputRegisters.Add(registerKey, registerDeclaration);
                                break;
                        }
                    }
                    else if (registerKey is D3D10RegisterKey d3d10RegisterKey)
                    {
                        switch (d3d10RegisterKey.OperandType)
                        {
                            case OperandType.Output:
                                MethodOutputRegisters.Add(registerKey, registerDeclaration);
                                break;
                        }
                    }
                }
                else if (instruction.Opcode == Opcode.Def)
                {
                    var constant = new ConstantRegister(
                        instruction.GetParamRegisterNumber(0),
                        instruction.GetParamSingle(1),
                        instruction.GetParamSingle(2),
                        instruction.GetParamSingle(3),
                        instruction.GetParamSingle(4));
                    _constantDefinitions.Add(constant);
                }
                else if (instruction.Opcode == Opcode.DefI)
                {
                    var constantInt = new ConstantIntRegister(instruction.GetParamRegisterNumber(0),
                        instruction.Params[1],
                        instruction.Params[2],
                        instruction.Params[3],
                        instruction.Params[4]);
                    _constantIntDefinitions.Add(constantInt);
                }
                else
                {
                    int destIndex = instruction.GetDestinationParamIndex();
                    RegisterType registerType = instruction.GetParamRegisterType(destIndex);
                    // Find assignments to color outputs, since pixel shader outputs are not pre-declared
                    if (registerType == RegisterType.ColorOut)
                    {
                        if (shader.Type == ShaderType.Pixel)
                        {
                            int registerNumber = instruction.GetParamRegisterNumber(destIndex);
                            var registerKey = new D3D9RegisterKey(registerType, registerNumber);
                            if (MethodOutputRegisters.ContainsKey(registerKey) == false)
                            {
                                var reg = CreateRegisterDeclarationFromD3D9RegistryKey(registerKey);
                                MethodOutputRegisters[registerKey] = reg;

                                if (!_registerDeclarations.TryGetValue(registerKey, out _))
                                {
                                    var registerDeclaration = CreateRegisterDeclarationFromD3D9RegistryKey(registerKey);
                                    _registerDeclarations.Add(registerKey, registerDeclaration);
                                }
                            }
                        }
                    }
                    // Find assignments to temporary registers, since they are not pre-declared
                    else if (registerType == RegisterType.Temp)
                    {
                        int registerNumber = instruction.GetParamRegisterNumber(destIndex);
                        var registerKey = new D3D9RegisterKey(registerType, registerNumber);
                        if (!_registerDeclarations.TryGetValue(registerKey, out _))
                        {
                            var registerDeclaration = CreateRegisterDeclarationFromD3D9RegistryKey(registerKey);
                            _registerDeclarations.Add(registerKey, registerDeclaration);
                        }
                    }
                    else if (registerType == RegisterType.Addr)
                    {
                        int registerNumber = instruction.GetParamRegisterNumber(destIndex);
                        var registerKey = new D3D9RegisterKey(registerType, registerNumber);
                        if (!_registerDeclarations.TryGetValue(registerKey, out _))
                        {
                            var registerDeclaration = CreateRegisterDeclarationFromD3D9RegistryKey(registerKey);
                            _registerDeclarations.Add(registerKey, registerDeclaration);
                        }
                    }
                }
            }
        }

        private void LoadD3D10State(ShaderModel shader)
        {
            foreach (D3D10Instruction instruction in shader.Instructions)
            {
                if (!instruction.HasDestination)
                {
                    continue;
                }

                if (instruction.Opcode == D3D10Opcode.DclTemps)
                {
                    int numTemps = (int) instruction.GetParamInt(0);
                    for (int registerNumber = 0; registerNumber < numTemps; registerNumber++)
                    {
                        var registerKey = new D3D10RegisterKey(OperandType.Temp, registerNumber);
                        if (!_registerDeclarations.TryGetValue(registerKey, out _))
                        {
                            var registerDeclaration = CreateRegisterDeclarationFromD3D10RegistryKey(registerKey);
                            _registerDeclarations.Add(registerKey, registerDeclaration);
                        }
                    }
                }
                else if (instruction.Opcode == D3D10Opcode.DclOutput)
                {
                    var registerDeclaration = CreateRegisterDeclarationFromD3D10Dcl(instruction, shader);
                    D3D10RegisterKey registerKey = (D3D10RegisterKey)registerDeclaration.RegisterKey;

                    _registerDeclarations.Add(registerKey, registerDeclaration);

                    switch (registerKey.OperandType)
                    {
                        case OperandType.Output:
                            MethodOutputRegisters.Add(registerKey, registerDeclaration);
                            break;
                    }
                }
                else if (instruction.Opcode == D3D10Opcode.DclInputPS ||
                    instruction.Opcode == D3D10Opcode.DclInputPSSiv)
                {
                    var registerDeclaration = CreateRegisterDeclarationFromD3D10Dcl(instruction, shader);
                    D3D10RegisterKey registerKey = (D3D10RegisterKey)registerDeclaration.RegisterKey;

                    _registerDeclarations.Add(registerKey, registerDeclaration);

                    switch (registerKey.OperandType)
                    {
                        case OperandType.Input:
                            MethodInputRegisters.Add(registerKey, registerDeclaration);
                            break;
                    }
                }
                else
                {
                    int destIndex = instruction.GetDestinationParamIndex();
                    OperandType operandType = instruction.GetOperandType(destIndex);
                    // Find assignments to color outputs, since pixel shader outputs are not pre-declared
                    if (operandType == OperandType.Output)
                    {
                        if (shader.Type == ShaderType.Pixel)
                        {
                            int registerNumber = instruction.GetParamRegisterNumber(destIndex);
                            var registerKey = new D3D10RegisterKey(operandType, registerNumber);
                            if (MethodOutputRegisters.ContainsKey(registerKey) == false)
                            {
                                var reg = CreateRegisterDeclarationFromD3D10RegistryKey(registerKey);
                                MethodOutputRegisters[registerKey] = reg;

                                if (!_registerDeclarations.TryGetValue(registerKey, out _))
                                {
                                    var registerDeclaration = CreateRegisterDeclarationFromD3D10RegistryKey(registerKey);
                                    _registerDeclarations.Add(registerKey, registerDeclaration);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static RegisterDeclaration CreateRegisterDeclarationFromD3D9RegistryKey(D3D9RegisterKey registerKey)
        {
            RegisterType type = registerKey.Type;
            switch (type)
            {
                case RegisterType.ColorOut:
                case RegisterType.Const:
                case RegisterType.Const2:
                case RegisterType.Const3:
                case RegisterType.Const4:
                case RegisterType.ConstBool:
                case RegisterType.ConstInt:
                case RegisterType.Temp:
                case RegisterType.Loop:
                case RegisterType.Addr:
                    break;
                default:
                    throw new ArgumentException($"Register type {type} requires declaration instruction,", nameof(registerKey));
            }

            string semantic = registerKey.Number == 0
                ? "COLOR"
                : "COLOR" + registerKey.Number;

            return new RegisterDeclaration(registerKey, semantic, 4);
        }

        private static RegisterDeclaration CreateRegisterDeclarationFromD3D10RegistryKey(D3D10RegisterKey registerKey)
        {
            string semantic = registerKey.Number == 0
                ? "SV_Target"
                : "SV_Target" + registerKey.Number;

            return new RegisterDeclaration(registerKey, semantic, 4);
        }

        private static RegisterDeclaration CreateRegisterDeclarationFromD3D9Dcl(Instruction instruction)
        {
            RegisterKey registerKey = instruction.GetParamRegisterKey(instruction.GetDestinationParamIndex());
            int maskedLength = GetMaskedLength(instruction.GetDestinationWriteMask());
            return new RegisterDeclaration(registerKey, instruction.GetDeclSemantic(), maskedLength);
        }

        private static RegisterDeclaration CreateRegisterDeclarationFromD3D10Dcl(Instruction instruction, ShaderModel shaderModel)
        {
            RegisterKey registerKey = instruction.GetParamRegisterKey(instruction.GetDestinationParamIndex());

            string semantic = instruction.GetDeclSemantic();
            int maskedLength = 4;
            RegisterSignature signature = shaderModel.InputSignatures
                .Concat(shaderModel.OutputSignatures)
                .FirstOrDefault(i => i.RegisterKey.Equals(registerKey));
            if (signature != null)
            {
                semantic = signature.Name;
                if (signature.Index != 0)
                {
                    semantic += signature.Index;
                }
                maskedLength = GetMaskedLength(signature.Mask);
            }

            return new RegisterDeclaration(registerKey, semantic, maskedLength);
        }

        // Length of ".xy" = 2
        // Length of ".yw" = 4 (xyzw)
        public static int GetMaskedLength(int mask)
        {
            for (int i = 3; i >= 0; i--)
            {
                if ((mask & (1 << i)) != 0)
                {
                    return i + 1;
                }
            }
            return 0;
        }

        private string GetSourceConstantName(D3D9Instruction instruction, int srcIndex)
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
                        var constantInt = _constantIntDefinitions.FirstOrDefault(x => x.RegisterIndex == registerNumber);
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
                        var constantRegister = _constantDefinitions.FirstOrDefault(x => x.RegisterIndex == registerNumber);
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
                            if (instruction is D3D9Instruction d3D9Instruction
                                && (d3D9Instruction.Opcode == Opcode.If || d3D9Instruction.Opcode == Opcode.IfC))
                            {
                                // TODO
                            }
                            destLength = 4;
                        }
                        switch (destLength)
                        {
                            case 1:
                                return constant[0].ToString(_culture);
                            case 2:
                                if (constant[0] == constant[1])
                                {
                                    return constant[0].ToString(_culture);
                                }
                                return string.Format("float2({0}, {1})",
                                    constant[0].ToString(_culture),
                                    constant[1].ToString(_culture));
                            case 3:
                                if (constant[0] == constant[1] && constant[0] == constant[2])
                                {
                                    return constant[0].ToString(_culture);
                                }
                                return string.Format("float3({0}, {1}, {2})",
                                    constant[0].ToString(_culture),
                                    constant[1].ToString(_culture),
                                    constant[2].ToString(_culture));
                            case 4:
                                if (constant[0] == constant[1] && constant[0] == constant[2] && constant[0] == constant[3])
                                {
                                    return constant[0].ToString(_culture);
                                }
                                return string.Format("float4({0}, {1}, {2}, {3})",
                                    constant[0].ToString(_culture),
                                    constant[1].ToString(_culture),
                                    constant[2].ToString(_culture),
                                    constant[3].ToString(_culture));
                            default:
                                throw new InvalidOperationException();
                        }
                    }
                default:
                    return null;
            }
        }


        private static string ApplyModifier(SourceModifier modifier, string value)
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
    }
}
