using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace HlslDecompiler.Tests;

/// <summary>
/// An effect's structure as text, everything in it but the shaders' code: the
/// buffers and variables with their types, values and annotations, the state
/// objects, the techniques and passes and what they set. A shader is its profile
/// and its stream output, its code being what the equivalence tier compares.
///
/// Two effects with the same description are the same effect to the runtime, which
/// is what an effect decompiled and compiled again is held to.
/// </summary>
public static class EffectDescription
{
    public static string Describe(Effect effect)
    {
        var text = new StringBuilder();
        text.AppendLine(effect.Profile);
        foreach (EffectConstantBuffer buffer in effect.ConstantBuffers)
        {
            text.AppendLine($"{(buffer.IsTextureBuffer ? "tbuffer" : "cbuffer")} {buffer.Name} size {buffer.Size} bind {buffer.ExplicitBindPoint}");
            Annotations(text, "  ", buffer.Annotations);
            foreach (EffectNumericVariable variable in buffer.Variables)
            {
                text.AppendLine($"  {Type(variable.Type)} {variable.Name}"
                    + (variable.Semantic != null ? $" : {variable.Semantic}" : "")
                    + $" @{variable.Offset}"
                    + (variable.HasExplicitBindPoint ? " explicit" : "")
                    + (variable.DefaultValue != null ? $" = {Value(variable.DefaultValue)}" : ""));
                Annotations(text, "    ", variable.Annotations);
            }
        }
        foreach (EffectObjectVariable variable in effect.ObjectVariables)
        {
            text.AppendLine($"{Type(variable.Type)} {variable.Name}"
                + (variable.Semantic != null ? $" : {variable.Semantic}" : "")
                + (variable.ExplicitBindPoint != -1 ? $" bind {variable.ExplicitBindPoint}" : ""));
            Annotations(text, "  ", variable.Annotations);
            for (int i = 0; i < variable.Blocks.Count; i++)
            {
                text.AppendLine($"  [{i}]");
                Assignments(text, "    ", variable.Blocks[i]);
            }
            foreach (EffectShader shader in variable.Shaders)
            {
                text.AppendLine($"  {Shader(shader)}");
            }
            foreach (string value in variable.Strings)
            {
                text.AppendLine($"  \"{value}\"");
            }
        }
        foreach (EffectGroup group in effect.Groups)
        {
            text.AppendLine($"group {group.Name ?? "(none)"}");
            Annotations(text, "  ", group.Annotations);
            foreach (EffectTechnique technique in group.Techniques)
            {
                text.AppendLine($"  technique {technique.Name}");
                Annotations(text, "    ", technique.Annotations);
                foreach (EffectPass pass in technique.Passes)
                {
                    text.AppendLine($"    pass {pass.Name}");
                    Annotations(text, "      ", pass.Annotations);
                    Assignments(text, "      ", pass.Assignments);
                }
            }
        }
        return text.ToString();
    }

    private static string Type(EffectType type)
    {
        string elements = type.Elements > 0 ? $"[{type.Elements}]" : "";
        return type.Class switch
        {
            EffectVariableClass.Numeric => $"{type.Name}{elements} ({type.Layout} {type.ScalarType} {type.Rows}x{type.Columns}"
                + (type.IsColumnMajor ? " column" : "") + $", {type.TotalSize}/{type.Stride}/{type.PackedSize})",
            EffectVariableClass.Struct => $"struct {type.Name}{elements} {{ "
                + string.Join("; ", type.Members.Select(m => $"{Type(m.Type)} {m.Name}"
                    + (m.Semantic != null ? $" : {m.Semantic}" : "") + $" @{m.Offset}"))
                + $" }} ({type.TotalSize}/{type.Stride}/{type.PackedSize})",
            _ => $"{type.Name}{elements} ({type.ObjectType})",
        };
    }

    private static void Annotations(StringBuilder text, string indent, IReadOnlyList<EffectAnnotation> annotations)
    {
        foreach (EffectAnnotation annotation in annotations)
        {
            string value = annotation.Value != null
                ? Value(annotation.Value)
                : string.Join(", ", annotation.Strings.Select(s => $"\"{s}\""));
            text.AppendLine($"{indent}<{Type(annotation.Type)} {annotation.Name} = {value}>");
        }
    }

    private static void Assignments(StringBuilder text, string indent, IReadOnlyList<EffectAssignment> assignments)
    {
        foreach (EffectAssignment assignment in assignments)
        {
            string state = assignment.State < EffectStates.All.Count
                ? EffectStates.All[assignment.State].Name
                : $"state{assignment.State}";
            string value = assignment.Kind switch
            {
                EffectAssignmentKind.Constant => string.Join(", ", assignment.Constants.Select(c => c.Type == EffectScalarType.Float
                    ? BitConverter.UInt32BitsToSingle(c.Bits).ToString("R", CultureInfo.InvariantCulture)
                    : $"{c.Type} {c.Bits}")),
                EffectAssignmentKind.Variable => assignment.VariableName,
                EffectAssignmentKind.ConstantIndex => $"{assignment.VariableName}[{assignment.ArrayIndex}]",
                EffectAssignmentKind.VariableIndex => $"{assignment.VariableName}[{assignment.IndexVariableName}]",
                EffectAssignmentKind.ExpressionIndex => $"{assignment.VariableName}[expression {assignment.Expression.Length} bytes]",
                EffectAssignmentKind.Expression => $"expression {assignment.Expression.Length} bytes",
                _ => Shader(assignment.Shader),
            };
            text.AppendLine($"{indent}{state}[{assignment.Index}] = {value}");
        }
    }

    private static string Shader(EffectShader shader)
    {
        string profile = "NULL";
        if (shader.Bytecode != null)
        {
            using var reader = new DxbcReader(new MemoryStream(shader.Bytecode));
            profile = reader.ReadShader().Profile;
        }
        return shader.HasStreamOut
            ? $"{profile} streamout({string.Join(" | ", shader.StreamOutDeclarations.Select(d => $"\"{d}\""))}) rasterized {shader.RasterizedStream}"
            : profile;
    }

    private static string Value(byte[] bytes)
    {
        return string.Join(" ", Enumerable.Range(0, bytes.Length / 4).Select(i => $"{BitConverter.ToUInt32(bytes, i * 4):X8}"));
    }
}
