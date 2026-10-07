using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// Dynamic linkage as the bytecode declares it: which function bodies a table holds,
/// which tables an interface could be filled by, and where in the instruction stream
/// each body lives - the interface's shape, seen from the outside.
///
/// fxc compiles a body at the call site it belongs to: it reads the caller's argument
/// registers directly and writes its result to the register the caller reads. So the
/// registers a body reads before writing it are the method's parameters, and the one
/// register it writes is the method's result. A body that reads a register it never
/// writes is reading what HLSL passed in as an argument.
/// </summary>
public class LinkageModel
{
    public class FunctionBodyInfo
    {
        /// <summary>The fb number the label names this body by.</summary>
        public int Number { get; init; }

        /// <summary>Where the body's instructions run to, past its label and its ret.</summary>
        public int First { get; set; }
        public int Last { get; set; }

        /// <summary>The registers it reads before writing them: the method's arguments.</summary>
        public List<RegisterKey> Parameters { get; } = [];

        /// <summary>The one register it writes: the method's result.</summary>
        public RegisterKey ReturnRegister { get; set; }
    }

    public class FunctionTableInfo
    {
        public int Number { get; init; }
        public int[] Bodies { get; init; }
    }

    public class InterfaceInfo
    {
        public int Number { get; init; }
        public int InstanceArrayLength { get; init; }
        public int FunctionsPerTable { get; init; }
        public int[] Tables { get; init; }

        public string Name => $"I{Number}";
        public string InstanceName => $"g{Number}";
        public bool IsArray => InstanceArrayLength > 1;
        public string ClassName(int tableSlot) => $"I{Number}C{Tables[tableSlot]}";
        public static string MethodName(int function) => $"F{function}";
    }

    public List<InterfaceInfo> Interfaces { get; } = [];
    public List<FunctionTableInfo> Tables { get; } = [];
    public List<FunctionBodyInfo> Bodies { get; } = [];

    /// <summary>
    /// How many instructions the main program holds: everything before the first
    /// label, which is where the first body starts.
    /// </summary>
    public int MainInstructionCount { get; private set; }

    public bool HasLinkage => Bodies.Count != 0;

    public FunctionBodyInfo BodyByLabel(int number) =>
        Bodies.FirstOrDefault(b => b.Number == number)
        ?? throw new NotImplementedException($"label fb{number} names no function body");

    public FunctionBodyInfo BodyForCall(int interfaceNumber, int instance, int function)
    {
        InterfaceInfo iface = Interfaces.First(i => i.Number == interfaceNumber);
        return BodyByLabel(Tables.First(t => t.Number == iface.Tables[0]).Bodies[function]);
    }

    public static LinkageModel Read(ShaderModel shader)
    {
        var model = new LinkageModel();
        if (shader.Instructions.Count == 0 || shader.Instructions[0] is not D3D10Instruction)
        {
            return model;
        }

        // The three declarations carry plain dwords where every other instruction
        // carries operands - the same layout AsmWriter reads them with.
        var labels = new List<(int Number, int Index)>();
        for (int i = 0; i < shader.Instructions.Count; i++)
        {
            if (shader.Instructions[i] is not D3D10Instruction instruction)
            {
                continue;
            }
            switch (instruction.Opcode)
            {
                case D3D10Opcode.DclFunctionBody:
                    model.Bodies.Add(new FunctionBodyInfo
                    {
                        Number = (int)instruction.OperandTokens.Tokens[0],
                    });
                    break;
                case D3D10Opcode.DclFunctionTable:
                    {
                        uint[] tokens = instruction.OperandTokens.Tokens;
                        model.Tables.Add(new FunctionTableInfo
                        {
                            Number = (int)tokens[0],
                            Bodies = [.. tokens.Skip(2).Take((int)tokens[1]).Select(b => (int)b)],
                        });
                        break;
                    }
                case D3D10Opcode.DclInterface:
                    {
                        uint[] tokens = instruction.OperandTokens.Tokens;
                        model.Interfaces.Add(new InterfaceInfo
                        {
                            Number = (int)tokens[0],
                            FunctionsPerTable = (int)tokens[1],
                            InstanceArrayLength = (int)(tokens[2] >> 16),
                            Tables = [.. tokens.Skip(3).Take((int)(tokens[2] & 0xFFFF))
                                .Select(t => (int)t)],
                        });
                        break;
                    }
                case D3D10Opcode.Label:
                    labels.Add(((int)instruction.GetParamRegisterNumber(0), i));
                    break;
            }
        }

        if (labels.Count == 0)
        {
            return model;
        }

        model.MainInstructionCount = labels[0].Index;
        foreach (var label in labels)
        {
            FunctionBodyInfo body = model.BodyByLabel(label.Number);
            body.First = label.Index + 1;
            body.Last = label.Index == labels[^1].Index
                ? shader.Instructions.Count
                : labels[labels.IndexOf(label) + 1].Index;
            ReadSignature(model, shader, body);
        }

        CheckAbisMatch(model);
        return model;
    }

