using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public sealed class RegisterState
{

    public ICollection<ConstantRegister> ConstantDefinitions = [];
    public IList<ConstantRegister> ImmediateConstantBuffer { get; } = [];

    // Base row -> read count. fxc lays literal arrays end to end, so `icb[r0.x + 3]`
    // reads the array starting at row 3. Counted because reads can be undeclared.
    private readonly SortedDictionary<int, int> _immediateConstantBufferBases = [];

    public void DeclareImmediateConstantBufferRead(int baseRow)
    {
        _immediateConstantBufferBases.TryGetValue(baseRow, out int reads);
        _immediateConstantBufferBases[baseRow] = reads + 1;
    }

    /// <summary>For a read rewritten into something that no longer names the buffer.</summary>
    public void UndeclareImmediateConstantBufferRead(int baseRow)
    {
        if (_immediateConstantBufferBases.TryGetValue(baseRow, out int reads) && reads > 1)
        {
            _immediateConstantBufferBases[baseRow] = reads - 1;
        }
        else
        {
            _immediateConstantBufferBases.Remove(baseRow);
        }
    }

    public bool IsImmediateConstantBufferRead => _immediateConstantBufferBases.Count != 0;

    /// <summary>
    /// An integer table: small integers are denormal floats, which fxc flushes in a
    /// float literal (so does NaN or infinity have no literal). Declared int4 then:
    /// signed, as negative entries need, and a utof says (uint). Per array.
    /// </summary>
    public bool ImmediateConstantBufferHoldsBits(int row)
    {
        foreach ((_, int start, int length) in ImmediateConstantBufferArrays())
        {
            if (row >= start && row < start + length)
            {
                return ImmediateConstantBuffer.Skip(start).Take(length)
                    .Any(r => r.Value.Any(value => float.IsSubnormal(value) || !float.IsFinite(value)));
            }
        }
        return false;
    }

    public bool IsImmediateConstantBufferIdentity(int baseRow, int width)
    {
        if (baseRow < 0 || baseRow + width > ImmediateConstantBuffer.Count)
        {
            return false;
        }
        for (int row = 0; row < width; row++)
        {
            for (int component = 0; component < width; component++)
            {
                if (ImmediateConstantBuffer[baseRow + row][component] != (row == component ? 1 : 0))
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>One literal array per base row, each running to the next.</summary>
    public IEnumerable<(string Name, int Start, int Length)> ImmediateConstantBufferArrays()
    {
        List<int> bases = ImmediateConstantBufferStarts();
        for (int i = 0; i < bases.Count; i++)
        {
            int end = i + 1 < bases.Count ? bases[i + 1] : ImmediateConstantBuffer.Count;
            yield return (ImmediateConstantBufferName(bases[i]), bases[i], end - bases[i]);
        }
    }

    // A read whose index is scaled (`icb[r0.x * 4 + 1]`) strides over rows: the
    // immediates are fields of one array of structs, not separate arrays.
    private bool _isImmediateConstantBufferStrided;

    public void MarkImmediateConstantBufferStrided()
    {
        _isImmediateConstantBufferStrided = true;
    }

    /// <summary>The row the array read at this base starts at.</summary>
    public int ImmediateConstantBufferArrayStart(int baseRow)
    {
        return ImmediateConstantBufferStarts().LastOrDefault(start => start <= baseRow);
    }

    public string ImmediateConstantBufferName(int baseRow)
    {
        List<int> bases = ImmediateConstantBufferStarts();
        string name = LocalNamePrefix + "icb";
        return bases.Count <= 1 ? name : name + Math.Max(bases.IndexOf(baseRow), 0);
    }

    private List<int> ImmediateConstantBufferStarts()
    {
        List<int> bases = _isImmediateConstantBufferStrided
            ? []
            : [.. _immediateConstantBufferBases.Keys.Where(b => b >= 0 && b < ImmediateConstantBuffer.Count)];
        // Rows before the first read are still declared, keeping later bases in place.
        if (bases.Count == 0 || bases[0] != 0)
        {
            bases.Insert(0, 0);
        }
        return bases;
    }
    private readonly HashSet<int> _indexedConstants = [];
    private List<ConstantArray> _constantArrays;

    private string _localNamePrefix = "";

    /// <summary>
    /// Prefix for made-up global names (icb, c4, g0), so two functions of one effect
    /// do not redefine each other's.
    /// </summary>
    public string LocalNamePrefix
    {
        get => _localNamePrefix;
        set
        {
            _localNamePrefix = value;
            _constantArrays = null;
        }
    }
    public ICollection<ConstantIntRegister> ConstantIntDefinitions = [];
    public ICollection<ConstantDeclaration> ConstantDeclarations { get; } = [];
    public ICollection<ResourceDefinition> ResourceDefinitions { get; } = [];

    /// <summary>
    /// Typed UAVs written by interlocked ops; their element is a scalar whatever
    /// the declaration says.
    /// </summary>
    private readonly HashSet<ResourceDefinition> _atomicViews = [];

    public ResourceDefinition FindStructuredBuffer(RegisterKey resourceKey)
    {
        // Match the register kind too: groupshared g0 and structured t0 share a number.
        D3DShaderInputType[] inputTypes = (resourceKey as D3D10RegisterKey)?.OperandType switch
        {
            OperandType.Resource => [D3DShaderInputType.Structured],
            OperandType.UnorderedAccessView =>
            [
                D3DShaderInputType.UavRWStructured,
                D3DShaderInputType.UavAppendStructured,
                D3DShaderInputType.UavConsumeStructured,
            ],
            _ => [],
        };
        return ResourceDefinitions.FirstOrDefault(r =>
            inputTypes.Contains(r.ShaderInputType) && r.BindPoint == resourceKey.Number);
    }

    /// <summary>
    /// A (RW)ByteAddressBuffer. Atomics don't encode this in the opcode, unlike stores.
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

    /// <summary>Only a structured buffer's GetDimensions also reports a stride.</summary>
    public bool IsStructuredResource(RegisterKey resourceKey)
    {
        bool isUav = resourceKey is D3D10RegisterKey { OperandType: OperandType.UnorderedAccessView };
        return ResourceDefinitions.Any(r => r.BindPoint == resourceKey.Number
            && (isUav
                ? r.ShaderInputType is D3DShaderInputType.UavRWStructured
                    or D3DShaderInputType.UavRWStucturedWithCounter
                    or D3DShaderInputType.UavAppendStructured
                    or D3DShaderInputType.UavConsumeStructured
                : r.ShaderInputType is D3DShaderInputType.Structured));
    }

    /// <summary>A typed buffer binds as a texture, so Texture is included.</summary>
    public ResourceDefinition GetBufferDefinition(RegisterKey registerKey)
    {
        bool isUav = registerKey is D3D10RegisterKey { OperandType: OperandType.UnorderedAccessView };
        return ResourceDefinitions.FirstOrDefault(d => d.BindPoint == registerKey.Number
            && (isUav
                ? d.ShaderInputType is D3DShaderInputType.UavRWTyped
                    or D3DShaderInputType.UavRWStructured
                    or D3DShaderInputType.UavRWByteAddress
                    or D3DShaderInputType.UavRWStucturedWithCounter
                    or D3DShaderInputType.UavAppendStructured
                    or D3DShaderInputType.UavConsumeStructured
                : d.ShaderInputType is D3DShaderInputType.Texture
                    or D3DShaderInputType.Structured
                    or D3DShaderInputType.ByteAddress));
    }

    /// <summary>
    /// Names the struct members a load or store reaches; one load can span several,
    /// giving a constructor. Null where the element is not a struct.
    /// </summary>
    public string NameStructuredMembers(
        RegisterKey resourceKey, string element, int byteOffset, IList<int> components)
    {
        IList<ShaderStructMemberInfo> members = FindStructuredBuffer(resourceKey)?.ElementType?.MemberInfo;
        if (members == null || members.Count == 0 || components.Count == 0)
        {
            return null;
        }

        IList<(string Name, int[] Values)> runs =
            FindStructuredMemberRuns(resourceKey, element, byteOffset, components);
        if (runs == null)
        {
            return null;
        }
        List<string> named = [.. runs.Select(r => r.Name)];
        // A float constructor would round double members, and convert integer ones.
        List<int> addresses = [.. components.Select(c => byteOffset + c * 4)];
        string constructor = GetStructuredComponentsPerElement(resourceKey) == 2
            ? "double"
            : addresses.All(a => IsIntegerStructuredMember(resourceKey, a) == true)
                ? addresses.All(a => IsUnsignedStructuredMember(resourceKey, a) == true) ? "uint" : "int"
                : "float";
        return named.Count == 1
            ? named[0]
            : $"{constructor}{components.Count}({string.Join(", ", named)})";
    }

    /// <summary>
    /// One run per member register reached, named with its swizzle, plus the indices
    /// of the components that go there. Null where the element is not a struct.
    /// </summary>
    public IList<(string Name, int[] Values)> FindStructuredMemberRuns(
        RegisterKey resourceKey, string element, int byteOffset, IList<int> components)
    {
        IList<ShaderStructMemberInfo> members = FindStructuredBuffer(resourceKey)?.ElementType?.MemberInfo;
        if (members == null || members.Count == 0 || components.Count == 0)
        {
            return null;
        }

        var runs = new List<(ShaderStructMemberInfo Member, string Path, int Register,
            List<int> InRegister, List<int> Values)>();
        for (int i = 0; i < components.Count; i++)
        {
            var found = FindStructuredMember(members, byteOffset + components[i] * 4, 0);
            if (found == null)
            {
                return null;
            }
            (ShaderStructMemberInfo member, string path, int memberOffset) = found.Value;
            // In values: a double takes two dwords, so d[2] is 16 bytes in.
            int bytesPerValue = member.TypeInfo.ParameterType == ParameterType.Double ? 8 : 4;
            int inMember = (byteOffset + components[i] * 4 - memberOffset) / bytesPerValue;
            // Matrix and array members span registers: split into register and
            // component (float 7 of a float4x4 is row 1's w).
            int stride = GetRegisterFloatCount(member.TypeInfo);
            int register = inMember / stride;
            int inRegister = inMember % stride;
            if (runs.Count != 0 && ReferenceEquals(runs[^1].Member, member)
                && runs[^1].Register == register)
            {
                runs[^1].InRegister.Add(inRegister);
                runs[^1].Values.Add(i);
            }
            else
            {
                runs.Add((member, path, register, [inRegister], [i]));
            }
        }

        return [.. runs.Select(run =>
        {
            ShaderTypeInfo typeInfo = run.Member.TypeInfo;
            bool isMatrix = typeInfo.Rows > 1;
            // The stored row's width, not the padded register's, so a whole row
            // gets no swizzle.
            int width = isMatrix
                ? (typeInfo.ParameterClass == ParameterClass.MatrixRows
                    ? typeInfo.Columns
                    : typeInfo.Rows)
                : typeInfo.Columns;
            // Full path here: a matrix row wraps it, `transpose(bones[i].skin)[0]`.
            string member = $"{element}.{run.Path}";
            int perElement = GetMemberRegisterCount(typeInfo);
            string one = typeInfo.NumElements > 1
                ? $"{member}[{run.Register / perElement}]"
                : member;
            string name = isMatrix
                ? MatrixRegisterName(typeInfo, one, run.Register % perElement)
                : one;
            string swizzle = width > 1 && !(run.InRegister.Count == width
                    && run.InRegister.SequenceEqual(Enumerable.Range(0, width)))
                ? "." + string.Concat(run.InRegister.Select(c => "xyzw"[c]))
                : "";
            return (name + swizzle, run.Values.ToArray());
        })];
    }

    /// <summary>Null where the member is not an integer or nothing says.</summary>
    public bool? IsUnsignedStructuredMember(RegisterKey resourceKey, int byteAddress)
    {
        if (FindStructuredTypeAt(resourceKey, byteAddress) is not var (type, _))
        {
            return null;
        }
        return type.ParameterType switch
        {
            ParameterType.Uint or ParameterType.Bool => true,
            ParameterType.Int => false,
            _ => null,
        };
    }

    /// <summary>The dwords a structured element takes, struct padding included; 0 if unknown.</summary>
    public int GetStructuredElementWords(RegisterKey resourceKey)
    {
        return FindStructuredElementType(resourceKey) is ShaderTypeInfo type
            ? GetElementByteSize(type) / 4
            : 0;
    }

    /// <summary>
    /// Element type for any structured kind, including counter buffers. Matches the
    /// register kind too: groupshared g0 shares t0's number but has no reflection.
    /// </summary>
    private ShaderTypeInfo FindStructuredElementType(RegisterKey resourceKey)
    {
        D3DShaderInputType[] inputTypes = (resourceKey as D3D10RegisterKey)?.OperandType switch
        {
            OperandType.Resource => [D3DShaderInputType.Structured],
            OperandType.UnorderedAccessView =>
            [
                D3DShaderInputType.UavRWStructured,
                D3DShaderInputType.UavRWStucturedWithCounter,
                D3DShaderInputType.UavAppendStructured,
                D3DShaderInputType.UavConsumeStructured,
            ],
            _ => [],
        };
        return ResourceDefinitions.FirstOrDefault(d => inputTypes.Contains(d.ShaderInputType)
            && d.BindPoint == resourceKey.Number)?.ElementType;
    }

    /// <summary>
    /// Asked per member: a struct element's own type says nothing. Null where
    /// reflection does not say (e.g. groupshared memory).
    /// </summary>
    public bool? IsIntegerStructuredMember(RegisterKey resourceKey, int byteAddress)
    {
        if (FindStructuredTypeAt(resourceKey, byteAddress) is not var (type, _))
        {
            return null;
        }
        return type.ParameterType switch
        {
            ParameterType.Int or ParameterType.Uint or ParameterType.Bool => true,
            ParameterType.Float => false,
            _ => null,
        };
    }

    /// <summary>
    /// The array member a byte offset falls in, for an index computed at run time:
    /// its path, bytes per element (packed tight), and where the offset lands.
    /// Arrays of scalars and vectors only; null otherwise.
    /// </summary>
    public (string Path, int ElementBytes, int Index, int Component, int Components, int WordsPerValue)?
        FindStructuredArrayMember(RegisterKey resourceKey, int byteOffset)
    {
        if (FindStructuredElementType(resourceKey)?.MemberInfo is not { Count: > 0 } members
            || FindStructuredMember(members, byteOffset, 0) is not var (member, path, offset)
            || member.TypeInfo.NumElements <= 1
            || member.TypeInfo.Rows > 1
            || member.TypeInfo.MemberInfo is { Count: > 0 })
        {
            return null;
        }
        // Component and Components count dwords: a double takes two.
        int wordsPerValue = member.TypeInfo.ParameterType == ParameterType.Double ? 2 : 1;
        int components = Math.Max(member.TypeInfo.Columns, 1) * wordsPerValue;
        int bytes = components * 4;
        int inMember = byteOffset - offset;
        return (path, bytes, inMember / bytes, inMember % bytes / 4, components, wordsPerValue);
    }

    /// <summary>
    /// Where a structured access's member is looked up: the immediate, or a computed
    /// offset's constant part, which lands in the array member (`(j << 4) + 16`).
    /// </summary>
    public static int MemberByteOffset(HlslTreeNode byteOffset) => SplitByteOffset(byteOffset).Constant;

    /// <summary>
    /// A byte offset as constant plus or minus a variable part: `(j << 2) + 16`, or
    /// `20 - (j << 2)` for an index counting down. The variable is null for an
    /// immediate.
    /// </summary>
    public static (HlslTreeNode Variable, int Constant, bool Subtracted) SplitByteOffset(
        HlslTreeNode byteOffset)
    {
        switch (byteOffset)
        {
            case null:
                return (null, 0, false);
            case ConstantNode { IntegerValue: int immediate }:
                return (null, immediate, false);
            // Nested, as a double's second dword is: `(16 - (j << 4)) + 8`.
            case AddOperation add
                when add.Inputs.FirstOrDefault(i => i is ConstantNode { IntegerValue: not null })
                    is ConstantNode { IntegerValue: int addend } constant:
                {
                    var inner = SplitByteOffset(
                        ReferenceEquals(add.Inputs[0], constant) ? add.Inputs[1] : add.Inputs[0]);
                    return (inner.Variable, inner.Constant + addend, inner.Subtracted);
                }
            case SubtractOperation { Minuend: ConstantNode { IntegerValue: int minuend } } subtract:
                {
                    var inner = SplitByteOffset(subtract.Subtrahend);
                    return (inner.Variable, minuend - inner.Constant, !inner.Subtracted);
                }
            case SubtractOperation { Subtrahend: ConstantNode { IntegerValue: int subtrahend } } subtract:
                {
                    var inner = SplitByteOffset(subtract.Minuend);
                    return (inner.Variable, inner.Constant - subtrahend, inner.Subtracted);
                }
            default:
                return (byteOffset, 0, false);
        }
    }

    /// <summary>The type at a byte address in the element, and its member's offset.</summary>
    private (ShaderTypeInfo Type, int ByteOffset)? FindStructuredTypeAt(
        RegisterKey resourceKey, int byteAddress)
    {
        ShaderTypeInfo elementType = FindStructuredElementType(resourceKey);
        if (elementType == null)
        {
            return null;
        }
        if (elementType.MemberInfo is not { Count: > 0 } members)
        {
            return (elementType, 0);
        }
        var found = FindStructuredMember(members, byteAddress, 0);
        return found == null
            ? null
            : (found.Value.Member.TypeInfo, found.Value.ByteOffset);
    }

    /// <summary>
    /// Loads count in 4 bytes, so every other component of a double is its upper half.
    /// </summary>
    public bool IsDoubleStructuredMemberUpperHalf(RegisterKey resourceKey, int byteAddress)
    {
        return FindStructuredTypeAt(resourceKey, byteAddress) is var (type, memberOffset)
            && type.ParameterType == ParameterType.Double
            && (byteAddress - memberOffset) % 8 != 0;
    }

    public bool IsDoubleStructuredMember(RegisterKey resourceKey, int byteAddress)
    {
        return FindStructuredTypeAt(resourceKey, byteAddress) is var (type, _)
            && type.ParameterType == ParameterType.Double;
    }

    /// <summary>Register components per value: 2 for doubles, else 1.</summary>
    public int GetStructuredComponentsPerElement(RegisterKey resourceKey)
    {
        return HasDoubleStructuredMember(resourceKey) ? 2 : 1;
    }

    public bool HasDoubleStructuredMember(RegisterKey resourceKey)
    {
        ShaderTypeInfo elementType = FindStructuredBuffer(resourceKey)?.ElementType;
        return elementType != null && HasDoubleMember(elementType);
    }

    // Recursive: nested structs hold doubles too.
    private static bool HasDoubleMember(ShaderTypeInfo typeInfo)
    {
        if (typeInfo.MemberInfo is { Count: > 0 } members)
        {
            return members.Any(m => HasDoubleMember(m.TypeInfo));
        }
        return typeInfo.ParameterType == ParameterType.Double;
    }

    /// <summary>
    /// The leaf member at a byte address, its path (`i.a`, descending into nested
    /// structs), and its start offset.
    /// </summary>
    internal static (ShaderStructMemberInfo Member, string Path, int ByteOffset)? FindStructuredMember(
        IList<ShaderStructMemberInfo> members, int byteAddress, int baseOffset)
    {
        foreach (ShaderStructMemberInfo member in members)
        {
            int offset = baseOffset + member.ByteOffset;
            if (byteAddress < offset || byteAddress >= offset + GetMemberByteSize(member.TypeInfo))
            {
                continue;
            }
            if (member.TypeInfo.MemberInfo is { Count: > 0 } nested)
            {
                int stride = GetElementByteSize(member.TypeInfo);
                int index = (byteAddress - offset) / stride;
                var inner = FindStructuredMember(nested, byteAddress, offset + index * stride);
                string name = member.TypeInfo.NumElements > 1
                    ? $"{member.Name}[{index}]"
                    : member.Name;
                return inner == null
                    ? null
                    : (inner.Value.Member, $"{name}.{inner.Value.Path}", inner.Value.ByteOffset);
            }
            return (member, member.Name, offset);
        }
        return null;
    }

    // Floats per matrix row. Buffer elements are packed tight: a float3x3's rows
    // are 12 bytes apart, not 16.
    private static int GetRegisterFloatCount(ShaderTypeInfo typeInfo)
    {
        if (typeInfo.Rows > 1)
        {
            return typeInfo.ParameterClass == ParameterClass.MatrixRows
                ? typeInfo.Columns
                : typeInfo.Rows;
        }
        return Math.Max(typeInfo.Columns, 1);
    }

    private static int GetMemberRegisterCount(ShaderTypeInfo typeInfo)
    {
        if (typeInfo.Rows <= 1)
        {
            return 1;
        }
        return typeInfo.ParameterClass == ParameterClass.MatrixRows
            ? typeInfo.Rows
            : typeInfo.Columns;
    }

    /// <summary>
    /// 8 for a double, else 4; a struct takes its largest member's. Gives the tail
    /// padding of a struct: { double; float; } is 16 bytes long, not 12.
    /// </summary>
    private static int GetTypeAlignment(ShaderTypeInfo typeInfo)
    {
        if (typeInfo.MemberInfo is { Count: > 0 } members)
        {
            return members.Max(m => GetTypeAlignment(m.TypeInfo));
        }
        return typeInfo.ParameterType == ParameterType.Double ? 8 : 4;
    }

    private static int GetElementByteSize(ShaderTypeInfo typeInfo)
    {
        if (typeInfo.MemberInfo is { Count: > 0 } members)
        {
            // End of the last member, rounded up to the struct's alignment.
            int end = members.Max(m => m.ByteOffset + GetMemberByteSize(m.TypeInfo));
            int alignment = GetTypeAlignment(typeInfo);
            return (end + alignment - 1) / alignment * alignment;
        }
        // Packed tight, not a register per row: a float3x3 is 36 bytes.
        int componentSize = typeInfo.ParameterType == ParameterType.Double ? 8 : 4;
        return typeInfo.Rows > 1
            ? typeInfo.Rows * typeInfo.Columns * componentSize
            : typeInfo.Columns * componentSize;
    }

    private static int GetMemberByteSize(ShaderTypeInfo typeInfo)
    {
        return GetElementByteSize(typeInfo) * Math.Max(typeInfo.NumElements, 1);
    }

    /// <summary>
    /// The matrix starting at a byte offset in the element, with its path ("" or
    /// `.world`). Null unless the offset is a matrix's first row.
    /// </summary>
    public (string MemberPath, ShaderTypeInfo TypeInfo)? FindStructuredMatrixAt(
        RegisterKey resourceKey, int byteOffset)
    {
        ShaderTypeInfo element = FindStructuredBuffer(resourceKey)?.ElementType;
        if (element == null)
        {
            return null;
        }
        if (element.MemberInfo == null || element.MemberInfo.Count == 0)
        {
            return element.Rows > 1 && byteOffset == 0 ? ("", element) : null;
        }
        foreach (ShaderStructMemberInfo member in element.MemberInfo)
        {
            if (member.ByteOffset == byteOffset && member.TypeInfo.Rows > 1
                && member.TypeInfo.NumElements <= 1)
            {
                return ($".{member.Name}", member.TypeInfo);
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
        // Rows are packed tight (12 bytes for a float3x3), not a register apart.
        int bytesPerRow = GetRegisterFloatCount(resource.ElementType) * 4;
        string matrix = $"transpose({element})";
        return $"{matrix}[{byteOffset / bytesPerRow}]";
    }
    public IDictionary<RegisterKey, RegisterInputNode> Samplers { get; } = new Dictionary<RegisterKey, RegisterInputNode>();

    public IDictionary<RegisterKey, RegisterDeclaration> RegisterDeclarations { get; } = new Dictionary<RegisterKey, RegisterDeclaration>();

    // A list, not keyed by register: TEXCOORD0 at v2.xy and TEXCOORD1 at v2.z share one.
    public IList<RegisterDeclaration> MethodInputRegisters { get; } = [];

    public string InputArrayName =>
        _shaderModel.Type is ShaderType.Domain or ShaderType.Hull ? "patch" : "i";

    /// <summary>
    /// Control point outputs (`vocp[1][0].w`) read back by the hull shader's patch
    /// constant phase through an OutputPatch parameter.
    /// </summary>
    public IList<RegisterDeclaration> ControlPointOutputRegisters { get; } = [];

    public const string OutputPatchName = "opatch";

    private IList<RegisterDeclaration> InputListOf(D3D10RegisterKey registerKey) =>
        registerKey.OperandType == OperandType.OutputControlPoint
            ? ControlPointOutputRegisters
            : MethodInputRegisters;
    public IList<RegisterDeclaration> MethodOutputRegisters = [];

    public RegisterDeclaration PrimitiveIdDeclaration { get; set; }

    public RegisterDeclaration GSInstanceIdDeclaration { get; set; }

    /// <summary>Geometry shader output streams in declaration order (SM5: up to 4).</summary>
    public IList<int> Streams { get; } = [];

    public void DeclareStream(int stream)
    {
        if (!Streams.Contains(stream))
        {
            Streams.Add(stream);
        }
    }

    public int? GSInstanceCount { get; set; }
    public int? MaxOutputVertexCount { get; set; }

    public int? InputControlPointCount { get; set; }

    // Only the patch constants actually read; the signature chunk lists them all.
    public IList<RegisterDeclaration> PatchConstantRegisters { get; } = [];
    public int? OutputControlPointCount { get; set; }

    /// <summary>From dcl_hs_max_tessfactor, or null.</summary>
    public float? MaxTessFactor { get; set; }

    public D3D10TessellatorDomain TessellatorDomain { get; set; }

    /// <summary>Hull shader only; a domain shader declares neither.</summary>
    public D3D10TessellatorPartitioning TessellatorPartitioning { get; set; }
    public D3D10TessellatorOutputPrimitive TessellatorOutputPrimitive { get; set; }
    /// <summary>[earlydepthstencil]: changes when a discard takes effect.</summary>
    public bool ForceEarlyDepthStencil { get; set; }

    public int[] NumThreads { get; set; }
    public D3D10Primitive? InputPrimitive { get; set; }
    public D3D10PrimitiveTopology? PrimitiveTopology { get; set; }

    // Streams may declare different topologies.
    public IDictionary<int, D3D10PrimitiveTopology> TopologyByStream { get; }
        = new Dictionary<int, D3D10PrimitiveTopology>();

    public bool HasSeveralStreams => Streams.Count > 1;

    public string StreamStructureName(int? stream) =>
        HasSeveralStreams ? $"GS_OUT{stream ?? 0}" : "GS_OUT";

    public string StreamVariableName(int? stream) =>
        HasSeveralStreams ? $"{OutputVariableName}{stream ?? 0}" : OutputVariableName;

    public string StreamParameterName(int? stream) =>
        HasSeveralStreams ? $"stream{stream ?? 0}" : "stream";

    // x# registers.
    public IDictionary<int, (int Elements, int Components)> IndexableTemps { get; } =
        new Dictionary<int, (int Elements, int Components)>();

    // g# registers; Stride is in bytes.
    public IDictionary<int, (int Stride, int Elements)> ThreadGroupSharedMemory { get; } =
        new Dictionary<int, (int Stride, int Elements)>();

    // fxc gives raw storage to a non-array groupshared variable, structured to an array.
    public ISet<int> RawThreadGroupSharedMemory { get; } = new HashSet<int>();

    // Structured groupshared memory some access reaches at a computed byte offset (a
    // two-dimensional array): declared as dwords, `g0[element][dword]`.
    public ISet<int> DwordAddressedThreadGroupSharedMemory { get; } = new HashSet<int>();

    // The scalar type each groupshared variable is declared with.
    public IDictionary<int, string> ThreadGroupSharedTypes { get; } = new Dictionary<int, string>();

    public LinkageModel Linkage { get; set; }

    /// <summary>
    /// Set while writing a linkage method: the inputs it reads are its parameters,
    /// named without main's input struct.
    /// </summary>
    public bool NamesInputsAsParameters { get; set; }

    private ShaderModel _shaderModel;

    public RegisterState(ShaderModel shaderModel)
    {
        _shaderModel = shaderModel;
    }

    // The width of the declaration or member covering the component, not of the
    // whole (packed) register.
    public int GetRegisterMaskedLength(RegisterComponentKey registerComponentKey)
    {
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
                out _, out int memberWidth, out _))
            {
                return memberWidth;
            }
            if (declaration != null && declaration.TypeInfo.Columns >= 1
                && declaration.TypeInfo.Columns <= 4)
            {
                return declaration.TypeInfo.Columns;
            }
            if (d3d10RegisterKey.OperandType is OperandType.Input or OperandType.OutputControlPoint)
            {
                RegisterDeclaration inputDeclaration = FindInputDeclaration(
                    d3d10RegisterKey, registerComponentKey.ComponentIndex);
                if (inputDeclaration != null)
                {
                    return inputDeclaration.MaskedLength;
                }
            }
            if (d3d10RegisterKey.OperandType == OperandType.InputPatchConstant
                && RegisterDeclarations.TryGetValue(d3d10RegisterKey, out RegisterDeclaration patchConstant))
            {
                return patchConstant.MaskedLength;
            }
        }
        return GetRegisterMaskedLength(registerComponentKey.RegisterKey);
    }

    // The declaration of a packed input register covering this component.
    public RegisterDeclaration FindInputDeclaration(D3D10RegisterKey registerKey, int componentIndex)
    {
        // GS reads name the vertex too; all vertices share one declaration.
        D3D10RegisterKey baseKey = registerKey.GetGSBaseKey();
        return InputListOf(registerKey).FirstOrDefault(d =>
            d.RegisterKey.Equals(baseKey) && (d.WriteMask & (1 << componentIndex)) != 0);
    }

    private RegisterDeclaration FindOutputDeclaration(RegisterKey registerKey, int componentIndex)
    {
        return MethodOutputRegisters.FirstOrDefault(d =>
            d.RegisterKey.Equals(registerKey) && (d.WriteMask & (1 << componentIndex)) != 0);
    }

    public bool IsPackedOutputComponent(RegisterComponentKey registerComponentKey)
    {
        return registerComponentKey.RegisterKey.IsOutput
            && MethodOutputRegisters.Count(d => d.RegisterKey.Equals(registerComponentKey.RegisterKey)) > 1;
    }

    /// <summary>Where a packed output starts, for rebasing its swizzle.</summary>
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

    public RegisterDeclaration GetOutputDeclaration(RegisterComponentKey registerComponentKey)
    {
        return FindOutputDeclaration(registerComponentKey.RegisterKey, registerComponentKey.ComponentIndex)
            ?? RegisterDeclarations[registerComponentKey.RegisterKey];
    }

    // Not the same as being a scalar: SV_VertexID is a scalar alone in its register.
    public bool IsPackedInputComponent(RegisterComponentKey registerComponentKey)
    {
        if (registerComponentKey.RegisterKey is not D3D10RegisterKey registerKey
            || registerKey.OperandType != OperandType.Input)
        {
            return false;
        }
        return MethodInputRegisters
            .Select(d => d.RegisterKey)
            .Distinct()
            .Count(k => k.Equals(registerKey.GetGSBaseKey())) > 1;
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
    /// Where a packed input starts (TEXCOORD1 at v2.z: 2), for rebasing its swizzle.
    /// </summary>
    public int GetInputComponentBase(RegisterComponentKey registerComponentKey)
    {
        if (registerComponentKey.RegisterKey is not D3D10RegisterKey registerKey
            || registerKey.OperandType is not OperandType.Input
                and not OperandType.InputPatchConstant and not OperandType.OutputControlPoint)
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

    /// <summary>
    /// TEXCOORD0's x and y are; TEXCOORD0's y and TEXCOORD1's z are not. Patch
    /// constants are never packed together, so need no check.
    /// </summary>
    public bool IsSameInputVariable(D3D10RegisterKey registerKey, int component1, int component2)
    {
        RegisterDeclaration first = FindInputDeclaration(registerKey, component1);
        RegisterDeclaration second = FindInputDeclaration(registerKey, component2);
        return first == null || second == null || ReferenceEquals(first, second);
    }

    public int GetRegisterMaskedLength(RegisterKey registerKey)
    {
        // As wide as declared: `int count` fills an i# register but is one component.
        if (registerKey is D3D9RegisterKey d3D9RegisterKey
            && (d3D9RegisterKey.Type == RegisterType.Const
                || d3D9RegisterKey.Type == RegisterType.ConstInt
                || d3D9RegisterKey.Type == RegisterType.ConstBool))
        {
            var constant = FindConstant(registerKey);
            if (constant != null)
            {
                // Column major: a register holds a column, as wide as the matrix is tall.
                return constant.TypeInfo.Rows > 1
                    ? constant.TypeInfo.Rows
                    : constant.TypeInfo.Columns;
            }

            // An indexed def is always a full float4.
            if (FindConstantArray(registerKey) != null)
            {
                return 4;
            }
        }

        if (registerKey is D3D10RegisterKey immediateKey
            && immediateKey.OperandType == OperandType.ImmediateConstantBuffer)
        {
            return 4;
        }

        if (RegisterDeclarations.TryGetValue(registerKey, out RegisterDeclaration registerDeclaration))
        {
            return RegisterDeclarations[registerKey].MaskedLength;
        }
        if (LinkageTempParameters.TryGetValue(registerKey, out int parameterWidth))
        {
            return parameterWidth;
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
                && ResourceDefinitions.Any(r => r.ShaderInputType is D3DShaderInputType.UavRWStructured
                    or D3DShaderInputType.UavRWByteAddress
                    or D3DShaderInputType.UavRWStucturedWithCounter
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

    // SM3 gives every struct member its own register(s), so the register within the
    // element names the member and its swizzle needs no rebasing.
    public bool TryGetConstantMember(
        RegisterComponentKey registerComponentKey, out StructMemberAccess member)
    {
        member = null;
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

        int registerOffset = d3d9RegisterKey.Number - declaration.RegisterIndex;
        int registersPerElement = Math.Max(declaration.RegistersPerElement, 1);
        string variable = declaration.TypeInfo.NumElements > 1
            ? $"{declaration.Name}[{registerOffset / registersPerElement}]"
            : declaration.Name;
        int registerWithinElement = registerOffset % registersPerElement;
        if (!TryGetStructMemberAtRegister(declaration, variable, registerWithinElement, out member))
        {
            return false;
        }
        // The walk returns a matrix whole; narrow to the register's row or column.
        if (member.IsMatrix)
        {
            member = MatrixRowOf(member, registerWithinElement);
        }
        return true;
    }

    /// <summary>The row or column one register of a matrix member holds, as a vector member.</summary>
    public static StructMemberAccess MatrixRowOf(
        StructMemberAccess matrix, int registerWithinElement)
    {
        int row = registerWithinElement - matrix.StartOffset / 4;
        var rowType = new ShaderTypeInfo(
            ParameterClass.Vector, matrix.TypeInfo.ParameterType, 1, matrix.MatrixRowWidth, 1, null);
        return new StructMemberAccess(matrix.MatrixRow(row), rowType, registerWithinElement * 4);
    }

    /// <summary>
    /// For an element picked at run time: `lights[i]` read as c[aL.x + 4], where 4
    /// is the register within the element.
    /// </summary>
    public static bool TryGetStructMemberAtRegister(
        ConstantDeclaration declaration,
        string element,
        int registerWithinElement,
        out StructMemberAccess member)
    {
        member = null;
        if (declaration.TypeInfo.MemberInfo == null)
        {
            return false;
        }
        return TryGetD3D9MemberAtRegister(declaration.TypeInfo, element, registerWithinElement, out member);
    }

    // The register alone picks the member, since each owns its registers.
    private static bool TryGetD3D9MemberAtRegister(
        ShaderTypeInfo typeInfo, string name, int register, out StructMemberAccess member)
    {
        member = null;
        int offset = 0;
        foreach (ShaderStructMemberInfo info in typeInfo.MemberInfo)
        {
            int registers = GetD3D9MemberRegisterCount(info.TypeInfo);
            if (register < offset + registers)
            {
                string memberName = $"{name}.{info.Name}";
                int within = register - offset;
                if (info.TypeInfo.MemberInfo != null)
                {
                    // Array of structs: pick the element, then descend on the remainder.
                    int perElement = registers / Math.Max(info.TypeInfo.NumElements, 1);
                    if (info.TypeInfo.NumElements > 1)
                    {
                        memberName += $"[{within / perElement}]";
                    }
                    return TryGetD3D9MemberAtRegister(
                        info.TypeInfo, memberName, within % perElement, out member);
                }
                // Returned whole: the grouper needs the matrix and where it starts;
                // writers narrow it to a row themselves.
                if (info.TypeInfo.Rows > 1)
                {
                    int perMatrix = MatrixRegisterCount(info.TypeInfo);
                    if (within >= registers)
                    {
                        return false;
                    }
                    if (info.TypeInfo.NumElements > 1)
                    {
                        memberName += $"[{within / perMatrix}]";
                    }
                    member = new StructMemberAccess(
                        memberName, info.TypeInfo, (offset + within / perMatrix * perMatrix) * 4);
                    return true;
                }
                if (info.TypeInfo.NumElements > 1)
                {
                    memberName += $"[{within}]";
                }
                member = new StructMemberAccess(memberName, info.TypeInfo, (offset + within) * 4);
                return true;
            }
            offset += registers;
        }
        return false;
    }

    /// <summary>
    /// The struct's matrix member at a register (`.world` of `instances[id].world`)
    /// and the element index.
    /// </summary>
    public bool TryGetStructMatrixAt(
        RegisterComponentKey key,
        ConstantDeclaration declaration,
        out StructMemberAccess member,
        out int elementIndex)
    {
        member = null;
        elementIndex = 0;
        if (declaration?.TypeInfo.MemberInfo == null)
        {
            return false;
        }
        int stride = Math.Max(declaration.RegistersPerElement, 1);
        int registerOffset = key.RegisterKey switch
        {
            D3D10RegisterKey d3d10Key => GetConstantBufferElementOffset(d3d10Key, declaration),
            D3D9RegisterKey d3d9Key => d3d9Key.Number - declaration.RegisterIndex,
            _ => -1,
        };
        if (registerOffset < 0)
        {
            return false;
        }
        elementIndex = registerOffset / stride;
        bool found = key.RegisterKey is D3D10RegisterKey
            ? TryGetStructMemberAt(
                declaration, "", registerOffset % stride, key.ComponentIndex, out member)
            : TryGetStructMemberAtRegister(
                declaration, "", registerOffset % stride, out member);
        return found && member.IsMatrix;
    }

    /// <summary>One register per column, or per row if row major.</summary>
    public static int MatrixRegisterCount(ShaderTypeInfo typeInfo)
    {
        return typeInfo.ParameterClass == ParameterClass.MatrixRows
            ? typeInfo.Rows
            : typeInfo.Columns;
    }

    /// <summary>
    /// The matrix row a run of components is, whole and in order; in a column major
    /// matrix it spans registers, one component each (as preshaders read rows).
    /// </summary>
    public (string Matrix, int Row)? TryGetMatrixRow(IList<HlslTreeNode> components)
    {
        if (components.Count < 2)
        {
            return null;
        }
        string matrix = null;
        int row = -1;
        int columns = 0;
        for (int i = 0; i < components.Count; i++)
        {
            if (components[i] is not RegisterInputNode input
                || TryGetMatrixRegister(input.RegisterComponentKey.RegisterKey) is not (ShaderTypeInfo type, string name, int register))
            {
                return null;
            }
            int component = input.RegisterComponentKey.ComponentIndex;
            (int elementRow, int elementColumn) = type.ParameterClass == ParameterClass.MatrixRows
                ? (register, component)
                : (component, register);
            if (elementColumn != i)
            {
                return null;
            }
            if (i == 0)
            {
                (matrix, row, columns) = (name, elementRow, type.Columns);
            }
            else if (name != matrix || elementRow != row)
            {
                return null;
            }
        }
        return components.Count == columns ? (matrix, row) : null;
    }

    /// <summary>
    /// HLSL subscripts by row, so a column register is reached through transpose;
    /// a row major register must not be.
    /// </summary>
    public static string MatrixRegisterName(ShaderTypeInfo typeInfo, string name, int register)
    {
        return typeInfo.ParameterClass == ParameterClass.MatrixRows
            ? $"{name}[{register}]"
            : $"transpose({name})[{register}]";
    }

    /// <summary>Name plus swizzle, except matrix registers (see MatrixRegisterComponents).</summary>
    public string GetRegisterComponents(RegisterComponentKey key, string swizzle)
    {
        // By the component's own declaration: a scalar packed after an int3x3's
        // rows (cb0[2].w) is in the matrix's register but not the matrix.
        if (key.RegisterKey is D3D10RegisterKey { OperandType: OperandType.ConstantBuffer }
            && FindConstantOfComponent(key) is { TypeInfo.Rows: <= 1 })
        {
            return GetRegisterName(key) + swizzle;
        }
        return TryGetMatrixRegister(key.RegisterKey) is (ShaderTypeInfo typeInfo, string name, int register)
            ? MatrixRegisterComponents(typeInfo, name, register, swizzle)
            : GetRegisterName(key) + swizzle;
    }

    /// <summary>
    /// The matrix (with element subscript) a constant register belongs to and which
    /// of its registers it is. For a cbuffer, RegisterIndex is the buffer, so the
    /// offset comes from VariableOffset.
    /// </summary>
    private (ShaderTypeInfo TypeInfo, string Name, int Register)? TryGetMatrixRegister(
        RegisterKey registerKey)
    {
        int registerOffset;
        ConstantDeclaration declaration;
        if (registerKey is D3D9RegisterKey
            {
                Type: RegisterType.Const or RegisterType.ConstInt or RegisterType.ConstBool
            })
        {
            declaration = FindConstant(registerKey);
            if (declaration == null || declaration.TypeInfo.Rows <= 1)
            {
                return null;
            }
            registerOffset = registerKey.Number - declaration.RegisterIndex;
        }
        else if (registerKey is D3D10RegisterKey
            {
                OperandType: OperandType.ConstantBuffer
            } d3d10RegisterKey)
        {
            declaration = FindConstant(registerKey);
            if (declaration == null || declaration.TypeInfo.Rows <= 1
                || d3d10RegisterKey.ConstantBufferOffset == null)
            {
                return null;
            }
            int variableRegister = declaration is D3D10ConstantDeclaration d3d10Declaration
                ? d3d10Declaration.VariableOffset / ConstantRegisterSizeInBytes
                : declaration.RegisterIndex;
            registerOffset = d3d10RegisterKey.ConstantBufferOffset.Value - variableRegister;
        }
        else
        {
            return null;
        }

        string name = declaration.Name;
        if (declaration.TypeInfo.NumElements > 1)
        {
            int registersPerElement = MatrixRegisterCount(declaration.TypeInfo);
            name += $"[{registerOffset / registersPerElement}]";
            registerOffset %= registersPerElement;
        }
        return (declaration.TypeInfo, name, registerOffset);
    }

    /// <summary>
    /// One component of a column major register is written `m[0][1]` rather than
    /// `transpose(m)[1].x`. Several components, or a row major register, keep the
    /// swizzle form.
    /// </summary>
    public static string MatrixRegisterComponents(
        ShaderTypeInfo typeInfo, string name, int register, string swizzle)
    {
        int component = swizzle.Length == 2 && swizzle[0] == '.'
            ? "xyzw".IndexOf(swizzle[1])
            : -1;
        return typeInfo.ParameterClass != ParameterClass.MatrixRows && component >= 0
            ? $"{name}[{component}][{register}]"
            : MatrixRegisterName(typeInfo, name, register) + swizzle;
    }

    // In SM3 a scalar or vector member never shares its register.
    private static int GetD3D9MemberRegisterCount(ShaderTypeInfo typeInfo)
    {
        int elements = Math.Max(typeInfo.NumElements, 1);
        if (typeInfo.MemberInfo != null)
        {
            int registers = 0;
            foreach (ShaderStructMemberInfo member in typeInfo.MemberInfo)
            {
                registers += GetD3D9MemberRegisterCount(member.TypeInfo);
            }
            return registers * elements;
        }
        if (typeInfo.Rows > 1)
        {
            return MatrixRegisterCount(typeInfo) * elements;
        }
        return elements;
    }

    // The struct member a cbuffer register component falls in, and its width.
    private static bool TryGetStructMember(
        ConstantDeclaration declaration,
        D3D10RegisterKey registerKey,
        int componentIndex,
        out string element,
        out int memberWidth,
        out int componentBase)
    {
        element = null;
        memberWidth = 4;
        componentBase = 0;
        if (declaration.TypeInfo.MemberInfo == null
            || declaration is not D3D10ConstantDeclaration d3d10Declaration
            || registerKey.ConstantBufferOffset == null)
        {
            return false;
        }

        // Rounds a part-used register up: a 72 byte element takes 5 registers.
        int registersPerElement = d3d10Declaration.RegistersPerElement;
        int registerOffset = registerKey.ConstantBufferOffset.Value
            - d3d10Declaration.VariableOffset / ConstantRegisterSizeInBytes;
        int elementIndex = registerOffset / registersPerElement;
        // In floats.
        int target = (registerOffset % registersPerElement) * 4 + componentIndex;

        string variable = declaration.TypeInfo.NumElements > 1
            ? $"{declaration.Name}[{elementIndex}]"
            : declaration.Name;
        if (!TryGetMemberAccessAtOffset(
                declaration.TypeInfo, variable, target, 0, out StructMemberAccess access))
        {
            return false;
        }
        // Narrow a matrix member to the row this register holds.
        if (access.IsMatrix)
        {
            int row = (target - access.StartOffset) / 4;
            element = access.MatrixRow(row);
            memberWidth = access.MatrixRowWidth;
            return true;
        }
        element = access.Name;
        memberWidth = access.Width;
        componentBase = access.ComponentBase;
        return true;
    }

    /// <summary>
    /// For an element picked at run time: `instances[id]` read as cb0[r0.x + 5],
    /// where 5 is the register within the element.
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
                int elements = Math.Max(info.TypeInfo.NumElements, 1);
                int elementStart = offset;
                // An array member starts where the element does, which is what a
                // swizzle is rebased onto.
                if (elements > 1)
                {
                    int stride = width / elements;
                    memberName += $"[{(target - offset) / stride}]";
                    elementStart += (target - offset) / stride * stride;
                }
                if (info.TypeInfo.MemberInfo != null)
                {
                    return TryGetMemberAccessAtOffset(
                        info.TypeInfo, memberName, target - elementStart,
                        start + elementStart, out member);
                }
                member = new StructMemberAccess(memberName, info.TypeInfo, start + elementStart);
                return true;
            }
            offset += width;
        }
        return false;
    }

    // Floats a type occupies in a cbuffer. A struct is rounded up to a register.
    private static int GetTypeWidth(ShaderTypeInfo typeInfo)
    {
        int elements = Math.Max(typeInfo.NumElements, 1);
        if (typeInfo.MemberInfo == null)
        {
            // Whole registers per column (per row if row major), padding included.
            if (typeInfo.Rows > 1)
            {
                int registers = typeInfo.ParameterClass == ParameterClass.MatrixRows
                    ? typeInfo.Rows
                    : typeInfo.Columns;
                return registers * 4 * elements;
            }
            int element = typeInfo.Rows * typeInfo.Columns;
            // Each array element starts a register: `float2 pair[2]` takes two.
            return elements > 1 ? (element + 3) / 4 * 4 * elements : element;
        }

        int size = 0;
        foreach (ShaderStructMemberInfo member in typeInfo.MemberInfo)
        {
            size += GetTypeWidth(member.TypeInfo);
        }
        return (size + 3) / 4 * 4 * elements;
    }

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

    // In registers: cb0[r0.x + 1] against an array at register 1 is arr[r0.x].
    public int GetConstantBufferElementOffset(
        D3D10RegisterKey registerKey, ConstantDeclaration declaration)
    {
        int variableRegister = declaration is D3D10ConstantDeclaration d3d10Declaration
            ? d3d10Declaration.VariableOffset / ConstantRegisterSizeInBytes
            : declaration.RegisterIndex;
        return (registerKey.ConstantBufferOffset ?? 0) - variableRegister;
    }

    /// <summary>
    /// Where a packed constant starts (a float3 after a float: 1), for rebasing its
    /// swizzle.
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
        if (declaration is not D3D10ConstantDeclaration d3d10)
        {
            return 0;
        }
        // Struct members are packed like plain variables; rebase onto the member.
        if (declaration.TypeInfo.ParameterClass == ParameterClass.Struct)
        {
            return TryGetStructMember(declaration, registerKey,
                registerComponentKey.ComponentIndex, out _, out _, out int memberBase)
                ? memberBase
                : 0;
        }
        // An array is named by element, which carries its own offset.
        if (declaration.TypeInfo.NumElements > 1)
        {
            return 0;
        }
        // A double vector spills into the next register (double4 is cb0[0..1].xyzw),
        // so the base counts back from the register being read; it may be negative.
        if (declaration.TypeInfo.ParameterType == ParameterType.Double)
        {
            return (d3d10.VariableOffset
                - registerKey.ConstantBufferOffset.Value * ConstantRegisterSizeInBytes) / 4;
        }
        return d3d10.VariableOffset % ConstantRegisterSizeInBytes / 4;
    }

    /// <summary>2 for a double (double2's .y is at .zw), else 1.</summary>
    public int GetConstantComponentsPerElement(RegisterComponentKey registerComponentKey)
    {
        if (registerComponentKey.RegisterKey is not D3D10RegisterKey registerKey
            || registerKey.OperandType != OperandType.ConstantBuffer)
        {
            return 1;
        }
        ConstantDeclaration declaration = FindConstant(
            registerKey, registerComponentKey.ComponentIndex);
        return declaration?.TypeInfo.ParameterType == ParameterType.Double ? 2 : 1;
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
                out string member, out _, out _))
            {
                return member;
            }
            if (declaration != null && declaration.TypeInfo.Rows == 1)
            {
                return GetConstantBufferName(declaration, d3d10RegisterKey);
            }
        }
        if (registerComponentKey.RegisterKey is D3D10RegisterKey inputRegisterKey
            && inputRegisterKey.OperandType is OperandType.Input or OperandType.OutputControlPoint)
        {
            RegisterDeclaration inputDeclaration = FindInputDeclaration(
                inputRegisterKey, registerComponentKey.ComponentIndex);
            if (inputDeclaration != null)
            {
                if (inputRegisterKey.OperandType == OperandType.OutputControlPoint)
                {
                    return $"{OutputPatchName}[{inputRegisterKey.GSVertex}].{inputDeclaration.Name}";
                }
                if (inputRegisterKey.GSVertex.HasValue)
                {
                    return $"{InputArrayName}[{inputRegisterKey.GSVertex}].{inputDeclaration.Name}";
                }
                return MethodInputRegisters.Count == 1 || NamesInputsAsParameters
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
                    ? StreamVariableName((registerComponentKey.RegisterKey as D3D10RegisterKey)?.Stream)
                        + "." + outputDeclaration.Name
                    : StreamVariableName((registerComponentKey.RegisterKey as D3D10RegisterKey)?.Stream);
            }
        }
        return GetRegisterName(registerComponentKey.RegisterKey);
    }

    private string _outputVariableName;

    /// <summary>"o", unless a global already has that name.</summary>
    public string OutputVariableName => _outputVariableName ??= UnusedName("o");

    /// <summary>A geometry shader always writes a struct to its stream.</summary>
    public bool HasOutputStruct =>
        MethodOutputRegisters.Count > 1 || _shaderModel.Type == ShaderType.Geometry;

    private string _inputVariableName;

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

    /// <summary>
    /// Temps main passes to a linkage method, by width: inside a body they are its
    /// parameters, named as the register (the instruction writer reads them so).
    /// </summary>
    public Dictionary<RegisterKey, int> LinkageTempParameters { get; } = [];

    public static string LinkageTempParameterName(RegisterKey register) => $"r{register.Number}";

    public string GetRegisterName(RegisterKey registerKey)
    {
        if (LinkageTempParameters.ContainsKey(registerKey))
        {
            return LinkageTempParameterName(registerKey);
        }
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
                // A ps_2_0 texture register is an ordinary input.
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
                        if (constDecl.TypeInfo.NumElements > 1)
                        {
                            int element = (registerKey.Number - constDecl.RegisterIndex)
                                / Math.Max(constDecl.RegistersPerElement, 1);
                            return $"{constDecl.Name}[{element}]";
                        }
                        return constDecl.Name;
                    }
                    (ShaderTypeInfo d3d9Matrix, string d3d9Name, int d3d9Register) =
                        TryGetMatrixRegister(registerKey).Value;
                    return MatrixRegisterName(d3d9Matrix, d3d9Name, d3d9Register);
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
                    (ShaderTypeInfo d3d10Matrix, string d3d10Name, int d3d10Register) =
                        TryGetMatrixRegister(registerKey).Value;
                    return MatrixRegisterName(d3d10Matrix, d3d10Name, d3d10Register);
                case OperandType.Immediate32:
                    return d3d10RegisterKey.Number.ToString();
                case OperandType.Input:
                    var decl = RegisterDeclarations[registerKey];
                    // Before the single-input shortcut: GS always reads the vertex array.
                    if (d3d10RegisterKey.GSVertex.HasValue)
                    {
                        return $"{InputArrayName}[{d3d10RegisterKey.GSVertex}].{decl.Name}";
                    }
                    if (MethodInputRegisters.Count == 1 || NamesInputsAsParameters)
                    {
                        return decl.Name;
                    }
                    return "i." + decl.Name;
                case OperandType.InputThreadID:
                case OperandType.InputThreadGroupID:
                case OperandType.InputThreadIDInGroup:
                case OperandType.InputThreadIDInGroupFlattened:
                case OperandType.InputCoverageMask:
                case OperandType.InputInnerCoverage:
                    {
                        string threadName = RegisterDeclarations[registerKey].Name;
                        return MethodInputRegisters.Count == 1 ? threadName : "i." + threadName;
                    }
                case OperandType.InputPrimitiveID:
                case OperandType.InputGSInstanceID:
                case OperandType.InputDomainPoint:
                case OperandType.OutputControlPointID:
                    return RegisterDeclarations[registerKey].Name;
                // Named from the signature: tess factors are one array over registers.
                case OperandType.InputPatchConstant:
                    {
                        RegisterSignature signature =
                            RegisterDeclarations[registerKey].PatchConstantSignature;
                        string field = signature == null
                            ? RegisterDeclarations[registerKey].Name
                            : PatchConstants.Reference(signature, _shaderModel.PatchConstantSignatures);
                        // A hull shader's join phase reads back the struct it is filling.
                        string holder = _shaderModel.Type == ShaderType.Hull
                            ? OutputVariableName
                            : PatchConstants.ParameterName;
                        return $"{holder}.{field}";
                    }
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
                case OperandType.ThreadGroupSharedMemory:
                    return GroupSharedName(registerKey.Number);
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

    /// <summary>
    /// By component where variables share a register, else by register (SM3, or
    /// a relative read).
    /// </summary>
    public ConstantDeclaration FindConstantOfComponent(RegisterComponentKey registerComponent)
    {
        return (registerComponent.RegisterKey is D3D10RegisterKey d3D10Key
            ? FindConstant(d3D10Key, registerComponent.ComponentIndex)
            : null) ?? FindConstant(registerComponent.RegisterKey);
    }

    /// <summary>
    /// From the constant or signature declaration; thread ids are uint. Unknown for
    /// temps and anything undeclared.
    /// </summary>
    public DeclaredType GetDeclaredType(RegisterComponentKey registerComponent)
    {
        if (registerComponent.RegisterKey is D3D10RegisterKey d3D10Key
            && D3D10Instruction.IsThreadRegister(d3D10Key.OperandType))
        {
            return DeclaredType.Uint;
        }
        if (registerComponent.RegisterKey is D3D10RegisterKey { OperandType: OperandType.ImmediateConstantBuffer })
        {
            return ImmediateConstantBufferHoldsBits(registerComponent.RegisterKey.Number)
                ? DeclaredType.Int
                : DeclaredType.Float;
        }
        if (FindConstantOfComponent(registerComponent) is ConstantDeclaration constant)
        {
            return constant.TypeInfo.ParameterType switch
            {
                ParameterType.Int => DeclaredType.Int,
                ParameterType.Uint => DeclaredType.Uint,
                ParameterType.Bool => DeclaredType.Bool,
                _ => DeclaredType.Float,
            };
        }
        if (RegisterDeclarations.TryGetValue(registerComponent.RegisterKey, out RegisterDeclaration declaration))
        {
            return declaration.ComponentType switch
            {
                1 => DeclaredType.Uint,
                2 => DeclaredType.Int,
                _ => declaration.IsInteger ? DeclaredType.Uint : DeclaredType.Float,
            };
        }
        return DeclaredType.Unknown;
    }

    /// <summary>Null unless groupshared memory wider than one register.</summary>
    public (string Name, int Components, int ComponentBase)? ThreadGroupSharedMember(
        RegisterKey resourceKey, int byteOffset)
    {
        if (resourceKey is not D3D10RegisterKey { OperandType: OperandType.ThreadGroupSharedMemory }
            || !ThreadGroupSharedMemory.TryGetValue(resourceKey.Number,
                out (int Stride, int Elements) shared)
            || shared.Stride <= 16)
        {
            return null;
        }
        return ThreadGroupSharedMemberAt(shared.Stride, byteOffset);
    }

    /// <summary>
    /// The dword a groupshared atomic reaches in an element wider than one: a lane
    /// of a vector element (`g0[i].y`) or of a struct's member (`g0[i].m4.x`).
    /// Null for a dword element, raw memory, or memory addressed by dword.
    /// </summary>
    public string ThreadGroupSharedLane(RegisterKey resourceKey, string element, int byteOffset)
    {
        if (resourceKey is not D3D10RegisterKey { OperandType: OperandType.ThreadGroupSharedMemory }
            || RawThreadGroupSharedMemory.Contains(resourceKey.Number)
            || DwordAddressedThreadGroupSharedMemory.Contains(resourceKey.Number)
            || !ThreadGroupSharedMemory.TryGetValue(resourceKey.Number, out (int Stride, int Elements) shared)
            || shared.Stride <= 4)
        {
            return null;
        }
        if (ThreadGroupSharedMember(resourceKey, byteOffset) is var (name, components, componentBase))
        {
            return components == 1
                ? $"{element}.{name}"
                : $"{element}.{name}.{"xyzw"[componentBase]}";
        }
        return $"{element}.{"xyzw"[byteOffset / 4]}";
    }

    /// <summary>
    /// The lanes a groupshared store writes where it leaves some of its vector
    /// (`g0[i].xy` of a uint3), or "" where it writes it whole or the element is a
    /// dword. Relative to the struct member where the element is a struct.
    /// </summary>
    public string ThreadGroupSharedStoreMask(RegisterKey resourceKey, int byteOffset, IList<int> components)
    {
        if (resourceKey is not D3D10RegisterKey { OperandType: OperandType.ThreadGroupSharedMemory }
            || RawThreadGroupSharedMemory.Contains(resourceKey.Number)
            || DwordAddressedThreadGroupSharedMemory.Contains(resourceKey.Number)
            || !ThreadGroupSharedMemory.TryGetValue(resourceKey.Number, out (int Stride, int Elements) shared)
            || shared.Stride <= 4)
        {
            return "";
        }
        (int width, int first) = ThreadGroupSharedMember(resourceKey, byteOffset) is var (_, members, memberBase)
            ? (members, memberBase)
            : (shared.Stride / 4, byteOffset / 4);
        int[] lanes = [.. components.Select(c => first + c)];
        return lanes.SequenceEqual(Enumerable.Range(0, width))
            ? ""
            : "." + string.Concat(lanes.Select(lane => "xyzw"[lane]));
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

    private string _temporaryPrefix;

    /// <summary>"t", unless a global is named t + digits (e.g. `sampler2D t0`).</summary>
    public string TemporaryPrefix
    {
        get
        {
            if (_temporaryPrefix != null)
            {
                return _temporaryPrefix;
            }
            HashSet<string> taken =
            [
                .. ConstantDeclarations.Select(d => d.Name),
                .. ResourceDefinitions.Select(d => d.Name),
            ];
            _temporaryPrefix = "t";
            while (taken.Any(name => IsPrefixedNumber(name, _temporaryPrefix)))
            {
                _temporaryPrefix += "_";
            }
            return _temporaryPrefix;
        }
    }

    private static bool IsPrefixedNumber(string name, string prefix)
    {
        return name.Length > prefix.Length
            && name.StartsWith(prefix, StringComparison.Ordinal)
            && name.Skip(prefix.Length).All(char.IsAsciiDigit);
    }

    public ConstantDeclaration FindConstant(RegisterKey registerKey)
    {
        if (registerKey is D3D10RegisterKey d3D10RegisterKey)
        {
            if (d3D10RegisterKey.OperandType == OperandType.ConstantBuffer)
            {
                const int RegisterSize = 4 * sizeof(float);
                int expectedOffset = (int)d3D10RegisterKey.ConstantBufferOffset * RegisterSize;
                ConstantDeclaration declaration = ConstantDeclarations.FirstOrDefault(d =>
                {
                    // A tbuffer variable has a register index too, but binds to t#.
                    if (d is D3D10ConstantDeclaration { IsTextureBuffer: true })
                    {
                        return false;
                    }
                    if (d.RegisterIndex != d3D10RegisterKey.Number)
                    {
                        return false;
                    }
                    // Overlapping the register anywhere: packoffset(c0.z) starts mid-way.
                    // The first of several is taken; per component, see the overload.
                    var constant = d as D3D10ConstantDeclaration;
                    return constant.VariableOffset < expectedOffset + RegisterSize
                        && expectedOffset < constant.VariableOffset + constant.VariableSize;
                });
                // Null for a register packoffset left empty; callers handle it.
                return declaration;
            }
            return null;
        }
        return FindConstant(registerKey as D3D9RegisterKey);
    }

    // By component, so packed scalars resolve to the right declaration.
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
            && !constant.IsTextureBuffer
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

        // Register numbers are per set: s0 and c0 differ.
        return ConstantDeclarations.FirstOrDefault(c =>
            c is D3D9ConstantDeclaration declaration
            && declaration.RegisterSet == registerSet
            && declaration.ContainsIndex(registerKey.Number));
    }

    // A def read through a0 or aL is a literal array rather than a folded constant.
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

        // A constant offset folded into the index may put the subscripted register
        // mid-array, so walk backwards to the run's start.
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
            arrays.Add(new ConstantArray(start, registers, LocalNamePrefix));
        }
        return arrays;
    }

    public string GroupSharedName(int register)
    {
        return $"{LocalNamePrefix}g{register}";
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
            // Every variable packed into the register. Offset is the variable's index
            // in the buffer, not a register, so VariableOffset is used.
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

    /// <summary>t and u registers number separately; the operand type picks which.</summary>
    public ResourceDefinition GetTextureDefinition(RegisterKey registerKey)
    {
        bool isUav = registerKey is D3D10RegisterKey { OperandType: OperandType.UnorderedAccessView };
        return ResourceDefinitions
            .FirstOrDefault(d => d.BindPoint == registerKey.Number
                && (isUav
                    ? d.ShaderInputType is D3DShaderInputType.UavRWTyped
                    : d.ShaderInputType is D3DShaderInputType.Texture));
    }

    public int GetResourceDimensionSize(RegisterKey registerKey)
    {
        return GetTextureDefinition(registerKey).GetDimensionSize();
    }

    /// <summary>
    /// Subscript components of an atomic's address: a typed UAV's dimension count,
    /// else 1 (the byte offset of structured or groupshared memory is not part of it).
    /// </summary>
    public int GetAtomicAddressWidth(RegisterKey resourceKey)
    {
        return FindTypedUavView(resourceKey)?.GetDimensionSize() ?? 1;
    }

    private ResourceDefinition FindTypedUavView(RegisterKey resourceKey)
    {
        // Check the kind: groupshared g0 and u0 share a number.
        if (resourceKey is not D3D10RegisterKey { OperandType: OperandType.UnorderedAccessView })
        {
            return null;
        }
        return ResourceDefinitions.FirstOrDefault(d =>
            d.ShaderInputType == D3DShaderInputType.UavRWTyped && d.BindPoint == resourceKey.Number);
    }

    /// <summary>
    /// Such a view is declared with a scalar element; see
    /// <see cref="ResourceDefinition.ReadWriteAtomicTypeName"/>.
    /// </summary>
    public void DeclareAtomicTarget(RegisterKey resourceKey)
    {
        ResourceDefinition view = FindTypedUavView(resourceKey);
        if (view != null)
        {
            _atomicViews.Add(view);
        }
    }

    public bool IsAtomicTarget(ResourceDefinition resource)
    {
        return _atomicViews.Contains(resource);
    }

    public void DeclareResource(D3D10RegisterKey registerKey, ResourceDimension resourceDimension, int resourceReturnType, int sampleCount = 0)
    {
        // A tbuffer declares like a texture; only the binding tells. Its variables
        // are never read as cb registers, so they are added here.
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

    // Element width in components, from the byte stride, capped at 4.
    private readonly Dictionary<RegisterKey, int> _structuredBufferComponents = [];

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

    // No stride; 4 components, the most one load reads.
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

    /// <summary>
    /// Groupshared memory has no reflection, so a wide element is split into whole
    /// registers plus the remainder (20 bytes: a float4 and a float), named by offset.
    /// </summary>
    public static IList<(string Name, int ByteOffset, int Components)> ThreadGroupSharedMembers(int stride)
    {
        var members = new List<(string, int, int)>();
        for (int offset = 0; offset < stride; offset += ConstantRegisterSizeInBytes)
        {
            int components = Math.Min(stride - offset, ConstantRegisterSizeInBytes) / 4;
            members.Add(($"m{offset / 4}", offset, components));
        }
        return members;
    }

    public static (string Name, int Components, int ComponentBase)? ThreadGroupSharedMemberAt(
        int stride, int byteOffset)
    {
        foreach ((string name, int offset, int components) in ThreadGroupSharedMembers(stride))
        {
            if (byteOffset >= offset && byteOffset < offset + components * 4)
            {
                return (name, components, (byteOffset - offset) / 4);
            }
        }
        return null;
    }

    public static string ThreadGroupSharedElementType(int register)
    {
        return $"G{register}Element";
    }

    public void DeclareThreadGroupSharedMemory(D3D10RegisterKey registerKey, uint stride, uint elements,
        bool isRaw = false)
    {
        DeclareStructuredStride(registerKey, stride);
        ThreadGroupSharedMemory[registerKey.Number] = ((int)stride, (int)elements);
        if (isRaw)
        {
            RawThreadGroupSharedMemory.Add(registerKey.Number);
        }
        else if (_shaderModel.Instructions.OfType<D3D10Instruction>().Any(instruction =>
            instruction.Opcode is D3D10Opcode.LdStructured or D3D10Opcode.StoreStructured
            && instruction.GetOperandType(2) != OperandType.Immediate32
            && Equals(instruction.GetParamRegisterKey(instruction.Opcode == D3D10Opcode.LdStructured ? 3 : 0), registerKey)))
        {
            DwordAddressedThreadGroupSharedMemory.Add(registerKey.Number);
        }
    }

    public bool IsDwordAddressedThreadGroupSharedMemory(RegisterKey registerKey)
    {
        return registerKey is D3D10RegisterKey { OperandType: OperandType.ThreadGroupSharedMemory } key
            && DwordAddressedThreadGroupSharedMemory.Contains(key.Number);
    }

    /// <summary>
    /// fxc declares a non-array groupshared variable raw, an array structured, so a
    /// raw 4-byte one is written without a subscript. Wider raw ones stay arrays.
    /// </summary>
    public bool IsGroupSharedScalar(int register)
    {
        return RawThreadGroupSharedMemory.Contains(register)
            && ThreadGroupSharedMemory.TryGetValue(register, out (int Stride, int Elements) shared)
            && shared.Stride == sizeof(uint) && shared.Elements == 1;
    }

    public bool IsGroupSharedScalar(RegisterKey registerKey)
    {
        return registerKey is D3D10RegisterKey
            {
                OperandType: OperandType.ThreadGroupSharedMemory
            } key
            && IsGroupSharedScalar(key.Number);
    }

    public string GroupSharedElementReference(RegisterKey registerKey, string name, string index)
    {
        return IsGroupSharedScalar(registerKey) ? name : $"{name}[{index}]";
    }

    public void DeclareUnorderedAccessView(D3D10RegisterKey registerKey, uint stride)
    {
        DeclareStructuredStride(registerKey, stride);
        // Append, consume and counter buffers all declare dcl_uav_structured.
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

            // A ps_2_0 texture register is an ordinary input.
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
        // The _sgv forms declare system-generated values (vertex_id, is_front_face...).
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
                    var vertexKey = registerKey.WithVertex(vertex);

                    if (RegisterDeclarations.TryGetValue(vertexKey, out var existingDeclaration))
                    {
                        // Packed inputs, as below.
                        RegisterDeclaration candidate = CreateRegisterDeclarationFromD3D10Dcl(instruction, vertexKey);
                        if (candidate.Semantic != existingDeclaration.Semantic)
                        {
                            candidate.MaskedLengthOverride = CountSetBits(candidate.WriteMask);
                            existingDeclaration.MaskedLengthOverride =
                                CountSetBits(existingDeclaration.WriteMask);
                            InputListOf(vertexKey).Add(candidate);
                        }
                        else
                        {
                            existingDeclaration.WriteMask |= instruction.GetDestinationWriteMask();
                        }
                    }
                    else
                    {
                        var registerDeclaration = CreateRegisterDeclarationFromD3D10Dcl(instruction, vertexKey);
                        RegisterDeclarations.Add(vertexKey, registerDeclaration);
                        InputListOf(vertexKey).Add(registerDeclaration);
                    }
                }
            }
            else
            {
                if (RegisterDeclarations.TryGetValue(registerKey, out var existingDeclaration))
                {
                    // Inputs and outputs are packed (TEXCOORD0 at v2.xy, TEXCOORD1 at
                    // v2.z); a different semantic is a second field of its own.
                    RegisterDeclaration candidate = registerKey.OperandType is OperandType.Input or OperandType.Output
                        ? CreateRegisterDeclarationFromD3D10Dcl(instruction, registerKey)
                        : null;
                    if (candidate != null && candidate.Semantic != existingDeclaration.Semantic)
                    {
                        // Neither starts at x necessarily, so count bits rather than
                        // taking the highest bit plus one.
                        candidate.MaskedLengthOverride = CountSetBits(candidate.WriteMask);
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
                        case OperandType.InputDomainPoint:
                        case OperandType.InputCoverageMask:
                        case OperandType.InputInnerCoverage:
                        case OperandType.OutputControlPointID:
                            MethodInputRegisters.Add(registerDeclaration);
                            break;
                        case OperandType.InputPatchConstant:
                            PatchConstantRegisters.Add(registerDeclaration);
                            break;
                        // Per primitive (and per GS instance), not per vertex: own parameters.
                        case OperandType.InputPrimitiveID:
                            PrimitiveIdDeclaration = registerDeclaration;
                            break;
                        case OperandType.InputGSInstanceID:
                            GSInstanceIdDeclaration = registerDeclaration;
                            break;
                        case OperandType.Output:
                        case OperandType.OutputDepth:
                        case OperandType.OutputDepthGreaterEqual:
                        case OperandType.OutputDepthLessEqual:
                        case OperandType.OutputCoverageMask:
                        case OperandType.OutputStencilRef:
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
    /// dcl_indexrange makes a run of inputs one array (`float4 texcoord[4] : TEXCOORD`):
    /// the first grows a length, the rest stop being parameters but stay in
    /// RegisterDeclarations for reads by name.
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
            // Not for a packed output: widening the first would swallow the others.
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

    /// <summary>
    /// GS streams each number their registers from o0, so the stream must match; a
    /// register without one is stream 0.
    /// </summary>
    private static bool MatchesSignature(RegisterSignature signature, D3D10RegisterKey registerKey)
    {
        // Control points read back by the patch constant phase are in the output signature.
        bool sameType = registerKey.OperandType == OperandType.OutputControlPoint
            ? signature.RegisterKey.OperandType == OperandType.Output && !signature.IsPatchConstant
            : signature.RegisterKey.OperandType == registerKey.OperandType;
        return sameType
            && signature.RegisterKey.Number == registerKey.Number
            && signature.Stream == (registerKey.Stream ?? 0);
    }

    private RegisterDeclaration CreateRegisterDeclarationFromD3D10Dcl(D3D10Instruction instruction, D3D10RegisterKey registerKey)
    {
        registerKey = registerKey.GetGSBaseKey();
        // Match on the declared mask too, so packed inputs find their own signatures.
        int declaredMask = instruction.GetDestinationWriteMask();
        IEnumerable<RegisterSignature> signatures = _shaderModel.InputSignatures
            .Concat(_shaderModel.OutputSignatures)
            .Concat(_shaderModel.PatchConstantSignatures)
            .Concat(_shaderModel.ControlPointSignatures ?? []);
        RegisterSignature signature =
            signatures.FirstOrDefault(i => MatchesSignature(i, registerKey) && (i.Mask & declaredMask) != 0)
            ?? signatures.FirstOrDefault(i => MatchesSignature(i, registerKey));
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
                MinPrecision = signature.MinPrecision,
                InterpolationMode = instruction.GetInterpolationMode(),
            };
            // Not starting at x means packed above something, declared here or not
            // (`float thickness` at v0.w is one component).
            if ((signature.Mask & 1) == 0)
            {
                declaration.MaskedLengthOverride = CountSetBits(signature.Mask);
            }
            // Patch constants pack around the tessellation factors.
            if (registerKey.OperandType == OperandType.InputPatchConstant)
            {
                declaration.MaskedLengthOverride = CountSetBits(signature.Mask);
                declaration.PatchConstantSignature = signature;
            }
            // The hull shader writes them, named as fields of the struct it returns.
            if (signature.IsPatchConstant && registerKey.OperandType == OperandType.Output)
            {
                declaration.MaskedLengthOverride = CountSetBits(signature.Mask);
                declaration.PatchConstantSignature = signature;
                declaration.NameOverride =
                    PatchConstants.Reference(signature, _shaderModel.PatchConstantSignatures);
            }
            return declaration;
        }

        // 4 is a guess for everything else.
        bool isScalar = registerKey.OperandType == OperandType.OutputDepth
            || registerKey.OperandType == OperandType.OutputDepthGreaterEqual
            || registerKey.OperandType == OperandType.OutputDepthLessEqual
            || registerKey.OperandType == OperandType.OutputCoverageMask
            || registerKey.OperandType == OperandType.InputCoverageMask
            || registerKey.OperandType == OperandType.OutputStencilRef
            || registerKey.OperandType == OperandType.InputInnerCoverage
            || registerKey.OperandType == OperandType.InputThreadIDInGroupFlattened
            || registerKey.OperandType == OperandType.InputPrimitiveID
            || registerKey.OperandType == OperandType.InputGSInstanceID
            || registerKey.OperandType == OperandType.OutputControlPointID;
        // vDomain is 2 wide on a quad, 3 on a triangle; the dcl mask says which.
        int writeMask = registerKey.OperandType == OperandType.InputDomainPoint
            ? instruction.GetDestinationWriteMask()
            : isScalar ? 1 : 4;
        // uints the signature keys to register -1, so the lookup above misses them.
        const int UInt32ComponentType = 1;
        int componentType =
            registerKey.OperandType == OperandType.OutputCoverageMask
            || registerKey.OperandType == OperandType.InputCoverageMask
            || registerKey.OperandType == OperandType.OutputStencilRef
            || registerKey.OperandType == OperandType.InputInnerCoverage
            || registerKey.OperandType == OperandType.OutputControlPointID
            ? UInt32ComponentType : 0;
        return new RegisterDeclaration(registerKey, instruction.GetDeclSemantic(), writeMask)
        {
            ComponentType = componentType,
        };
    }
}
