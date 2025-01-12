using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public sealed class RegisterState
    {
        public readonly bool ColumnMajorOrder = true;

        private readonly IDictionary<RegisterKey, RegisterDeclaration> _registerDeclarations = new Dictionary<RegisterKey, RegisterDeclaration>();

        public ICollection<ConstantRegister> ConstantDefinitions = new List<ConstantRegister>();
        public ICollection<ConstantIntRegister> ConstantIntDefinitions = new List<ConstantIntRegister>();
        public ICollection<ConstantDeclaration> ConstantDeclarations { get; } = new List<ConstantDeclaration>();
        public IDictionary<RegisterKey, HlslTreeNode> Samplers { get; } = new Dictionary<RegisterKey, HlslTreeNode>();

        public IDictionary<RegisterKey, RegisterDeclaration> MethodInputRegisters { get; } = new Dictionary<RegisterKey, RegisterDeclaration>();
        public IDictionary<RegisterKey, RegisterDeclaration> MethodOutputRegisters { get; } = new Dictionary<RegisterKey, RegisterDeclaration>();

        private ShaderModel _shaderModel;

        public RegisterState(ShaderModel shaderModel)
        {
            _shaderModel = shaderModel;
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
                    case RegisterType.MiscType:
                        return (MethodInputRegisters.Count == 1) ? decl.Name : ("i." + decl.Name);
                    case RegisterType.Output:
                    case RegisterType.ColorOut:
                    case RegisterType.DepthOut:
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
                    case RegisterType.Loop:
                        return "aL";
                    default:
                        throw new NotImplementedException();
                }
            }
            else if (registerKey is D3D10RegisterKey d3D10RegisterKey)
            {
                switch (d3D10RegisterKey.OperandType)
                {
                    case OperandType.ConstantBuffer:
                        return "constant" + d3D10RegisterKey.Number.ToString();
                    case OperandType.Immediate32:
                        return d3D10RegisterKey.Number.ToString();
                    case OperandType.Input:
                        var decl = _registerDeclarations[registerKey];
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
            return ConstantIntDefinitions.FirstOrDefault(c => c.RegisterIndex == index);
        }

        public void DeclareRegister(D3D9RegisterKey registerKey)
        {
            var registerDeclaration = CreateRegisterDeclarationFromRegisterKey(registerKey);
            _registerDeclarations.Add(registerKey, registerDeclaration);
        }


        public void DeclareRegister(D3D10RegisterKey registerKey)
        {
            var registerDeclaration = CreateRegisterDeclarationFromRegisterKey(registerKey);
            _registerDeclarations.Add(registerKey, registerDeclaration);
        }

        public void DeclareConstant(ConstantDeclaration constant)
        {
            ConstantDeclarations.Add(constant);

            if (constant.RegisterSet == RegisterSet.Sampler)
            {
                var registerKey = new D3D9RegisterKey(RegisterType.Sampler, constant.RegisterIndex);
                var destinationKey = new RegisterComponentKey(registerKey, 0);
                var shaderInput = new RegisterInputNode(destinationKey);
                Samplers.Add(registerKey, shaderInput);
            }
            else
            {
                var registerType = constant.RegisterSet switch
                {
                    RegisterSet.Bool => RegisterType.ConstBool,
                    RegisterSet.Float4 => RegisterType.Const,
                    RegisterSet.Int4 => RegisterType.Input,
                    _ => throw new InvalidOperationException(),
                };
                for (int r = 0; r < constant.RegisterCount; r++)
                {
                    var registerKey = new D3D9RegisterKey(registerType, constant.RegisterIndex + r);
                    DeclareRegister(registerKey);
                }
            }
        }

        public void DeclareDestinationRegister(D3D9Instruction instruction)
        {
            if (instruction.Opcode == Opcode.Dcl)
            {
                var registerKey = instruction.GetParamRegisterKey(instruction.GetDestinationParamIndex()) as D3D9RegisterKey;
                int maskedLength = GetMaskedLength(instruction.GetDestinationWriteMask());
                D3D9RegisterKey paramRegisterKey = (D3D9RegisterKey)instruction.GetParamRegisterKey(1);
                if (paramRegisterKey.Type == RegisterType.MiscType && paramRegisterKey.Number == 1)
                {
                    maskedLength = 1;
                }

                var registerDeclaration = new RegisterDeclaration(registerKey, instruction.GetDeclSemantic(), maskedLength);

                _registerDeclarations.Add(registerKey, registerDeclaration);

                switch (registerKey.Type)
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
            else if (instruction.Opcode == Opcode.Def)
            {
                var constant = new ConstantRegister(
                    instruction.GetParamRegisterNumber(0),
                    instruction.GetParamSingle(1),
                    instruction.GetParamSingle(2),
                    instruction.GetParamSingle(3),
                    instruction.GetParamSingle(4));
                ConstantDefinitions.Add(constant);
            }
            else if (instruction.Opcode == Opcode.DefI)
            {
                var constantInt = new ConstantIntRegister(instruction.GetParamRegisterNumber(0),
                    instruction.Params[1],
                    instruction.Params[2],
                    instruction.Params[3],
                    instruction.Params[4]);
                ConstantIntDefinitions.Add(constantInt);
            }
            else
            {
                int destIndex = instruction.GetDestinationParamIndex();
                RegisterType registerType = instruction.GetParamRegisterType(destIndex);

                if (registerType == RegisterType.ColorOut || registerType == RegisterType.DepthOut)
                {
                    int registerNumber = instruction.GetParamRegisterNumber(destIndex);
                    var registerKey = new D3D9RegisterKey(registerType, registerNumber);
                    if (MethodOutputRegisters.ContainsKey(registerKey) == false)
                    {
                        var reg = CreateRegisterDeclarationFromRegisterKey(registerKey);
                        MethodOutputRegisters[registerKey] = reg;

                        if (!_registerDeclarations.TryGetValue(registerKey, out _))
                        {
                            var registerDeclaration = CreateRegisterDeclarationFromRegisterKey(registerKey);
                            _registerDeclarations.Add(registerKey, registerDeclaration);
                        }
                    }
                }
                else if (registerType == RegisterType.Temp)
                {
                    int registerNumber = instruction.GetParamRegisterNumber(destIndex);
                    var registerKey = new D3D9RegisterKey(registerType, registerNumber);
                    if (!_registerDeclarations.TryGetValue(registerKey, out _))
                    {
                        var registerDeclaration = CreateRegisterDeclarationFromRegisterKey(registerKey);
                        _registerDeclarations.Add(registerKey, registerDeclaration);
                    }
                }
                else if (registerType == RegisterType.Addr)
                {
                    int registerNumber = instruction.GetParamRegisterNumber(destIndex);
                    var registerKey = new D3D9RegisterKey(registerType, registerNumber);
                    if (!_registerDeclarations.TryGetValue(registerKey, out _))
                    {
                        var registerDeclaration = CreateRegisterDeclarationFromRegisterKey(registerKey);
                        _registerDeclarations.Add(registerKey, registerDeclaration);
                    }
                }
            }
        }

        public void DeclareDestinationRegister(D3D10Instruction instruction)
        {
            if (instruction.Opcode == D3D10Opcode.DclInput ||
                instruction.Opcode == D3D10Opcode.DclInputPS ||
                instruction.Opcode == D3D10Opcode.DclInputPSSiv ||
                instruction.Opcode == D3D10Opcode.DclOutput)
            {
                var registerDeclaration = CreateRegisterDeclarationFromD3D10Dcl(instruction);
                D3D10RegisterKey registerKey = (D3D10RegisterKey)registerDeclaration.RegisterKey;

                _registerDeclarations.Add(registerKey, registerDeclaration);

                switch (registerKey.OperandType)
                {
                    case OperandType.Input:
                        MethodInputRegisters.Add(registerKey, registerDeclaration);
                        break;
                    case OperandType.Output:
                        MethodOutputRegisters.Add(registerKey, registerDeclaration);
                        break;
                }
            }
        }

        private static RegisterDeclaration CreateRegisterDeclarationFromRegisterKey(D3D9RegisterKey registerKey)
        {
            RegisterType type = registerKey.Type;
            switch (type)
            {
                case RegisterType.ColorOut:
                case RegisterType.DepthOut:
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

            string semantic;
            int maskedLength;
            if (type == RegisterType.DepthOut)
            {
                semantic = "DEPTH";
                maskedLength = 1;
            }
            else
            {
                semantic = "COLOR";
                if (registerKey.Number != 0)
                {
                    semantic += registerKey.Number;
                }
                maskedLength = 4;
            }

            return new RegisterDeclaration(registerKey, semantic, maskedLength);
        }

        private static RegisterDeclaration CreateRegisterDeclarationFromRegisterKey(D3D10RegisterKey registerKey)
        {
            string semantic = registerKey.Number == 0
                ? "SV_Target"
                : "SV_Target" + registerKey.Number;

            return new RegisterDeclaration(registerKey, semantic, 4);
        }

        private RegisterDeclaration CreateRegisterDeclarationFromD3D10Dcl(Instruction instruction)
        {
            RegisterKey registerKey = instruction.GetParamRegisterKey(instruction.GetDestinationParamIndex());

            string semantic = instruction.GetDeclSemantic();
            int maskedLength = 4;
            RegisterSignature signature = _shaderModel.InputSignatures
                .Concat(_shaderModel.OutputSignatures)
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
    }
}
