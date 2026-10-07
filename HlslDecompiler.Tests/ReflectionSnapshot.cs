using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HlslDecompiler.Tests;

/// <summary>
/// What a shader declares, as lines that compare: what the host binds and reads it
/// by, and the declarations that change what it does without changing its
/// arithmetic. The equivalence tier runs a shader and compares what it computes;
/// none of this is visible to it - a constant buffer laid out differently, a
/// variable renamed, an interpolation mode dropped all compute the same in the
/// machine and something else on a GPU, or nothing at all once the host looks a
/// name up.
///
/// Write masks are left out of the declarations: a decompilation may read fewer
/// components of an input than the original declared, and fxc declares what is read.
/// </summary>
public static class ReflectionSnapshot
{
    public static List<string> Lines(ShaderModel shader)
    {
        var lines = new List<string>();
        if (shader.Instructions.FirstOrDefault() is D3D10Instruction)
        {
            AddConstantBuffers(shader, lines);
            AddResources(shader, lines);
            AddSignatures(shader, lines);
            AddInterfaces(shader, lines);
        }
        else
        {
            AddConstantTable(shader, lines);
        }
        AddDeclarations(shader, lines);
        lines.Sort(System.StringComparer.Ordinal);
        return lines;
    }

    private static void AddConstantBuffers(ShaderModel shader, List<string> lines)
    {
        foreach (D3D10ConstantDeclaration variable in shader.ConstantDeclarations)
        {
            string buffer = variable.IsTextureBuffer ? "tbuffer" : "cbuffer";
            lines.Add($"{buffer} {variable.BufferName} {variable.Name} " +
                $"offset {variable.VariableOffset} size {variable.VariableSize} {Describe(variable.TypeInfo)}");
        }
    }

    private static void AddResources(ShaderModel shader, List<string> lines)
    {
        foreach (ResourceDefinition resource in shader.ResourceDefinitions)
        {
            string element = resource.ElementType == null ? "" : $" element {Describe(resource.ElementType)}";
            // Whether the author named the slot: the decompilation names every one,
            // and the slot itself is the bind point compared beside it.
            D3DShaderInputFlags flags = resource.Flags & ~D3DShaderInputFlags.UserPacked;
            lines.Add($"resource {resource.Name} {resource.ShaderInputType} {resource.ResourceReturnType} " +
                $"dimension {resource.ResourceViewDimension} samples {resource.NumSamples} " +
                $"bind {resource.BindPoint} count {resource.BindCount} flags {flags}{element}");
        }
    }

    private static void AddSignatures(ShaderModel shader, List<string> lines)
    {
        void Add(string kind, IEnumerable<RegisterSignature> signatures)
        {
            foreach (RegisterSignature signature in signatures)
            {
                lines.Add($"{kind} {signature.Name}{signature.Index} register {signature.RegisterKey.Number} " +
                    $"mask {signature.Mask:X} system {signature.ValueType} component {signature.ComponentType} " +
                    $"stream {signature.Stream}");
            }
        }
        Add("input", shader.InputSignatures);
        Add("output", shader.OutputSignatures);
        Add("patch", shader.PatchConstantSignatures);
    }

    private static void AddInterfaces(ShaderModel shader, List<string> lines)
    {
        if (shader.Interfaces == null)
        {
            return;
        }
        for (int i = 0; i < shader.Interfaces.ClassTypeNames.Count; i++)
        {
            lines.Add($"class {i} {shader.Interfaces.ClassTypeNames[i]}");
        }
        foreach (InterfaceSlotRecord record in shader.Interfaces.SlotRecords)
        {
            lines.Add($"slots {record.SlotSpan} types {string.Join(",", record.TypeIds)} " +
                $"tables {string.Join(",", record.TableIds)}");
        }
    }

    private static void AddConstantTable(ShaderModel shader, List<string> lines)
    {
        // The reader answers an empty table for a comment that is not one - a
        // preshader, say.
        foreach (D3D9Instruction comment in shader.Instructions.OfType<D3D9Instruction>()
            .Where(i => i.Opcode == Opcode.Comment))
        {
            using var reader = new ConstantTableCommentReader(comment);
            foreach (D3D9ConstantDeclaration constant in reader.ReadTable().Declarations)
            {
                lines.Add($"constant {constant.Name} {constant.RegisterSet} {constant.RegisterIndex} " +
                    $"count {constant.RegisterCount} {Describe(constant.TypeInfo)}");
            }
        }
    }

    // The declarations a shader runs differently by: everything a dcl says but the
    // registers' masks, and the temps it needs, which are the compiler's business.
    private static readonly Regex Mask = new(@"\b([a-zA-Z]+\d*(\[\d+\])*)\.[xyzw]{1,4}\b");

    // What a hull shader's phases hand one another: the control points they read and
    // the patch constants a later phase reads back from an earlier one. Which phase
    // computes a value, and which reads it back rather than computing it again, is
    // fxc's to schedule; what leaves the shader is its signatures, compared above.
    private static readonly Regex HullPhaseInput = new(@"^dcl_input (vpc|vicp|vocp)");

    private static void AddDeclarations(ShaderModel shader, List<string> lines)
    {
        foreach (string line in ShaderAssembler.WriteAsm(shader).Split('\n'))
        {
            if (!line.StartsWith("dcl") || line.StartsWith("dcl_temps")
                || line.StartsWith("dcl_indexableTemp") || line.StartsWith("dcl_immediateConstantBuffer"))
            {
                continue;
            }
            if (shader.Type == ShaderType.Hull && HullPhaseInput.IsMatch(line.Trim()))
            {
                continue;
            }
            lines.Add("declaration " + Mask.Replace(line.Trim(), "$1"));
        }
    }

    private static string Describe(ShaderTypeInfo type)
    {
        string described = $"{type.ParameterClass} {type.ParameterType} {type.Rows}x{type.Columns}" +
            (type.NumElements > 1 ? $"[{type.NumElements}]" : "");
        if (type.MemberInfo != null && type.MemberInfo.Count != 0)
        {
            described += " { " + string.Join("; ", type.MemberInfo.Select(member =>
                $"{member.Name} @{member.ByteOffset} {Describe(member.TypeInfo)}")) + " }";
        }
        return described;
    }
}
