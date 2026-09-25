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
            current?.Add(instruction);
        }

        CoalesceTempDeclarations(patchConstant);

        return (
            controlPoint == null
                ? null
                : Phase(shader, declarations, controlPoint, shader.OutputSignatures),
            patchConstant == null
                ? null
                : Phase(shader, declarations, patchConstant,
                    [.. shader.PatchConstantSignatures.Select(s => s.AsOutput())]));
    }

    /// <summary>
    /// Leaves one dcl_temps in a body made of several phases: the largest, since the
    /// function needs as many temps as the hungriest phase in it. Every phase declares
    /// its own, so two of them declaring r0 was the same register declared twice, and
    /// the register state would not have it.
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
    /// One phase as a shader of its own. Which signatures its output registers answer
    /// to is the phase's own business: the control point phase writes the output
    /// signature and the fork and join phases write the patch constant signature,
    /// and both call them o0 upwards.
    /// </summary>
    private static ShaderModel Phase(
        ShaderModel shader,
        IList<Instruction> declarations,
        IList<Instruction> body,
        IList<RegisterSignature> outputSignatures)
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
            [.. declarations, .. body]);
    }
}
