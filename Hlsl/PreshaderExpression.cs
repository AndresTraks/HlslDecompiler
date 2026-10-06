using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// An expression an effect evaluates, as HLSL: the instructions it was compiled
/// to, run over the names of their operands rather than over values. What is
/// written is what the instructions do, so it compiles back to them, if not always
/// in the words it was written in.
///
/// The effect compiler uses the preshader's program for these - an fx_2_0 state
/// set from uniforms, or the index of `shaders[i]` in any effect - and an
/// expression is the program's output: one component, or as many as the state
/// takes, a matrix's sixteen.
/// </summary>
public static class PreshaderExpression
{
    /// <returns>The expression for each of the first <paramref name="components"/> components of the output.</returns>
    public static string[] Write(Preshader expression, int components)
    {
        var values = new Dictionary<(PreshaderRegisterTable, int), string>();

        string Read(PreshaderOperand operand, int component)
        {
            if (operand.IsIndexed)
            {
                throw new NotSupportedException("An effect expression indexed by another register.");
            }
            int offset = operand.Offset + component;
            return operand.Table switch
            {
                PreshaderRegisterTable.Literal => FloatLiteral((float)expression.Literals[offset]),
                PreshaderRegisterTable.Input => InputName(expression.Inputs, offset),
                _ => values[(operand.Table, offset)],
            };
        }

        foreach (PreshaderInstruction instruction in expression.Instructions)
        {
            if (instruction.Opcode == PreshaderOpcode.Dot)
            {
                string sum = string.Join(" + ", Enumerable.Range(0, instruction.ComponentCount)
                    .Select(c => $"{Read(instruction.Inputs[0], c)} * {Read(instruction.Inputs[1], c)}"));
                values[(instruction.Output.Table, instruction.Output.Offset)] = $"({sum})";
                continue;
            }
            for (int c = 0; c < instruction.ComponentCount; c++)
            {
                string[] a = [.. instruction.Inputs.Select((operand, i) =>
                    Read(operand, instruction.IsScalar && i == 0 ? 0 : c))];
                values[(instruction.Output.Table, instruction.Output.Offset + c)] = instruction.Opcode switch
                {
                    PreshaderOpcode.Mov => a[0],
                    PreshaderOpcode.Neg => $"(-{a[0]})",
                    PreshaderOpcode.Rcp => $"(1 / {a[0]})",
                    PreshaderOpcode.Frc => $"frac({a[0]})",
                    PreshaderOpcode.Exp => $"exp2({a[0]})",
                    PreshaderOpcode.Log => $"log2({a[0]})",
                    PreshaderOpcode.Rsq => $"rsqrt({a[0]})",
                    PreshaderOpcode.Sin => $"sin({a[0]})",
                    PreshaderOpcode.Cos => $"cos({a[0]})",
                    PreshaderOpcode.Asin => $"asin({a[0]})",
                    PreshaderOpcode.Acos => $"acos({a[0]})",
                    PreshaderOpcode.Atan => $"atan({a[0]})",
                    PreshaderOpcode.Min => $"min({a[0]}, {a[1]})",
                    PreshaderOpcode.Max => $"max({a[0]}, {a[1]})",
                    PreshaderOpcode.Lt => $"({a[0]} < {a[1]})",
                    PreshaderOpcode.Ge => $"({a[0]} >= {a[1]})",
                    PreshaderOpcode.Add => $"({a[0]} + {a[1]})",
                    PreshaderOpcode.Mul => $"({a[0]} * {a[1]})",
                    PreshaderOpcode.Atan2 => $"atan2({a[0]}, {a[1]})",
                    PreshaderOpcode.Div => $"({a[0]} / {a[1]})",
                    PreshaderOpcode.Cmp => $"({a[0]} >= 0 ? {a[1]} : {a[2]})",
                    PreshaderOpcode.Movc => $"({a[0]} ? {a[1]} : {a[2]})",
                    _ => throw new NotSupportedException($"The effect expression instruction {instruction.Opcode}."),
                };
            }
        }
        return [.. Enumerable.Range(0, components).Select(c => Unbracketed(values[(PreshaderRegisterTable.Output, c)]))];
    }

    /// <summary>The one value an expression computes - an index, a scalar state.</summary>
    public static string Write(Preshader expression)
    {
        return Write(expression, 1)[0];
    }

    /// <summary>
    /// The uniform an expression reads, by the component of its table it is at: a
    /// scalar or a vector by name, an element of an array by its subscript, a
    /// component of a matrix by its row and column, whichever way it was packed.
    /// </summary>
    public static string InputName(ConstantTable inputs, int offset)
    {
        int register = offset / 4;
        int component = offset % 4;
        D3D9ConstantDeclaration declaration = inputs.Declarations.FirstOrDefault(d => d.ContainsIndex(register))
            ?? throw new InvalidDataException($"An effect expression reads c{register}, which its table does not name.");
        ShaderTypeInfo type = declaration.TypeInfo;
        int registerInVariable = register - declaration.RegisterIndex;
        int elements = Math.Max(type.NumElements, 1);
        int registersPerElement = Math.Max(declaration.RegisterCount / elements, 1);
        string name = declaration.Name;
        if (type.NumElements > 1)
        {
            name += $"[{registerInVariable / registersPerElement}]";
            registerInVariable %= registersPerElement;
        }
        return type.ParameterClass switch
        {
            ParameterClass.MatrixRows => $"{name}[{registerInVariable}][{component}]",
            ParameterClass.MatrixColumns => $"{name}[{component}][{registerInVariable}]",
            _ => type.Columns > 1 ? $"{name}.{"xyzw"[component]}" : name,
        };
    }

    private static string Unbracketed(string expression)
    {
        if (expression.Length < 2 || expression[0] != '(' || expression[^1] != ')')
        {
            return expression;
        }
        int depth = 0;
        for (int i = 0; i < expression.Length - 1; i++)
        {
            depth += expression[i] == '(' ? 1 : expression[i] == ')' ? -1 : 0;
            if (depth == 0)
            {
                return expression;
            }
        }
        return expression[1..^1];
    }

    private static string FloatLiteral(float value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }
}
