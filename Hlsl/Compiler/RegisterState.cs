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
        // A constant is as wide as it was declared, whichever set it lives in: an
        // `int count` fills a whole i# register but is still one component.
        if (registerKey is D3D9RegisterKey d3D9RegisterKey
            && (d3D9RegisterKey.Type == RegisterType.Const
                || d3D9RegisterKey.Type == RegisterType.ConstInt
                || d3D9RegisterKey.Type == RegisterType.ConstBool))
        {
            var constant = FindConstant(registerKey);
            if (constant != null)
            {
                return constant.TypeInfo.Columns;
            }
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
                return GetStructuredBufferComponents(registerKey);
            }
            if (d3D10RegisterKey.OperandType == OperandType.UnorderedAccessView
                && ResourceDefinitions.Any(r => r.ShaderInputType == D3DShaderInputType.UavRWStructured
                && r.BindPoint == registerKey.Number))
            {
                return GetStructuredBufferComponents(registerKey);
            }
        }
        throw new NotImplementedException();
    }

    // A DXBC constant buffer register holds 16 bytes, so several scalars can share
    // one. Naming by register alone cannot tell `float a, b` apart - both live in
    // cb0[0], at .x and .y - so the component decides which declaration is meant.
    // A struct constant occupies components of a register, so `s1.a` arrives as
    // c0.x. Names the member that component belongs to, rather than letting it be
    // written as a swizzle. Only scalar members are handled: naming a vector member
    // would need the swizzle rebased onto it, which the caller cannot express yet.
    public bool TryGetConstantMemberName(RegisterComponentKey registerComponentKey, out string name)
    {
        name = null;
        if (registerComponentKey.RegisterKey is not D3D9RegisterKey d3d9RegisterKey
            || d3d9RegisterKey.Type != RegisterType.Const)
        {
            return false;
        }

        ConstantDeclaration declaration = FindConstant(d3d9RegisterKey);
        if (declaration?.TypeInfo.MemberInfo == null)
        {
            return false;
        }

        int component = registerComponentKey.ComponentIndex
            + (d3d9RegisterKey.Number - declaration.RegisterIndex) * 4;
        int offset = 0;
        foreach (ShaderStructMemberInfo member in declaration.TypeInfo.MemberInfo)
        {
            int size = member.TypeInfo.Rows * member.TypeInfo.Columns;
            if (component < offset + size)
            {
                if (size != 1)
                {
                    return false;
                }
                name = declaration.Name + "." + member.Name;
                return true;
            }
            offset += size;
        }
        return false;
    }

    // `float4 arr[4]` spans four registers under one declaration, so the element
    // has to be named or every one of them reads as `arr`.
    private static string GetConstantBufferName(
        ConstantDeclaration declaration, D3D10RegisterKey registerKey)
    {
        if (declaration.TypeInfo.NumElements <= 1 || registerKey.ConstantBufferOffset == null)
        {
            return declaration.Name;
        }
        int variableRegister = declaration is D3D10ConstantDeclaration d3d10Declaration
            ? d3d10Declaration.VariableOffset / ConstantRegisterSizeInBytes
            : declaration.RegisterIndex;
        return $"{declaration.Name}[{registerKey.ConstantBufferOffset.Value - variableRegister}]";
    }

    public string GetRegisterName(RegisterComponentKey registerComponentKey)
    {
        if (registerComponentKey.RegisterKey is D3D10RegisterKey d3d10RegisterKey
            && d3d10RegisterKey.OperandType == OperandType.ConstantBuffer)
        {
            ConstantDeclaration declaration = FindConstant(
                d3d10RegisterKey, registerComponentKey.ComponentIndex);
            if (declaration != null && declaration.TypeInfo.Rows == 1)
            {
                return GetConstantBufferName(declaration, d3d10RegisterKey);
            }
        }
        return GetRegisterName(registerComponentKey.RegisterKey);
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
                        // Each element of `float4 m[4]` gets its own register, so the
                        // element has to be named - every one of them read as `m`.
                        if (constDecl.TypeInfo.NumElements > 1)
                        {
                            int element = registerKey.Number - constDecl.RegisterIndex;
                            return $"{constDecl.Name}[{element}]";
                        }
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
                        return GetConstantBufferName(declaration, d3d10RegisterKey);
                    }
                    // RegisterIndex is the cbuffer itself, b0, and is the same for
                    // every variable in it. The row is the register's distance from
                    // where this variable starts, which the reader records in bytes.
                    int variableRegister = declaration is D3D10ConstantDeclaration d3d10Declaration
                        ? d3d10Declaration.VariableOffset / ConstantRegisterSizeInBytes
                        : declaration.RegisterIndex;
                    int rowIndex = d3d10RegisterKey.ConstantBufferOffset.Value - variableRegister;
                    return ColumnMajorOrder
                        ? $"transpose({declaration.Name})[{rowIndex}]"
                        : $"{declaration.Name}[{rowIndex}]";
                case OperandType.Immediate32:
                    return d3d10RegisterKey.Number.ToString();
                case OperandType.Input:
                    var decl = RegisterDeclarations[registerKey];
                    // A geometry shader reads through the vertex array however few
                    // registers the input struct holds, so this comes before the
                    // single-input shortcut rather than after it.
                    if (d3d10RegisterKey.GSVertex.HasValue)
                    {
                        return $"i[{d3d10RegisterKey.GSVertex}].{decl.Name}";
                    }
                    if (MethodInputRegisters.Count == 1)
                    {
                        return decl.Name;
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
        {
            D3D9ConstantDeclaration d3D9ConstantDeclaration = (c as D3D9ConstantDeclaration);
            return d3D9ConstantDeclaration.RegisterSet == set &&
                d3D9ConstantDeclaration.ContainsIndex(index);
        });
    }

    public ConstantDeclaration FindConstant(RegisterKey registerKey)
    {
        if (registerKey is D3D10RegisterKey d3D10RegisterKey)
        {
            if (d3D10RegisterKey.OperandType == OperandType.ConstantBuffer)
            {
                int expectedOffset = (int)d3D10RegisterKey.ConstantBufferOffset * 4 * sizeof(float);
                ConstantDeclaration declaration = ConstantDeclarations.FirstOrDefault(d =>
                {
                    if (d.RegisterIndex != d3D10RegisterKey.Number)
                    {
                        return false;
                    }
                    var constant = d as D3D10ConstantDeclaration;
                    return constant.VariableOffset <= expectedOffset && expectedOffset < constant.VariableOffset + constant.VariableSize;
                });
                if (declaration == null)
                {
                    throw new InvalidOperationException();
                }
                return declaration;
            }
            return null;
        }
        return FindConstant(registerKey as D3D9RegisterKey);
    }

    // Byte offset of a single component within the constant buffer, rather than of
    // the whole register, so packed scalars resolve to the right declaration.
    public ConstantDeclaration FindConstant(D3D10RegisterKey registerKey, int componentIndex)
    {
        if (registerKey.OperandType != OperandType.ConstantBuffer
            || registerKey.ConstantBufferOffset == null)
        {
            return null;
        }

        int expectedOffset = registerKey.ConstantBufferOffset.Value * 4 * sizeof(float)
            + componentIndex * sizeof(float);
        return ConstantDeclarations.FirstOrDefault(d =>
            d.RegisterIndex == registerKey.Number
            && d is D3D10ConstantDeclaration constant
            && constant.VariableOffset <= expectedOffset
            && expectedOffset < constant.VariableOffset + constant.VariableSize);
    }

    public ConstantDeclaration FindConstant(D3D9RegisterKey registerKey)
    {
        RegisterSet? registerSet = registerKey.Type switch
        {
            RegisterType.Const => RegisterSet.Float4,
            RegisterType.ConstInt => RegisterSet.Int4,
            RegisterType.ConstBool => RegisterSet.Bool,
            RegisterType.Sampler => RegisterSet.Sampler,
            _ => null,
        };
        if (registerSet == null)
        {
            return null;
        }

        // Register numbers are per set: sampler s0 and constant c0 are different
        // registers that share an index, so the set has to match as well.
        return ConstantDeclarations.FirstOrDefault(c =>
            c is D3D9ConstantDeclaration declaration
            && declaration.RegisterSet == registerSet
            && declaration.ContainsIndex(registerKey.Number));
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


    private const int ConstantRegisterSizeInBytes = 4 * sizeof(float);

    public void DeclareRegister(D3D10RegisterKey registerKey, int writeMask)
    {
        if (registerKey.IsConstant)
        {
            // Several variables can share one 16-byte register - `float a, b` both
            // live in cb0[0], at .x and .y - so every declaration whose bytes fall in
            // the slot matters, not just the first. Matching on the declaration's
            // Offset does not work: the reader sets it to the variable's index within
            // the buffer, not to a register slot.
            foreach (D3D10ConstantDeclaration declaration in _shaderModel.ConstantDeclarations
                .Where(d => d.RegisterIndex == registerKey.Number
                    && d.VariableOffset / ConstantRegisterSizeInBytes == registerKey.ConstantBufferOffset))
            {
                if (!ConstantDeclarations.Contains(declaration))
                {
                    ConstantDeclarations.Add(declaration);
                }
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

    // How wide one element of a structured buffer is, in components. The
    // declaration gives it in bytes, so a StructuredBuffer<float4> fills a register
    // and a StructuredBuffer<float> is one component. Anything wider than a
    // register cannot be addressed as one, so it is capped there.
    private readonly Dictionary<RegisterKey, int> _structuredBufferComponents = [];

    // The declaration writer has a ResourceDefinition rather than a register key.
    public int GetStructuredBufferComponents(D3DShaderInputType shaderInputType, int bindPoint)
    {
        OperandType operandType = shaderInputType == D3DShaderInputType.UavRWStructured
            ? OperandType.UnorderedAccessView
            : OperandType.Resource;
        foreach (var entry in _structuredBufferComponents)
        {
            if (entry.Key is D3D10RegisterKey key
                && key.OperandType == operandType
                && key.Number == bindPoint)
            {
                return entry.Value;
            }
        }
        return 1;
    }

    public int GetStructuredBufferComponents(RegisterKey registerKey)
    {
        return _structuredBufferComponents.TryGetValue(registerKey, out int components)
            ? components
            : 1;
    }

    private void DeclareStructuredStride(RegisterKey registerKey, uint stride)
    {
        _structuredBufferComponents[registerKey] =
            Math.Clamp((int)stride / sizeof(float), 1, 4);
    }

    public void DeclareStructuredBuffer(D3D10RegisterKey registerKey, uint stride)
    {
        DeclareStructuredStride(registerKey, stride);
        ResourceDefinition definition = _shaderModel.ResourceDefinitions
            .Where(d => d.ShaderInputType == D3DShaderInputType.Structured)
            .FirstOrDefault(d => d.BindPoint == registerKey.Number);
        if (definition != null)
        {
            ResourceDefinitions.Add(definition);
        }
    }

    public void DeclareUnorderedAccessView(D3D10RegisterKey registerKey, uint stride)
    {
        DeclareStructuredStride(registerKey, stride);
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
                RegisterSet.Int4 => RegisterType.ConstInt,
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
            int destIndex = instruction.GetDestinationParamIndex().Value;
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

            // A ps_2_0 texture register is an input like any other; it just carries
            // its semantic in the register number.
            if (registerKey.Type == RegisterType.Input
                || registerKey.Type == RegisterType.MiscType
                || registerKey.Type == RegisterType.Texture)
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
            int destIndex = instruction.GetDestinationParamIndex().Value;
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
        // The _sgv forms declare system-generated values - vertex_id, instance_id,
        // is_front_face. Leaving them out meant they never reached the input
        // registers, so they went unnamed, undeclared and missing from the
        // signature, and fell back on the SV_Target default.
        if (instruction.Opcode == D3D10Opcode.DclInput ||
            instruction.Opcode == D3D10Opcode.DclInputPS ||
            instruction.Opcode == D3D10Opcode.DclInputPSSgv ||
            instruction.Opcode == D3D10Opcode.DclInputPSSiv ||
            instruction.Opcode == D3D10Opcode.DclInputSgv ||
            instruction.Opcode == D3D10Opcode.DclInputSiv ||
            instruction.Opcode == D3D10Opcode.DclOutput ||
            instruction.Opcode == D3D10Opcode.DclOutputSgv ||
            instruction.Opcode == D3D10Opcode.DclOutputSiv)
        {
            var registerKey = instruction.GetParamRegisterKey(instruction.GetDestinationParamIndex().Value);

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
            int destIndex = instruction.GetDestinationParamIndex().Value;
            DeclareRegisterWrite(instruction.GetParamRegisterKey(destIndex), instruction.GetDestinationWriteMask());
        }
    }

    public void DeclareRegisterWrite(D3D10RegisterKey registerKey, int writeMask)
    {
        if (RegisterDeclarations.TryGetValue(registerKey, out var existingDeclaration))
        {
            existingDeclaration.WriteMask |= writeMask;
        }
        else
        {
            var registerDeclaration = CreateRegisterDeclarationFromRegisterKey(registerKey, writeMask);
            RegisterDeclarations.Add(registerKey, registerDeclaration);
            if (registerKey.IsOutput)
            {
                MethodOutputRegisters.Add(registerDeclaration);
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
            return new RegisterDeclaration(registerKey, semantic, signature.Mask)
            {
                ComponentType = signature.ComponentType,
                InterpolationMode = instruction.GetInterpolationMode(),
            };
        }

        int writeMask = 4;
        return new RegisterDeclaration(registerKey, instruction.GetDeclSemantic(), writeMask);
    }
}
