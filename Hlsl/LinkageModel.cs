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

        /// <summary>Components read of each parameter: a temp argument has no dcl.</summary>
        public Dictionary<RegisterKey, int> ParameterWidths { get; } = [];

        public RegisterKey ReturnRegister { get; set; }
        public int ReturnComponents { get; set; } = 4;

        public ClassInfo Class { get; set; }
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

        /// <summary>The widest any body reads each parameter.</summary>
        public Dictionary<RegisterKey, int> ParameterWidths { get; } = [];
        public RegisterKey ReturnRegister { get; set; }
        public List<FunctionBodyInfo> Bodies { get; } = [];

        /// <summary>The components the caller reads: float3 if only .xyz.</summary>
        public int ReturnComponents { get; set; } = 4;
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

        /// <summary>
        /// Member data its bodies read through `this`, as cb[this.x][this.y + row]:
        /// the kind each (row * 4 + component) is read as, and the members laid over
        /// it, declared on a constant buffer number no shader binds.
        /// </summary>
        public SortedDictionary<int, ParameterType> DataComponents { get; } = [];
        public int DataRegister { get; set; }
        public List<D3D10ConstantDeclaration> Members { get; } = [];
    }

    /// <summary>Past the 14 slots a shader may bind.</summary>
    private const int FirstClassDataRegister = 1000;

    private const int RegisterSizeInBytes = 16;

    // Movs of `this` into index registers; a body reads its data through them.
    private readonly HashSet<D3D10Instruction> _thisMoves = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<D3D10Instruction, Dictionary<int, (ClassInfo Class, int Row)>> _classDataReads =
        new(ReferenceEqualityComparer.Instance);

    public bool IsThisMove(D3D10Instruction instruction) => _thisMoves.Contains(instruction);

    /// <summary>Whether an operand reads class member data, and which row of it.</summary>
    public bool TryGetClassData(D3D10Instruction instruction, int operand,
        out ClassInfo classInfo, out int row)
    {
        if (_classDataReads.TryGetValue(instruction, out var reads)
            && reads.TryGetValue(operand, out var read))
        {
            (classInfo, row) = read;
            return true;
        }
        (classInfo, row) = (null, 0);
        return false;
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
        // fxc inlines a call through an interface only one class implements.
        if (shader.Instructions.Take(model.MainInstructionCount).Any(i =>
            i is D3D10Instruction { Opcode: D3D10Opcode.Mov } move
            && move.GetOperandType(1) == OperandType.ThisPointer))
        {
            throw new NotImplementedException("class data read outside a method (an inlined call)");
        }
        HashSet<RegisterKey> readAfterCalls = ReadAfterCalls(shader, model.MainInstructionCount);
        foreach (var label in labels)
        {
            FunctionBodyInfo body = model.BodyByLabel(label.Number);
            body.First = label.Index + 1;
            body.Last = label.Index == labels[^1].Index
                ? shader.Instructions.Count
                : labels[labels.IndexOf(label) + 1].Index;
            model.ReadSignature(shader, body, readAfterCalls);
        }

        if (!ReadNames(model, shader))
        {
            NameByNumber(model);
        }
        ReadMethods(model);
        ReadClasses(model);
        model.ReadClassData(shader);
        ReadReturnComponents(model, shader);
        return model;
    }

    /// <summary>
    /// Parameters are registers read before written, excluding globals (cbuffers,
    /// resources, samplers...); the result is the one register written.
    /// </summary>
    private void ReadSignature(ShaderModel shader, FunctionBodyInfo body,
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
            // Only addresses the body's data; neither an argument nor a result.
            if (instruction.Opcode == D3D10Opcode.Mov
                && instruction.GetOperandType(1) == OperandType.ThisPointer)
            {
                _thisMoves.Add(instruction);
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
                if (body.Parameters.Contains(read))
                {
                    body.ParameterWidths[read] = Math.Max(
                        body.ParameterWidths.GetValueOrDefault(read), ReadWidth(instruction, operand));
                }
            }

            if (destination != null)
            {
                RegisterKey writtenKey = instruction.GetParamRegisterKey(destination.Value);
                written.Add(writtenKey);
                // A parameter the body writes into (its result in r0.yzw beside the
                // argument in r0.x) is as wide as those lanes too.
                if (body.Parameters.Contains(writtenKey) && IsTempParameter(writtenKey))
                {
                    int mask = instruction.GetWriteMask(destination.Value);
                    body.ParameterWidths[writtenKey] = Math.Max(
                        body.ParameterWidths.GetValueOrDefault(writtenKey),
                        32 - System.Numerics.BitOperations.LeadingZeroCount((uint)mask));
                }
            }
        }

        // A body also writes its own temps; the result is the register the caller
        // reads after a call.
        List<RegisterKey> results = written.Count == 1
            ? [.. written]
            : [.. written.Where(readAfterCalls.Contains)];
        // A body may reuse its argument's register as scratch (`dp3_sat r0.x,
        // r0.xyzx, ...` then the result in r1) while main reads that register after
        // another call: the result is the register that is not the argument.
        if (results.Count > 1 && results.Count(r => !body.Parameters.Contains(r)) == 1)
        {
            results = [.. results.Where(r => !body.Parameters.Contains(r))];
        }
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
            // A temp is a value main computed before the call (`Shade(normalize(n))`).
            if (parameter is not D3D10RegisterKey { OperandType: OperandType.Input or OperandType.Temp })
            {
                throw new NotImplementedException(
                    "a function body argument that is not an input or temp register");
            }
        }
    }

    /// <summary>
    /// The instance an fcall calls: `fp0[2][0]` the immediate 2; `fp0[r0.w + 1][0]`
    /// 1 plus the register's component, read at run time. The function index dword
    /// comes before the operand, so the operand is decoded here, not by position.
    /// </summary>
    public static (int Immediate, (OperandType Type, int Number, byte Component)? Relative) CallInstance(
        D3D10Instruction call)
    {
        uint[] tokens = call.OperandTokens.Tokens;
        uint operand = tokens[1];
        int i = (operand & 0x80000000) != 0 ? 3 : 2;
        i++; // the interface number, an immediate
        var representation = (D3D10OperandIndexRepresentation)((operand >> 25) & 7);
        switch (representation)
        {
            case D3D10OperandIndexRepresentation.Immediate32:
                return ((int)tokens[i], null);
            case D3D10OperandIndexRepresentation.Relative:
                return (0, RelativeOperand(tokens, i));
            case D3D10OperandIndexRepresentation.Immediate32PlusRelative:
                return ((int)tokens[i], RelativeOperand(tokens, i + 1));
            default:
                throw new NotImplementedException($"an interface call indexed as {representation}");
        }

        static (OperandType, int, byte) RelativeOperand(uint[] tokens, int at) =>
            ((OperandType)((tokens[at] >> 12) & 0xFF), (int)tokens[at + 1], (byte)((tokens[at] >> 4) & 3));
    }

    /// <summary>
    /// Whether a parameter is a temp main computed, named and typed by the linkage
    /// rather than by an input declaration.
    /// </summary>
    public static bool IsTempParameter(RegisterKey parameter)
    {
        return parameter is D3D10RegisterKey { OperandType: OperandType.Temp };
    }

    // The components of a register an operand reads, as a width from x: a dot
    // product its first two, three or four, else those under the write mask.
    private static int ReadWidth(D3D10Instruction instruction, int operand)
    {
        byte[] swizzle = instruction.GetSourceSwizzleComponents(operand);
        int count = instruction.Opcode switch
        {
            D3D10Opcode.Dp2 => 2,
            D3D10Opcode.Dp3 => 3,
            D3D10Opcode.Dp4 => 4,
            _ => 0,
        };
        IEnumerable<byte> read = count != 0
            ? swizzle.Take(count)
            : instruction.GetDestinationParamIndex() is int destination
                ? Enumerable.Range(0, 4)
                    .Where(c => (instruction.GetWriteMask(destination) & (1 << c)) != 0)
                    .Select(c => swizzle[c])
                : swizzle;
        return read.Max() + 1;
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
                        method.ParameterWidths[parameter] = Math.Max(
                            method.ParameterWidths.GetValueOrDefault(parameter),
                            body.ParameterWidths.GetValueOrDefault(parameter, 4));
                    }
                }
                // Inputs, then the temps main computed.
                method.Parameters.Sort((a, b) =>
                    (IsTempParameter(a), a.Number).CompareTo((IsTempParameter(b), b.Number)));
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
                    FunctionBodyInfo body = model.BodyByLabel(bodies[method.Function]);
                    classInfo.Bodies[method] = body;
                    body.Class = classInfo;
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
    /// Follows `this` through the index registers each body moves it into, and
    /// records the rows and components of its class's data the body reads, and as
    /// what. Only data members: textures and samplers (this.z, .w) are not read.
    /// </summary>
    private void ReadClassData(ShaderModel shader)
    {
        var integerAnalysis = new IntegerOperandAnalysis(shader);
        for (int i = 0; i < Classes.Count; i++)
        {
            Classes[i].DataRegister = FirstClassDataRegister + i;
        }
        foreach (FunctionBodyInfo body in Bodies)
        {
            // The `this` component each index register component holds.
            var thisParts = new Dictionary<RegisterComponentKey, int>();
            for (int i = body.First; i < body.Last; i++)
            {
                var instruction = (D3D10Instruction)shader.Instructions[i];
                if (_thisMoves.Contains(instruction))
                {
                    RegisterKey destination = instruction.GetParamRegisterKey(0);
                    byte[] swizzle = instruction.GetSourceSwizzleComponents(1);
                    for (int component = 0; component < 4; component++)
                    {
                        if ((instruction.GetWriteMask(0) & (1 << component)) != 0)
                        {
                            thisParts[new RegisterComponentKey(destination, component)] = swizzle[component];
                        }
                    }
                    continue;
                }

                for (int operand = 0; operand < instruction.OperandTokens.OperandCount; operand++)
                {
                    if (instruction.GetOperandType(operand) != OperandType.ConstantBuffer)
                    {
                        continue;
                    }
                    var indices = instruction.OperandTokens.GetOperandIndices(operand);
                    if (!indices[0].IsRelative)
                    {
                        continue;
                    }
                    if (indices.Length != 2 || !indices[1].IsRelative || indices[0].Immediate != 0
                        || ThisPart(instruction, operand, 0, thisParts) != 0
                        || ThisPart(instruction, operand, 1, thisParts) != 1)
                    {
                        throw new NotImplementedException(
                            "a constant buffer chosen by a register other than this");
                    }
                    if (body.Class == null)
                    {
                        throw new NotImplementedException("class data read by a body of no class");
                    }
                    int row = (int)indices[1].Immediate;
                    ParameterType kind = ReadKind(instruction, integerAnalysis);
                    foreach (byte component in instruction.GetSourceSwizzleComponents(operand).Distinct())
                    {
                        body.Class.DataComponents.TryAdd(row * 4 + component, kind);
                    }
                    if (!_classDataReads.TryGetValue(instruction, out var reads))
                    {
                        reads = [];
                        _classDataReads[instruction] = reads;
                    }
                    reads[operand] = (body.Class, row);
                }
            }
        }
        foreach (ClassInfo classInfo in Classes)
        {
            LayOutMembers(classInfo);
        }
    }

    private static int? ThisPart(D3D10Instruction instruction, int operand, int index,
        Dictionary<RegisterComponentKey, int> thisParts)
    {
        (OperandType type, int number, byte component) =
            instruction.OperandTokens.GetRelativeIndexOperand(operand, index);
        return thisParts.TryGetValue(
            new RegisterComponentKey(new D3D10RegisterKey(type, number), component), out int part)
            ? part
            : null;
    }

    // Integers are uint unless a signed operation reads them.
    private static ParameterType ReadKind(D3D10Instruction instruction, IntegerOperandAnalysis analysis)
    {
        if (instruction.Opcode is not (D3D10Opcode.And or D3D10Opcode.Or or D3D10Opcode.Xor or D3D10Opcode.Not)
            && !analysis.IsIntegerOperand(instruction))
        {
            return ParameterType.Float;
        }
        return instruction.Opcode is D3D10Opcode.IShr or D3D10Opcode.Ilt or D3D10Opcode.Ige
            or D3D10Opcode.IMin or D3D10Opcode.IMax or D3D10Opcode.IBFE or D3D10Opcode.IMul
            or D3D10Opcode.IMad or D3D10Opcode.INeg or D3D10Opcode.IToF
            ? ParameterType.Int
            : ParameterType.Uint;
    }

    /// <summary>
    /// One member per run of components read alike within a row. Unread ones before
    /// the last read one are padding, so every member keeps its offset.
    /// </summary>
    private static void LayOutMembers(ClassInfo classInfo)
    {
        if (classInfo.DataComponents.Count == 0)
        {
            return;
        }
        int last = classInfo.DataComponents.Keys.Max();
        int member = 0;
        int padding = 0;
        for (int start = 0; start <= last;)
        {
            bool read = classInfo.DataComponents.TryGetValue(start, out ParameterType kind);
            int end = start + 1;
            while (end <= last && end % 4 != 0
                && classInfo.DataComponents.TryGetValue(end, out ParameterType next) == read
                && (!read || next == kind))
            {
                end++;
            }
            int columns = end - start;
            var type = new ShaderTypeInfo(
                columns == 1 ? ParameterClass.Scalar : ParameterClass.Vector,
                read ? kind : ParameterType.Float, 1, columns, 0, null);
            classInfo.Members.Add(new D3D10ConstantDeclaration(
                read ? $"m{member++}" : $"pad{padding++}", (short)classInfo.DataRegister,
                columns * 4, start * 4, type, classInfo.Members.Count));
            start = end;
        }
    }

    /// <summary>
    /// How much of each method's result main reads after calling it, before the
    /// register is written again.
    /// </summary>
    private static void ReadReturnComponents(LinkageModel model, ShaderModel shader)
    {
        var read = new Dictionary<MethodInfo, int>();
        for (int call = 0; call < model.MainInstructionCount; call++)
        {
            if (shader.Instructions[call] is not D3D10Instruction { Opcode: D3D10Opcode.InterfaceCall } fcall)
            {
                continue;
            }
            MethodInfo method = model.MethodForCall(
                (int)fcall.OperandTokens.Tokens[2], (int)fcall.OperandTokens.Tokens[0]);
            int components = read.GetValueOrDefault(method);
            var written = new HashSet<int>();
            for (int i = call + 1; i < model.MainInstructionCount && written.Count < 4; i++)
            {
                var instruction = (D3D10Instruction)shader.Instructions[i];
                if (instruction.Opcode == D3D10Opcode.InterfaceCall)
                {
                    MethodInfo next = model.MethodForCall(
                        (int)instruction.OperandTokens.Tokens[2],
                        (int)instruction.OperandTokens.Tokens[0]);
                    // A call that takes the result as its argument reads it: `g.Go(f.Go(..))`.
                    if (next.ParameterWidths.TryGetValue(method.ReturnRegister, out int passed))
                    {
                        components = Math.Max(components, passed);
                    }
                    if (Equals(method.ReturnRegister, next.ReturnRegister))
                    {
                        break;
                    }
                    continue;
                }
                int? destination = instruction.GetDestinationParamIndex();
                for (int operand = 0; operand < instruction.OperandTokens.OperandCount; operand++)
                {
                    if (operand == destination
                        || instruction.GetOperandType(operand) != OperandType.Temp
                        || !Equals(instruction.GetParamRegisterKey(operand), method.ReturnRegister))
                    {
                        continue;
                    }
                    foreach (byte component in instruction.GetSourceSwizzleComponents(operand))
                    {
                        if (!written.Contains(component))
                        {
                            components = Math.Max(components, component + 1);
                        }
                    }
                }
                if (destination != null
                    && Equals(instruction.GetParamRegisterKey(destination.Value), method.ReturnRegister))
                {
                    for (int component = 0; component < 4; component++)
                    {
                        if ((instruction.GetWriteMask(destination.Value) & (1 << component)) != 0)
                        {
                            written.Add(component);
                        }
                    }
                }
            }
            read[method] = components;
        }
        foreach (InterfaceInfo iface in model.Interfaces)
        {
            foreach (MethodInfo method in iface.Methods)
            {
                if (read.TryGetValue(method, out int components) && components != 0)
                {
                    method.ReturnComponents = components;
                    foreach (FunctionBodyInfo body in method.Bodies)
                    {
                        body.ReturnComponents = components;
                    }
                }
            }
        }
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
