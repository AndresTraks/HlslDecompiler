using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// What a preshader computes, as the trees the shader would have computed it with
/// had the effect compiler left the arithmetic where it was written. The shader
/// reads a constant register the preshader wrote, and gets the expression over the
/// uniforms instead of a register: `scale * 2 + 1` rather than a c1 nothing declares.
///
/// The instructions are run symbolically, a component at a time in order, which is
/// how the runtime runs them: a temporary written by one is the tree the next one
/// reads.
/// </summary>
public static class PreshaderTree
{
    /// <param name="readInput">The uniform at a component of the preshader's input table.</param>
    /// <returns>Every component the preshader leaves in the shader's constants, by table and component.</returns>
    public static Dictionary<(PreshaderRegisterTable Table, int Offset), HlslTreeNode> Build(
        Preshader preshader, Func<int, HlslTreeNode> readInput)
    {
        var temps = new Dictionary<int, HlslTreeNode>();
        var outputs = new Dictionary<(PreshaderRegisterTable, int), HlslTreeNode>();

        HlslTreeNode Read(PreshaderOperand operand, int component)
        {
            if (operand.IsIndexed)
            {
                throw new NotSupportedException("A preshader operand indexed by another register.");
            }
            int offset = operand.Offset + component;
            return operand.Table switch
            {
                PreshaderRegisterTable.Literal => new ConstantNode((float)preshader.Literals[offset]),
                PreshaderRegisterTable.Input => readInput(offset),
                PreshaderRegisterTable.Temp => temps[offset],
                _ => outputs[(operand.Table, offset)],
            };
        }

        void Write(PreshaderOperand operand, int component, HlslTreeNode value)
        {
            if (operand.IsIndexed)
            {
                throw new NotSupportedException("A preshader output indexed by another register.");
            }
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
                HlslTreeNode sum = Enumerable.Range(0, instruction.ComponentCount)
                    .Select(c => (HlslTreeNode)ReadingFloats(new MultiplyOperation(
                        Read(instruction.Inputs[0], c), Read(instruction.Inputs[1], c))))
                    .Aggregate((addition, addend) => ReadingFloats(new AddOperation(addition, addend)));
                Write(instruction.Output, 0, sum);
                continue;
            }

            for (int component = 0; component < instruction.ComponentCount; component++)
            {
                // A scalar instruction reads one component of its first operand for
                // all of them - the 2 of `2 * v`, broadcast.
                HlslTreeNode[] inputs = [.. instruction.Inputs.Select((operand, i) =>
                    Read(operand, instruction.IsScalar && i == 0 ? 0 : component))];
                Write(instruction.Output, component, Create(instruction.Opcode, inputs));
            }
        }
        return outputs;
    }

    private static HlslTreeNode Create(PreshaderOpcode opcode, HlslTreeNode[] inputs)
    {
        HlslTreeNode node = opcode switch
        {
            PreshaderOpcode.Mov => inputs[0],
            PreshaderOpcode.Neg => new NegateOperation(inputs[0]),
            PreshaderOpcode.Rcp => new ReciprocalOperation(inputs[0]),
            PreshaderOpcode.Frc => new FractionalOperation(inputs[0]),
            PreshaderOpcode.Exp => new ExponentialOperation(inputs[0]),
            PreshaderOpcode.Log => new LogOperation(inputs[0]),
            PreshaderOpcode.Rsq => new ReciprocalSquareRootOperation(inputs[0]),
            PreshaderOpcode.Sin => new SineOperation(inputs[0]),
            PreshaderOpcode.Cos => new CosineOperation(inputs[0]),
            PreshaderOpcode.Asin => new ArcSineOperation(inputs[0]),
            PreshaderOpcode.Acos => new ArcCosineOperation(inputs[0]),
            PreshaderOpcode.Atan => new ArcTangentOperation(inputs[0]),
            PreshaderOpcode.Min => new MinimumOperation(inputs[0], inputs[1]),
            PreshaderOpcode.Max => new MaximumOperation(inputs[0], inputs[1]),
            PreshaderOpcode.Lt => new SignLessOperation(inputs[0], inputs[1]),
            PreshaderOpcode.Ge => new SignGreaterOrEqualOperation(inputs[0], inputs[1]),
            PreshaderOpcode.Add => new AddOperation(inputs[0], inputs[1]),
            PreshaderOpcode.Mul => new MultiplyOperation(inputs[0], inputs[1]),
            PreshaderOpcode.Atan2 => new ArcTangent2Operation(inputs[0], inputs[1]),
            PreshaderOpcode.Div => new DivisionOperation(inputs[0], inputs[1]),
            PreshaderOpcode.Cmp => new CompareOperation(inputs[0], inputs[1], inputs[2]),
            PreshaderOpcode.Movc => new MoveConditionalOperation(inputs[0], inputs[1], inputs[2]),
            _ => throw new NotSupportedException($"The preshader instruction {opcode}."),
        };
        return ReadingFloats(node);
    }

    private static T ReadingFloats<T>(T node) where T : HlslTreeNode
    {
        node.ConsumesInteger ??= false;
        return node;
    }
}
