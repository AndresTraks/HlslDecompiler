using System;
using System.Collections.Generic;
using System.Linq;
using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// The values a hull shader computes once for a whole patch, which a domain shader
/// reads through one struct parameter. They are not in the input signature - they
/// have a chunk of their own - and the tessellation factors among them are in the
/// signature whether the shader reads them or not, so a domain shader's parameter
/// list is built from this rather than from what it declares.
/// </summary>
public static class PatchConstants
{
    public const string StructureName = "DS_CONST";
    public const string ParameterName = "constants";

    private const string TessFactorSemantic = "SV_TessFactor";
    private const string InsideTessFactorSemantic = "SV_InsideTessFactor";

    /// <summary>
    /// The field a signature element belongs to. The tessellation factors come one
    /// element per edge and are one array in HLSL, so several elements share a name.
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

    /// <summary>How the field is read, subscript and all.</summary>
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
    /// One line per field, in the order that packs the way the bytecode is packed:
    /// the tessellation factors take a register's x apiece and everything else fills
    /// in around them, which is what fxc does when they are declared first.
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
            yield return $"{TypeName(first)} {field.Key}{subscript} : {first.Name};";
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
