using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// Dynamic linkage as declared: function bodies, tables, interfaces, and where each
/// body lives. A body reads the caller's registers directly, so registers it reads
/// before writing are the method's parameters and the one it writes is the result.
/// Names come from RDEF (interface variables) and IFCE (classes); without both,
/// every interface instance gets its own interface and every table its own class.
/// </summary>
public class LinkageModel
{
    public class FunctionBodyInfo
    {
        public int Number { get; init; }

        /// <summary>Instruction range after the label, up to the next label.</summary>
        public int First { get; set; }
        public int Last { get; set; }

        public List<RegisterKey> Parameters { get; } = [];

        public RegisterKey ReturnRegister { get; set; }
    }

    public class FunctionTableInfo
    {
        public int Number { get; init; }
        public int[] Bodies { get; init; }
    }

    /// <summary>One declared interface instance (fp).</summary>
    public class InterfaceInfo
    {
        public int Number { get; init; }
        public int InstanceArrayLength { get; init; }
        public int FunctionsPerTable { get; init; }
        public int[] Tables { get; init; }

        public string InstanceName { get; set; }
        public bool IsArray { get; set; }
        public InterfaceTypeInfo Type { get; set; }

        /// <summary>Parallel to <see cref="Tables"/>.</summary>
        public ClassInfo[] TableClasses { get; set; }

        /// <summary>Indexed by function index.</summary>
        public MethodInfo[] Methods { get; set; }

        public string InstanceExpression(int instance) =>
            IsArray ? $"{InstanceName}[{instance}]" : InstanceName;
    }

    /// <summary>
    /// One function index of one interface instance. All bodies must return the same
    /// register; the parameters are the union of what any body reads.
    /// </summary>
    public class MethodInfo
    {
        public string Name { get; init; }
        public InterfaceInfo Interface { get; init; }
        public int Function { get; init; }
        public List<RegisterKey> Parameters { get; } = [];
        public RegisterKey ReturnRegister { get; set; }
        public List<FunctionBodyInfo> Bodies { get; } = [];
    }

    public class InterfaceTypeInfo
    {
        public string Name { get; init; }
        public List<MethodInfo> Methods { get; } = [];
    }

    public class ClassInfo
    {
        public string Name { get; init; }

        /// <summary>The IFCE class type ID, or the table number without IFCE.</summary>
        public int TypeId { get; init; }

        public List<InterfaceTypeInfo> Implements { get; } = [];

        public Dictionary<MethodInfo, FunctionBodyInfo> Bodies { get; } = [];
    }

    public List<InterfaceInfo> Interfaces { get; } = [];
    public List<FunctionTableInfo> Tables { get; } = [];
    public List<FunctionBodyInfo> Bodies { get; } = [];
    public List<InterfaceTypeInfo> InterfaceTypes { get; } = [];
    public List<ClassInfo> Classes { get; } = [];

    /// <summary>Instructions before the first label.</summary>
    public int MainInstructionCount { get; private set; }

    public bool HasLinkage => Bodies.Count != 0;

    public FunctionBodyInfo BodyByLabel(int number) =>
        Bodies.FirstOrDefault(b => b.Number == number)
        ?? throw new NotImplementedException($"label fb{number} names no function body");

    public InterfaceInfo InterfaceByNumber(int interfaceNumber) =>
        Interfaces.First(i => i.Number == interfaceNumber);

    public MethodInfo MethodForCall(int interfaceNumber, int function) =>
        InterfaceByNumber(interfaceNumber).Methods[function];

