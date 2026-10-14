using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// Splits a hull shader into its control point phase and its patch constant
/// (fork/join) phases, each as a standalone shader model with the shared
/// declarations in front.
/// </summary>
public static class HullShaderPhases
{
    /// <summary>
    /// Either phase is null where absent; fxc omits a pass-through control point phase.
    /// </summary>
    public static (ShaderModel ControlPoint, ShaderModel PatchConstant) Split(ShaderModel shader)
    {
        int phaseStart = shader.Instructions.Count;
        for (int i = 0; i < shader.Instructions.Count; i++)
        {
            if (IsPhaseStart(shader.Instructions[i]))
            {
                phaseStart = i;
                break;
            }
        }
        IList<Instruction> declarations = [.. shader.Instructions.Take(phaseStart)];

        // fxc splits the patch constant function into straight-line fork/join phases;
        // concatenated in order they are that function again.
        List<Instruction> controlPoint = null;
        List<Instruction> patchConstant = null;
        List<Instruction> current = null;
        List<Instruction> phase = [];
        foreach (Instruction instruction in shader.Instructions.Skip(phaseStart))
        {
            if (instruction is D3D10Instruction { Opcode: D3D10Opcode.HsControlPointPhase })
            {
                controlPoint ??= [];
                current = controlPoint;
                continue;
            }
            if (instruction is D3D10Instruction
                { Opcode: D3D10Opcode.HsForkPhase or D3D10Opcode.HsJoinPhase })
            {
                patchConstant ??= [];
                UnrollInstances(phase, patchConstant);
                phase = [];
                // Only the last phase's ret may end the concatenated function.
                if (patchConstant.Count != 0
                    && patchConstant[^1] is D3D10Instruction { Opcode: D3D10Opcode.Ret })
                {
                    patchConstant.RemoveAt(patchConstant.Count - 1);
                }
                current = patchConstant;
                continue;
            }
            // Gathered separately so an instanced phase can be unrolled first.
            if (ReferenceEquals(current, patchConstant))
            {
                phase.Add(instruction);
                continue;
            }
            current?.Add(instruction);
        }
        if (patchConstant != null)
        {
            UnrollInstances(phase, patchConstant);
        }

        CoalesceTempDeclarations(patchConstant);

        return (
            controlPoint == null
                ? null
                : Phase(shader, declarations, controlPoint, shader.OutputSignatures),
            patchConstant == null
                ? null
                : Phase(shader, declarations, patchConstant,
                    [.. shader.PatchConstantSignatures.Select(s => s.AsOutput())],
                    shader.OutputSignatures));
    }

    /// <summary>
    /// Unrolls a phase with an instance count into one body copy per instance, each
    /// with its own vForkInstanceID. HLSL has no instanced phase.
    /// </summary>
    private static void UnrollInstances(List<Instruction> phase, List<Instruction> into)
    {
        int instances = 1;
        foreach (Instruction instruction in phase)
        {
            if (instruction is D3D10Instruction
                { Opcode: D3D10Opcode.DclHSForkPhaseInstanceCount
                    or D3D10Opcode.DclHSJoinPhaseInstanceCount } count)
            {
                instances = count.GetParamInt(0);
            }
        }
        List<Instruction> declarations = [.. phase
            .TakeWhile(i => i is D3D10Instruction d && d.Opcode.IsDeclaration())];
        into.AddRange(declarations);
        List<Instruction> body = [.. phase.Skip(declarations.Count)];
        if (instances <= 1)
        {
            into.AddRange(body);
            return;
        }
        // The ret ends the phase, not each instance.
        Instruction end = null;
        if (body.Count != 0 && body[^1] is D3D10Instruction { Opcode: D3D10Opcode.Ret })
        {
            end = body[^1];
            body.RemoveAt(body.Count - 1);
        }
        for (int instance = 0; instance < instances; instance++)
        {
            foreach (Instruction instruction in body)
            {
                into.Add(instruction is D3D10Instruction d3d10
                    ? d3d10.ForInstance(instance)
                    : instruction);
            }
        }
        if (end != null)
        {
            into.Add(end);
        }
    }

    /// <summary>
    /// Keeps only the largest of the phases' dcl_temps; a register declared twice is rejected.
    /// </summary>
    private static void CoalesceTempDeclarations(List<Instruction> body)
    {
        if (body == null)
        {
            return;
        }
        List<int> declarations = [.. Enumerable.Range(0, body.Count)
            .Where(i => body[i] is D3D10Instruction { Opcode: D3D10Opcode.DclTemps })];
        if (declarations.Count < 2)
        {
            return;
        }
        Instruction largest = declarations
            .Select(i => body[i])
            .OrderByDescending(d => ((D3D10Instruction)d).GetParamInt(0))
            .First();
        foreach (int i in Enumerable.Reverse(declarations))
        {
            body.RemoveAt(i);
        }
        body.Insert(declarations[0], largest);
    }

    private static bool IsPhaseStart(Instruction instruction)
    {
        return instruction is D3D10Instruction
        {
            Opcode: D3D10Opcode.HsControlPointPhase or D3D10Opcode.HsForkPhase
                or D3D10Opcode.HsJoinPhase
        };
    }

    /// <summary>
    /// Both phases number outputs from o0, but against different signatures.
    /// </summary>
    private static ShaderModel Phase(
        ShaderModel shader,
        IList<Instruction> declarations,
        IList<Instruction> body,
        IList<RegisterSignature> outputSignatures,
        IList<RegisterSignature> controlPointSignatures = null)
    {
        return new ShaderModel(
            shader.MajorVersion,
            shader.MinorVersion,
            shader.Type,
            shader.InputSignatures,
            outputSignatures,
            shader.PatchConstantSignatures,
            shader.ConstantDeclarations,
            shader.ResourceDefinitions,
            [.. declarations, .. body])
        {
            ControlPointSignatures = controlPointSignatures,
        };
    }
}
