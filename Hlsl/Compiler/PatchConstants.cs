using System;
using System.Collections.Generic;
using System.Linq;
using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// Per-patch values a hull shader writes and a domain shader reads as one struct.
/// They have their own signature chunk, which lists the tessellation factors even
/// when unread, so the domain shader's parameter is built from it, not from dcls.
/// </summary>
public static class PatchConstants
{
    public const string ParameterName = "constants";

    /// <summary>
    /// The patch constant function. Its real name is not in the bytecode, so any
    /// name works as long as the attribute and the definition agree.
    /// </summary>
    public const string FunctionName = "constants";

    public static string StructureName(ShaderType type)
    {
        return type == ShaderType.Hull ? "HS_CONST" : "DS_CONST";
    }

    private const string TessFactorSemantic = "SV_TessFactor";
    private const string InsideTessFactorSemantic = "SV_InsideTessFactor";

    /// <summary>
    /// The tessellation factors are one element per edge but one array in HLSL,
    /// so several elements share a field.
    /// </summary>
    public static string FieldName(RegisterSignature signature)
    {
        if (IsTessFactor(signature))
        {
            return "edges";
        }
        if (IsInsideTessFactor(signature))
        {
            return "inside";
        }
        return signature.Index == 0
            ? signature.Name.ToLower()
            : signature.Name.ToLower() + signature.Index;
    }

    public static string Reference(RegisterSignature signature, IEnumerable<RegisterSignature> all)
    {
        string field = FieldName(signature);
        if (IsTessFactor(signature) || (IsInsideTessFactor(signature) && Count(all, signature) > 1))
        {
            return $"{field}[{signature.Index}]";
        }
        return field;
    }

    /// <summary>
    /// Tessellation factors first: fxc then gives each a register's x and packs the
    /// rest around them, matching the bytecode.
    /// </summary>
    public static IEnumerable<string> Fields(IList<RegisterSignature> signatures)
    {
        foreach (IGrouping<string, RegisterSignature> field in signatures
            .OrderBy(s => IsTessFactor(s) ? 0 : IsInsideTessFactor(s) ? 1 : 2)
            .ThenBy(s => s.RegisterKey.Number)
            .ThenBy(s => s.Mask)
            .GroupBy(FieldName))
        {
            RegisterSignature first = field.First();
            int count = field.Count();
            string subscript = count > 1 ? $"[{count}]" : "";
            // A single field carries its index (`: B210`); an array's elements take
            // theirs from their position.
            string semantic = count == 1 && first.Index != 0 ? first.Name + first.Index : first.Name;
            yield return $"{TypeName(first)} {field.Key}{subscript} : {semantic};";
        }
    }

    private static string TypeName(RegisterSignature signature)
    {
        string type = signature.ComponentType switch
        {
            1 => "uint",
            2 => "int",
            _ => "float",
        };
        int width = CountSetBits(signature.Mask);
        return width > 1 ? type + width : type;
    }

    private static int Count(IEnumerable<RegisterSignature> all, RegisterSignature signature)
    {
        return all.Count(s => s.Name.Equals(signature.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTessFactor(RegisterSignature signature)
    {
        return signature.Name.Equals(TessFactorSemantic, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInsideTessFactor(RegisterSignature signature)
    {
        return signature.Name.Equals(InsideTessFactorSemantic, StringComparison.OrdinalIgnoreCase);
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
}
