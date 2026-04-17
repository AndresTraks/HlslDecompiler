using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public sealed class RegisterState
{
    public readonly bool ColumnMajorOrder = true;

    public ICollection<ConstantRegister> ConstantDefinitions = [];
    public ICollection<ConstantIntRegister> ConstantIntDefinitions = [];
    public ICollection<ConstantDeclaration> ConstantDeclarations { get; } = [];
    public ICollection<ResourceDefinition> ResourceDefinitions { get; } = [];
    public IDictionary<RegisterKey, RegisterInputNode> Samplers { get; } = new Dictionary<RegisterKey, RegisterInputNode>();

    public IDictionary<RegisterKey, RegisterDeclaration> RegisterDeclarations { get; } = new Dictionary<RegisterKey, RegisterDeclaration>();
    public IDictionary<RegisterKey, RegisterDeclaration> MethodInputRegisters { get; } = new Dictionary<RegisterKey, RegisterDeclaration>();
    public IList<RegisterDeclaration> MethodOutputRegisters = [];
    public int? MaxOutputVertexCount { get; set; }
    public int[]? NumThreads { get; set; }
    public D3D10Primitive? InputPrimitive { get; set; }
    public D3D10PrimitiveTopology? PrimitiveTopology { get; set; }

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

        if (RegisterDeclarations.TryGetValue(registerKey, out RegisterDeclaration registerDeclaration))
        {
            return RegisterDeclarations[registerKey].MaskedLength;
        }
        if (registerKey is D3D10RegisterKey d3D10RegisterKey)
        {
            if (d3D10RegisterKey.OperandType == OperandType.Resource
                && ResourceDefinitions.Any(r => r.ShaderInputType == D3DShaderInputType.Structured
                && r.BindPoint == registerKey.Number))
            {
                return 1;
            }
            if (d3D10RegisterKey.OperandType == OperandType.UnorderedAccessView
                && ResourceDefinitions.Any(r => r.ShaderInputType == D3DShaderInputType.UavRWStructured
                && r.BindPoint == registerKey.Number))
            {
                return 1;
            }
        }
        throw new NotImplementedException();
    }

    public string GetRegisterName(RegisterKey registerKey)
    {
        if (registerKey.IsOutput)
        {
            var decl = RegisterDeclarations[registerKey];
            return (MethodOutputRegisters.Count == 1) ? "o" : ("o." + decl.Name);
        }
        if (registerKey is D3D9RegisterKey d3D9RegisterKey)
        {
            var decl = RegisterDeclarations[registerKey];
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
        else if (registerKey is D3D10RegisterKey d3d10RegisterKey)
        {
            switch (d3d10RegisterKey.OperandType)
            {
                case OperandType.ConstantBuffer:
                    var declaration = FindConstant(registerKey);
                    if (declaration.TypeInfo.Rows == 1)
                    {
                        return declaration.Name;
                    }
                    if (ColumnMajorOrder)
                    {
                        int column = d3d10RegisterKey.ConstantBufferOffset.Value - declaration.RegisterIndex;
                        return $"transpose({declaration.Name})[{column}]";
                    }
                    string row = (registerKey.Number - declaration.RegisterIndex).ToString();
                    return declaration.Name + $"[{row}]";
                case OperandType.Immediate32:
                    return d3d10RegisterKey.Number.ToString();
                case OperandType.Input:
                    var decl = RegisterDeclarations[registerKey];
                    if (MethodInputRegisters.Count == 1)
                    {
                        return decl.Name;
                    }
                    if (d3d10RegisterKey.GSVertex.HasValue)
                    {
                        return $"i[{d3d10RegisterKey.GSVertex}].{decl.Name}";
                    }
                    return "i." + decl.Name;
                case OperandType.InputThreadID:
                    return RegisterDeclarations[registerKey].Name;
                case OperandType.Resource:
                    return ResourceDefinitions
                        .Where(d => d.ShaderInputType == D3DShaderInputType.Texture || d.ShaderInputType == D3DShaderInputType.Structured)
                        .First(d => d.BindPoint == registerKey.Number)
                        .Name;
                case OperandType.Sampler:
                    return ResourceDefinitions
                        .Where(d => d.ShaderInputType == D3DShaderInputType.Sampler)
                        .First(d => d.BindPoint == registerKey.Number)
                        .Name;
                case OperandType.Temp:
                    return "r" + registerKey.Number;
                case OperandType.UnorderedAccessView:
                    return ResourceDefinitions
                        .Where(d => d.ShaderInputType == D3DShaderInputType.UavRWStructured)
                        .First(d => d.BindPoint == registerKey.Number)
                        .Name;
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


    public void DeclareRegister(D3D10RegisterKey registerKey, int writeMask)
    {
        if (registerKey.IsConstant)
        {
            var declaration = _shaderModel.ConstantDeclarations.FirstOrDefault(d => d.RegisterIndex == registerKey.Number && d.Offset == registerKey.ConstantBufferOffset);
            if (declaration != null)
            {
                ConstantDeclarations.Add(declaration);
            }
        }
        else if (registerKey.OperandType == OperandType.Sampler)
        {
            var definition = _shaderModel.ResourceDefinitions
                .Where(d => d.ShaderInputType == D3DShaderInputType.Sampler)
                .FirstOrDefault(d => d.BindPoint == registerKey.Number);
            if (definition != null)
            {
                ResourceDefinitions.Add(definition);
            }
        }
        var registerDeclaration = CreateRegisterDeclarationFromRegisterKey(registerKey, writeMask);
        RegisterDeclarations.Add(registerKey, registerDeclaration);
    }

    public void DeclareResource(D3D10RegisterKey registerKey, ResourceDimension resourceDimension, int resourceReturnType)
    {
        ResourceDefinition definition = _shaderModel.ResourceDefinitions
            .Where(d => d.ShaderInputType == D3DShaderInputType.Texture)
            .FirstOrDefault(d => d.BindPoint == registerKey.Number);
        if (definition != null)
        {
            definition.Dimension = resourceDimension;
            ResourceDefinitions.Add(definition);
        }
    }

    public void DeclareStructuredBuffer(D3D10RegisterKey registerKey, uint stride)
    {
        ResourceDefinition definition = _shaderModel.ResourceDefinitions
            .Where(d => d.ShaderInputType == D3DShaderInputType.Structured)
            .FirstOrDefault(d => d.BindPoint == registerKey.Number);
        if (definition != null)
        {
            ResourceDefinitions.Add(definition);
        }
    }

    public void DeclareUnorderedAccessView(D3D10RegisterKey registerKey)
    {
        ResourceDefinition definition = _shaderModel.ResourceDefinitions
            .Where(d => d.ShaderInputType == D3DShaderInputType.UavRWStructured)
            .FirstOrDefault(d => d.BindPoint == registerKey.Number);
        if (definition != null)
        {
            ResourceDefinitions.Add(definition);
        }
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
            var registerKey = instruction.GetParamRegisterKey(destIndex);
            int writeMask = instruction.GetDestinationWriteMask();
            D3D9RegisterKey paramRegisterKey = instruction.GetParamRegisterKey(1);
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
                instruction.GetParamSingle(1)[0],
                instruction.GetParamSingle(2)[0],
                instruction.GetParamSingle(3)[0],
                instruction.GetParamSingle(4)[0]);
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
            var registerKey = instruction.GetParamRegisterKey(destIndex);

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
            instruction.Opcode == D3D10Opcode.DclInputSiv ||
            instruction.Opcode == D3D10Opcode.DclOutput)
        {
            var registerKey = instruction.GetParamRegisterKey(instruction.GetDestinationParamIndex());

            if (registerKey.GSVertex.HasValue)
            {
                for (int vertex = 0; vertex < registerKey.GSVertex.Value; vertex++)
                {
                    var vertexKey = D3D10RegisterKey.CreateGSInput(registerKey.Number, vertex);

                    if (RegisterDeclarations.TryGetValue(vertexKey, out var existingDeclaration))
                    {
                        existingDeclaration.WriteMask |= instruction.GetDestinationWriteMask();
                    }
                    else
                    {
                        var registerDeclaration = CreateRegisterDeclarationFromD3D10Dcl(instruction, vertexKey);
                        RegisterDeclarations.Add(vertexKey, registerDeclaration);
                        MethodInputRegisters.Add(vertexKey, registerDeclaration);
                    }
                }
            }
            else
            {
                if (RegisterDeclarations.TryGetValue(registerKey, out var existingDeclaration))
                {
                    existingDeclaration.WriteMask |= instruction.GetDestinationWriteMask();
                }
                else
                {
                    var registerDeclaration = CreateRegisterDeclarationFromD3D10Dcl(instruction, registerKey);
                    RegisterDeclarations.Add(registerKey, registerDeclaration);

                    switch (registerKey.OperandType)
                    {
                        case OperandType.Input:
                        case OperandType.InputThreadID:
                            MethodInputRegisters.Add(registerKey, registerDeclaration);
                            break;
                        case OperandType.Output:
                            MethodOutputRegisters.Add(registerDeclaration);
                            break;
                    }
                }
            }
        }
        else
        {
            int destIndex = instruction.GetDestinationParamIndex();
            var registerKey = instruction.GetParamRegisterKey(destIndex);

            if (RegisterDeclarations.TryGetValue(registerKey, out var existingDeclaration))
            {
                existingDeclaration.WriteMask |= instruction.GetDestinationWriteMask();
            }
            else
            {
                var registerDeclaration = CreateRegisterDeclarationFromRegisterKey(
                    registerKey,
                    instruction.GetDestinationWriteMask());
                RegisterDeclarations.Add(registerKey, registerDeclaration);
                if (registerKey.IsOutput)
                {
                    MethodOutputRegisters.Add(registerDeclaration);
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

    private RegisterDeclaration CreateRegisterDeclarationFromRegisterKey(D3D10RegisterKey registerKey, int writeMask)
    {
        string semantic = registerKey.Number == 0
            ? "SV_Target"
            : "SV_Target" + registerKey.Number;

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

    private RegisterDeclaration CreateRegisterDeclarationFromD3D10Dcl(D3D10Instruction instruction, D3D10RegisterKey registerKey)
    {
        registerKey = registerKey.GetGSBaseKey();
        RegisterSignature signature = _shaderModel.InputSignatures
            .Concat(_shaderModel.OutputSignatures)
            .FirstOrDefault(i => i.RegisterKey.Equals(registerKey));
        if (signature != null)
        {
            string semantic = signature.Name;
            if (signature.Index != 0)
            {
                semantic += signature.Index;
            }
            return new RegisterDeclaration(registerKey, semantic, signature.Mask);
        }

        int writeMask = 4;
        return new RegisterDeclaration(registerKey, instruction.GetDeclSemantic(), writeMask);
    }
}