    /// <summary>
    /// The registers the body reads before writing them, and the one it writes. A read
    /// of a constant buffer, a sampler, a resource or the rasterizer is a read of
    /// something HLSL declares outside the method too, and not a parameter; anything
    /// else the body reads without first writing it came in as an argument.
    /// </summary>
    private static void ReadSignature(LinkageModel model, ShaderModel shader, FunctionBodyInfo body)
    {
        var written = new HashSet<RegisterKey>();
        for (int i = body.First; i < body.Last; i++)
        {
            var instruction = (D3D10Instruction)shader.Instructions[i];
            if (instruction.Opcode == D3D10Opcode.Ret)
            {
                continue;
            }
            if (instruction.Opcode == D3D10Opcode.InterfaceCall)
            {
                throw new NotImplementedException(
                    "a function body calling through an interface");
            }

            int? destination = instruction.GetDestinationParamIndex();
            for (int operand = 0; operand < instruction.OperandTokens.OperandCount; operand++)
            {
                if (operand == destination)
                {
                    continue;
                }
                OperandType type = instruction.GetOperandType(operand);
                if (IsOutsideTheMethod(type))
                {
                    continue;
                }
                RegisterKey read = instruction.GetParamRegisterKey(operand);
                if (!written.Contains(read) && !body.Parameters.Contains(read))
                {
                    body.Parameters.Add(read);
                }
            }

            if (destination != null)
            {
                written.Add(instruction.GetParamRegisterKey(destination.Value));
            }
        }

        if (written.Count != 1)
        {
            throw new NotImplementedException(
                $"a function body writing {written.Count} registers");
        }
        body.ReturnRegister = written.First();
        if (body.ReturnRegister is not D3D10RegisterKey { OperandType: OperandType.Temp })
        {
            throw new NotImplementedException(
                "a function body whose result is not a temp register");
        }
        foreach (RegisterKey parameter in body.Parameters)
        {
            if (parameter is not D3D10RegisterKey { OperandType: OperandType.Input })
            {
                throw new NotImplementedException(
                    "a function body argument that is not an input register");
            }
        }
    }

    /// <summary>
    /// Every body that one interface method could run has to take the same arguments
    /// and hand back the same register, or the call site has no one shape to match:
    /// they are implementations of the one HLSL method, and HLSL checks they agree.
    /// </summary>
    private static void CheckAbisMatch(LinkageModel model)
    {
        foreach (InterfaceInfo iface in model.Interfaces)
        {
            foreach (int table in iface.Tables)
            {
                FunctionBodyInfo[] bodies = model.Tables
                    .First(t => t.Number == table).Bodies
                    .Select(model.BodyByLabel).ToArray();
                foreach (FunctionBodyInfo body in bodies)
                {
                    if (body.Parameters.Count != bodies[0].Parameters.Count
                        || !body.Parameters.SequenceEqual(bodies[0].Parameters)
                        || !Equals(body.ReturnRegister, bodies[0].ReturnRegister))
                    {
                        throw new NotImplementedException(
                            "one interface method with two shapes of body");
                    }
                }
            }
        }
    }

    private static bool IsOutsideTheMethod(OperandType type) =>
        type is OperandType.Immediate32 or OperandType.Immediate64
            or OperandType.Sampler or OperandType.Resource
            or OperandType.ConstantBuffer or OperandType.ImmediateConstantBuffer
            or OperandType.UnorderedAccessView or OperandType.ThreadGroupSharedMemory
            or OperandType.Rasterizer or OperandType.Label or OperandType.Null
            or OperandType.Stream or OperandType.FunctionBody or OperandType.FunctionTable
            or OperandType.Interface or OperandType.ThisPointer;
}
