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
///
/// The names come from the reflection data: the interface variables and the
/// interface each was declared as from RDEF, the classes and which table each fills
/// from IFCE. A host binds a class instance by its class name and finds the
/// interface by its variable name, so those are kept. Without both chunks every
/// interface instance gets an interface of its own and every table a class.
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

    /// <summary>
    /// One interface instance as declared: an fp, its variable, the tables a class
    /// bound to it fills, and the class each table belongs to.
    /// </summary>
    public class InterfaceInfo
    {
        public int Number { get; init; }
        public int InstanceArrayLength { get; init; }
        public int FunctionsPerTable { get; init; }
        public int[] Tables { get; init; }

        public string InstanceName { get; set; }
        public bool IsArray { get; set; }
        public InterfaceTypeInfo Type { get; set; }

        /// <summary>The class filling each table, in the order of <see cref="Tables"/>.</summary>
        public ClassInfo[] TableClasses { get; set; }

        /// <summary>The method called through each function index of this instance.</summary>
        public MethodInfo[] Methods { get; set; }

        public string InstanceExpression(int instance) =>
            IsArray ? $"{InstanceName}[{instance}]" : InstanceName;
    }

    /// <summary>
    /// A method of an interface: what one function index of one interface instance
    /// calls. Every class's body for it has to hand back the same register - the
    /// call site reads one - and the parameters are every register any of them
    /// reads, since the call site passes the same arguments whichever runs.
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

        /// <summary>The class type ID, where IFCE gave one.</summary>
        public int TypeId { get; init; }

        public List<InterfaceTypeInfo> Implements { get; } = [];

        /// <summary>The body this class runs for each method of what it implements.</summary>
        public Dictionary<MethodInfo, FunctionBodyInfo> Bodies { get; } = [];
    }

    public List<InterfaceInfo> Interfaces { get; } = [];
    public List<FunctionTableInfo> Tables { get; } = [];
    public List<FunctionBodyInfo> Bodies { get; } = [];
    public List<InterfaceTypeInfo> InterfaceTypes { get; } = [];
    public List<ClassInfo> Classes { get; } = [];

    /// <summary>
    /// How many instructions the main program holds: everything before the first
    /// label, which is where the first body starts.
    /// </summary>
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
        foreach (var label in labels)
        {
            FunctionBodyInfo body = model.BodyByLabel(label.Number);
            body.First = label.Index + 1;
            body.Last = label.Index == labels[^1].Index
                ? shader.Instructions.Count
                : labels[labels.IndexOf(label) + 1].Index;
            ReadSignature(shader, body);
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
    /// The registers the body reads before writing them, and the one it writes. A read
    /// of a constant buffer, a sampler, a resource or the rasterizer is a read of
    /// something HLSL declares outside the method too, and not a parameter; anything
    /// else the body reads without first writing it came in as an argument.
    /// </summary>
    private static void ReadSignature(ShaderModel shader, FunctionBodyInfo body)
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
    /// The names the reflection data keeps. The fps take the interface slots in
    /// order, as many each as the array declares, and an interface variable's offset
    /// in $ThisPointer is the first slot it takes - so g_many at offset 1 is fp1
    /// when g_one before it takes slot 0. The slot record covering that slot says
    /// which class type fills which of the fp's tables. Every interface that has
    /// the same declared type is one interface, and every table of one class type
    /// one class, however many instances it was bound through.
    ///
    /// False, with nothing named, when either chunk is missing or the two do not
    /// account for every fp and every table.
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

    /// <summary>
    /// Names for a shader whose reflection data is gone: each fp an interface of its
    /// own with a global of its own, and each of its tables a class implementing it.
    /// </summary>
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
    /// One method per function index of each fp, numbered across the shader so that
    /// two interfaces one class implements never declare the same name. Two fps of
    /// one interface may be calling one method or two, and nothing in the bytecode
    /// says which - each gets its own, which compiles to the same tables.
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
    /// Each class with the interfaces it implements and the body it runs for every
    /// method of them. A class bound through one instance of an interface and not
    /// through another of the same interface has no body for half its methods, and
    /// says so.
    ///
    /// The order is fxc's: it numbers the classes of one interface last declared
    /// first, so declaring them by descending type ID within each interface gives
    /// them back the IDs they had.
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

    private static bool IsOutsideTheMethod(OperandType type) =>
        type is OperandType.Immediate32 or OperandType.Immediate64
            or OperandType.Sampler or OperandType.Resource
            or OperandType.ConstantBuffer or OperandType.ImmediateConstantBuffer
            or OperandType.UnorderedAccessView or OperandType.ThreadGroupSharedMemory
            or OperandType.Rasterizer or OperandType.Label or OperandType.Null
            or OperandType.Stream or OperandType.FunctionBody or OperandType.FunctionTable
            or OperandType.Interface or OperandType.ThisPointer;
}
