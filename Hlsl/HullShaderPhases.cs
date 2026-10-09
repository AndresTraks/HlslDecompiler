using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// Cuts a hull shader's bytecode into the programs it runs together. A hull shader
/// is not one program: the declarations come first, then the phase that runs once
/// per output control point, then the fork and join phases that compute the patch's
/// own constants. Each comes back as a shader model of its own, the shared
/// declarations in front of its body, so that anything able to read one shader can
/// read a phase - the decompiler's parser and the tests' interpreter both do.
/// </summary>
public static class HullShaderPhases
{
    /// <summary>
    /// Either phase is null where the bytecode has none. fxc writes no control point
    /// phase for a shader whose points come out as they went in.
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

        // The fork and join phases are one HLSL function between them. fxc splits the
        // patch constant function into a phase per factor it writes - six of them for
        // a quad domain - and each is a straight line of its own, so running them
        // together in the order they come is the one function they were written as.
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
                // Each phase ends with a ret of its own, and run together only the
                // last of them ends the function. Left in, the instruction writer
                // returned after the first factor and the rest of the function was
                // unreachable, and the interpreter stopped there too.
                if (patchConstant.Count != 0
                    && patchConstant[^1] is D3D10Instruction { Opcode: D3D10Opcode.Ret })
                {
                    patchConstant.RemoveAt(patchConstant.Count - 1);
                }
                current = patchConstant;
                continue;
            }
            // A patch constant phase is gathered on its own first, so that one
            // declaring an instance count can be unrolled before the phases are run
            // together.
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
    /// Leaves one dcl_temps in a body made of several phases: the largest, since the
    /// function needs as many temps as the hungriest phase in it. Every phase declares
    /// its own, so two of them declaring r0 was the same register declared twice, and
    /// the register state would not have it.
    /// </summary>
    /// <summary>
    /// A phase that declares an instance count, written out as a copy of its body per
    /// run. fxc writes the factors of a patch that are all computed the same way as
    /// one phase run once for each of them, which is not something HLSL can say: what
    /// it said was an assignment per factor, or a loop over them, and a copy per run
    /// is that back again. Each copy knows which run it is, so the vForkInstanceID it
    /// reads is a different number in each and the factor it writes is a different
    /// register.
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
        // The declarations belong to the phase and are made once; the body is what
        // runs per instance.
        List<Instruction> declarations = [.. phase
            .TakeWhile(i => i is D3D10Instruction d && d.Opcode.IsDeclaration())];
        into.AddRange(declarations);
        List<Instruction> body = [.. phase.Skip(declarations.Count)];
        if (instances <= 1)
        {
            into.AddRange(body);
            return;
        }
        // The ret ends the phase, not each run of it. Copied along with the rest, the
        // first run returned and every factor after the first was unreachable.
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
    /// One phase as a shader of its own. Which signatures its output registers answer
    /// to is the phase's own business: the control point phase writes the output
    /// signature and the fork and join phases write the patch constant signature,
    /// and both call them o0 upwards.
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
