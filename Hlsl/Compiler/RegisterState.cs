using HlslDecompiler.DirectXShaderModel;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HlslDecompiler.Hlsl
{
    public sealed class RegisterState
    {
        public readonly bool ColumnMajorOrder = true;

        public ICollection<ConstantRegister> ConstantDefinitions = new List<ConstantRegister>();
        public ICollection<ConstantIntRegister> ConstantIntDefinitions = new List<ConstantIntRegister>();
        public ICollection<ConstantDeclaration> ConstantDeclarations { get; } = new List<ConstantDeclaration>();
        public IDictionary<RegisterKey, RegisterInputNode> Samplers { get; } = new Dictionary<RegisterKey, RegisterInputNode>();

        public IDictionary<RegisterKey, RegisterDeclaration> RegisterDeclarations { get; } = new Dictionary<RegisterKey, RegisterDeclaration>();
        public IDictionary<RegisterKey, RegisterDeclaration> MethodInputRegisters { get; } = new Dictionary<RegisterKey, RegisterDeclaration>();
        public IList<RegisterDeclaration> MethodOutputRegisters = [];

        private ShaderModel _shaderModel;

        public RegisterState(ShaderModel shaderModel)
        {
            _shaderModel = shaderModel;
        }

        public int GetRegisterMaskedLength(RegisterKey registerKey)
        {
            if (registerKey is D3D9RegisterKey d3D9RegisterKey && d3D9RegisterKey.Type == RegisterType.Const)
            {
                var constant = FindConstant(registerKey);
                return constant.TypeInfo.Columns;
            }

            return RegisterDeclarations[registerKey].MaskedLength;
        }

        public string GetRegisterName(RegisterKey registerKey)
        {
            var decl = RegisterDeclarations[registerKey];
            if (registerKey.IsOutput)
            {
                return (MethodOutputRegisters.Count == 1) ? "o" : ("o." + decl.Name);
            }
            if (registerKey is D3D9RegisterKey d3D9RegisterKey)
            {
                switch (d3D9RegisterKey.Type)
                {
                    case RegisterType.Texture:
                        return decl.Name;
                    case RegisterType.Input:
                    case RegisterType.MiscType:
                        return (MethodInputRegisters.Count == 1) ? decl.Name : ("i." + decl.Name);
                    case RegisterType.Const:
                    case RegisterType.ConstInt:
                    case RegisterType.ConstBool:
                        var constDecl = FindConstant(registerKey);
                        if (constDecl.TypeInfo.Rows == 1)
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
                        ConstantDeclaration samplerDecl = FindConstant(registerKey);
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
                        var declaration = FindConstant(registerKey);
                        return declaration.Name;
                    case OperandType.Immediate32:
                        return d3D10RegisterKey.Number.ToString();
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
            return FindConstant(register.RegisterComponentKey.RegisterKey);
        }

        public ConstantDeclaration FindConstant(RegisterSet set, int index)
        {
            return ConstantDeclarations.FirstOrDefault(c =>
                (c as D3D9ConstantDeclaration).RegisterSet == set &&
                c.ContainsIndex(index));
        }

        public ConstantDeclaration FindConstant(RegisterKey registerKey)
        {
            if (registerKey is D3D10RegisterKey d3D10RegisterKey)
            {
                if (d3D10RegisterKey.OperandType == OperandType.ConstantBuffer)
                {
                    ConstantDeclaration declaration = ConstantDeclarations.FirstOrDefault(d => d.RegisterIndex == d3D10RegisterKey.Number
                        && (d as D3D10ConstantDeclaration).Offset == d3D10RegisterKey.ConstantBufferOffset);
                    if (declaration == null)
                    {
                        declaration = ConstantDeclarations.FirstOrDefault(d => d.RegisterIndex == d3D10RegisterKey.Number);
                    }
                    return declaration;
                }
                return null;
            }
            return FindConstant(registerKey as D3D9RegisterKey);
        }

        public ConstantDeclaration FindConstant(D3D9RegisterKey registerKey)
        {
            if (registerKey.Type == RegisterType.Const || registerKey.Type == RegisterType.Sampler)
            {
                return ConstantDeclarations.FirstOrDefault(c => c.ContainsIndex(registerKey.Number));
            }
            return null;
        }

        public ConstantIntRegister FindConstantIntRegister(int index)
        {
            return ConstantIntDefinitions.FirstOrDefault(c => c.RegisterIndex == index);
        }

        public void DeclareRegister(D3D9RegisterKey registerKey, int writeMask)
        {
            var registerDeclaration = CreateRegisterDeclarationFromRegisterKey(registerKey, ResultModifier.None, writeMask);
            RegisterDeclarations.Add(registerKey, registerDeclaration);
        }


        public void DeclareRegister(D3D10RegisterKey registerKey)
        {
            if (registerKey.IsConstant)
            {
                var declaration = _shaderModel.ConstantDeclarations.FirstOrDefault(d => d.RegisterIndex == registerKey.Number && d.Offset == registerKey.ConstantBufferOffset);
                if (declaration != null)
                {
                    ConstantDeclarations.Add(declaration);
                }
            }
            var registerDeclaration = CreateRegisterDeclarationFromRegisterKey(registerKey);
            RegisterDeclarations.Add(registerKey, registerDeclaration);
        }

        public void DeclareConstant(D3D9ConstantDeclaration constant)
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
                    int writeMask = 0xF;
                    DeclareRegister(registerKey, writeMask);
                }
            }
        }

        public void DeclareDestinationRegister(D3D9Instruction instruction)
        {
            if (instruction.Opcode == Opcode.Dcl)
            {
                int destIndex = instruction.GetDestinationParamIndex();
                var registerKey = instruction.GetParamRegisterKey(destIndex) as D3D9RegisterKey;
                int writeMask = instruction.GetDestinationWriteMask();
                D3D9RegisterKey paramRegisterKey = (D3D9RegisterKey)instruction.GetParamRegisterKey(1);
                if (paramRegisterKey.Type == RegisterType.MiscType && paramRegisterKey.Number == 1)
                {
                    writeMask = 1;
                }

                var registerDeclaration = new RegisterDeclaration(registerKey,
                    instruction.GetDeclSemantic(),
                    writeMask,
                    instruction.GetDestinationResultModifier());
                RegisterDeclarations.Add(registerKey, registerDeclaration);

                if (registerKey.Type == RegisterType.Input || registerKey.Type == RegisterType.MiscType)
                {
                    MethodInputRegisters.Add(registerKey, registerDeclaration);
                }
                else if (registerKey.IsOutput)
                {
                    MethodOutputRegisters.Add(registerDeclaration);
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
                var registerKey = instruction.GetParamRegisterKey(destIndex) as D3D9RegisterKey;

                if (RegisterDeclarations.TryGetValue(registerKey, out var existingDeclaration))
                {
                    existingDeclaration.WriteMask |= instruction.GetDestinationWriteMask();
                }
                else
                {
                    var registerDeclaration = CreateRegisterDeclarationFromRegisterKey(
                        registerKey,
                        instruction.GetDestinationResultModifier(),
                        instruction.GetDestinationWriteMask());
                    RegisterDeclarations.Add(registerKey, registerDeclaration);
                    if (registerKey.IsOutput)
                    {
                        MethodOutputRegisters.Add(registerDeclaration);
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
                var registerKey = instruction.GetParamRegisterKey(instruction.GetDestinationParamIndex()) as D3D10RegisterKey;

                if (RegisterDeclarations.TryGetValue(registerKey, out var existingDeclaration))
                {
                    existingDeclaration.WriteMask |= instruction.GetDestinationWriteMask();
                }
                else
                {
                    var registerDeclaration = CreateRegisterDeclarationFromD3D10Dcl(instruction);
                    RegisterDeclarations.Add(registerKey, registerDeclaration);

                    switch (registerKey.OperandType)
                    {
                        case OperandType.Input:
                            MethodInputRegisters.Add(registerKey, registerDeclaration);
                            break;
                        case OperandType.Output:
                            MethodOutputRegisters.Add(registerDeclaration);
                            break;
                    }
                }
            }
        }

        private static RegisterDeclaration CreateRegisterDeclarationFromRegisterKey(D3D9RegisterKey registerKey, ResultModifier resultModifier, int writeMask)
        {
            RegisterType type = registerKey.Type;
            switch (type)
            {
                case RegisterType.ColorOut:
                case RegisterType.DepthOut:
                case RegisterType.Output:
                case RegisterType.RastOut:
                case RegisterType.AttrOut:
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
            if (type == RegisterType.DepthOut)
            {
                semantic = "DEPTH";
                writeMask = 1;
            }
            else if (type == RegisterType.RastOut)
            {
                switch (registerKey.Number)
                {
                    case 0:
                        semantic = "POSITION";
                        break;
                    case 1:
                        semantic = "FOG";
                        writeMask = 1;
                        break;
                    case 2:
                        semantic = "PSIZE";
                        writeMask = 1;
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
            else
            {
                semantic = type == RegisterType.Output ? "TEXCOORD" : "COLOR";
                if (registerKey.Number != 0)
                {
                    semantic += registerKey.Number;
                }
            }

            return new RegisterDeclaration(registerKey, semantic, writeMask, resultModifier);
        }

        private RegisterDeclaration CreateRegisterDeclarationFromRegisterKey(D3D10RegisterKey registerKey)
        {
            string semantic = registerKey.Number == 0
                ? "SV_Target"
                : "SV_Target" + registerKey.Number;

            int writeMask = 0xF;
            if (registerKey.OperandType == OperandType.ConstantBuffer)
            {
                ConstantDeclaration declaration = FindConstant(registerKey);
                if (declaration != null)
                {
                    writeMask = 0;
                    int maskedLength = declaration.TypeInfo.Rows * declaration.TypeInfo.Columns;
                    for (int i = 0; i < maskedLength; i++)
                    {
                        writeMask |= 1 << i;
                    }
                }
            }

            return new RegisterDeclaration(registerKey, semantic, writeMask);
        }

        private RegisterDeclaration CreateRegisterDeclarationFromD3D10Dcl(Instruction instruction)
        {
            RegisterKey registerKey = instruction.GetParamRegisterKey(instruction.GetDestinationParamIndex());

            string semantic = instruction.GetDeclSemantic();
            int writeMask = 4;
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
                writeMask = signature.Mask;
            }

            return new RegisterDeclaration(registerKey, semantic, writeMask);
        }
    }
}
