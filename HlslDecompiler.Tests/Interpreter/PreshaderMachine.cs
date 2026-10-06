using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Tests.Interpreter;

/// <summary>
/// Runs an fx_2_0 preshader, the way the effect runtime does before the shader:
/// in doubles, a component at a time, from the uniforms to the shader's constant
/// registers.
///
/// Written apart from the decompiler's reading of the same instructions on purpose,
/// and checked apart from it too - against the shader fxc compiles from the same
/// source with /Op, which does the arithmetic in the shader instead, where the
/// machine already knows what each instruction means.
/// </summary>
public static class PreshaderMachine
{
    /// <param name="named">The value of the uniform register named, as the shader machine gives it.</param>
    /// <returns>What it leaves in each component of the shader's constants.</returns>
    public static Dictionary<(PreshaderRegisterTable Table, int Offset), double> Run(
        Preshader preshader, Func<string, float[]> named)
    {
        var input = new Dictionary<int, double>();
        foreach (D3D9ConstantDeclaration declaration in preshader.Inputs.Declarations)
        {
            for (int r = 0; r < declaration.RegisterCount; r++)
            {
                float[] value = named($"{declaration.Name}[{r}]");
                for (int c = 0; c < 4; c++)
                {
                    input[(declaration.RegisterIndex + r) * 4 + c] = value[c];
                }
            }
        }

        var temps = new Dictionary<int, double>();
        var outputs = new Dictionary<(PreshaderRegisterTable, int), double>();

        double Read(PreshaderOperand operand, int component)
        {
            if (operand.IsIndexed)
            {
                throw new D3D9Machine.UnsupportedException("an indexed preshader operand");
            }
            int offset = operand.Offset + component;
            return operand.Table switch
            {
                PreshaderRegisterTable.Literal => preshader.Literals[offset],
                PreshaderRegisterTable.Input => input[offset],
                PreshaderRegisterTable.Temp => temps[offset],
                _ => outputs[(operand.Table, offset)],
            };
        }

        void Write(PreshaderOperand operand, int component, double value)
        {
            int offset = operand.Offset + component;
            if (operand.Table == PreshaderRegisterTable.Temp)
            {
                temps[offset] = value;
            }
            else
            {
                outputs[(operand.Table, offset)] = value;
            }
        }

        foreach (PreshaderInstruction instruction in preshader.Instructions)
        {
            if (instruction.Opcode == PreshaderOpcode.Dot)
            {
                double sum = 0;
                for (int c = 0; c < instruction.ComponentCount; c++)
                {
                    sum += Read(instruction.Inputs[0], c) * Read(instruction.Inputs[1], c);
                }
                Write(instruction.Output, 0, sum);
                continue;
            }

            for (int c = 0; c < instruction.ComponentCount; c++)
            {
                double[] a = [.. instruction.Inputs.Select((operand, i) =>
                    Read(operand, instruction.IsScalar && i == 0 ? 0 : c))];
                Write(instruction.Output, c, Execute(instruction.Opcode, a));
            }
        }
        return outputs;
    }

    private static double Execute(PreshaderOpcode opcode, double[] a)
    {
        return opcode switch
        {
            PreshaderOpcode.Mov => a[0],
            PreshaderOpcode.Neg => -a[0],
            PreshaderOpcode.Rcp => 1 / a[0],
            PreshaderOpcode.Frc => a[0] - Math.Floor(a[0]),
            PreshaderOpcode.Exp => Math.Pow(2, a[0]),
            // Of the magnitude, the way the shader instruction of the same name is.
            PreshaderOpcode.Log => a[0] == 0 ? double.NegativeInfinity : Math.Log2(Math.Abs(a[0])),
            PreshaderOpcode.Rsq => 1 / Math.Sqrt(Math.Abs(a[0])),
            PreshaderOpcode.Sin => Math.Sin(a[0]),
            PreshaderOpcode.Cos => Math.Cos(a[0]),
            PreshaderOpcode.Asin => Math.Asin(a[0]),
            PreshaderOpcode.Acos => Math.Acos(a[0]),
            PreshaderOpcode.Atan => Math.Atan(a[0]),
            PreshaderOpcode.Min => Math.Min(a[0], a[1]),
            PreshaderOpcode.Max => Math.Max(a[0], a[1]),
            PreshaderOpcode.Lt => a[0] < a[1] ? 1 : 0,
            PreshaderOpcode.Ge => a[0] >= a[1] ? 1 : 0,
            PreshaderOpcode.Add => a[0] + a[1],
            PreshaderOpcode.Mul => a[0] * a[1],
            PreshaderOpcode.Atan2 => Math.Atan2(a[0], a[1]),
            PreshaderOpcode.Div => a[0] / a[1],
            PreshaderOpcode.Cmp => a[0] >= 0 ? a[1] : a[2],
            PreshaderOpcode.Movc => a[0] != 0 ? a[1] : a[2],
            _ => throw new D3D9Machine.UnsupportedException($"the preshader instruction {opcode}"),
        };
    }
}