    public static LinkageModel Read(ShaderModel shader)
    {
        var model = new LinkageModel();
        if (shader.Instructions.Count == 0 || shader.Instructions[0] is not D3D10Instruction)
        {
            return model;
        }

        // These declarations carry plain dwords, not operands (as in AsmWriter).
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
                        int arrayLength = (int)(tokens[2] >> 16);
                        model.Interfaces.Add(new InterfaceInfo
                        {
                            Number = (int)tokens[0],
                            FunctionsPerTable = (int)tokens[1],
                            InstanceArrayLength = arrayLength,
                            IsArray = arrayLength > 1,
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
        HashSet<RegisterKey> readAfterCalls = ReadAfterCalls(shader, model.MainInstructionCount);
        foreach (var label in labels)
        {
            FunctionBodyInfo body = model.BodyByLabel(label.Number);
            body.First = label.Index + 1;
            body.Last = label.Index == labels[^1].Index
                ? shader.Instructions.Count
                : labels[labels.IndexOf(label) + 1].Index;
            ReadSignature(shader, body, readAfterCalls);
        }

        if (!ReadNames(model, shader))
        {
            NameByNumber(model);
        }
        ReadMethods(model);
        ReadClasses(model);
        return model;
    }

    /// <summary>
    /// Parameters are registers read before written, excluding globals (cbuffers,
    /// resources, samplers...); the result is the one register written.
    /// </summary>
    private static void ReadSignature(ShaderModel shader, FunctionBodyInfo body,
        HashSet<RegisterKey> readAfterCalls)
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

        // A body also writes its own temps; the result is the register the caller
        // reads after a call.
        List<RegisterKey> results = written.Count == 1
            ? [.. written]
            : [.. written.Where(readAfterCalls.Contains)];
        if (results.Count != 1)
        {
            throw new NotImplementedException(
                $"a function body writing {written.Count} registers, {results.Count} read after a call");
        }
        body.ReturnRegister = results[0];
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
    /// fps take interface slots in order, one per array element; a variable's
    /// $ThisPointer offset is its first slot, and that slot's record maps tables to
    /// class types. False, naming nothing, if the chunks don't cover every fp/table.
    /// </summary>
    private static bool ReadNames(LinkageModel model, ShaderModel shader)
    {
        ShaderInterfaces interfaces = shader.Interfaces;
        if (interfaces == null || shader.ConstantDeclarations == null)
        {
            return false;
        }
        if (interfaces.ClassInstanceCount != 0)
        {
            throw new NotImplementedException("a class instance declared in the shader");
        }

        var variables = shader.ConstantDeclarations
            .Where(d => d.TypeInfo?.ParameterClass == ParameterClass.InterfacePointer
                && d.TypeInfo.Name != null)
            .ToList();
        var named = new List<(InterfaceInfo Interface, D3D10ConstantDeclaration Variable,
            InterfaceSlotRecord Record)>();
        int slot = 0;
        foreach (InterfaceInfo iface in model.Interfaces.OrderBy(i => i.Number))
        {
            D3D10ConstantDeclaration variable = variables
                .FirstOrDefault(v => v.VariableOffset == slot);
            InterfaceSlotRecord record = interfaces.RecordForSlot(slot);
            if (variable == null || record == null
                || iface.Tables.Any(t => !record.TableIds.Contains(t))
                || record.TypeIds.Any(id => id >= interfaces.ClassTypeNames.Count))
            {
                return false;
            }
            named.Add((iface, variable, record));
            slot += Math.Max(iface.InstanceArrayLength, 1);
        }

        var types = new Dictionary<string, InterfaceTypeInfo>();
        var classes = new Dictionary<int, ClassInfo>();
        foreach ((InterfaceInfo iface, D3D10ConstantDeclaration variable,
            InterfaceSlotRecord record) in named)
        {
            iface.InstanceName = variable.Name;
            iface.IsArray = variable.TypeInfo.NumElements > 0;
            if (!types.TryGetValue(variable.TypeInfo.Name, out InterfaceTypeInfo type))
            {
                type = new InterfaceTypeInfo { Name = variable.TypeInfo.Name };
                types.Add(type.Name, type);
                model.InterfaceTypes.Add(type);
            }
            iface.Type = type;
            iface.TableClasses = [.. iface.Tables.Select(table =>
            {
                int typeId = record.TypeIds[Array.IndexOf(record.TableIds, table)];
                if (!classes.TryGetValue(typeId, out ClassInfo classInfo))
                {
                    classInfo = new ClassInfo
                    {
                        Name = interfaces.ClassTypeNames[typeId],
                        TypeId = typeId,
                    };
                    classes.Add(typeId, classInfo);
                }
                return classInfo;
            })];
        }
        return true;
    }

    private static void NameByNumber(LinkageModel model)
    {
        foreach (InterfaceInfo iface in model.Interfaces)
        {
            iface.InstanceName = $"g{iface.Number}";
            iface.Type = new InterfaceTypeInfo { Name = $"I{iface.Number}" };
            model.InterfaceTypes.Add(iface.Type);
            iface.TableClasses = [.. iface.Tables.Select(table => new ClassInfo
            {
                Name = $"I{iface.Number}C{table}",
                TypeId = table,
            })];
        }
    }

    /// <summary>
    /// Numbered across the shader so a class implementing two interfaces gets no
    /// clash. Two fps of one interface get separate methods; the bytecode can't tell.
    /// </summary>
    private static void ReadMethods(LinkageModel model)
    {
        int methodNumber = 0;
        foreach (InterfaceInfo iface in model.Interfaces.OrderBy(i => i.Number))
        {
            iface.Methods = new MethodInfo[iface.FunctionsPerTable];
            for (int function = 0; function < iface.FunctionsPerTable; function++)
            {
                var method = new MethodInfo
                {
                    Name = $"F{methodNumber++}",
                    Interface = iface,
                    Function = function,
                };
                foreach (int table in iface.Tables)
                {
                    FunctionBodyInfo body = model.BodyByLabel(
                        model.Tables.First(t => t.Number == table).Bodies[function]);
                    method.Bodies.Add(body);
                    if (method.ReturnRegister == null)
                    {
                        method.ReturnRegister = body.ReturnRegister;
                    }
                    else if (!Equals(method.ReturnRegister, body.ReturnRegister))
                    {
                        throw new NotImplementedException(
                            "one interface method with two shapes of body");
                    }
                    foreach (RegisterKey parameter in body.Parameters)
                    {
                        if (!method.Parameters.Contains(parameter))
                        {
                            method.Parameters.Add(parameter);
                        }
                    }
                }
                method.Parameters.Sort((a, b) => a.Number.CompareTo(b.Number));
                iface.Methods[function] = method;
                iface.Type.Methods.Add(method);
            }
        }
    }

    /// <summary>
    /// Descending type ID within each interface: fxc numbers a class declared later
    /// lower, so this order reproduces the IDs.
    /// </summary>
    private static void ReadClasses(LinkageModel model)
    {
        var classes = new List<ClassInfo>();
        foreach (InterfaceInfo iface in model.Interfaces.OrderBy(i => i.Number))
        {
            for (int slot = 0; slot < iface.Tables.Length; slot++)
            {
                ClassInfo classInfo = iface.TableClasses[slot];
                if (!classes.Contains(classInfo))
                {
                    classes.Add(classInfo);
                }
                if (!classInfo.Implements.Contains(iface.Type))
                {
                    classInfo.Implements.Add(iface.Type);
                }
                int[] bodies = model.Tables.First(t => t.Number == iface.Tables[slot]).Bodies;
                foreach (MethodInfo method in iface.Methods)
                {
                    classInfo.Bodies[method] = model.BodyByLabel(bodies[method.Function]);
                }
            }
        }

        foreach (ClassInfo classInfo in classes)
        {
            if (classInfo.Implements.SelectMany(t => t.Methods)
                .Any(method => !classInfo.Bodies.ContainsKey(method)))
            {
                throw new NotImplementedException(
                    "a class bound through one instance of an interface and not another");
            }
        }

        model.Classes.AddRange(classes
            .OrderBy(c => model.InterfaceTypes.IndexOf(c.Implements[0]))
            .ThenByDescending(c => c.TypeId));
    }

    /// <summary>
    /// Temps the main program reads after an interface call before writing them:
    /// where a body's result can be.
    /// </summary>
    private static HashSet<RegisterKey> ReadAfterCalls(ShaderModel shader, int mainInstructionCount)
    {
        var read = new HashSet<RegisterKey>();
        for (int call = 0; call < mainInstructionCount; call++)
        {
            if (shader.Instructions[call] is not D3D10Instruction { Opcode: D3D10Opcode.InterfaceCall })
            {
                continue;
            }
            var written = new HashSet<RegisterKey>();
            for (int i = call + 1; i < mainInstructionCount; i++)
            {
                var instruction = (D3D10Instruction)shader.Instructions[i];
                int? destination = instruction.GetDestinationParamIndex();
                for (int operand = 0; operand < instruction.OperandTokens.OperandCount; operand++)
                {
                    if (operand != destination
                        && instruction.GetOperandType(operand) == OperandType.Temp
                        && !written.Contains(instruction.GetParamRegisterKey(operand)))
                    {
                        read.Add(instruction.GetParamRegisterKey(operand));
                    }
                }
                if (destination != null)
                {
                    written.Add(instruction.GetParamRegisterKey(destination.Value));
                }
            }
        }
        return read;
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
