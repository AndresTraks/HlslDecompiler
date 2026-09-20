using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public sealed class RegisterState
{

    public ICollection<ConstantRegister> ConstantDefinitions = [];
    /// <summary>
    /// The rows of an immediate constant buffer, which fxc emits for a small
    /// constant array it would rather index than unroll. Nothing names it, so it is
    /// called icb as the disassembly calls it.
    /// </summary>
    public IList<ConstantRegister> ImmediateConstantBuffer { get; } = [];
    private readonly HashSet<int> _indexedConstants = [];
    private List<ConstantArray> _constantArrays;
    public ICollection<ConstantIntRegister> ConstantIntDefinitions = [];
    public ICollection<ConstantDeclaration> ConstantDeclarations { get; } = [];
    public ICollection<ResourceDefinition> ResourceDefinitions { get; } = [];

    /// <summary>
    /// A structured buffer whose element is a matrix is loaded a row at a time, and
    /// the byte offset of the load says which row: `ld_structured ..., l(48), t0` is
    /// the fourth of a float4x4. Dropped, every row of an instance transform read as
    /// the first one.
    ///
    /// Which way round a row is stored is the same question a constant buffer
    /// matrix asks, and is answered the same way. A struct element would have the
    /// offset pick a member, which is not read yet.
    /// </summary>
    /// <summary>The structured buffer a load or a store names, or null.</summary>
    public ResourceDefinition FindStructuredBuffer(RegisterKey resourceKey)
    {
        // By what kind of register it is as well as by its number. Groupshared
        // memory is g0 and a structured buffer t0, both number zero, so a load
        // from the first was finding the element type of the second and naming
        // members of a float4 array.
        D3DShaderInputType inputType = (resourceKey as D3D10RegisterKey)?.OperandType switch
        {
            OperandType.Resource => D3DShaderInputType.Structured,
            OperandType.UnorderedAccessView => D3DShaderInputType.UavRWStructured,
            _ => (D3DShaderInputType)(-1),
        };
        return ResourceDefinitions.FirstOrDefault(r =>
            r.ShaderInputType == inputType && r.BindPoint == resourceKey.Number);
    }

    /// <summary>
    /// Whether the resource is addressed by a byte offset alone rather than by an
    /// element and an offset within it - a ByteAddressBuffer either way round. An
    /// atomic does not say which kind it is in its opcode, unlike a store, so what
    /// the address operand means has to be read off the declaration.
    /// </summary>
    public bool IsRawResource(RegisterKey resourceKey)
    {
        D3DShaderInputType rawType = (resourceKey as D3D10RegisterKey)?.OperandType switch
        {
            OperandType.Resource => D3DShaderInputType.ByteAddress,
            OperandType.UnorderedAccessView => D3DShaderInputType.UavRWByteAddress,
            _ => (D3DShaderInputType)(-1),
        };
        return ResourceDefinitions.Any(r =>
            r.ShaderInputType == rawType && r.BindPoint == resourceKey.Number);
    }

    /// <summary>
    /// Names the members a load or a store reaches. An element that is a struct is
    /// addressed by a byte offset and nothing else, so `ld_structured ..., l(0), u0`
    /// over a struct of a float3 and a float reads two members at once and has to be
    /// written as both. Null where the element is not a struct.
    /// </summary>
    /// <param name="components">Which components of the element are read, in the
    /// order they are written.</param>
    public string NameStructuredMembers(
        RegisterKey resourceKey, string element, int byteOffset, IList<int> components)
    {
        IList<ShaderStructMemberInfo> members = FindStructuredBuffer(resourceKey)?.ElementType?.MemberInfo;
        if (members == null || members.Count == 0 || components.Count == 0)
        {
            return null;
        }

        IList<(string Name, int[] Values)> runs = FindStructuredMemberRuns(resourceKey, byteOffset, components);
        if (runs == null)
        {
            return null;
        }
        List<string> named = [.. runs.Select(r => $"{element}.{r.Name}")];
        return named.Count == 1
            ? named[0]
            : $"float{components.Count}({string.Join(", ", named)})";
    }

    /// <summary>
    /// The members a load or a store reaches, each named with the swizzle that
    /// selects the part of it touched, and with which of the components handed in
    /// go there. One run per member, in the order the components come. Null where
    /// the element is not a struct.
    /// </summary>
    public IList<(string Name, int[] Values)> FindStructuredMemberRuns(
        RegisterKey resourceKey, int byteOffset, IList<int> components)
    {
        IList<ShaderStructMemberInfo> members = FindStructuredBuffer(resourceKey)?.ElementType?.MemberInfo;
        if (members == null || members.Count == 0 || components.Count == 0)
        {
            return null;
        }

        var runs = new List<(ShaderStructMemberInfo Member, List<int> InMember, List<int> Values)>();
        for (int i = 0; i < components.Count; i++)
        {
            ShaderStructMemberInfo member = FindStructuredMember(members, byteOffset + components[i] * 4);
            if (member == null)
            {
                return null;
            }
            int inMember = (byteOffset + components[i] * 4 - member.ByteOffset) / 4;
            if (runs.Count != 0 && ReferenceEquals(runs[^1].Member, member))
            {
                runs[^1].InMember.Add(inMember);
                runs[^1].Values.Add(i);
            }
            else
            {
                runs.Add((member, [inMember], [i]));
            }
        }

        return [.. runs.Select(run =>
        {
            int width = run.Member.TypeInfo.Columns;
            string swizzle = width > 1 && !(run.InMember.Count == width
                    && run.InMember.SequenceEqual(Enumerable.Range(0, width)))
                ? "." + string.Concat(run.InMember.Select(c => "xyzw"[c]))
                : "";
            return (run.Member.Name + swizzle, run.Values.ToArray());
        })];
    }

    private static ShaderStructMemberInfo FindStructuredMember(
        IList<ShaderStructMemberInfo> members, int byteAddress)
    {
        foreach (ShaderStructMemberInfo member in members)
        {
            int size = member.TypeInfo.Rows > 1
                ? member.TypeInfo.Rows * 16
                : member.TypeInfo.Columns * 4;
            if (byteAddress >= member.ByteOffset && byteAddress < member.ByteOffset + size)
            {
                return member;
            }
        }
        return null;
    }

    public string ApplyStructuredElementRow(RegisterKey resourceKey, string element, int byteOffset)
    {
        ResourceDefinition resource = FindStructuredBuffer(resourceKey);
        if (resource?.ElementType == null || resource.ElementType.Rows <= 1)
        {
            return element;
        }
        const int BytesPerRow = 16;
        string matrix = $"transpose({element})";
        return $"{matrix}[{byteOffset / BytesPerRow}]";
    }
    public IDictionary<RegisterKey, RegisterInputNode> Samplers { get; } = new Dictionary<RegisterKey, RegisterInputNode>();

    public IDictionary<RegisterKey, RegisterDeclaration> RegisterDeclarations { get; } = new Dictionary<RegisterKey, RegisterDeclaration>();

    // A list rather than a dictionary keyed by register: fxc packs interpolators as
    // tightly as constants, so two differently named inputs - TEXCOORD0 at v2.xy and
    // TEXCOORD1 at v2.z - can share one register, and a plain per-register key could
    // not hold both.
    public IList<RegisterDeclaration> MethodInputRegisters { get; } = [];
    public IList<RegisterDeclaration> MethodOutputRegisters = [];

    /// <summary>A geometry shader's SV_PrimitiveID input, when it reads one.</summary>
    public RegisterDeclaration PrimitiveIdDeclaration { get; set; }
    public int? MaxOutputVertexCount { get; set; }

    /// <summary>How many control points a patch comes in with, and goes out with.
    /// The first is the size of the array a domain shader reads.</summary>
    public int? InputControlPointCount { get; set; }

    // The patch constants this shader actually reads. What it is given is in the
    // signature chunk, which holds the tessellation factors as well.
    public IList<RegisterDeclaration> PatchConstantRegisters { get; } = [];
    public int? OutputControlPointCount { get; set; }

    /// <summary>What the tessellator subdivides, which the shader declares with
    /// the [domain(...)] attribute.</summary>
    public D3D10TessellatorDomain TessellatorDomain { get; set; }
    public int[] NumThreads { get; set; }
    public D3D10Primitive? InputPrimitive { get; set; }
    public D3D10PrimitiveTopology? PrimitiveTopology { get; set; }

    // x# registers: a local array the shader indexes at run time, declared with
    // its element count and how many components each element holds.
    public IDictionary<int, (int Elements, int Components)> IndexableTemps { get; } =
        new Dictionary<int, (int Elements, int Components)>();

    // g# registers: a compute shader's groupshared arrays, declared with the
    // element stride in bytes and the element count.
    public IDictionary<int, (int Stride, int Elements)> ThreadGroupSharedMemory { get; } =
        new Dictionary<int, (int Stride, int Elements)>();

    private ShaderModel _shaderModel;

    public RegisterState(ShaderModel shaderModel)
    {
        _shaderModel = shaderModel;
    }

    // A struct member is as wide as the member, not as the register holding it, so
    // reading float3 dir does not need a .xyz spelling it out.
    public int GetRegisterMaskedLength(RegisterComponentKey registerComponentKey)
    {
        // A packed output is as wide as its own declaration, not as the register
        // both of them share.
        if (registerComponentKey.RegisterKey.IsOutput
            && IsPackedOutputComponent(registerComponentKey))
        {
            return GetOutputDeclaration(registerComponentKey).MaskedLength;
        }
        if (registerComponentKey.RegisterKey is D3D10RegisterKey d3d10RegisterKey)
        {
            ConstantDeclaration declaration = FindConstant(
                d3d10RegisterKey, registerComponentKey.ComponentIndex);
            if (declaration != null && TryGetStructMember(
                declaration, d3d10RegisterKey, registerComponentKey.ComponentIndex,
                out _, out int memberWidth))
            {
                return memberWidth;
            }
            // Not a struct member, but still narrower than the register it shares: a
            // float packed alongside three others is one component wide, and naming
            // it after its own component made `power.w` out of a scalar.
            if (declaration != null && declaration.TypeInfo.Columns >= 1
                && declaration.TypeInfo.Columns <= 4)
            {
                return declaration.TypeInfo.Columns;
            }
            // An input register can be packed the same way a constant buffer is, so
            // its width is that of the declaration actually covering this component,
            // not of the register's merged, wider mask.
            if (d3d10RegisterKey.OperandType == OperandType.Input)
            {
                RegisterDeclaration inputDeclaration = FindInputDeclaration(
                    d3d10RegisterKey, registerComponentKey.ComponentIndex);
                if (inputDeclaration != null)
                {
                    return inputDeclaration.MaskedLength;
                }
            }
            // A patch constant register is packed the same way, and more reliably:
            // a tessellation factor takes its x whatever else is in it.
            if (d3d10RegisterKey.OperandType == OperandType.InputPatchConstant
                && RegisterDeclarations.TryGetValue(d3d10RegisterKey, out RegisterDeclaration patchConstant))
            {
                return patchConstant.MaskedLength;
            }
        }
        return GetRegisterMaskedLength(registerComponentKey.RegisterKey);
    }

    // Which declaration of a packed input register actually covers this component -
    // fxc can declare TEXCOORD0 at v2.xy and TEXCOORD1 at v2.z, each with its own
    // dcl_input_ps, and MethodInputRegisters holds one entry per declaration rather
    // than one per register so both survive.
    private RegisterDeclaration FindInputDeclaration(D3D10RegisterKey registerKey, int componentIndex)
    {
        return MethodInputRegisters.FirstOrDefault(d =>
            d.RegisterKey.Equals(registerKey) && (d.WriteMask & (1 << componentIndex)) != 0);
    }

    // The same for an output: fxc packs o1.xy and o1.z as readily as it packs
    // inputs, each with its own dcl_output.
    private RegisterDeclaration FindOutputDeclaration(RegisterKey registerKey, int componentIndex)
    {
        return MethodOutputRegisters.FirstOrDefault(d =>
            d.RegisterKey.Equals(registerKey) && (d.WriteMask & (1 << componentIndex)) != 0);
    }

    /// <summary>Whether another declaration shares this output component's
    /// register.</summary>
    public bool IsPackedOutputComponent(RegisterComponentKey registerComponentKey)
    {
        return registerComponentKey.RegisterKey.IsOutput
            && MethodOutputRegisters.Count(d => d.RegisterKey.Equals(registerComponentKey.RegisterKey)) > 1;
    }

    /// <summary>Which component of its register a packed output starts at, so that
    /// the swizzle naming it is rebased onto the field rather than the
    /// register.</summary>
    public int GetOutputComponentBase(RegisterComponentKey registerComponentKey)
    {
        RegisterDeclaration declaration = !registerComponentKey.RegisterKey.IsOutput
            ? null
            : FindOutputDeclaration(registerComponentKey.RegisterKey, registerComponentKey.ComponentIndex);
        if (declaration == null)
        {
            return 0;
        }
        for (int i = 0; i < 4; i++)
        {
            if ((declaration.WriteMask & (1 << i)) != 0)
            {
                return i;
            }
        }
        return 0;
    }

    /// <summary>The output declaration covering a component, for naming it.</summary>
    public RegisterDeclaration GetOutputDeclaration(RegisterComponentKey registerComponentKey)
    {
        return FindOutputDeclaration(registerComponentKey.RegisterKey, registerComponentKey.ComponentIndex)
            ?? RegisterDeclarations[registerComponentKey.RegisterKey];
    }

    // True only when another declaration actually shares this component's register -
    // SV_VertexID is one component wide too, but alone in its register, and printing
    // it as sv_vertexid rather than sv_vertexid.x is what the golden files expect.
    public bool IsPackedInputComponent(RegisterComponentKey registerComponentKey)
    {
        if (registerComponentKey.RegisterKey is not D3D10RegisterKey registerKey
            || registerKey.OperandType != OperandType.Input)
        {
            return false;
        }
        return MethodInputRegisters.Count(d => d.RegisterKey.Equals(registerKey)) > 1;
    }

    private static int CountSetBits(int mask)
    {
        int count = 0;
        for (int i = 0; i < 4; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Which component of its register a packed input starts at, the same way a
    /// packed constant does: TEXCOORD1 at v2.z starts at component 2, and a swizzle
    /// naming it has to be rebased onto the variable, whose own components start at x.
    /// </summary>
    public int GetInputComponentBase(RegisterComponentKey registerComponentKey)
    {
        if (registerComponentKey.RegisterKey is not D3D10RegisterKey registerKey
            || registerKey.OperandType is not OperandType.Input
                and not OperandType.InputPatchConstant)
        {
            return 0;
        }

        RegisterDeclaration declaration = registerKey.OperandType == OperandType.InputPatchConstant
            ? (RegisterDeclarations.TryGetValue(registerKey, out RegisterDeclaration patchConstant)
                ? patchConstant : null)
            : FindInputDeclaration(registerKey, registerComponentKey.ComponentIndex);
        if (declaration == null)
        {
            return 0;
        }
        for (int i = 0; i < 4; i++)
        {
            if ((declaration.WriteMask & (1 << i)) != 0)
            {
                return i;
            }
        }
        return 0;
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
                // A matrix stored column-major puts a column in each register, so
                // the register is as wide as the matrix is tall: a float4x3's is a
                // float4, and reading three of its components is a swizzle.
                return constant.TypeInfo.Rows > 1
                    ? constant.TypeInfo.Rows
                    : constant.TypeInfo.Columns;
            }

            // An indexed def has no declaration to be as wide as. Every def is a
            // full float4, which is the width the subscript reads.
            if (FindConstantArray(registerKey) != null)
            {
                return 4;
            }
        }

        // An immediate constant buffer row is a float4 and is never declared.
        if (registerKey is D3D10RegisterKey immediateKey
            && immediateKey.OperandType == OperandType.ImmediateConstantBuffer)
        {
            return 4;
        }

        if (RegisterDeclarations.TryGetValue(registerKey, out RegisterDeclaration registerDeclaration))
        {
            return RegisterDeclarations[registerKey].MaskedLength;
        }
        if (registerKey is D3D10RegisterKey d3D10RegisterKey)
        {
            if (d3D10RegisterKey.OperandType == OperandType.Resource
                && ResourceDefinitions.Any(r => r.ShaderInputType is D3DShaderInputType.Structured or D3DShaderInputType.ByteAddress
                && r.BindPoint == registerKey.Number))
            {
                return GetStructuredBufferComponents(registerKey);
            }
            if (d3D10RegisterKey.OperandType == OperandType.UnorderedAccessView
                && ResourceDefinitions.Any(r => r.ShaderInputType is D3DShaderInputType.UavRWStructured or D3DShaderInputType.UavRWByteAddress
                && r.BindPoint == registerKey.Number))
            {
                return GetStructuredBufferComponents(registerKey);
            }
            if (d3D10RegisterKey.OperandType == OperandType.IndexableTemp)
            {
                return IndexableTemps[registerKey.Number].Components;
            }
            if (d3D10RegisterKey.OperandType == OperandType.ThreadGroupSharedMemory)
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

    // Which member of a struct a register component falls in, and how wide that
    // member is. An element of `struct1 lights[2]` spans two registers, so the
    // register alone names neither the element nor the member.
    private static bool TryGetStructMember(
        ConstantDeclaration declaration,
        D3D10RegisterKey registerKey,
        int componentIndex,
        out string element,
        out int memberWidth)
    {
        element = null;
        memberWidth = 4;
        if (declaration.TypeInfo.MemberInfo == null
            || declaration is not D3D10ConstantDeclaration d3d10Declaration
            || registerKey.ConstantBufferOffset == null)
        {
            return false;
        }

        int elementCount = Math.Max(declaration.TypeInfo.NumElements, 1);
        int registersPerElement = Math.Max(
            d3d10Declaration.VariableSize / elementCount / ConstantRegisterSizeInBytes, 1);
        int registerOffset = registerKey.ConstantBufferOffset.Value
            - d3d10Declaration.VariableOffset / ConstantRegisterSizeInBytes;
        int elementIndex = registerOffset / registersPerElement;
        // Where the component sits inside the element, counted in floats.
        int target = (registerOffset % registersPerElement) * 4 + componentIndex;

        string variable = declaration.TypeInfo.NumElements > 1
            ? $"{declaration.Name}[{elementIndex}]"
            : declaration.Name;
        // A matrix member takes a row, or every row of it names the whole matrix -
        // `dot(position, lights[1].shadowMatrix)` four times over. Stored by
        // column, the register is a column, which is a row of the transpose.
        if (TryGetMemberAccessAtOffset(declaration.TypeInfo, variable, target, 0, out StructMemberAccess access)
            && access.IsMatrix)
        {
            int row = (target - access.StartOffset) / 4;
            element = $"transpose({access.Name})[{row}]";
            memberWidth = access.TypeInfo.Rows;
            return true;
        }
        return TryGetMemberAtOffset(declaration.TypeInfo, variable, target, out element, out memberWidth);
    }

    /// <summary>
    /// The member of an element of a struct array that a register within the
    /// element falls in, for an element picked at run time - `instances[id]` read
    /// as cb0[r0.x + 5], where 5 is the register within the element. Says where
    /// the member starts and what it is, so that a scalar prints without a swizzle
    /// and a matrix member takes a row.
    /// </summary>
    public static bool TryGetStructMemberAt(
        ConstantDeclaration declaration,
        string element,
        int registerWithinElement,
        int componentIndex,
        out StructMemberAccess member)
    {
        member = null;
        if (declaration.TypeInfo.MemberInfo == null)
        {
            return false;
        }
        int target = registerWithinElement * 4 + componentIndex;
        return TryGetMemberAccessAtOffset(declaration.TypeInfo, element, target, 0, out member);
    }

    private static bool TryGetMemberAccessAtOffset(
        ShaderTypeInfo typeInfo, string name, int target, int start, out StructMemberAccess member)
    {
        member = null;
        int offset = 0;
        foreach (ShaderStructMemberInfo info in typeInfo.MemberInfo)
        {
            int width = GetTypeWidth(info.TypeInfo);
            if (target < offset + width)
            {
                string memberName = $"{name}.{info.Name}";
                if (info.TypeInfo.MemberInfo != null)
                {
                    return TryGetMemberAccessAtOffset(
                        info.TypeInfo, memberName, target - offset, start + offset, out member);
                }
                member = new StructMemberAccess(memberName, info.TypeInfo, start + offset);
                return true;
            }
            offset += width;
        }
        return false;
    }

    // The member covering a float offset within a struct, descending into a member
    // that is a struct itself so that Outer.a.v does not stop at Outer.a.
    private static bool TryGetMemberAtOffset(
        ShaderTypeInfo typeInfo, string name, int target, out string element, out int memberWidth)
    {
        element = null;
        memberWidth = 4;
        if (typeInfo.MemberInfo == null)
        {
            return false;
        }

        int offset = 0;
        foreach (ShaderStructMemberInfo member in typeInfo.MemberInfo)
        {
            int width = GetTypeWidth(member.TypeInfo);
            if (target < offset + width)
            {
                string memberName = $"{name}.{member.Name}";
                if (member.TypeInfo.MemberInfo != null)
                {
                    return TryGetMemberAtOffset(
                        member.TypeInfo, memberName, target - offset, out element, out memberWidth);
                }
                element = memberName;
                memberWidth = width;
                return true;
            }
            offset += width;
        }
        return false;
    }

    // How many floats a type occupies. A struct is the sum of its members, rounded
    // up to a register, which is where a following member starts.
    private static int GetTypeWidth(ShaderTypeInfo typeInfo)
    {
        int elements = Math.Max(typeInfo.NumElements, 1);
        if (typeInfo.MemberInfo == null)
        {
            return typeInfo.Rows * typeInfo.Columns * elements;
        }

        int size = 0;
        foreach (ShaderStructMemberInfo member in typeInfo.MemberInfo)
        {
            size += GetTypeWidth(member.TypeInfo);
        }
        return (size + 3) / 4 * 4 * elements;
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

    // How far a register sits past the start of the variable occupying it, in
    // elements. cb0[r0.x + 1] against an array starting at register 1 is arr[r0.x].
    public int GetConstantBufferElementOffset(
        D3D10RegisterKey registerKey, ConstantDeclaration declaration)
    {
        int variableRegister = declaration is D3D10ConstantDeclaration d3d10Declaration
            ? d3d10Declaration.VariableOffset / ConstantRegisterSizeInBytes
            : declaration.RegisterIndex;
        return (registerKey.ConstantBufferOffset ?? 0) - variableRegister;
    }

    /// <summary>
    /// Which component of its register a constant starts at. A constant buffer packs
    /// several variables into one register, so a float3 declared after a float sits
    /// at .yzw - and the swizzle naming it has to be rebased onto the variable, which
    /// has components of its own starting at x.
    /// </summary>
    public int GetConstantComponentBase(RegisterComponentKey registerComponentKey)
    {
        if (registerComponentKey.RegisterKey is not D3D10RegisterKey registerKey
            || registerKey.OperandType != OperandType.ConstantBuffer)
        {
            return 0;
        }

        ConstantDeclaration declaration = FindConstant(
            registerKey, registerComponentKey.ComponentIndex);
        // An array or a struct is named by element or member, which carries its own
        // offset; only a plain variable is named whole.
        if (declaration is not D3D10ConstantDeclaration d3d10
            || declaration.TypeInfo.NumElements > 1
            || declaration.TypeInfo.ParameterClass == ParameterClass.Struct)
        {
            return 0;
        }
        return d3d10.VariableOffset % ConstantRegisterSizeInBytes / 4;
    }

    public string GetRegisterName(RegisterComponentKey registerComponentKey)
    {
        if (registerComponentKey.RegisterKey is D3D10RegisterKey d3d10RegisterKey
            && d3d10RegisterKey.OperandType == OperandType.ConstantBuffer)
        {
            ConstantDeclaration declaration = FindConstant(
                d3d10RegisterKey, registerComponentKey.ComponentIndex);
            if (declaration != null && TryGetStructMember(
                declaration, d3d10RegisterKey, registerComponentKey.ComponentIndex,
                out string member, out _))
            {
                return member;
            }
            if (declaration != null && declaration.TypeInfo.Rows == 1)
            {
                return GetConstantBufferName(declaration, d3d10RegisterKey);
            }
        }
        if (registerComponentKey.RegisterKey is D3D10RegisterKey inputRegisterKey
            && inputRegisterKey.OperandType == OperandType.Input)
        {
            RegisterDeclaration inputDeclaration = FindInputDeclaration(
                inputRegisterKey, registerComponentKey.ComponentIndex);
            if (inputDeclaration != null)
            {
                if (inputRegisterKey.GSVertex.HasValue)
                {
                    return $"i[{inputRegisterKey.GSVertex}].{inputDeclaration.Name}";
                }
                return MethodInputRegisters.Count == 1
                    ? inputDeclaration.Name
                    : "i." + inputDeclaration.Name;
            }
        }
        if (registerComponentKey.RegisterKey.IsOutput)
        {
            RegisterDeclaration outputDeclaration = FindOutputDeclaration(
                registerComponentKey.RegisterKey, registerComponentKey.ComponentIndex);
            if (outputDeclaration != null)
            {
                return HasOutputStruct
                    ? OutputVariableName + "." + outputDeclaration.Name
                    : OutputVariableName;
            }
        }
        return GetRegisterName(registerComponentKey.RegisterKey);
    }

    private string _outputVariableName;

    /// <summary>
    /// What the output struct is called. Normally "o", unless the shader already has
    /// something by that name - a constant buffer called o shadowed it, and every
    /// read of one of its members stopped compiling.
    /// </summary>
    public string OutputVariableName => _outputVariableName ??= UnusedName("o");

    /// <summary>
    /// Whether the outputs are written through a struct rather than as the one value
    /// the method returns. A geometry shader always is: it writes its vertices
    /// through the stream, so `o` is a GS_OUT however few members it has.
    /// </summary>
    public bool HasOutputStruct =>
        MethodOutputRegisters.Count > 1 || _shaderModel.Type == ShaderType.Geometry;

    private string _inputVariableName;

    /// <summary>What the input struct is called, on the same terms.</summary>
    public string InputVariableName => _inputVariableName ??= UnusedName("i");

    private string UnusedName(string preferred)
    {
        var taken = new HashSet<string>();
        foreach (ConstantDeclaration declaration in ConstantDeclarations)
        {
            taken.Add(declaration.Name);
        }
        foreach (ResourceDefinition resource in ResourceDefinitions)
        {
            taken.Add(resource.Name);
        }
        string name = preferred;
        while (taken.Contains(name))
        {
            name += "_";
        }
        return name;
    }

    public string GetRegisterName(RegisterKey registerKey)
    {
        if (registerKey.IsOutput)
        {
            var decl = RegisterDeclarations[registerKey];
            return HasOutputStruct
                ? OutputVariableName + "." + decl.Name
                : OutputVariableName;
        }
        if (registerKey is D3D9RegisterKey d3D9RegisterKey)
        {
            var decl = RegisterDeclarations[registerKey];
            switch (d3D9RegisterKey.Type)
            {
                // A ps_2_0 texture register is an input like any other and is
                // registered as one, so it is named through the input struct on the
                // same terms rather than bare.
                case RegisterType.Texture:
                case RegisterType.Input:
                case RegisterType.MiscType:
                    return (MethodInputRegisters.Count == 1)
                        ? decl.Name
                        : (InputVariableName + "." + decl.Name);
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
                    int column = registerKey.Number - constDecl.RegisterIndex;
                    return $"transpose({constDecl.Name})[{column}]";
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
                    int registerOffset = d3d10RegisterKey.ConstantBufferOffset.Value - variableRegister;
                    // An array of matrices gives every row a register of its own, so
                    // the element and the row have to be separated: row 0 of bones[1]
                    // is register 4, not row 4 of something.
                    string matrixName = declaration.Name;
                    int rowIndex = registerOffset;
                    if (declaration.TypeInfo.NumElements > 1)
                    {
                        int rowsPerElement = declaration.TypeInfo.Rows;
                        matrixName += $"[{registerOffset / rowsPerElement}]";
                        rowIndex = registerOffset % rowsPerElement;
                    }
                    return $"transpose({matrixName})[{rowIndex}]";
                case OperandType.Immediate32:
                    return d3d10RegisterKey.Number.ToString();
                case OperandType.Input:
                    var decl = RegisterDeclarations[registerKey];
                    // A geometry shader reads through the vertex array however few
                    // registers the input struct holds, so this comes before the
                    // single-input shortcut rather than after it.
                    if (d3d10RegisterKey.GSVertex.HasValue)
                    {
                        // A domain shader's array is the patch it was given; a
                        // geometry shader's is the primitive's vertices.
                        string array = _shaderModel.Type == ShaderType.Domain ? "patch" : "i";
                        return $"{array}[{d3d10RegisterKey.GSVertex}].{decl.Name}";
                    }
                    if (MethodInputRegisters.Count == 1)
                    {
                        return decl.Name;
                    }
                    return "i." + decl.Name;
                case OperandType.InputThreadID:
                case OperandType.InputThreadGroupID:
                case OperandType.InputThreadIDInGroup:
                case OperandType.InputThreadIDInGroupFlattened:
                    {
                        // In the input structure with every other input once there
                        // is more than one of them.
                        string threadName = RegisterDeclarations[registerKey].Name;
                        return MethodInputRegisters.Count == 1 ? threadName : "i." + threadName;
                    }
                case OperandType.InputPrimitiveID:
                case OperandType.InputDomainPoint:
                case OperandType.OutputControlPointID:
                    return RegisterDeclarations[registerKey].Name;
                // A field of the struct the hull shader filled in. The tessellation
                // factors are one array over several registers, so the name comes
                // from the signature rather than from the register.
                case OperandType.InputPatchConstant:
                    {
                        RegisterSignature signature =
                            RegisterDeclarations[registerKey].PatchConstantSignature;
                        string field = signature == null
                            ? RegisterDeclarations[registerKey].Name
                            : PatchConstants.Reference(signature, _shaderModel.PatchConstantSignatures);
                        return $"{PatchConstants.ParameterName}.{field}";
                    }
                // A control point of the patch, read the way a geometry shader
                // reads a vertex of its primitive.
                case OperandType.InputControlPoint:
                    return $"patch[{d3d10RegisterKey.GSVertex}]."
                        + RegisterDeclarations[registerKey].Name;
                case OperandType.Resource:
                    return ResourceDefinitions
                        .Where(d => d.ShaderInputType is D3DShaderInputType.Texture
                            or D3DShaderInputType.Structured or D3DShaderInputType.ByteAddress
                            or D3DShaderInputType.TBuffer)
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
                        .Where(d => d.ShaderInputType is D3DShaderInputType.UavRWStructured
                            or D3DShaderInputType.UavRWByteAddress
                            or D3DShaderInputType.UavRWTyped
                            or D3DShaderInputType.UavAppendStructured
                            or D3DShaderInputType.UavConsumeStructured
                            or D3DShaderInputType.UavRWStucturedWithCounter)
                        .First(d => d.BindPoint == registerKey.Number)
                        .Name;
                // Groupshared memory has no reflection entry to take a name from.
                case OperandType.ThreadGroupSharedMemory:
                    return "g" + registerKey.Number;
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

    // Remembers that a def is read through a0 or aL, which is the only thing that
    // distinguishes an array of literals from constants the parser folds in place.
    public void MarkIndexedConstant(RegisterKey registerKey)
    {
        if (registerKey is D3D9RegisterKey d3D9RegisterKey
            && d3D9RegisterKey.Type == RegisterType.Const)
        {
            _indexedConstants.Add(d3D9RegisterKey.Number);
            _constantArrays = null;
        }
    }

    public ConstantArray FindConstantArray(RegisterKey registerKey)
    {
        if (registerKey is not D3D9RegisterKey d3D9RegisterKey
            || d3D9RegisterKey.Type != RegisterType.Const)
        {
            return null;
        }
        return ConstantArrays.FirstOrDefault(a => a.Contains(d3D9RegisterKey.Number));
    }

    public IReadOnlyList<ConstantArray> ConstantArrays => _constantArrays ??= BuildConstantArrays();

    private List<ConstantArray> BuildConstantArrays()
    {
        var byIndex = new Dictionary<int, ConstantRegister>();
        foreach (ConstantRegister definition in ConstantDefinitions)
        {
            byIndex[definition.RegisterIndex] = definition;
        }

        // The subscripted register is element zero of the array as far as the
        // bytecode shows, but a constant offset folded into the index would put it
        // mid-array, so the run is walked backwards as well as forwards.
        var starts = new SortedSet<int>();
        foreach (int indexed in _indexedConstants)
        {
            if (!byIndex.ContainsKey(indexed))
            {
                continue;
            }
            int start = indexed;
            while (byIndex.ContainsKey(start - 1))
            {
                start--;
            }
            starts.Add(start);
        }

        var arrays = new List<ConstantArray>();
        foreach (int start in starts)
        {
            var registers = new List<ConstantRegister>();
            for (int i = start; byIndex.TryGetValue(i, out ConstantRegister register); i++)
            {
                registers.Add(register);
            }
            arrays.Add(new ConstantArray(start, registers));
        }
        return arrays;
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

    /// <summary>
    /// How many coordinates address one texel of a resource - two for a Texture2D,
    /// three for a Texture3D or a Texture2DArray.
    /// </summary>
    public int GetResourceDimensionSize(RegisterKey registerKey)
    {
        bool isUav = registerKey is D3D10RegisterKey { OperandType: OperandType.UnorderedAccessView };
        return ResourceDefinitions
            .First(d => d.BindPoint == registerKey.Number
                && (isUav
                    ? d.ShaderInputType is D3DShaderInputType.UavRWTyped
                    : d.ShaderInputType is D3DShaderInputType.Texture))
            .GetDimensionSize();
    }

    public void DeclareResource(D3D10RegisterKey registerKey, ResourceDimension resourceDimension, int resourceReturnType, int sampleCount = 0)
    {
        // A texture buffer declares itself the same way a texture does, with a
        // dcl_resource_buffer, and is told apart only by the binding calling it a
        // tbuffer. Its variables are never read as constant registers, so nothing
        // else would ever bring them in.
        // t and u registers number separately, so which of the two the declaration
        // is for has to come from the register rather than from the bind point.
        ResourceDefinition definition = _shaderModel.ResourceDefinitions
            .Where(d => registerKey.OperandType == OperandType.UnorderedAccessView
                ? d.ShaderInputType is D3DShaderInputType.UavRWTyped
                : d.ShaderInputType is D3DShaderInputType.Texture or D3DShaderInputType.TBuffer)
            .FirstOrDefault(d => d.BindPoint == registerKey.Number);
        if (definition != null)
        {
            definition.Dimension = resourceDimension;
            definition.SampleCount = sampleCount;
            ResourceDefinitions.Add(definition);
            if (definition.ShaderInputType == D3DShaderInputType.TBuffer)
            {
                foreach (D3D10ConstantDeclaration variable in _shaderModel.ConstantDeclarations
                    .OfType<D3D10ConstantDeclaration>()
                    .Where(d => d.IsTextureBuffer && d.BufferName == definition.Name))
                {
                    ConstantDeclarations.Add(variable);
                }
            }
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

    // A raw buffer has no stride: it is read and written by byte offset, so many
    // dwords at a time. Four components, since a load can take up to four.
    public void DeclareRawBuffer(D3D10RegisterKey registerKey)
    {
        DeclareStructuredStride(registerKey, 16);
        D3DShaderInputType inputType = registerKey.OperandType == OperandType.UnorderedAccessView
            ? D3DShaderInputType.UavRWByteAddress
            : D3DShaderInputType.ByteAddress;
        ResourceDefinition definition = _shaderModel.ResourceDefinitions
            .Where(d => d.ShaderInputType == inputType)
            .FirstOrDefault(d => d.BindPoint == registerKey.Number);
        if (definition != null)
        {
            ResourceDefinitions.Add(definition);
        }
    }

    public void DeclareThreadGroupSharedMemory(D3D10RegisterKey registerKey, uint stride, uint elements)
    {
        DeclareStructuredStride(registerKey, stride);
        ThreadGroupSharedMemory[registerKey.Number] = ((int)stride, (int)elements);
    }

    public void DeclareUnorderedAccessView(D3D10RegisterKey registerKey, uint stride)
    {
        DeclareStructuredStride(registerKey, stride);
        // An append buffer, a consume buffer and one with a counter all declare
        // themselves dcl_uav_structured; which of the four it is, only the binding
        // says.
        ResourceDefinition definition = _shaderModel.ResourceDefinitions
            .Where(d => d.ShaderInputType is D3DShaderInputType.UavRWStructured
                or D3DShaderInputType.UavAppendStructured
                or D3DShaderInputType.UavConsumeStructured
                or D3DShaderInputType.UavRWStucturedWithCounter)
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
                MethodInputRegisters.Add(registerDeclaration);
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
                        MethodInputRegisters.Add(registerDeclaration);
                    }
                }
            }
            else
            {
                if (RegisterDeclarations.TryGetValue(registerKey, out var existingDeclaration))
                {
                    // fxc packs interpolators as tightly as constants: TEXCOORD0 can be
                    // v2.xy and TEXCOORD1 v2.z, each declared by its own dcl_input_ps.
                    // Only widen the existing field when this dcl names the same thing;
                    // otherwise it is a second field sharing the register, and needs a
                    // declaration - and an input struct field - of its own.
                    // Outputs pack the same way and were left out of this, so a
                    // vertex shader writing TEXCOORD0 to o1.xy and TEXCOORD1 to o1.z
                    // declared one float3 and lost the second semantic.
                    RegisterDeclaration candidate = registerKey.OperandType is OperandType.Input or OperandType.Output
                        ? CreateRegisterDeclarationFromD3D10Dcl(instruction, registerKey)
                        : null;
                    if (candidate != null && candidate.Semantic != existingDeclaration.Semantic)
                    {
                        // candidate.WriteMask does not start at x, so the usual
                        // highest-bit-plus-one width would count the other
                        // declaration's components as its own.
                        candidate.MaskedLengthOverride = CountSetBits(candidate.WriteMask);
                        // And the one already there is as wide as its own components
                        // too: a write of o2.xy covering both would otherwise widen
                        // the first of them over the second.
                        existingDeclaration.MaskedLengthOverride =
                            CountSetBits(existingDeclaration.WriteMask);
                        if (registerKey.OperandType == OperandType.Input)
                        {
                            MethodInputRegisters.Add(candidate);
                        }
                        else
                        {
                            MethodOutputRegisters.Add(candidate);
                        }
                    }
                    else
                    {
                        existingDeclaration.WriteMask |= instruction.GetDestinationWriteMask();
                    }
                }
                else
                {
                    var registerDeclaration = CreateRegisterDeclarationFromD3D10Dcl(instruction, registerKey);
                    RegisterDeclarations.Add(registerKey, registerDeclaration);

                    switch (registerKey.OperandType)
                    {
                        case OperandType.Input:
                        case OperandType.InputThreadID:
                        case OperandType.InputThreadGroupID:
                        case OperandType.InputThreadIDInGroup:
                        case OperandType.InputThreadIDInGroupFlattened:
                        // Where in the patch this run is, which is a parameter of
                        // main beside the patch itself.
                        case OperandType.InputDomainPoint:
                            MethodInputRegisters.Add(registerDeclaration);
                            break;
                        // A field of the patch constant struct, which is written
                        // from the signature chunk rather than from what is read.
                        case OperandType.InputPatchConstant:
                            PatchConstantRegisters.Add(registerDeclaration);
                            break;
                        // Per primitive rather than per vertex, so not a field of the
                        // vertex struct: a parameter of main of its own.
                        case OperandType.InputPrimitiveID:
                            PrimitiveIdDeclaration = registerDeclaration;
                            break;
                        case OperandType.Output:
                        // A depth output is written like any other, and naming no
                        // register does not make it less of one.
                        case OperandType.OutputDepth:
                        case OperandType.OutputDepthGreaterEqual:
                        case OperandType.OutputDepthLessEqual:
                        // And a coverage mask names none either.
                        case OperandType.OutputCoverageMask:
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

    /// <summary>
    /// dcl_indexrange says a run of input registers is one array, indexed at run
    /// time. Each of them has been declared separately by a dcl_input of its own,
    /// and HLSL writes the run as one parameter - `float4 texcoord[4] : TEXCOORD`,
    /// whose elements take the consecutive semantics those declarations carry. So
    /// the first keeps its declaration and grows a length, and the rest stop being
    /// parameters of their own. They stay in RegisterDeclarations, since an
    /// instruction reading one by name still has to find it.
    /// </summary>
    public void DeclareIndexRange(D3D10Instruction instruction)
    {
        var baseKey = instruction.GetParamRegisterKey(0) as D3D10RegisterKey;
        if (baseKey == null
            || !RegisterDeclarations.TryGetValue(baseKey, out RegisterDeclaration first))
        {
            return;
        }
        int count = instruction.IndexRangeCount;
        first.ArrayLength = count;
        for (int offset = 1; offset < count; offset++)
        {
            var key = new D3D10RegisterKey(baseKey.OperandType, baseKey.Number + offset);
            if (RegisterDeclarations.TryGetValue(key, out RegisterDeclaration element))
            {
                MethodInputRegisters.Remove(element);
            }
        }
    }

    public void DeclareRegisterWrite(D3D10RegisterKey registerKey, int writeMask)
    {
        if (RegisterDeclarations.TryGetValue(registerKey, out var existingDeclaration))
        {
            // Not where the register carries more than one declaration: a write of
            // o2.xy covering a SV_ClipDistance at x and a SV_CullDistance at y would
            // widen the first over the second, and every component would then be
            // found in the first.
            if (!registerKey.IsOutput
                || MethodOutputRegisters.Count(d => d.RegisterKey.Equals(registerKey)) <= 1)
            {
                existingDeclaration.WriteMask |= writeMask;
            }
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
        // fxc can pack two differently named inputs into one register - TEXCOORD0 at
        // v2.xy and TEXCOORD1 at v2.z - each declared by its own dcl_input_ps. Match
        // on the mask this dcl actually declares, not just the register, so the two
        // resolve to their own signatures instead of both finding whichever is first.
        int declaredMask = instruction.GetDestinationWriteMask();
        IEnumerable<RegisterSignature> signatures = _shaderModel.InputSignatures
            .Concat(_shaderModel.OutputSignatures)
            .Concat(_shaderModel.PatchConstantSignatures);
        RegisterSignature signature =
            signatures.FirstOrDefault(i => i.RegisterKey.Equals(registerKey) && (i.Mask & declaredMask) != 0)
            ?? signatures.FirstOrDefault(i => i.RegisterKey.Equals(registerKey));
        if (signature != null)
        {
            string semantic = signature.Name;
            if (signature.Index != 0)
            {
                semantic += signature.Index;
            }
            var declaration = new RegisterDeclaration(registerKey, semantic, signature.Mask)
            {
                ComponentType = signature.ComponentType,
                InterpolationMode = instruction.GetInterpolationMode(),
            };
            // A patch constant register is packed with the tessellation factors as a
            // matter of course - a float3 lands in a register's yzw because a factor
            // has its x - so its width is the count of its own components rather
            // than the highest one it reaches.
            if (registerKey.OperandType == OperandType.InputPatchConstant)
            {
                declaration.MaskedLengthOverride = CountSetBits(signature.Mask);
                declaration.PatchConstantSignature = signature;
            }
            return declaration;
        }

        // A depth output is one component, and so is SV_GroupIndex, the flattened
        // thread id; the 4 is a guess for everything else - it happens to make the
        // three-wide thread ids three wide.
        bool isScalar = registerKey.OperandType == OperandType.OutputDepth
            || registerKey.OperandType == OperandType.OutputDepthGreaterEqual
            || registerKey.OperandType == OperandType.OutputDepthLessEqual
            || registerKey.OperandType == OperandType.OutputCoverageMask
            || registerKey.OperandType == OperandType.InputThreadIDInGroupFlattened
            || registerKey.OperandType == OperandType.InputPrimitiveID;
        int writeMask = isScalar ? 1 : 4;
        // SV_Coverage is a uint. The signature says so too, but keys it to register
        // -1, which is why the lookup above did not find it.
        const int UInt32ComponentType = 1;
        int componentType = registerKey.OperandType == OperandType.OutputCoverageMask ? UInt32ComponentType : 0;
        return new RegisterDeclaration(registerKey, instruction.GetDeclSemantic(), writeMask)
        {
            ComponentType = componentType,
        };
    }
}
