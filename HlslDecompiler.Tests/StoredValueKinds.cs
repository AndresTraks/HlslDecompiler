using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HlslDecompiler.Tests;

public enum StoredValueKind { Unknown, Integer, Float }

/// <summary>
/// What a machine output holds, by the shader's own reflection data: integers
/// compare as bits, since a packed uint with its high bits set reads as an
/// ordinary float and a relative tolerance would equate it with one missing its
/// low bits. Floats keep the tolerance; anything untyped (a raw store, a D3D9
/// output, a double half) keeps the old rule.
/// </summary>
internal sealed partial class StoredValueKinds(ShaderModel shader)
{
    [GeneratedRegex(@"^STORE(\d+)\[-?\d+\]\[(-?\d+)\]$")]
    private static partial Regex StructuredStore();

    [GeneratedRegex(@"^STORETYPED(\d+)\[")]
    private static partial Regex TypedStore();

    public StoredValueKind Of(string name, int component)
    {
        if (name.StartsWith("ATOMIC"))
        {
            return StoredValueKind.Integer;
        }
        if (name.StartsWith("STORERAW"))
        {
            return StoredValueKind.Unknown;
        }
        Match typed = TypedStore().Match(name);
        if (typed.Success)
        {
            return View(int.Parse(typed.Groups[1].Value), [D3DShaderInputType.UavRWTyped])?
                .ResourceReturnType switch
            {
                D3DResourceReturnType.UInt or D3DResourceReturnType.SInt => StoredValueKind.Integer,
                D3DResourceReturnType.Float => StoredValueKind.Float,
                _ => StoredValueKind.Unknown,
            };
        }
        Match structured = StructuredStore().Match(name);
        if (structured.Success)
        {
            ResourceDefinition view = View(int.Parse(structured.Groups[1].Value),
            [
                D3DShaderInputType.UavRWStructured,
                D3DShaderInputType.UavAppendStructured,
                D3DShaderInputType.UavConsumeStructured,
                D3DShaderInputType.UavRWStucturedWithCounter,
            ]);
            int offset = int.Parse(structured.Groups[2].Value);
            return view?.ElementType == null ? StoredValueKind.Unknown : KindAt(view.ElementType, offset);
        }
        return OutputKind(name, component);
    }

    private ResourceDefinition View(int bindPoint, D3DShaderInputType[] types)
    {
        return shader.ResourceDefinitions?
            .FirstOrDefault(d => d.BindPoint == bindPoint && types.Contains(d.ShaderInputType));
    }

    // An output is keyed by one semantic but holds its whole register, which
    // other semantics may share: each component is typed by whichever covers it.
    private StoredValueKind OutputKind(string semantic, int component)
    {
        IList<RegisterSignature> outputs = shader.OutputSignatures;
        RegisterSignature named = outputs?.FirstOrDefault(s => s.Name + s.Index == semantic);
        if (named == null)
        {
            return StoredValueKind.Unknown;
        }
        RegisterSignature covering = outputs.FirstOrDefault(s =>
            s.RegisterKey.Equals(named.RegisterKey) && (s.Mask & (1 << component)) != 0);
        const int UInt32ComponentType = 1;
        const int SInt32ComponentType = 2;
        const int Float32ComponentType = 3;
        return covering?.ComponentType switch
        {
            UInt32ComponentType or SInt32ComponentType => StoredValueKind.Integer,
            Float32ComponentType => StoredValueKind.Float,
            _ => StoredValueKind.Unknown,
        };
    }

    // The leaf a byte offset falls in, in a tightly packed structured element.
    private static StoredValueKind KindAt(ShaderTypeInfo type, int offset)
    {
        int one = ElementSize(type);
        if (one <= 0 || offset < 0 || offset >= one * System.Math.Max(type.NumElements, 1))
        {
            return StoredValueKind.Unknown;
        }
        offset %= one;
        if (type.MemberInfo is { Count: > 0 } members)
        {
            ShaderStructMemberInfo member = members.LastOrDefault(m => m.ByteOffset <= offset);
            return member == null ? StoredValueKind.Unknown : KindAt(member.TypeInfo, offset - member.ByteOffset);
        }
        return type.ParameterType switch
        {
            ParameterType.Int or ParameterType.Uint or ParameterType.Bool => StoredValueKind.Integer,
            ParameterType.Float => StoredValueKind.Float,
            _ => StoredValueKind.Unknown,
        };
    }

    // One element's bytes; doubles are 8 a component.
    private static int ElementSize(ShaderTypeInfo type)
    {
        if (type.MemberInfo is { Count: > 0 } members)
        {
            return members.Max(m => m.ByteOffset + ElementSize(m.TypeInfo) * System.Math.Max(m.TypeInfo.NumElements, 1));
        }
        int width = type.ParameterType == ParameterType.Double ? 8 : 4;
        return System.Math.Max(type.Rows, 1) * System.Math.Max(type.Columns, 1) * width;
    }
}
