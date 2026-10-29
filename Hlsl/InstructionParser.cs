using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using HlslDecompiler.Hlsl.TemplateMatch;
using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public class InstructionParser
{
    private ShaderModel _shaderModel;
    private RegisterState _registerState;
    private IList<IStatement> _statements;
    private Stack<IStatement> _currentStatements;
    private int _instructionPointer;
    private IntegerOperandAnalysis _integerOperandAnalysis;

    // mov/movc immediates, typed after parsing by what reads them.
    private readonly List<(ConstantNode Constant, uint Bits)> _polymorphicImmediates = [];
    private readonly Dictionary<HlslTreeNode, bool> _storedTypes =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The values that are doubles, recorded where made: neither the node (a double
    /// add looks like a float add) nor the register (fxc reuses it) can say so.
    /// </summary>
    private readonly HashSet<HlslTreeNode> _doubleValues =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Components holding a double's high word (the double is keyed one below).
    /// </summary>
    private readonly HashSet<RegisterComponentKey> _doubleHighWords = [];

    /// <summary>
    /// msad instructions whose windows are the four true sliding windows of the
    /// recovered uint2; only those the instruction writer can write as one msad4().
    /// </summary>
    private readonly HashSet<D3D10Instruction> _standardMsadWindows =
        new(ReferenceEqualityComparer.Instance);

    private IStatement ActiveStatement => _currentStatements.Count != 0 ? _currentStatements.Peek() : null;
    private IDictionary<RegisterComponentKey, HlslTreeNode> ActiveOutputs => ActiveStatement?.Outputs;
    private IList<IStatement> ActiveStatementSequence
    {
        get
        {
            if (_currentStatements.Count == 0)
            {
                return _statements;
            }
            if (_currentStatements.Peek() is IfStatement ifStatement)
            {
                return ifStatement.IsTrueParsed ? ifStatement.FalseBody : ifStatement.TrueBody;
            }
            if (_currentStatements.Peek() is LoopStatement loopStatement)
            {
                return loopStatement.Body;
            }
            if (_currentStatements.Peek() is SwitchStatement switchStatement)
            {
                return switchStatement.CurrentCase.Body;
            }
            throw new NotImplementedException();
        }
    }

    public static HlslAst Parse(ShaderModel shader)
    {
        var parser = new InstructionParser();
        return parser.ParseToAst(PixelShader1Lowering.Lower(shader));
    }

    /// <summary>
    /// Parses the control point and patch constant phases separately, each with
    /// the shared declarations and its own register state (both write o0 with
    /// different meanings).
    /// </summary>
    public static HullShaderAst ParseHullShader(ShaderModel shader)
    {
        (ShaderModel controlPoint, ShaderModel patchConstant) = HullShaderPhases.Split(shader);
        return new HullShaderAst(
            controlPoint == null ? null : new HullPhase(controlPoint, Parse(controlPoint)),
            patchConstant == null ? null : new HullPhase(patchConstant, Parse(patchConstant)));
    }

    private HlslAst ParseToAst(ShaderModel shader)
    {
        _shaderModel = shader;
        _integerOperandAnalysis = new IntegerOperandAnalysis(shader);
        _registerState = new RegisterState(shader);
        _statements = [];
        _currentStatements = new Stack<IStatement>();

        _instructionPointer = 0;
        List<(LinkageModel.FunctionBodyInfo, IList<IStatement>)> linkageBodies = null;
        if (shader.Instructions[0] is D3D10Instruction)
        {
            _registerState.Linkage = LinkageModel.Read(shader);
            foreach (LinkageModel.MethodInfo method in _registerState.Linkage.Interfaces
                .SelectMany(i => i.Methods ?? []).Where(m => m != null))
            {
                foreach (RegisterKey parameter in method.Parameters.Where(LinkageModel.IsTempParameter))
                {
                    _registerState.LinkageTempParameters[parameter] = Math.Max(
                        _registerState.LinkageTempParameters.GetValueOrDefault(parameter),
                        method.ParameterWidths.GetValueOrDefault(parameter, 4));
                }
            }
            int mainEnd = _registerState.Linkage.HasLinkage
                ? _registerState.Linkage.MainInstructionCount
                : shader.Instructions.Count;
            while (_instructionPointer < mainEnd)
            {
                ParseInstruction(shader.Instructions[_instructionPointer] as D3D10Instruction);
                _instructionPointer++;
            }
            if (_registerState.Linkage.HasLinkage)
            {
                // Each body is parsed as its own method against main's declarations.
                IList<IStatement> mainStatements = _statements;
                Stack<IStatement> mainStack = _currentStatements;
                // A body starts from the declared inputs only, never from main's
                // values: what it reads before writing is a parameter.
                var declared = new Dictionary<RegisterComponentKey, HlslTreeNode>();
                if (mainStatements.Count != 0)
                {
                    foreach (var entry in mainStatements[^1].Outputs)
                    {
                        if (entry.Value is RegisterInputNode)
                        {
                            declared[entry.Key] = entry.Value;
                        }
                    }
                }
                DeclareClassData(declared);
                linkageBodies = [];
                foreach (LinkageModel.FunctionBodyInfo body in _registerState.Linkage.Bodies)
                {
                    _statements = [];
                    _currentStatements = new Stack<IStatement>();
                    // A temp main passes in is the body's parameter, read as itself.
                    var bodyStart = new Dictionary<RegisterComponentKey, HlslTreeNode>(declared);
                    foreach (RegisterKey parameter in body.Parameters.Where(LinkageModel.IsTempParameter))
                    {
                        for (int component = 0; component < _registerState.LinkageTempParameters[parameter]; component++)
                        {
                            var componentKey = new RegisterComponentKey(parameter, component);
                            bodyStart[componentKey] = new RegisterInputNode(componentKey);
                        }
                    }
                    // Stands in for main's dcl instructions, which are not re-parsed.
                    InsertStatement(new AssignmentStatement(bodyStart));
                    _instructionPointer = body.First;
                    while (_instructionPointer < body.Last)
                    {
                        ParseInstruction(shader.Instructions[_instructionPointer] as D3D10Instruction);
                        _instructionPointer++;
                    }
                    linkageBodies.Add((body, _statements));
                }
                _statements = mainStatements;
                _currentStatements = mainStack;
                _instructionPointer = mainEnd;
            }
        }
        else
        {
            while (_instructionPointer < shader.Instructions.Count)
            {
                ParseInstruction(shader.Instructions[_instructionPointer] as D3D9Instruction);
                _instructionPointer++;
            }
        }

        ParsedIdioms.Recover(_registerState,
            [_statements, .. (linkageBodies ?? []).Select(body => body.Item2)]);
        ResolvePolymorphicImmediates();
        return new HlslAst(_statements, _registerState, _doubleValues)
        {
            PreshaderOutputs = _preshaderOutputs,
            LinkageBodies = linkageBodies,
            StandardMsadWindows = _standardMsadWindows,
        };
    }

    private void ParseInstruction(D3D9Instruction instruction)
    {
        if (instruction.HasDestination)
        {
            if (instruction.Opcode == Opcode.TexKill)
            {
                InsertClip(instruction);
            }
            else
            {
                if (instruction.Opcode == Opcode.Dcl
                    && instruction.GetParamRegisterType(1) == RegisterType.Sampler)
                {
                    DeclareUnnamedSampler(instruction);
                }
                ParseAssignmentInstruction(instruction);
            }
        }
        else
        {
            switch (instruction.Opcode)
            {
                case Opcode.Comment:
                    if (instruction.Params.Count > 0 && instruction.Params[0] == FourCC.Make("PRES"))
                    {
                        ParsePreshaderComment(instruction);
                    }
                    else
                    {
                        ParseConstantTableComment(instruction);
                    }
                    break;
                case Opcode.If:
                case Opcode.IfC:
                case Opcode.Else:
                case Opcode.Loop:
                case Opcode.Rep:
                case Opcode.Endif:
                case Opcode.EndLoop:
                case Opcode.EndRep:
                case Opcode.Break:
                case Opcode.BreakC:
                case Opcode.End:
                    ParseControlInstruction(instruction);
                    break;
                default:
                    throw new NotImplementedException($"{instruction.Opcode}");
            }
        }
    }

    /// <summary>
    /// Class member data reads like a constant buffer of its own, one per class.
    /// </summary>
    private void DeclareClassData(Dictionary<RegisterComponentKey, HlslTreeNode> declared)
    {
        foreach (LinkageModel.ClassInfo classInfo in _registerState.Linkage.Classes)
        {
            if (classInfo.Members.Count == 0)
            {
                continue;
            }
            foreach (D3D10ConstantDeclaration member in classInfo.Members)
            {
                _registerState.ConstantDeclarations.Add(member);
            }
            int rows = (classInfo.Members.Max(m => m.VariableOffset + m.VariableSize) + 15) / 16;
            for (int row = 0; row < rows; row++)
            {
                var registerKey = new D3D10RegisterKey(OperandType.ConstantBuffer, classInfo.DataRegister, row);
                _registerState.DeclareRegister(registerKey, 0xF);
                for (int component = 0; component < 4; component++)
                {
                    var componentKey = new RegisterComponentKey(registerKey, component);
                    declared[componentKey] = new RegisterInputNode(componentKey);
                }
            }
        }
    }

    private void ParseInstruction(D3D10Instruction instruction)
    {
        // Only addresses class data, which is read by name.
        if (_registerState.Linkage?.IsThisMove(instruction) == true)
        {
            return;
        }
        // vForkInstanceID and vJoinInstanceID need no declaration: unrolled phases
        // read them as constants.
        if (instruction.Opcode == D3D10Opcode.DclInput
            && instruction.GetOperandType(0) is OperandType.InputForkInstanceID or OperandType.InputJoinInstanceID)
        {
            return;
        }
        // StoreStructured names a destination operand but writes through a statement,
        // and SinCos, Udiv, IMul, UMul, UAddC, USubB and SwapC each write two
        // destinations, so none of them fits the single-destination assignment path.
        if (instruction.HasDestination
            && instruction.GetOperandType(instruction.GetDestinationParamIndex().Value) == OperandType.IndexableTemp)
        {
            InsertIndexableTempStore(instruction);
        }
        else if (instruction.HasDestination
            && instruction.Opcode != D3D10Opcode.StoreStructured
            && instruction.Opcode != D3D10Opcode.StoreUAVTyped
            && instruction.Opcode != D3D10Opcode.StoreRaw
            && instruction.Opcode != D3D10Opcode.SinCos
            && instruction.Opcode != D3D10Opcode.IMul
            && instruction.Opcode != D3D10Opcode.UMul
            && instruction.Opcode != D3D10Opcode.UAddC
            && instruction.Opcode != D3D10Opcode.USubB
            && instruction.Opcode != D3D10Opcode.SwapC
            && instruction.Opcode != D3D10Opcode.Udiv
            // imm_atomic_* writes a register but is an ordered side effect.
            && !instruction.Opcode.IsImmediateAtomic())
        {
            ParseAssignmentInstruction(instruction);
        }
        else
        {
            switch (instruction.Opcode)
            {
                case D3D10Opcode.Break:
                    InsertStatement(new BreakStatement(null, ActiveOutputs));
                    break;
                case D3D10Opcode.BreakC:
                    InsertBreak(instruction);
                    break;
                case D3D10Opcode.Continue:
                    InsertStatement(new ContinueStatement(null, ActiveOutputs));
                    break;
                case D3D10Opcode.ContinueC:
                    InsertStatement(new ContinueStatement(GetConditionNode(instruction), ActiveOutputs));
                    break;
                case D3D10Opcode.Swtich:
                    InsertSwitchStatement(instruction);
                    break;
                case D3D10Opcode.Case:
                    AddSwitchCase(new ConstantNode((int)instruction.GetParamInt(0)));
                    break;
                case D3D10Opcode.Default:
                    AddSwitchCase(null);
                    break;
                case D3D10Opcode.EndSwitch:
                    EndSwitch();
                    break;
                case D3D10Opcode.If:
                    InsertIfStatement(instruction);
                    break;
                case D3D10Opcode.Else:
                    SwitchToElseBranch();
                    break;
                case D3D10Opcode.EndIf:
                    EndIf();
                    break;
                case D3D10Opcode.SinCos:
                    ParseSinCosInstruction(instruction);
                    break;
                case D3D10Opcode.Udiv:
                    ParseIntegerDivideInstruction(instruction);
                    break;
                case D3D10Opcode.IMul:
                case D3D10Opcode.UMul:
                    ParseIntegerMultiplyInstruction(instruction);
                    break;
                case D3D10Opcode.Cut:
                case D3D10Opcode.CutStream:
                    InsertRestartStrip(instruction.Stream);
                    break;
                case D3D10Opcode.EmitThenCut:
                case D3D10Opcode.EmitThenCutStream:
                    InsertAppend(instruction.Stream);
                    InsertRestartStrip(instruction.Stream);
                    break;
                // Each stream has its own output registers; see D3D10RegisterKey.Stream.
                case D3D10Opcode.DclStream:
                    _registerState.DeclareStream(instruction.GetParamRegisterNumber(0));
                    break;
                case D3D10Opcode.Discard:
                    {
                        InsertClip(instruction);
                        break;
                    }
                case D3D10Opcode.DclTemps:
                    {
                        int count = (int)instruction.GetParamInt(0);
                        for (int registerNumber = 0; registerNumber < count; registerNumber++)
                        {
                            var registerKey = new D3D10RegisterKey(OperandType.Temp, registerNumber);
                            int writeMask = 1; // declare only first component here, expand later
                            _registerState.DeclareRegister(registerKey, writeMask);
                            // No seed value: a temp has no meaning until written.
                        }
                        break;
                    }
                case D3D10Opcode.DclIndexableTemp:
                    _registerState.IndexableTemps[instruction.IndexableTempRegister] =
                        (instruction.IndexableTempElementCount, instruction.IndexableTempComponentCount);
                    break;
                case D3D10Opcode.DclConstantBuffer:
                    {
                        int registerNumber = (int)instruction.GetParamInt(0);
                        int constantBufferSize = instruction.GetParamConstantBufferOffset(0);
                        for (int i = 0; i < constantBufferSize; i++)
                        {
                            var registerKey = new D3D10RegisterKey(OperandType.ConstantBuffer, registerNumber, i);
                            _registerState.DeclareRegister(registerKey, 0xF);
                            for (int c = 0; c < 4; c++)
                            {
                                var destinationKey = new RegisterComponentKey(registerKey, c);
                                var resourceInput = new RegisterInputNode(destinationKey);
                                SetActiveOutput(destinationKey, resourceInput);
                            }
                        }
                        break;
                    }
                case D3D10Opcode.DclGSInputPrimitive:
                    {
                        _registerState.InputPrimitive = instruction.GetPrimitive();
                        break;
                    }
                case D3D10Opcode.DclGSMaxOutputVertexCount:
                    {
                        _registerState.MaxOutputVertexCount = (int)instruction.GetParamInt(0);
                        break;
                    }
                case D3D10Opcode.DclGSInstanceCount:
                    {
                        _registerState.GSInstanceCount = (int)instruction.GetParamInt(0);
                        break;
                    }
                case D3D10Opcode.DclGSOutputPrimitiveTopology:
                    {
                        _registerState.PrimitiveTopology = instruction.GetPrimitiveTopology();
                        if (instruction.Stream is int topologyStream)
                        {
                            _registerState.TopologyByStream[topologyStream] =
                                instruction.GetPrimitiveTopology();
                        }
                        break;
                    }
                case D3D10Opcode.DclResource:
                    {
                        var registerKey = instruction.GetParamRegisterKey(0);
                        _registerState.DeclareResource(registerKey, instruction.GetResourceDimension(), instruction.GetResourceReturnTypeToken(), instruction.ResourceSampleCount);
                        // All four: a sample reads the resource swizzled (t2.xyzw).
                        for (int component = 0; component < 4; component++)
                        {
                            var destinationKey = new RegisterComponentKey(registerKey, component);
                            SetActiveOutput(destinationKey, new RegisterInputNode(destinationKey));
                        }
                        break;
                    }
                case D3D10Opcode.DclUnorderedAccessViewTyped:
                    {
                        var registerKey = instruction.GetParamRegisterKey(0);
                        _registerState.DeclareResource(registerKey,
                            instruction.GetResourceDimension(), instruction.GetResourceReturnTypeToken());
                        for (int component = 0; component < 4; component++)
                        {
                            var destinationKey = new RegisterComponentKey(registerKey, component);
                            SetActiveOutput(destinationKey, new RegisterInputNode(destinationKey));
                        }
                        break;
                    }
                case D3D10Opcode.StoreUAVTyped:
                    {
                        // store_uav_typed u0, coordinate, value: the coordinate is
                        // as wide as the resource's dimensions.
                        RegisterComponentKey[] destinationKeys = GetDestinationKeys(instruction).ToArray();
                        var output = new RegisterInputNode(destinationKeys[0]);
                        int dimensions = _registerState.GetResourceDimensionSize(
                            destinationKeys[0].RegisterKey);
                        HlslTreeNode[] coordinates = [.. Enumerable.Range(0, dimensions)
                            .Select(component => GetInputs(instruction, component)[0])];
                        HlslTreeNode[] values = destinationKeys
                            .Select(key => GetInputs(instruction, key.ComponentIndex)[1])
                            .ToArray();
                        SplitStoredDoubles(values);
                        RecordStoredType(instruction, values);
                        InsertStatement(new StoreTypedStatement(
                            output, coordinates, values, ActiveOutputs));
                        break;
                    }
                case D3D10Opcode.DclResourceStructured:
                    {
                        var registerKey = instruction.GetParamRegisterKey(0);
                        _registerState.DeclareStructuredBuffer(registerKey, instruction.GetResourceStructuredBufferStride());
                        SeedResourceComponents(registerKey);
                        break;
                    }
                case D3D10Opcode.DclResourceRaw:
                case D3D10Opcode.DclUnorderedAccessViewRaw:
                    {
                        var registerKey = instruction.GetParamRegisterKey(0);
                        _registerState.DeclareRawBuffer(registerKey);
                        SeedResourceComponents(registerKey);
                        break;
                    }
                case D3D10Opcode.CustomData:
                    {
                        // Immediate constant buffer, the only customdata fxc emits.
                        uint[] data = instruction.CustomData ?? [];
                        for (int row = 0; row + 3 < data.Length; row += 4)
                        {
                            _registerState.ImmediateConstantBuffer.Add(new ConstantRegister(
                                row / 4,
                                BitConverter.Int32BitsToSingle((int)data[row]),
                                BitConverter.Int32BitsToSingle((int)data[row + 1]),
                                BitConverter.Int32BitsToSingle((int)data[row + 2]),
                                BitConverter.Int32BitsToSingle((int)data[row + 3])));
                        }
                        break;
                    }
                case D3D10Opcode.DclSampler:
                    {
                        var registerKey = instruction.GetParamRegisterKey(0);
                        _registerState.DeclareRegister(registerKey, 0xF);
                        // All four: a gather picks its channel by sampler swizzle (s0.w).
                        for (int component = 0; component < 4; component++)
                        {
                            var destinationKey = new RegisterComponentKey(registerKey, component);
                            SetActiveOutput(destinationKey, new RegisterInputNode(destinationKey));
                        }
                        break;
                    }
                case D3D10Opcode.DclThreadGroup:
                    {
                        _registerState.NumThreads = [
                            (int)instruction.GetParamIndexImmediate32(0, 0),
                            (int)instruction.GetParamIndexImmediate32(0, 1),
                            (int)instruction.GetParamIndexImmediate32(0, 2)];
                        break;
                    }
                case D3D10Opcode.DclUnorderedAccessViewStructured:
                    {
                        var registerKey = instruction.GetParamRegisterKey(0);
                        _registerState.DeclareUnorderedAccessView(
                            registerKey, instruction.GetResourceStructuredBufferStride());
                        SeedResourceComponents(registerKey);
                        break;
                    }
                case D3D10Opcode.DclThreadGroupSharedMemoryRaw:
                    {
                        var registerKey = instruction.GetParamRegisterKey(0);
                        // fxc uses raw for a non-array groupshared variable; it is
                        // modelled as dword elements and RegisterState.IsGroupSharedScalar
                        // decides whether it can be written without a subscript.
                        uint bytes = instruction.GetParamIndexImmediate32(1, 0);
                        _registerState.DeclareThreadGroupSharedMemory(registerKey,
                            sizeof(uint), bytes / sizeof(uint), isRaw: true);
                        // One access can read up to four dwords, so seed that many.
                        for (uint component = 0; component < Math.Min(bytes / sizeof(uint), 4); component++)
                        {
                            var sharedComponent = new RegisterComponentKey(registerKey, (int)component);
                            SetActiveOutput(sharedComponent, new RegisterInputNode(sharedComponent));
                        }
                        break;
                    }
                case D3D10Opcode.DclThreadGroupSharedMemoryStructured:
                    {
                        var registerKey = instruction.GetParamRegisterKey(0);
                        _registerState.DeclareThreadGroupSharedMemory(registerKey,
                            instruction.GetThreadGroupSharedMemoryStride(),
                            instruction.GetThreadGroupSharedMemoryCount());
                        SeedResourceComponents(registerKey);
                        break;
                    }
                case D3D10Opcode.Sync:
                    InsertStatement(new SyncStatement(instruction.SyncFlags, ActiveOutputs));
                    break;
                case D3D10Opcode.EndLoop:
                    EndLoop();
                    break;
                case D3D10Opcode.Emit:
                case D3D10Opcode.EmitStream:
                    InsertAppend(instruction.Stream);
                    break;
                case D3D10Opcode.Loop:
                    {
                        // DXBC loops carry no trip count; they exit through breakc.
                        var loop = new LoopStatement(null, ActiveOutputs);
                        SeedLoopHeaderPhis(loop);
                        InsertStatement(loop);
                        break;
                    }
                case D3D10Opcode.StoreStructured:
                    {
                        RegisterComponentKey[] destinationKeys = GetDestinationKeys(instruction).ToArray();
                        var output = new RegisterInputNode(destinationKeys[0]);
                        HlslTreeNode address = GetInputs(instruction, destinationKeys[0].ComponentIndex)[0];
                        HlslTreeNode[] values = destinationKeys
                            .Select(key => GetInputs(instruction, key.ComponentIndex)[2])
                            .ToArray();
                        SplitStoredDoubles(values);
                        RecordStoredType(instruction, values);
                        // A store at an alloc'd slot completes an Append. A struct wider
                        // than a register is stored a few members at a time (xyzw at 0,
                        // then xy at 16): one Append, at the last store.
                        if (address is AppendSlotNode appendSlot
                            && appendSlot.Buffer.RegisterComponentKey.RegisterKey.Number
                                == instruction.GetParamRegisterNumber(0))
                        {
                            int firstWord = instruction.GetOperandType(2) == OperandType.Immediate32
                                ? instruction.GetParamInt(2, 0) / 4
                                : 0;
                            HlslTreeNode[] words = _appendsBySlot.TryGetValue(appendSlot, out BufferAppendStatement earlier)
                                ? earlier.Values
                                : [];
                            if (words.Length < firstWord + values.Length)
                            {
                                Array.Resize(ref words, firstWord + values.Length);
                            }
                            else
                            {
                                words = [.. words];
                            }
                            values.CopyTo(words, firstWord);
                            if (earlier != null)
                            {
                                earlier.IsSuperseded = true;
                            }
                            var append = new BufferAppendStatement(appendSlot.Buffer, words, ActiveOutputs);
                            _appendsBySlot[appendSlot] = append;
                            InsertStatement(append);
                            break;
                        }
                        InsertStatement(new StoreStructuredStatement(output, address, values, ActiveOutputs)
                        {
                            ElementByteOffset = instruction.GetOperandType(2) == OperandType.Immediate32
                                ? instruction.GetParamInt(2, 0)
                                : 0,
                            ByteOffset = instruction.GetOperandType(2) == OperandType.Immediate32
                                ? null
                                : GetInputs(instruction, destinationKeys[0].ComponentIndex)[1],
                            MemberByteOffset = RegisterState.MemberByteOffset(
                                GetInputs(instruction, destinationKeys[0].ComponentIndex)[1]),
                            Components = [.. destinationKeys.Select(k => k.ComponentIndex)],
                        });
                        break;
                    }
                case D3D10Opcode.ImmAtomicConsume:
                    {
                        // On a consume buffer the loads through the slot become the
                        // Consume() call; on a counter UAV the slot is a plain value.
                        var consumeKey = new RegisterComponentKey(
                            instruction.GetParamRegisterKey(1), 0);
                        HlslTreeNode slot = IsConsumeBuffer(instruction.GetParamRegisterKey(1))
                            ? new ConsumeSlotNode(new RegisterInputNode(consumeKey))
                            : new BufferCounterNode(new RegisterInputNode(consumeKey), isIncrement: false);
                        // The only thing ordering two otherwise independent consumes.
                        StampSourceInstruction(slot, FirstWrittenComponent(instruction));
                        var slotKey = (D3D10RegisterKey)instruction.GetParamRegisterKey(0);
                        _registerState.DeclareRegisterWrite(slotKey, instruction.GetWriteMask(0));
                        SetActiveOutput(
                            new RegisterComponentKey(slotKey, FirstWrittenComponent(instruction)),
                            slot);
                        break;
                    }
                case D3D10Opcode.ImmAtomicAlloc:
                    {
                        // alloc + the store at its slot form one Append(), made by the
                        // store since code may sit between them. On a counter UAV the
                        // slot is a plain value.
                        var appendKey = new RegisterComponentKey(
                            instruction.GetParamRegisterKey(1), 0);
                        HlslTreeNode appendSlot = IsAppendBuffer(instruction.GetParamRegisterKey(1))
                            ? new AppendSlotNode(new RegisterInputNode(appendKey))
                            : new BufferCounterNode(new RegisterInputNode(appendKey), isIncrement: true);
                        var allocKey = (D3D10RegisterKey)instruction.GetParamRegisterKey(0);
                        _registerState.DeclareRegisterWrite(allocKey, instruction.GetWriteMask(0));
                        SetActiveOutput(
                            new RegisterComponentKey(allocKey, FirstWrittenComponent(instruction)),
                            appendSlot);
                        break;
                    }
                case D3D10Opcode.AtomicIAdd:
                case D3D10Opcode.AtomicAnd:
                case D3D10Opcode.AtomicOr:
                case D3D10Opcode.AtomicXor:
                case D3D10Opcode.AtomicIMax:
                case D3D10Opcode.AtomicIMin:
                case D3D10Opcode.AtomicUMax:
                case D3D10Opcode.AtomicUMin:
                case D3D10Opcode.AtomicCmpStore:
                case D3D10Opcode.ImmAtomicIAdd:
                case D3D10Opcode.ImmAtomicAnd:
                case D3D10Opcode.ImmAtomicOr:
                case D3D10Opcode.ImmAtomicXor:
                case D3D10Opcode.ImmAtomicIMax:
                case D3D10Opcode.ImmAtomicIMin:
                case D3D10Opcode.ImmAtomicUMax:
                case D3D10Opcode.ImmAtomicUMin:
                case D3D10Opcode.ImmAtomicExch:
                case D3D10Opcode.ImmAtomicCmpExch:
                    {
                        // atomic_iadd u0, address, value (cmp_store has a compare before
                        // the value). One statement; the resource has no write mask.
                        // imm_ forms put the original-value register first.
                        bool keepsOriginal = instruction.Opcode.IsImmediateAtomic();
                        int first = keepsOriginal ? 1 : 0;
                        var resourceKey = new RegisterComponentKey(
                            instruction.GetParamRegisterKey(first), 0);
                        _registerState.DeclareAtomicTarget(resourceKey.RegisterKey);
                        var destination = new RegisterInputNode(resourceKey);
                        bool hasCompare = instruction.Opcode
                            is D3D10Opcode.AtomicCmpStore or D3D10Opcode.ImmAtomicCmpExch;
                        // Structured: element + byte offset; raw: offset; typed: a
                        // coordinate per dimension.
                        int addressWidth = _registerState.GetAtomicAddressWidth(
                            resourceKey.RegisterKey);
                        HlslTreeNode[] coordinates = addressWidth > 1
                            ? [.. Enumerable.Range(0, addressWidth)
                                .Select(component => GetInputs(instruction, component)[first])]
                            : null;
                        // Shares the node with coordinates[0] so rewrites hit both.
                        HlslTreeNode address = coordinates?[0] ?? GetInputs(instruction, 0)[first];
                        HlslTreeNode elementByteOffset =
                            _registerState.IsRawResource(resourceKey.RegisterKey) || coordinates != null
                            ? null
                            : GetInputs(instruction, 1)[first];
                        TempVariableNode original = keepsOriginal
                            ? new TempVariableNode { IsInteger = true, VariableSize = 1 }
                            : null;
                        HlslTreeNode atomicValue = GetInputs(instruction, 0)[first + (hasCompare ? 2 : 1)];
                        // Atomics read integers; a statement, so the reader walk does
                        // not see it (a movc's `l(9)` arm would print as a float).
                        _storedTypes[atomicValue] = true;
                        if (hasCompare)
                        {
                            _storedTypes[GetInputs(instruction, 0)[first + 1]] = true;
                        }
                        InsertStatement(new AtomicStatement(
                            destination,
                            address,
                            atomicValue,
                            instruction.Opcode.AtomicMethodName(),
                            ActiveOutputs)
                        {
                            ElementByteOffset = elementByteOffset,
                            Coordinates = coordinates,
                            Compare = hasCompare ? GetInputs(instruction, 0)[first + 1] : null,
                            Original = original,
                            IsUnsigned = instruction.Opcode switch
                            {
                                D3D10Opcode.AtomicUMax or D3D10Opcode.AtomicUMin
                                    or D3D10Opcode.ImmAtomicUMax or D3D10Opcode.ImmAtomicUMin => true,
                                D3D10Opcode.AtomicIMax or D3D10Opcode.AtomicIMin
                                    or D3D10Opcode.ImmAtomicIMax or D3D10Opcode.ImmAtomicIMin => false,
                                _ => null,
                            },
                        });
                        if (original != null)
                        {
                            var originalKey = (D3D10RegisterKey)instruction.GetParamRegisterKey(0);
                            _registerState.DeclareRegisterWrite(
                                originalKey, instruction.GetWriteMask(0));
                            SetActiveOutput(
                                new RegisterComponentKey(originalKey, FirstWrittenComponent(instruction)),
                                original);
                        }
                        break;
                    }
                case D3D10Opcode.StoreRaw:
                    {
                        // store_raw u0.xy, byteOffset, value.
                        RegisterComponentKey[] destinationKeys = GetDestinationKeys(instruction).ToArray();
                        var output = new RegisterInputNode(destinationKeys[0]);
                        HlslTreeNode address = GetInputs(instruction, destinationKeys[0].ComponentIndex)[0];
                        HlslTreeNode[] values = destinationKeys
                            .Select(key => GetInputs(instruction, key.ComponentIndex)[1])
                            .ToArray();
                        SplitStoredDoubles(values);
                        RecordStoredType(instruction, values);
                        if (IsGroupSharedResource(instruction, 0))
                        {
                            // One assignment per dword element.
                            for (int value = 0; value < values.Length; value++)
                            {
                                InsertStatement(new StoreStructuredStatement(output,
                                    GroupSharedElementOfByteAddress(
                                        address, destinationKeys[value].ComponentIndex),
                                    [values[value]], ActiveOutputs)
                                {
                                    IsRaw = true,
                                    IsGroupShared = true,
                                });
                            }
                            break;
                        }
                        InsertStatement(new StoreStructuredStatement(output, address, values, ActiveOutputs) { IsRaw = true });
                        break;
                    }
                case D3D10Opcode.Ret:
                    InsertReturn();
                    break;
                case D3D10Opcode.RetC:
                    InsertStatement(new ReturnStatement(ActiveOutputs)
                    {
                        Comparison = GetConditionNode(instruction),
                    });
                    break;
                case D3D10Opcode.DclGlobalFlags:
                    // The only flag the source states; fxc derives the rest.
                    _registerState.ForceEarlyDepthStencil = instruction.GetGlobalFlags()
                        .HasFlag(D3D10GlobalFlags.ForceEarlyDepthStencil);
                    break;
                case D3D10Opcode.DclInputControlPointCount:
                    _registerState.InputControlPointCount = instruction.ControlPointCount;
                    break;
                case D3D10Opcode.DclOutputControlPointCount:
                    _registerState.OutputControlPointCount = instruction.ControlPointCount;
                    break;
                case D3D10Opcode.DclTessDomain:
                    _registerState.TessellatorDomain = instruction.TessellatorDomain;
                    break;
                case D3D10Opcode.DclTessPartitioning:
                    _registerState.TessellatorPartitioning = instruction.TessellatorPartitioning;
                    break;
                case D3D10Opcode.DclTessOutputPrimitive:
                    _registerState.TessellatorOutputPrimitive =
                        instruction.TessellatorOutputPrimitive;
                    break;
                case D3D10Opcode.DclHSMaxTessFactor:
                    _registerState.MaxTessFactor = BitConverter.Int32BitsToSingle(instruction.GetParamInt(0));
                    break;
                // Phases were already split by HullShaderPhases.
                case D3D10Opcode.HsDecls:
                case D3D10Opcode.HsControlPointPhase:
                case D3D10Opcode.HsForkPhase:
                case D3D10Opcode.HsJoinPhase:
                    break;
                case D3D10Opcode.DclIndexRange:
                    _registerState.DeclareIndexRange(instruction);
                    break;
                // Already applied: the phase is unrolled per instance.
                case D3D10Opcode.DclHSForkPhaseInstanceCount:
                case D3D10Opcode.DclHSJoinPhaseInstanceCount:
                    break;
                // Already read by LinkageModel.
                case D3D10Opcode.DclFunctionBody:
                case D3D10Opcode.DclFunctionTable:
                case D3D10Opcode.DclInterface:
                    break;
                case D3D10Opcode.InterfaceCall:
                    InsertInterfaceCall(instruction);
                    break;
                default:
                    throw new NotImplementedException(instruction.Opcode.ToString());
            }
        }
    }

    private void ParseConstantTableComment(D3D9Instruction instruction)
    {
        using var reader = new ConstantTableCommentReader(instruction);
        ConstantTable constantTable = reader.ReadTable();
        foreach (D3D9ConstantDeclaration constant in constantTable.Declarations)
        {
            DeclareConstant(constant);
        }
    }

    /// <summary>
    /// Register base for preshader-only uniforms, past any real float constant.
    /// </summary>
    private const int PreshaderInputBase = 0x2000;

    private readonly Dictionary<RegisterComponentKey, HlslTreeNode> _preshaderOutputs = [];

    /// <summary>
    /// Replaces each constant an fx_2_0 preshader writes with its expression.
    /// Preshader inputs the shader also reads share its declaration.
    /// </summary>
    private void ParsePreshaderComment(D3D9Instruction instruction)
    {
        Preshader preshader = Preshader.Read(instruction);

        var inputRegisters = new Dictionary<int, int>();
        foreach (D3D9ConstantDeclaration input in preshader.Inputs.Declarations)
        {
            if (input.RegisterSet != RegisterSet.Float4)
            {
                throw new NotSupportedException($"A preshader input in the {input.RegisterSet} registers.");
            }

            var shaderConstant = _registerState.ConstantDeclarations
                .OfType<D3D9ConstantDeclaration>()
                .FirstOrDefault(c => c.Name == input.Name && c.RegisterSet == RegisterSet.Float4);
            int register;
            if (shaderConstant != null)
            {
                register = shaderConstant.RegisterIndex;
            }
            else
            {
                register = PreshaderInputBase + input.RegisterIndex;
                DeclareConstant(new D3D9ConstantDeclaration(
                    input.Name, input.RegisterSet, (short)register, input.RegisterCount, input.TypeInfo)
                {
                    IsPreshaderInput = true,
                });
            }
            for (int r = 0; r < input.RegisterCount; r++)
            {
                inputRegisters[input.RegisterIndex + r] = register + r;
            }
        }

        var outputs = PreshaderTree.Build(preshader, offset => GetActiveOutput(new RegisterComponentKey(
            new D3D9RegisterKey(RegisterType.Const, inputRegisters[offset / 4]), offset % 4)));
        foreach (((PreshaderRegisterTable table, int offset), HlslTreeNode value) in outputs)
        {
            RegisterType registerType = table switch
            {
                PreshaderRegisterTable.Output => RegisterType.Const,
                PreshaderRegisterTable.OutputBool => RegisterType.ConstBool,
                PreshaderRegisterTable.OutputInt => RegisterType.ConstInt,
                _ => throw new InvalidOperationException(table.ToString()),
            };
            var key = new RegisterComponentKey(new D3D9RegisterKey(registerType, offset / 4), offset % 4);
            SetActiveOutput(key, value);
            _preshaderOutputs[key] = value;
        }
    }

    /// <summary>
    /// Declares a sampler the constant table did not (e.g. no table), named for its
    /// register and typed by its dcl.
    /// </summary>
    private void DeclareUnnamedSampler(D3D9Instruction instruction)
    {
        int number = instruction.GetParamRegisterNumber(1);
        if (_registerState.Samplers.ContainsKey(new D3D9RegisterKey(RegisterType.Sampler, number)))
        {
            return;
        }
        ParameterType type = instruction.GetDeclSamplerTextureType() switch
        {
            SamplerTextureType.Cube => ParameterType.SamplerCube,
            SamplerTextureType.Volume => ParameterType.Sampler3D,
            _ => ParameterType.Sampler2D,
        };
        DeclareConstant(new D3D9ConstantDeclaration($"s{number}", RegisterSet.Sampler,
            (short)number, 1, new ShaderTypeInfo(ParameterClass.Object, type, 1, 1, 1, null)));
    }

    private void DeclareConstant(D3D9ConstantDeclaration constant)
    {
        _registerState.DeclareConstant(constant);

        var registerType = constant.RegisterSet switch
        {
            RegisterSet.Bool => RegisterType.ConstBool,
            RegisterSet.Float4 => RegisterType.Const,
            RegisterSet.Int4 => RegisterType.Input,
            RegisterSet.Sampler => RegisterType.Sampler,
            _ => throw new InvalidOperationException(),
        };
        for (int r = 0; r < constant.RegisterCount; r++)
        {
            var registerKey = new D3D9RegisterKey(registerType, constant.RegisterIndex + r);
            for (int i = 0; i < 4; i++)
            {
                var destinationKey = new RegisterComponentKey(registerKey, i);
                var shaderInput = new RegisterInputNode(destinationKey);
                SetActiveOutput(destinationKey, shaderInput);
            }
        }
    }

    private void ParseControlInstruction(D3D9Instruction instruction)
    {
        if (instruction.Opcode == Opcode.Loop)
        {
            // loop aL, iN - the counter register is operand 0, the trip count is operand 1.
            D3D9RegisterKey registerKey = new D3D9RegisterKey(RegisterType.Loop, 0);
            _registerState.DeclareRegister(registerKey, 1);
            InsertLoop(instruction, 1, hasLoopCounter: true);
        }
        else if (instruction.Opcode == Opcode.Rep)
        {
            // rep iN - the trip count is operand 0.
            InsertLoop(instruction, 0);
        }
        else if (instruction.Opcode == Opcode.EndRep || instruction.Opcode == Opcode.EndLoop)
        {
            EndLoop();
        }
        else if (instruction.Opcode == Opcode.BreakC)
        {
            InsertBreak(instruction);
        }
        else if (instruction.Opcode == Opcode.Break)
        {
            InsertStatement(new BreakStatement(null, ActiveOutputs));
        }
        else if (instruction.Opcode == Opcode.If)
        {
            InsertIfStatement(instruction);
        }
        else if (instruction.Opcode == Opcode.IfC)
        {
            InsertIfCStatement(instruction);
        }
        else if (instruction.Opcode == Opcode.Else)
        {
            SwitchToElseBranch();
        }
        else if (instruction.Opcode == Opcode.Endif)
        {
            EndIf();
        }
        else if (instruction.Opcode == Opcode.End)
        {
        }
        else
        {
            throw new NotImplementedException($"{instruction.Opcode}");
        }
    }

    private void ParseAssignmentInstruction(D3D9Instruction instruction)
    {
        _registerState.DeclareDestinationRegister(instruction);

        var newOutputs = new Dictionary<RegisterComponentKey, HlslTreeNode>();

        RegisterComponentKey[] destinationKeys = GetDestinationKeys(instruction).ToArray();
        foreach (RegisterComponentKey destinationKey in destinationKeys)
        {
            HlslTreeNode instructionTree = CreateInstructionTree(instruction, destinationKey);
            if (instructionTree is RegisterInputNode registerInput && registerInput.RegisterComponentKey.RegisterKey.IsOutput)
            {
                continue;
            }
            instructionTree = ApplyModifier(instructionTree, instruction.GetDestinationResultModifier());
            newOutputs[destinationKey] = instructionTree;
        }

        foreach (var output in newOutputs)
        {
            SetActiveOutput(output.Key, output.Value);
        }
    }

    /// <summary>Unwinds to the innermost enclosing block of the given kind.</summary>
    /// <param name="isOpen">Skips blocks already closed.</param>
    private T UnwindTo<T>(Func<T, bool> isOpen = null) where T : class, IStatement
    {
        while (true)
        {
            if (_currentStatements.Peek() is T block && (isOpen == null || isOpen(block)))
            {
                return block;
            }
            _currentStatements.Pop();
        }
    }

    /// <summary>The lowest component in the destination mask (not always x).</summary>
    private static int FirstWrittenComponent(D3D10Instruction instruction)
    {
        int writeMask = instruction.GetWriteMask(0);
        for (int component = 0; component < 4; component++)
        {
            if ((writeMask & (1 << component)) != 0)
            {
                return component;
            }
        }
        return 0;
    }

    private bool IsConsumeBuffer(RegisterKey registerKey)
    {
        return _registerState.ResourceDefinitions.Any(d => d.BindPoint == registerKey.Number
            && d.ShaderInputType == D3DShaderInputType.UavConsumeStructured);
    }

    private bool IsAppendBuffer(RegisterKey registerKey)
    {
        return _registerState.ResourceDefinitions.Any(d => d.BindPoint == registerKey.Number
            && d.ShaderInputType == D3DShaderInputType.UavAppendStructured);
    }

    private D3D10Instruction NextInstruction()
    {
        return _instructionPointer + 1 < _shaderModel.Instructions.Count
            ? _shaderModel.Instructions[_instructionPointer + 1] as D3D10Instruction
            : null;
    }

    private static bool ReadsRegister(D3D10Instruction instruction, int operandIndex, RegisterKey registerKey)
    {
        return instruction.GetOperandType(operandIndex) != OperandType.Immediate32
            && instruction.GetParamRegisterKey(operandIndex).Equals(registerKey);
    }

    private void InsertStatement(IStatement statement)
    {
        // Closed blocks and non-blocks take no more statements.
        if (_currentStatements.Count != 0 && IsClosed(_currentStatements.Peek()))
        {
            _currentStatements.Pop();
        }
        // A memory write is ordered against the loads by instruction.
        switch (statement)
        {
            case StoreStructuredStatement store:
                store.SourceInstruction = _instructionPointer + 1;
                break;
            case StoreTypedStatement store:
                store.SourceInstruction = _instructionPointer + 1;
                break;
            case AtomicStatement atomic:
                atomic.SourceInstruction = _instructionPointer + 1;
                break;
        }
        ActiveStatementSequence.Add(statement);
        _currentStatements.Push(statement);
    }

    private static bool IsClosed(IStatement statement)
    {
        return statement switch
        {
            IfStatement ifStatement => ifStatement.IsParsed,
            LoopStatement loopStatement => loopStatement.IsParsed,
            SwitchStatement switchStatement => switchStatement.IsParsed,
            _ => true,
        };
    }

    private void InsertAssignment()
    {
        if (ActiveStatement == null)
        {
            InsertStatement(new AssignmentStatement(new Dictionary<RegisterComponentKey, HlslTreeNode>()));
        }
        else if (ActiveStatement is not AssignmentStatement)
        {
            InsertStatement(new AssignmentStatement(ActiveOutputs));
        }
    }

    private void InsertClip(Instruction instruction)
    {
        HlslTreeNode[] values;
        if (instruction is D3D10Instruction d3d10Instruction)
        {
            InsertDiscard(d3d10Instruction);
            return;
        }
        else
        {
            values = GetDestinationKeys(instruction)
                .Select(GetActiveOutput)
                .ToArray();
        }
        var clip = new ClipStatement(values, ActiveOutputs);
        InsertStatement(clip);
    }

    // clip(x) when the condition is x < 0, else a guarded discard.
    private void InsertDiscard(D3D10Instruction instruction)
    {
        HlslTreeNode condition = GetConditionNode(instruction);
        if (condition is ComparisonNode comparison
            && comparison.Comparison == IfComparison.LT
            && comparison.Right is ConstantNode zero
            && zero.Value == 0)
        {
            InsertStatement(new ClipStatement([comparison.Left], ActiveOutputs));
            return;
        }

        InsertStatement(new DiscardStatement(condition, ActiveOutputs));
    }

    private void InsertAppend(int? stream = null)
    {
        InsertStatement(new AppendStatement(ActiveOutputs) { Stream = stream });
    }

    /// <summary>
    /// A call through an interface. Arguments are the caller's parameter registers
    /// (whole registers); the result is the body's return register.
    /// </summary>
    private void InsertInterfaceCall(D3D10Instruction instruction)
    {
        // Function index dword, then the fp operand: interface, instance (fp1[2][0]).
        int function = (int)instruction.OperandTokens.Tokens[0];
        int interfaceNumber = (int)instruction.OperandTokens.Tokens[2];
        (int instance, var relative) = LinkageModel.CallInstance(instruction);
        // An instance picked at run time: its index register's value at the call.
        HlslTreeNode index = relative is var (indexType, indexNumber, indexComponent)
            ? GetActiveOutput(new RegisterComponentKey(new D3D10RegisterKey(indexType, indexNumber), indexComponent))
            : null;
        LinkageModel.MethodInfo method =
            _registerState.Linkage.MethodForCall(interfaceNumber, function);

        // As wide as declared: a float3 input has no .w.
        var arguments = new List<HlslTreeNode>();
        int[] argumentWidths = [.. method.Parameters.Select(parameter =>
            _registerState.MethodInputRegisters.FirstOrDefault(d => d.RegisterKey.Equals(parameter))
                is RegisterDeclaration declaration
                ? 32 - System.Numerics.BitOperations.LeadingZeroCount((uint)declaration.WriteMask)
                : _registerState.LinkageTempParameters.GetValueOrDefault(parameter, 4))];
        for (int parameter = 0; parameter < method.Parameters.Count; parameter++)
        {
            for (int component = 0; component < argumentWidths[parameter]; component++)
            {
                var argumentKey = new RegisterComponentKey(method.Parameters[parameter], component);
                // An input passed whole may have lanes main never declared (TEXCOORD
                // a float4 of which main reads .xyz); an input is always its register.
                // A temp lane main never wrote is one the body only writes into.
                arguments.Add(ActiveOutputs.TryGetValue(argumentKey, out HlslTreeNode value)
                    ? value
                    : method.Parameters[parameter] is D3D10RegisterKey { OperandType: OperandType.Input }
                        ? new RegisterInputNode(argumentKey)
                        : new ConstantNode(0));
            }
        }

        for (int component = 0; component < method.ReturnComponents; component++)
        {
            var callNode = new InterfaceCallNode(
                interfaceNumber, instance, function, [.. arguments], argumentWidths, component, index);
            callNode.SourceInstruction = _instructionPointer + 1;
            callNode.SourceComponent = component;
            SetActiveOutput(
                new RegisterComponentKey(method.ReturnRegister, component), callNode);
        }
    }

    private void InsertRestartStrip(int? stream = null)
    {
        InsertStatement(new RestartStripStatement(ActiveOutputs) { Stream = stream });
    }

    /// <summary>
    /// Binds each register the loop body writes to a header phi; <see cref="EndLoop"/>
    /// adds the backedge.
    /// </summary>
    private void SeedLoopHeaderPhis(LoopStatement loop)
    {
        foreach (RegisterComponentKey key in ScanLoopBodyDestinations())
        {
            if (loop.Outputs.TryGetValue(key, out HlslTreeNode preLoopValue) && preLoopValue is not PhiNode)
            {
                loop.Outputs[key] = new PhiNode(preLoopValue);
            }
        }
    }

    /// <summary>
    /// Registers written in the loop at the current pointer. Must not touch parser state.
    /// </summary>
    private IEnumerable<RegisterComponentKey> ScanLoopBodyDestinations()
    {
        var destinations = new List<RegisterComponentKey>();
        int depth = 0;

        for (int i = _instructionPointer + 1; i < _shaderModel.Instructions.Count; i++)
        {
            Instruction instruction = _shaderModel.Instructions[i];

            if (IsLoopStart(instruction))
            {
                depth++;
                continue;
            }
            if (IsLoopEnd(instruction))
            {
                if (depth == 0)
                {
                    break;
                }
                depth--;
                continue;
            }
            if (!instruction.HasDestination || IsStoreStructured(instruction))
            {
                continue;
            }

            foreach (RegisterComponentKey key in GetDestinationKeys(instruction))
            {
                if (key.RegisterKey.IsTempRegister || key.RegisterKey.IsOutput)
                {
                    destinations.Add(key);
                }
            }
        }

        return destinations.Distinct();
    }

    private static bool IsLoopStart(Instruction instruction)
    {
        return instruction switch
        {
            D3D9Instruction d3d9 => d3d9.Opcode == Opcode.Rep || d3d9.Opcode == Opcode.Loop,
            D3D10Instruction d3d10 => d3d10.Opcode == D3D10Opcode.Loop,
            _ => false,
        };
    }

    private static bool IsLoopEnd(Instruction instruction)
    {
        return instruction switch
        {
            D3D9Instruction d3d9 => d3d9.Opcode == Opcode.EndRep || d3d9.Opcode == Opcode.EndLoop,
            D3D10Instruction d3d10 => d3d10.Opcode == D3D10Opcode.EndLoop,
            _ => false,
        };
    }

    private static bool IsStoreStructured(Instruction instruction)
    {
        return instruction is D3D10Instruction d3d10
            && d3d10.Opcode is D3D10Opcode.StoreStructured or D3D10Opcode.StoreRaw;
    }

    private void InsertLoop(Instruction instruction, int countParamIndex, bool hasLoopCounter = false)
    {
        int loopRegisterNumber = instruction.GetParamRegisterNumber(countParamIndex);
        ConstantIntRegister countRegister = _registerState.FindConstantIntRegister(loopRegisterNumber);
        // From a defi, else the x of a uniform iN.
        uint? repeatCount = countRegister?[0];
        var loop = new LoopStatement(repeatCount, ActiveOutputs) { HasLoopCounter = hasLoopCounter };
        if (repeatCount == null)
        {
            loop.RepeatCountNode = new RegisterInputNode(
                new RegisterComponentKey(RegisterType.ConstInt, loopRegisterNumber, 0));
        }
        SeedLoopHeaderPhis(loop);

        InsertStatement(loop);
    }

    /// <summary>
    /// The condition operand of a DXBC branch. Comparison instructions already
    /// produce a condition; anything else is a register tested against zero.
    /// </summary>
    private HlslTreeNode GetConditionNode(D3D10Instruction instruction)
    {
        byte component = instruction.GetSourceSwizzleComponents(0)[0];
        RegisterKey registerKey = instruction.GetParamRegisterKey(0);
        HlslTreeNode condition = GetActiveOutput(new RegisterComponentKey(registerKey, component));

        // `if (a < b && c < d)`, not `if ((a < b && c < d) != 0)`.
        if (instruction.TestNonZero && condition is LogicalAndOperation or LogicalOrOperation)
        {
            return condition;
        }

        ComparisonNode comparison;
        if (condition is ComparisonNode conditionComparison)
        {
            comparison = conditionComparison;
        }
        else if (condition is GreaterEqualOperation greaterEqual)
        {
            comparison = new ComparisonNode(greaterEqual.Inputs[0], greaterEqual.Inputs[1], IfComparison.GE);
        }
        else
        {
            comparison = new ComparisonNode(condition, new ConstantNode(0), IfComparison.NE)
            {
                IsBitsTest = true,
            };
        }
        // _z forms branch when the comparison does not hold.
        return instruction.TestNonZero ? comparison : comparison.Inverted();
    }

    // imul/umul hi, lo, a, b: fxc leaves hi null for a plain 32-bit multiply, the
    // only form HLSL can express.
    private void ParseIntegerMultiplyInstruction(D3D10Instruction instruction)
    {
        const int HighDestinationIndex = 0;
        const int LowDestinationIndex = 1;
        const int Factor1Index = 2;
        const int Factor2Index = 3;

        if (instruction.GetOperandType(HighDestinationIndex) != OperandType.Null)
        {
            throw new NotImplementedException("imul/umul writing the high half of the product");
        }
        if (instruction.GetOperandType(LowDestinationIndex) == OperandType.Null)
        {
            return;
        }

        var destinationKey = instruction.GetParamRegisterKey(LowDestinationIndex);
        int writeMask = instruction.GetWriteMask(LowDestinationIndex);
        _registerState.DeclareRegisterWrite(destinationKey, writeMask);

        var newOutputs = new Dictionary<RegisterComponentKey, HlslTreeNode>();
        for (int component = 0; component < 4; component++)
        {
            if ((writeMask & (1 << component)) == 0)
            {
                continue;
            }
            newOutputs[new RegisterComponentKey(destinationKey, component)] =
                new MultiplyOperation(
                    GetInputComponent(instruction, Factor1Index, component),
                    GetInputComponent(instruction, Factor2Index, component))
                {
                    ConsumesInteger = true,
                };
        }

        foreach (var output in newOutputs)
        {
            SetActiveOutput(output.Key, output.Value);
        }
    }

    // udiv quotient, remainder, a, b; either destination may be null.
    private void ParseIntegerDivideInstruction(D3D10Instruction instruction)
    {
        const int DividendIndex = 2;
        const int DivisorIndex = 3;
        var newOutputs = new Dictionary<RegisterComponentKey, HlslTreeNode>();

        for (int destinationIndex = 0; destinationIndex <= 1; destinationIndex++)
        {
            if (instruction.GetOperandType(destinationIndex) == OperandType.Null)
            {
                continue;
            }

            var destinationKey = instruction.GetParamRegisterKey(destinationIndex);
            int writeMask = instruction.GetWriteMask(destinationIndex);
            _registerState.DeclareRegisterWrite(destinationKey, writeMask);

            for (int component = 0; component < 4; component++)
            {
                if ((writeMask & (1 << component)) == 0)
                {
                    continue;
                }

                HlslTreeNode dividend = GetInputComponent(instruction, DividendIndex, component);
                HlslTreeNode divisor = GetInputComponent(instruction, DivisorIndex, component);
                newOutputs[new RegisterComponentKey(destinationKey, component)] = destinationIndex == 0
                    ? new DivisionOperation(dividend, divisor) { ConsumesInteger = true }
                    : new ModuloOperation(dividend, divisor) { ConsumesInteger = true };
            }
        }

        foreach (var output in newOutputs)
        {
            SetActiveOutput(output.Key, output.Value);
        }
    }

    // One component of an integer operand, with its modifier (`-r1.z`).
    private HlslTreeNode GetInputComponent(D3D10Instruction instruction, int operandIndex, int component)
    {
        if (instruction.GetOperandType(operandIndex) == OperandType.Immediate32)
        {
            return new ConstantNode((int)instruction.GetParamInt(operandIndex, component));
        }
        RegisterKey registerKey = instruction.GetParamRegisterKey(operandIndex);
        byte[] swizzle = instruction.GetSourceSwizzleComponents(operandIndex);
        HlslTreeNode input = GetActiveOutput(new RegisterComponentKey(registerKey, swizzle[component]));
        return ApplyModifier(input, instruction.GetOperandModifier(operandIndex));
    }

    // sincos sin, cos, src; either destination may be null.
    private void ParseSinCosInstruction(D3D10Instruction instruction)
    {
        const int sourceIndex = 2;
        RegisterKey sourceKey = instruction.GetParamRegisterKey(sourceIndex);
        byte[] sourceSwizzle = instruction.GetSourceSwizzleComponents(sourceIndex);

        var newOutputs = new Dictionary<RegisterComponentKey, HlslTreeNode>();

        for (int destinationIndex = 0; destinationIndex <= 1; destinationIndex++)
        {
            if (instruction.GetOperandType(destinationIndex) == OperandType.Null)
            {
                continue;
            }

            var destinationKey = instruction.GetParamRegisterKey(destinationIndex);
            int writeMask = instruction.GetWriteMask(destinationIndex);
            _registerState.DeclareRegisterWrite(destinationKey, writeMask);

            for (int component = 0; component < 4; component++)
            {
                if ((writeMask & (1 << component)) == 0)
                {
                    continue;
                }

                HlslTreeNode source = GetActiveOutput(
                    new RegisterComponentKey(sourceKey, sourceSwizzle[component]));
                newOutputs[new RegisterComponentKey(destinationKey, component)] = destinationIndex == 0
                    ? new SineOperation(source)
                    : new CosineOperation(source);
            }
        }

        foreach (var output in newOutputs)
        {
            SetActiveOutput(output.Key, output.Value);
        }
    }

    /// <summary>
    /// Only a <c>ret</c> inside a block is a statement; the final one is implicit
    /// (<see cref="StatementFinalizer"/> makes the return).
    /// </summary>
    private void InsertReturn()
    {
        // A closed block still on the stack does not count (ret after endloop).
        bool insideOpenBlock = _currentStatements.Any(s =>
            (s is IfStatement || s is LoopStatement || s is SwitchStatement) && !IsClosed(s));
        if (!insideOpenBlock)
        {
            return;
        }

        // An assignment right before the ret becomes the return.
        if (ActiveStatement is AssignmentStatement assignment)
        {
            _currentStatements.Pop();
            IList<IStatement> sequence = ActiveStatementSequence;
            if (sequence.Count != 0 && ReferenceEquals(sequence[sequence.Count - 1], assignment))
            {
                // Keep the assignment's inputs: identical inputs and outputs would
                // tell the writer the return assigns nothing.
                var replacement = new ReturnStatement(assignment.Inputs, assignment.Outputs);
                sequence[sequence.Count - 1] = replacement;
                _currentStatements.Push(replacement);
                return;
            }
            _currentStatements.Push(assignment);
        }

        InsertStatement(new ReturnStatement(ActiveOutputs));
    }

    /// <summary>
    /// What `and cond, value` selects. A constant is the bit pattern of the value
    /// (step masks with 0x3f800000, i.e. 1.0f).
    /// </summary>
    private HlslTreeNode MaskedValue(HlslTreeNode condition, HlslTreeNode value, D3D10Instruction instruction)
    {
        return value is ConstantNode mask ? AsMaskedValue(condition, mask, instruction) : value;
    }

    private ConstantNode AsMaskedValue(
        HlslTreeNode condition, ConstantNode mask, D3D10Instruction instruction)
    {
        bool isInteger = condition is ComparisonNode comparison && comparison.IsInteger;
        if (mask.IntegerValue == null)
        {
            return mask;
        }
        if (isInteger)
        {
            // An integer, unless written to a float output (isinf() & 1.0f bits).
            return WritesFloatOutput(instruction) ? AsFloatBits(mask) : mask;
        }
        // Against a float comparison it may be float bits (step) or an integer
        // (`c ? 1u : 0u`). Normal-float bit patterns read as floats; anything
        // smaller would be a denormal, so it is the integer.
        const int SmallestNormalFloatBits = 0x00800000;
        int bits = mask.IntegerValue.Value;
        return (bits & 0x7FFFFFFF) >= SmallestNormalFloatBits
            ? AsFloatBits(mask)
            : mask;
    }

    private static ConstantNode AsFloatBits(ConstantNode mask)
    {
        return new ConstantNode(BitConverter.Int32BitsToSingle(mask.IntegerValue.Value));
    }

    private bool WritesFloatOutput(D3D10Instruction instruction)
    {
        int? destination = instruction.GetDestinationParamIndex();
        if (destination == null
            || instruction.GetOperandType(destination.Value) != OperandType.Output)
        {
            return false;
        }
        RegisterKey outputRegister = instruction.GetParamRegisterKey(destination.Value);
        int writeMask = instruction.GetWriteMask(destination.Value);
        for (int component = 0; component < 4; component++)
        {
            if ((writeMask & (1 << component)) != 0
                && !_integerOperandAnalysis.IsIntegerOutputSignature(
                    new RegisterComponentKey(outputRegister, component)))
            {
                return true;
            }
        }
        return false;
    }

    // The negated condition, or null if it cannot be inverted.
    private static HlslTreeNode InvertCondition(HlslTreeNode node)
    {
        switch (node)
        {
            case ComparisonNode comparison:
                return comparison.Inverted();
            case GreaterEqualOperation ge:
                return new ComparisonNode(ge.Source0, ge.Source1, IfComparison.LT);
            case LogicalAndOperation and:
                {
                    HlslTreeNode left = InvertCondition(and.Inputs[0]);
                    HlslTreeNode right = left == null ? null : InvertCondition(and.Inputs[1]);
                    return right == null ? null : new LogicalOrOperation(left, right);
                }
            case LogicalOrOperation or:
                {
                    HlslTreeNode left = InvertCondition(or.Inputs[0]);
                    HlslTreeNode right = left == null ? null : InvertCondition(or.Inputs[1]);
                    return right == null ? null : new LogicalAndOperation(left, right);
                }
            default:
                return null;
        }
    }

    // GE is an operation rather than a ComparisonNode. Combined conditions count
    // too: any() over a bool4 is an or of ors.
    private static bool IsCondition(HlslTreeNode node)
    {
        return node is ComparisonNode || node is GreaterEqualOperation
            || node is LogicalAndOperation || node is LogicalOrOperation;
    }

    /// <summary>
    /// and/or/xor: two conditions are a logical operator; a condition anded with a
    /// value is `cond ? value : 0` (HLSL cannot and a float, X3082); else bitwise.
    /// </summary>
    private HlslTreeNode CreateLogicalOperation(
        D3D10Opcode opcode, HlslTreeNode[] inputs, D3D10Instruction instruction)
    {
        if (IsCondition(inputs[0]) && IsCondition(inputs[1]))
        {
            return opcode == D3D10Opcode.And
                ? new LogicalAndOperation(inputs[0], inputs[1])
                : new LogicalOrOperation(inputs[0], inputs[1]);
        }

        if (opcode == D3D10Opcode.And)
        {
            if (IsCondition(inputs[0]))
            {
                return new MoveConditionalOperation(
                    inputs[0], MaskedValue(inputs[0], inputs[1], instruction), new ConstantNode(0));
            }
            if (IsCondition(inputs[1]))
            {
                return new MoveConditionalOperation(
                    inputs[1], MaskedValue(inputs[1], inputs[0], instruction), new ConstantNode(0));
            }
        }

        return opcode switch
        {
            D3D10Opcode.And => new BitwiseAndOperation(inputs[0], inputs[1]),
            D3D10Opcode.Or => new BitwiseOrOperation(inputs[0], inputs[1]),
            _ => new BitwiseXorOperation(inputs[0], inputs[1]),
        };
    }

    private void InsertSwitchStatement(D3D10Instruction instruction)
    {
        byte component = instruction.GetSourceSwizzleComponents(0)[0];
        RegisterKey registerKey = instruction.GetParamRegisterKey(0);
        HlslTreeNode selector = GetActiveOutput(new RegisterComponentKey(registerKey, component));

        InsertStatement(new SwitchStatement(selector, ActiveOutputs));
    }

    /// <param name="label">The case value, or null for <c>default</c>.</param>
    private void AddSwitchCase(HlslTreeNode label)
    {
        UnwindTo<SwitchStatement>().Cases.Add(new SwitchCase(label));
    }

    private void EndSwitch()
    {
        SwitchStatement switchStatement = UnwindTo<SwitchStatement>(s => !s.IsParsed);
        switchStatement.IsParsed = true;

        // Join each register over the cases that assign it plus its entry value;
        // registers first written inside a case are carried out too. With a
        // default every path runs a case, which carries the entry value if it
        // leaves the register alone.
        bool hasDefault = switchStatement.Cases.Any(switchCase => switchCase.Label == null);
        var caseValues = new Dictionary<RegisterComponentKey, List<HlslTreeNode>>();
        foreach (SwitchCase switchCase in switchStatement.Cases)
        {
            if (switchCase.Body.Count == 0)
            {
                continue;
            }
            foreach (IStatement exit in CaseExitStatements(switchCase.Body))
            {
                foreach (var caseOutput in exit.Outputs)
                {
                    if (!caseValues.TryGetValue(caseOutput.Key, out var values))
                    {
                        values = [];
                        caseValues[caseOutput.Key] = values;
                    }
                    if (!values.Contains(caseOutput.Value))
                    {
                        values.Add(caseOutput.Value);
                    }
                }
            }
        }

        foreach (var caseValue in caseValues)
        {
            List<HlslTreeNode> joined = caseValue.Value;
            if (!hasDefault
                && switchStatement.Outputs.TryGetValue(caseValue.Key, out var parentNode)
                && !joined.Contains(parentNode))
            {
                joined = [.. joined, parentNode];
            }

            switchStatement.Outputs[caseValue.Key] = joined.Count == 1
                ? joined[0]
                : new PhiNode([.. joined]);
        }
    }

    /// <summary>
    /// A case's statements that carry values out of the switch: the last statement
    /// plus any break nested in the case's ifs, which leaves with its own values.
    /// A loop's or inner switch's breaks leave those instead, so the walk stops
    /// there.
    /// </summary>
    private static IEnumerable<IStatement> CaseExitStatements(IList<IStatement> body)
    {
        if (body.Count != 0)
        {
            yield return body.Last();
        }
        foreach (IStatement statement in body)
        {
            if (statement is IfStatement ifStatement)
            {
                foreach (IStatement exit in IfExitStatements(ifStatement.TrueBody))
                {
                    yield return exit;
                }
                if (ifStatement.FalseBody != null)
                {
                    foreach (IStatement exit in IfExitStatements(ifStatement.FalseBody))
                    {
                        yield return exit;
                    }
                }
            }
        }
    }

    private static IEnumerable<IStatement> IfExitStatements(IList<IStatement> body)
    {
        if (body.Count != 0 && body.Last() is BreakStatement)
        {
            yield return body.Last();
        }
        foreach (IStatement statement in body)
        {
            if (statement is IfStatement ifStatement)
            {
                foreach (IStatement exit in IfExitStatements(ifStatement.TrueBody))
                {
                    yield return exit;
                }
                if (ifStatement.FalseBody != null)
                {
                    foreach (IStatement exit in IfExitStatements(ifStatement.FalseBody))
                    {
                        yield return exit;
                    }
                }
            }
        }
    }

    private void InsertIfStatement(D3D10Instruction instruction)
    {
        InsertStatement(new IfStatement([GetConditionNode(instruction)], ActiveOutputs));
    }

    private void InsertBreak(D3D10Instruction instruction)
    {
        InsertStatement(new BreakStatement(GetConditionNode(instruction), ActiveOutputs));
    }

    private void InsertBreak(D3D9Instruction instruction)
    {
        HlslTreeNode comparison = new GroupNode(Enumerable.Range(0, 4)
            .Select(i => GetInputs(instruction, i))
            .Select(inputs => new ComparisonNode(inputs[0], inputs[1], instruction.Comparison))
            .ToArray());
        var breakStatement = new BreakStatement(comparison, ActiveOutputs);

        InsertStatement(breakStatement);
    }

    private void EndLoop()
    {
        LoopStatement loopStatement = UnwindTo<LoopStatement>();
        loopStatement.IsParsed = true;

        foreach (var output in loopStatement.Body.Last().Outputs)
        {
            RegisterComponentKey registerComponent = output.Key;
            HlslTreeNode node = output.Value;
            if (loopStatement.Outputs.TryGetValue(registerComponent, out var parentNode))
            {
                if (node == parentNode)
                {
                    continue;
                }
                // Only a one-input phi is this loop's header phi; a branch join
                // already has two.
                if (parentNode is PhiNode headerPhi
                    && !headerPhi.IsLoopHeader
                    && headerPhi.Inputs.Count == 1)
                {
                    headerPhi.SetBackedgeValue(node);
                }
                else
                {
                    loopStatement.Outputs[registerComponent] = new PhiNode(node, parentNode);
                }
            }
            else
            {
                // Variable is assigned only in loop body, not passing output forward
            }
        }
    }

    private void InsertIfStatement(D3D9Instruction instruction)
    {
        var ifStatement = new IfStatement(GetInputs(instruction, 0), ActiveOutputs);

        InsertStatement(ifStatement);
    }

    private void InsertIfCStatement(D3D9Instruction instruction)
    {
        HlslTreeNode[] comparison = Enumerable.Range(0, 4)
            .Select(i => GetInputs(instruction, i))
            .Select(inputs => new ComparisonNode(inputs[0], inputs[1], instruction.Comparison))
            .ToArray();
        var ifStatement = new IfStatement(comparison, ActiveOutputs);

        InsertStatement(ifStatement);
    }

    private void SwitchToElseBranch()
    {
        // The nearest open if; a closed nested if may still be on the stack.
        IfStatement ifStatement = UnwindTo<IfStatement>(i => !i.IsParsed);
        ifStatement.IsTrueParsed = true;
        ifStatement.FalseBody = [];
    }

    private void EndIf()
    {
        IfStatement ifStatement = UnwindTo<IfStatement>(i => !i.IsParsed);
        ifStatement.IsTrueParsed = true;
        ifStatement.IsParsed = true;

        // A branch may be empty (fxc's continue is an if with an empty true branch).
        var trueOutputs = BranchOutputs(ifStatement.TrueBody);
        var falseOutputs = BranchOutputs(ifStatement.FalseBody);

        foreach (var trueOutput in trueOutputs)
        {
            RegisterComponentKey registerComponent = trueOutput.Key;
            HlslTreeNode trueNode = trueOutput.Value;
            if (ifStatement.FalseBody != null && falseOutputs.TryGetValue(registerComponent, out var falseNode))
            {
                if (trueNode == falseNode)
                {
                    continue;
                }
                ifStatement.Outputs[registerComponent] = new PhiNode(trueNode, falseNode);
            }
            else if (ifStatement.Outputs.TryGetValue(registerComponent, out var parentNode))
            {
                if (trueNode == parentNode)
                {
                    continue;
                }
                ifStatement.Outputs[registerComponent] = new PhiNode(trueNode, parentNode);
            }
            else
            {
                // Variable is assigned only in true branch, not passing output forward
            }
        }

        if (ifStatement.FalseBody != null)
        {
            foreach (var falseOutput in falseOutputs)
            {
                RegisterComponentKey registerComponent = falseOutput.Key;
                HlslTreeNode falseNode = falseOutput.Value;
                if (trueOutputs.ContainsKey(registerComponent))
                {
                    // Phi node was already created
                }
                else if (ifStatement.Outputs.TryGetValue(registerComponent, out var parentNode))
                {
                    if (falseNode == parentNode)
                    {
                        continue;
                    }
                    ifStatement.Outputs[registerComponent] = new PhiNode(falseNode, parentNode);
                }
                else
                {
                    // Variable is assigned only in false branch, not passing output forward
                }
            }
        }
    }

    private static IDictionary<RegisterComponentKey, HlslTreeNode> BranchOutputs(
        IList<IStatement> body)
    {
        return body == null || body.Count == 0
            ? new Dictionary<RegisterComponentKey, HlslTreeNode>()
            : body.Last().Outputs;
    }

    private HlslTreeNode GetActiveOutput(RegisterComponentKey registerComponent)
    {
        if (registerComponent.RegisterKey is D3D10RegisterKey d3D10RegisterKey)
        {
            if (d3D10RegisterKey.ImmediateDouble != null)
            {
                return new DoubleConstantNode(
                    d3D10RegisterKey.ImmediateDouble[registerComponent.ComponentIndex / 2]);
            }
            if (d3D10RegisterKey.OperandType == OperandType.Immediate32)
            {
                if (d3D10RegisterKey.ImmediateSingle != null)
                {
                    if (d3D10RegisterKey.ImmediateSingle.Length == 1)
                    {
                        return new ConstantNode(d3D10RegisterKey.ImmediateSingle[0]);
                    }
                    return new ConstantNode(d3D10RegisterKey.ImmediateSingle[registerComponent.ComponentIndex]);
                }
                return new ConstantNode(d3D10RegisterKey.ImmediateInt.Value);
            }
        }
        if (_doubleHighWords.Contains(registerComponent)
            && ActiveOutputs.TryGetValue(new RegisterComponentKey(
                registerComponent.RegisterKey, registerComponent.ComponentIndex - 1),
                out HlslTreeNode pairedDouble))
        {
            // A read of a double's upper component is its high word; the old
            // value there is stale.
            return new DoubleBitsNode(pairedDouble, 1);
        }
        return ActiveOutputs[registerComponent];
    }

    private void SetActiveOutput(RegisterComponentKey registerComponent, HlslTreeNode value)
    {
        InsertAssignment();
        ActiveOutputs[registerComponent] = value;
        // A write of either half ends the double in that pair.
        _doubleHighWords.Remove(registerComponent);
        if (registerComponent.ComponentIndex % 2 == 0)
        {
            _doubleHighWords.Remove(new RegisterComponentKey(
                registerComponent.RegisterKey, registerComponent.ComponentIndex + 1));
        }
    }

    private void ParseAssignmentInstruction(D3D10Instruction instruction)
    {
        _registerState.DeclareDestinationRegister(instruction);

        var newOutputs = new Dictionary<RegisterComponentKey, HlslTreeNode>();
        var doubleDestinations = new List<RegisterComponentKey>();

        RegisterComponentKey[] destinationKeys = GetDestinationKeys(instruction).ToArray();
        foreach (RegisterComponentKey destinationKey in destinationKeys)
        {
            HlslTreeNode instructionTree = CreateInstructionTree(instruction, destinationKey);
            if (instructionTree is RegisterInputNode registerInput && registerInput.RegisterComponentKey.RegisterKey.IsOutput)
            {
                continue;
            }
            if (instruction.Saturate)
            {
                instructionTree = new SaturateOperation(instructionTree);
            }
            if (IsDoubleResult(instruction, destinationKey))
            {
                _doubleValues.Add(instructionTree);
                doubleDestinations.Add(destinationKey);
            }
            newOutputs[destinationKey] = instructionTree;
        }
        // A double moved into an output is its two words, like a store's: nothing
        // reads an output back, so the high word is keyed too.
        foreach (RegisterComponentKey destinationKey in doubleDestinations.ToList())
        {
            if (destinationKey.RegisterKey.IsOutput)
            {
                HlslTreeNode value = newOutputs[destinationKey];
                newOutputs[destinationKey] = new DoubleBitsNode(value, 0);
                newOutputs[new RegisterComponentKey(destinationKey.RegisterKey, destinationKey.ComponentIndex + 1)]
                    = new DoubleBitsNode(value, 1);
                doubleDestinations.Remove(destinationKey);
            }
        }

        foreach (var output in newOutputs)
        {
            SetActiveOutput(output.Key, output.Value);
        }
        // Record the high words instead of keying a value there: a keyed value
        // would be a second reader of the double and force it into a variable.
        // GetActiveOutput makes the word only where it is read.
        foreach (RegisterComponentKey destinationKey in doubleDestinations)
        {
            _doubleHighWords.Add(new RegisterComponentKey(
                destinationKey.RegisterKey, destinationKey.ComponentIndex + 1));
        }
    }

    private IEnumerable<RegisterComponentKey> GetDestinationKeys(Instruction instruction)
    {
        int index = instruction.GetDestinationParamIndex().Value;
        int mask = instruction.GetDestinationWriteMask();
        // A double is keyed at the lower component of its aligned pair (.xy, .zw).
        if (instruction is D3D10Instruction d3d10)
        {
            mask = d3d10.WritesDoubles
                ? mask & 0b0101
                : GetMovedDoubleMask(d3d10, GetStructuredValueMask(d3d10, mask));
            // `mov o[r0.x + 0].x` in an unrolled fork phase: the index is a known
            // constant, so resolve the real output register.
            if (ResolveRelativeOutputKey(d3d10, index) is RegisterKey resolved)
            {
                return MaskedComponentKeys(resolved, mask);
            }
        }
        return GetParameterRegisterKeys(instruction, index, mask);
    }

    private static IEnumerable<RegisterComponentKey> MaskedComponentKeys(RegisterKey key, int mask)
    {
        for (int component = 0; component < 4; component++)
        {
            if ((mask & (1 << component)) != 0)
            {
                yield return new RegisterComponentKey(key, component);
            }
        }
    }

    /// <summary>
    /// The output a relative write reaches, or null unless the index is a known constant.
    /// </summary>
    private RegisterKey ResolveRelativeOutputKey(D3D10Instruction instruction, int operandIndex)
    {
        if (instruction.GetOperandType(operandIndex) != OperandType.Output)
        {
            return null;
        }
        D3D10OperandTokenCollection.OperandIndex[] indices =
            instruction.OperandTokens.GetOperandIndices(operandIndex);
        if (indices.Length == 0 || !indices[0].IsRelative)
        {
            return null;
        }
        (OperandType indexType, int indexNumber, byte indexComponent) =
            instruction.OperandTokens.GetRelativeIndexOperand(operandIndex, 0);
        var indexKey = new RegisterComponentKey(
            new D3D10RegisterKey(indexType, indexNumber), indexComponent);
        if (ActiveOutputs == null
            || !ActiveOutputs.TryGetValue(indexKey, out HlslTreeNode value)
            || IndexValue(value) is not int offset)
        {
            return null;
        }
        return new D3D10RegisterKey(OperandType.Output, (int)indices[0].Immediate + offset);
    }

    // The value of an operand's first relative index register where it is a constant.
    private int? RelativeIndexConstant(D3D10Instruction instruction, int operandIndex)
    {
        (OperandType indexType, int indexNumber, byte indexComponent) =
            instruction.OperandTokens.GetRelativeIndexOperand(operandIndex, 0);
        var indexKey = new RegisterComponentKey(new D3D10RegisterKey(indexType, indexNumber), indexComponent);
        return ActiveOutputs != null
            && ActiveOutputs.TryGetValue(indexKey, out HlslTreeNode value)
            ? IndexValue(value)
            : null;
    }

    // The instance id is a constant in each unrolled copy; fxc may index with
    // arithmetic on it (`iadd r0.x, -vJoinInstanceID.x, l(1)`) and pick between
    // registers with a select (`movc r0.x, r0.x, vForkInstanceID.x, l(4)`).
    private int? IndexValue(HlslTreeNode value)
    {
        if (IndexConstant(value) is ConstantNode constant)
        {
            if (constant.IntegerValue is int integerValue)
            {
                return integerValue;
            }
            // A select's immediates are still float-typed here; their raw bits are
            // the integer an index wants (l(4) is 4, not the denormal 0).
            foreach ((ConstantNode polymorphic, uint bits) in _polymorphicImmediates)
            {
                if (bits != 0 && ReferenceEquals(polymorphic, constant))
                {
                    return unchecked((int)bits);
                }
            }
            return (int)constant.Value;
        }
        return value switch
        {
            NegateOperation negate => -IndexValue(negate.Inputs[0]),
            AddOperation add => IndexValue(add.Inputs[0]) + IndexValue(add.Inputs[1]),
            SubtractOperation subtract => IndexValue(subtract.Inputs[0]) - IndexValue(subtract.Inputs[1]),
            MultiplyOperation multiply => IndexValue(multiply.Inputs[0]) * IndexValue(multiply.Inputs[1]),
            DivisionOperation divide => IndexValue(divide.Dividend) / IndexValue(divide.Divisor),
            // A udiv writing only its second destination is a remainder (`mod`).
            ModuloOperation modulo => IndexValue(modulo.Dividend) % IndexValue(modulo.Divisor),
            MoveOperation move => IndexValue(move.Inputs[0]),
            MoveConditionalOperation select => IndexValue(select.Condition) is int test
                ? test != 0 ? IndexValue(select.Source1) : IndexValue(select.Source2)
                : null,
            ComparisonNode comparison => IndexComparisonValue(comparison),
            _ => null,
        };
    }

    // A comparison of known values, at -1 or 0 as DXBC writes it; movc only
    // tests it against zero.
    private int? IndexComparisonValue(ComparisonNode comparison)
    {
        int? left = IndexValue(comparison.Left);
        int? right = IndexValue(comparison.Right);
        if (left is not int l || right is not int r)
        {
            return null;
        }
        bool result = comparison.Comparison switch
        {
            IfComparison.LT => comparison.IsUnsigned ? (uint)l < (uint)r : l < r,
            IfComparison.GT => comparison.IsUnsigned ? (uint)l > (uint)r : l > r,
            IfComparison.LE => comparison.IsUnsigned ? (uint)l <= (uint)r : l <= r,
            IfComparison.GE => comparison.IsUnsigned ? (uint)l >= (uint)r : l >= r,
            IfComparison.EQ => l == r,
            IfComparison.NE => l != r,
            _ => false,
        };
        return result ? -1 : 0;
    }

    // Looks through the movs fxc copies vForkInstanceID with.
    private static ConstantNode IndexConstant(HlslTreeNode value)
    {
        while (value is MoveOperation move)
        {
            value = move.Inputs[0];
        }
        return value as ConstantNode;
    }

    /// <summary>
    /// Whether a destination gets a double: by opcode, by structured member, or for
    /// a mov by its source value (`mov r0.zw, r0.xxxy` may move one double).
    /// </summary>
    private bool IsDoubleResult(D3D10Instruction instruction, RegisterComponentKey destinationKey)
    {
        if (instruction.WritesDoubles)
        {
            return true;
        }
        if (instruction.Opcode == D3D10Opcode.LdStructured)
        {
            byte[] swizzle = instruction.GetSourceSwizzleComponents(3);
            int elementByteOffset = StructuredMemberOffset(instruction);
            return _registerState.IsDoubleStructuredMember(
                instruction.GetParamRegisterKey(3),
                elementByteOffset + swizzle[destinationKey.ComponentIndex] * 4);
        }
        return instruction.Opcode is D3D10Opcode.Mov or D3D10Opcode.DMov
            && IsMovedDouble(instruction, destinationKey.ComponentIndex);
    }

    /// <summary>
    /// Whether a mov's live source value is a double (the value, not the register).
    /// </summary>
    private bool IsMovedDouble(D3D10Instruction instruction, int destinationComponent)
    {
        if (instruction.GetOperandType(1) is OperandType.Immediate32
            or OperandType.Immediate64)
        {
            return false;
        }
        RegisterComponentKey sourceKey;
        try
        {
            sourceKey = GetParamRegisterComponentKey(instruction, 1, destinationComponent);
        }
        catch (NotImplementedException)
        {
            return false;
        }
        return ActiveOutputs != null
            && ActiveOutputs.TryGetValue(sourceKey, out HlslTreeNode source)
            && HoldsDouble(sourceKey, source);
    }

    /// <summary>
    /// The mov's mask with the upper half of each moved double removed.
    /// </summary>
    private int GetMovedDoubleMask(D3D10Instruction instruction, int mask)
    {
        if (instruction.Opcode != D3D10Opcode.Mov)
        {
            return mask;
        }
        int valueMask = mask;
        for (int pair = 0; pair < 4; pair += 2)
        {
            if ((mask & (0b11 << pair)) == (0b11 << pair) && IsMovedDouble(instruction, pair))
            {
                valueMask &= ~(1 << (pair + 1));
            }
        }
        return valueMask;
    }

    /// <summary>
    /// One component of an msad4, recovering the uint2 source from the four
    /// byte-sliding windows fxc builds in front of the instruction.
    /// </summary>
    private HlslTreeNode CreateMsad4Node(D3D10Instruction instruction, int componentIndex)
    {
        const int WindowsOperand = 2;
        HlslTreeNode[] windows = GetInputComponents(instruction, WindowsOperand, 4);
        HlslTreeNode[] inputs = GetInputs(instruction, componentIndex);
        int lane = componentIndex;
        bool standardWindows = TryGetMsadSource(instruction, WindowsOperand, windows,
            out HlslTreeNode low, out HlslTreeNode high);
        if (!standardWindows
            && !TryGetCompactedMsadSource(instruction, windows, componentIndex,
                out low, out high, out lane))
        {
            throw new NotImplementedException(
                "msad over windows this does not recognise as a uint2");
        }
        if (standardWindows)
        {
            _standardMsadWindows.Add(instruction);
        }
        var node = new Msad4Node(inputs[0], low, high, inputs[2], lane);
        // Detach the now-unread windows so they stop counting as readers of the
        // shifts under them; those then fall out as dead on their own.
        for (int window = 1; window < windows.Length; window++)
        {
            if (windows[window].Outputs.Count == 0)
            {
                windows[window].Remove();
            }
        }
        return node;
    }

    /// <summary>
    /// An msad4 read at some lanes only, or with a lane read twice: fxc packs just
    /// those windows to the front (`m.x + m.w` is `msad r0.xy`), and duplicates a
    /// whole window when the result duplicates a component (`uint4(r.x, r.x, r.y,
    /// r.z)` writes lane 0 into x and y). Each window's slide gives its lane; the
    /// lane a component reads is that slide's, never the component's own.
    /// </summary>
    private bool TryGetCompactedMsadSource(D3D10Instruction instruction, HlslTreeNode[] windows,
        int componentIndex, out HlslTreeNode low, out HlslTreeNode high, out int lane)
    {
        low = null;
        high = null;
        lane = -1;
        int mask = instruction.GetDestinationWriteMask();
        for (int component = 0; component < 4; component++)
        {
            if ((mask & (1 << component)) == 0)
            {
                continue;
            }
            HlslTreeNode window = Unwrap(windows[component]);
            HlslTreeNode windowLow = window;
            int windowLane = 0;
            if (window is BitFieldInsertOperation insert
                && AsConstantInt(insert.Width) is int bits
                && bits is 8 or 16 or 24
                && AsConstantInt(insert.Offset) == 32 - bits
                && Unwrap(insert.Value) is ShiftRightOperation shift
                && AsConstantInt(shift.Amount) == bits)
            {
                windowLane = bits / 8;
                windowLow = Unwrap(shift.Value);
                HlslTreeNode inserted = Unwrap(insert.Insert);
                if (high == null)
                {
                    high = inserted;
                }
                else if (!NodeGrouper.AreNodesEquivalent(high, inserted))
                {
                    return false;
                }
            }
            // A duplicated lane reads the same window twice, and equal windows of
            // one source are equal by construction: the low and high checks above
            // leave a lane's window nothing free to differ in.
            if (low == null)
            {
                low = windowLow;
            }
            else if (!NodeGrouper.AreNodesEquivalent(low, windowLow))
            {
                return false;
            }
            if (component == componentIndex)
            {
                lane = windowLane;
            }
        }
        // Windows that name no bfi read a zero high word; any will do.
        high ??= new ConstantNode(0);
        return lane >= 0;
    }

    /// <summary>
    /// The two words of the windows: window n is bfi(8n, 32-8n, high, low >> 8n),
    /// or one of fxc's folded forms of that.
    /// </summary>
    private bool TryGetMsadSource(D3D10Instruction instruction, int operand,
        HlslTreeNode[] windows, out HlslTreeNode low, out HlslTreeNode high)
    {
        low = Unwrap(windows[0]);
        high = null;
        if (TryGetFoldedMsadSource(instruction, operand, ref low, ref high))
        {
            return true;
        }
        // A zero high word: fxc drops every bfi.
        if (AllWindowsAreShiftsOf(windows, low))
        {
            high = new ConstantNode(0);
            return true;
        }
        // A literal high word is added instead of inserted.
        if (TryGetAddedMsadSource(windows, low, ref high))
        {
            return true;
        }
        // A literal low word is pre-shifted by fxc.
        if (TryGetInsertedOverConstantLow(windows, ref low, ref high))
        {
            return true;
        }
        for (int window = 1; window < 4; window++)
        {
            int bits = window * 8;
            if (Unwrap(windows[window]) is not BitFieldInsertOperation insert
                || AsConstantInt(insert.Width) != bits
                || AsConstantInt(insert.Offset) != 32 - bits
                || !IsLowWordShiftedBy(insert.Value, low, bits))
            {
                return false;
            }
            HlslTreeNode inserted = Unwrap(insert.Insert);
            if (high == null)
            {
                high = inserted;
            }
            else if (!NodeGrouper.AreNodesEquivalent(high, inserted))
            {
                return false;
            }
        }
        return high != null;
    }

    /// <summary>
    /// Both words literal: four folded windows. The high word's top byte never
    /// reaches msad4, so it comes back as zero (equivalent, maybe not the original).
    /// </summary>
    private static bool TryGetFoldedMsadSource(D3D10Instruction instruction, int operand,
        ref HlslTreeNode low, ref HlslTreeNode high)
    {
        if (instruction.GetOperandType(operand) != OperandType.Immediate32)
        {
            return false;
        }
        // Bits from the instruction: the nodes hold them as lossy floats.
        byte[] swizzle = instruction.GetSourceSwizzleComponents(operand);
        var values = new uint[4];
        for (int window = 0; window < 4; window++)
        {
            values[window] = (uint)instruction.GetParamInt(operand, swizzle[window]);
        }
        uint lowWord = values[0];
        uint highWord = values[3] >> 8;
        ulong source = lowWord | ((ulong)highWord << 32);
        for (int window = 1; window < 4; window++)
        {
            if (values[window] != (uint)(source >> (window * 8)))
            {
                return false;
            }
        }
        low = new ConstantNode((int)lowWord);
        high = new ConstantNode((int)highWord);
        return true;
    }

    /// <summary>Whether a window's value is `low >> bits`.</summary>
    private static bool IsLowWordShiftedBy(HlslTreeNode value, HlslTreeNode low, int bits)
    {
        if (Unwrap(value) is ShiftRightOperation shift)
        {
            return AsConstantInt(shift.Amount) == bits
                && NodeGrouper.AreNodesEquivalent(Unwrap(shift.Value), low);
        }
        return false;
    }

    /// <summary>
    /// A literal low word, pre-shifted per window, with the high register inserted.
    /// Read as exact bits: an untyped mov immediate is still a float here.
    /// </summary>
    private bool TryGetInsertedOverConstantLow(
        HlslTreeNode[] windows, ref HlslTreeNode low, ref HlslTreeNode high)
    {
        if (AsExactImmediate(windows[0]) is not int lowBits)
        {
            return false;
        }
        HlslTreeNode inserted = null;
        for (int window = 1; window < 4; window++)
        {
            int bits = window * 8;
            if (Unwrap(windows[window]) is not BitFieldInsertOperation insert
                || AsConstantInt(insert.Width) != bits
                || AsConstantInt(insert.Offset) != 32 - bits
                || AsExactImmediate(insert.Value) is not int shifted
                || (uint)shifted != (uint)lowBits >> bits)
            {
                return false;
            }
            HlslTreeNode candidate = Unwrap(insert.Insert);
            if (inserted == null)
            {
                inserted = candidate;
            }
            else if (!NodeGrouper.AreNodesEquivalent(inserted, candidate))
            {
                return false;
            }
        }
        low = new ConstantNode(lowBits);
        high = inserted;
        return true;
    }

    // A constant's exact bits (integer-typed or a recorded mov immediate), else null.
    private int? AsExactImmediate(HlslTreeNode node)
    {
        if (Unwrap(node) is not ConstantNode constant)
        {
            return null;
        }
        if (constant.IntegerValue is int typed)
        {
            return typed;
        }
        foreach ((ConstantNode immediate, uint bits) in _polymorphicImmediates)
        {
            if (ReferenceEquals(immediate, constant))
            {
                return (int)bits;
            }
        }
        return null;
    }

    /// <summary>
    /// A literal high word: each window is `(low >> 8n) + literal`. All three
    /// windows must agree on the high word.
    /// </summary>
    private static bool TryGetAddedMsadSource(
        HlslTreeNode[] windows, HlslTreeNode low, ref HlslTreeNode high)
    {
        var added = new uint[4];
        for (int window = 1; window < 4; window++)
        {
            int bits = window * 8;
            if (Unwrap(windows[window]) is not AddOperation add)
            {
                return false;
            }
            HlslTreeNode[] operands = [Unwrap(add.Addend1), Unwrap(add.Addend2)];
            int shiftedOperand = IsLowWordShiftedBy(operands[0], low, bits) ? 0
                : IsLowWordShiftedBy(operands[1], low, bits) ? 1
                : -1;
            if (shiftedOperand < 0
                || AsConstantInt(operands[1 - shiftedOperand]) is not int addend)
            {
                return false;
            }
            added[window] = (uint)addend;
        }
        uint highWord = added[3] >> 8;
        for (int window = 1; window < 3; window++)
        {
            if (added[window] != (uint)(highWord << (32 - window * 8)))
            {
                return false;
            }
        }
        high = new ConstantNode((int)highWord);
        return true;
    }

    private static bool AllWindowsAreShiftsOf(HlslTreeNode[] windows, HlslTreeNode low)
    {
        for (int window = 1; window < 4; window++)
        {
            if (Unwrap(windows[window]) is not ShiftRightOperation shift
                || AsConstantInt(shift.Amount) != window * 8
                || !NodeGrouper.AreNodesEquivalent(Unwrap(shift.Value), low))
            {
                return false;
            }
        }
        return true;
    }

    // Through the moves fxc leaves between a value and where it is read.
    private static HlslTreeNode Unwrap(HlslTreeNode node)
    {
        while (node is MoveOperation move)
        {
            node = move.Inputs[0];
        }
        return node;
    }

    private static int? AsConstantInt(HlslTreeNode node)
    {
        return Unwrap(node) is ConstantNode constant
            ? constant.IntegerValue ?? (int)constant.Value
            : null;
    }

    /// <summary>
    /// Where a structured access's member is looked up: the immediate, or the
    /// constant part of the computed offset's value, which lands in its array member.
    /// </summary>
    private int StructuredMemberOffset(D3D10Instruction instruction)
    {
        const int ByteOffsetOperand = 2;
        return instruction.GetOperandType(ByteOffsetOperand) == OperandType.Immediate32
            ? instruction.GetParamInt(ByteOffsetOperand, 0)
            : RegisterState.MemberByteOffset(GetActiveOutput(
                GetParamRegisterComponentKey(instruction, ByteOffsetOperand, 0)));
    }

    /// <summary>
    /// A structured load/store mask with the upper half of each double member removed
    /// (`ld_structured r0.xy` loads one double).
    /// </summary>
    private int GetStructuredValueMask(D3D10Instruction instruction, int mask)
    {
        int resourceOperand = instruction.Opcode switch
        {
            D3D10Opcode.LdStructured => 3,
            D3D10Opcode.StoreStructured => 0,
            _ => -1,
        };
        if (resourceOperand < 0)
        {
            return mask;
        }
        RegisterKey resourceKey = instruction.GetParamRegisterKey(resourceOperand);
        if (!_registerState.HasDoubleStructuredMember(resourceKey))
        {
            return mask;
        }
        int elementByteOffset = StructuredMemberOffset(instruction);
        // A load maps components through the resource swizzle; a store by mask
        // position.
        byte[] swizzle = instruction.Opcode == D3D10Opcode.LdStructured
            ? instruction.GetSourceSwizzleComponents(resourceOperand)
            : null;
        var elementComponents = new List<int>();
        int valueMask = 0;
        for (int component = 0; component < 4; component++)
        {
            if ((mask & (1 << component)) == 0)
            {
                continue;
            }
            int elementComponent = swizzle != null ? swizzle[component] : component;
            elementComponents.Add(elementComponent);
            if (!_registerState.IsDoubleStructuredMemberUpperHalf(
                resourceKey, elementByteOffset + elementComponent * 4))
            {
                valueMask |= 1 << component;
            }
        }
        return valueMask;
    }

    private static IEnumerable<RegisterComponentKey> GetParameterRegisterKeys(Instruction instruction, int index, int mask)
    {
        RegisterKey registerKey = instruction.GetParamRegisterKey(index);

        if (registerKey is D3D10RegisterKey d3D10RegisterKey)
        {
            if (d3D10RegisterKey.GSVertex.HasValue)
            {
                for (int vertex = 0; vertex < d3D10RegisterKey.GSVertex.Value; vertex++)
                {
                    for (int component = 0; component < 4; component++)
                    {
                        if ((mask & (1 << component)) == 0) continue;

                        RegisterKey vertexKey = d3D10RegisterKey.WithVertex(vertex);
                        yield return new RegisterComponentKey(vertexKey, component);
                    }
                }
                yield break;
            }
        }
        else
        {
            D3D9RegisterKey d3D9RegisterKey = registerKey as D3D9RegisterKey;
            if (d3D9RegisterKey.Type == RegisterType.Sampler)
            {
                yield break;
            }
            if (d3D9RegisterKey.Type == RegisterType.MiscType && d3D9RegisterKey.Number == 1) // VFACE
            {
                yield return new RegisterComponentKey(registerKey, 0);
                yield break;
            }
        }

        for (int component = 0; component < 4; component++)
        {
            if ((mask & (1 << component)) == 0) continue;

            yield return new RegisterComponentKey(registerKey, component);
        }
    }

    private HlslTreeNode CreateInstructionTree(D3D9Instruction instruction, RegisterComponentKey destinationKey)
    {
        HlslTreeNode stamped = CreateD3D9InstructionTree(instruction, destinationKey);
        StampSourceInstruction(stamped, destinationKey.ComponentIndex);
        return stamped;
    }

    private HlslTreeNode CreateD3D9InstructionTree(D3D9Instruction instruction, RegisterComponentKey destinationKey)
    {
        int componentIndex = destinationKey.ComponentIndex;

        switch (instruction.Opcode)
        {
            case Opcode.Dcl:
                {
                    var shaderInput = new RegisterInputNode(destinationKey);
                    return shaderInput;
                }
            case Opcode.Def:
                {
                    var constant = new ConstantNode(instruction.GetParamSingle(componentIndex + 1)[0]);
                    return constant;
                }
            case Opcode.DefI:
                {
                    var constant = new ConstantNode(instruction.GetParamInt(componentIndex + 1));
                    return constant;
                }
            case Opcode.DefB:
                {
                    throw new NotImplementedException();
                }
            case Opcode.ExpP when _shaderModel.MajorVersion == 1:
                {
                    // vs_1_1's expp is the partial exponential: it writes 2^v whole,
                    // split into its power and fraction, and a one, per component.
                    // From vs_2_0 it is the exp below, replicated.
                    HlslTreeNode part = GetInputs(instruction, componentIndex)[0];
                    switch (componentIndex)
                    {
                        case 0:
                            return new ExponentialOperation(new FloorOperation(part));
                        case 1:
                            return new FractionalOperation(part);
                        case 2:
                            return new ExponentialOperation(part);
                        default:
                            return new ConstantNode(1.0f);
                    }
                }
            case Opcode.ExpP:
                {
                    // From vs_2_0 expp is exp replicated; vs_1_1's partial form is above.
                    return new ExponentialOperation(GetInputs(instruction, componentIndex)[0]);
                }
            case Opcode.Abs:
            case Opcode.Add:
            case Opcode.Cmp:
            case Opcode.DSX:
            case Opcode.DSY:
            case Opcode.Exp:
            case Opcode.Frc:
            case Opcode.Log:
            case Opcode.LogP:
            case Opcode.Lrp:
            case Opcode.Mad:
            case Opcode.Max:
            case Opcode.Min:
            case Opcode.Mov:
            case Opcode.MovA:
            case Opcode.Mul:
            case Opcode.Pow:
            case Opcode.Rcp:
            case Opcode.Rsq:
            case Opcode.SinCos:
            case Opcode.Sge:
            case Opcode.Slt:
            case Opcode.TexKill:
                {
                    HlslTreeNode[] inputs = GetInputs(instruction, componentIndex);
                    switch (instruction.Opcode)
                    {
                        case Opcode.Abs:
                            return new AbsoluteOperation(inputs[0]);
                        case Opcode.Cmp:
                            return new CompareOperation(inputs[0], inputs[1], inputs[2]);
                        case Opcode.DSX:
                            return new PartialDerivativeXOperation(inputs[0]);
                        case Opcode.DSY:
                            return new PartialDerivativeYOperation(inputs[0]);
                        case Opcode.Exp:
                            return new ExponentialOperation(inputs[0]);
                        case Opcode.Frc:
                            return new FractionalOperation(inputs[0]);
                        case Opcode.Log:
                        case Opcode.LogP:
                            // logp is log at every version.
                            return new LogOperation(inputs[0]);
                        case Opcode.Lrp:
                            return new LinearInterpolateOperation(inputs[0], inputs[1], inputs[2]);
                        case Opcode.Max:
                            return new MaximumOperation(inputs[0], inputs[1]);
                        case Opcode.Min:
                            return new MinimumOperation(inputs[0], inputs[1]);
                        case Opcode.Mov:
                            return new MoveOperation(inputs[0]);
                        case Opcode.MovA:
                            return new MoveOperation(inputs[0]); // TODO: cast?
                        case Opcode.Add:
                            return new AddOperation(inputs[0], inputs[1]);
                        case Opcode.Mul:
                            return new MultiplyOperation(inputs[0], inputs[1]);
                        case Opcode.Mad:
                            return new MultiplyAddOperation(inputs[0], inputs[1], inputs[2]);
                        case Opcode.Pow:
                            return new PowerOperation(inputs[0], inputs[1]);
                        case Opcode.Rcp:
                            return new ReciprocalOperation(inputs[0]);
                        case Opcode.Rsq:
                            return new ReciprocalSquareRootOperation(inputs[0]);
                        case Opcode.SinCos:
                            if (componentIndex == 0)
                            {
                                return new CosineOperation(inputs[0]);
                            }
                            return new SineOperation(inputs[0]);
                        case Opcode.Sge:
                            return new SignGreaterOrEqualOperation(inputs[0], inputs[1]);
                        case Opcode.Slt:
                            return new SignLessOperation(inputs[0], inputs[1]);
                        default:
                            throw new NotImplementedException();
                    }
                }
            case Opcode.Tex:
            case Opcode.TexLDL:
            case Opcode.TexLDD:
                return CreateTextureLoadOutputNode(instruction, componentIndex);
            case Opcode.DP2Add:
                return CreateDotProduct2AddNode(instruction);
            case Opcode.Dp3:
            case Opcode.Dp4:
                return CreateDotProductNode(instruction);
            case Opcode.M3x2:
            case Opcode.M3x3:
            case Opcode.M3x4:
            case Opcode.M4x3:
            case Opcode.M4x4:
                return CreateMatrixProductNode(instruction, componentIndex);
            case Opcode.Crs:
                return CreateCrossProductNode(instruction, componentIndex);
            case Opcode.Sgn:
                // The two registers after the value are hardware scratch, not operands.
                return ReadingFloats(new SignOperation(
                    GetInputComponents(instruction, 1, componentIndex + 1)[componentIndex]));
            case Opcode.Nrm:
                return CreateNormalizeOutputNode(instruction, componentIndex);
            case Opcode.Dst:
                // dst: [1, a.y * b.y, a.z, b.w].
                switch (componentIndex)
                {
                    case 0:
                        return new ConstantNode(1.0f);
                    case 1:
                        {
                            HlslTreeNode[] yInputs = GetInputs(instruction, 1);
                            return new MultiplyOperation(yInputs[0], yInputs[1]);
                        }
                    case 2:
                        return GetInputs(instruction, 2)[0];
                    default:
                        return GetInputs(instruction, 3)[1];
                }
            case Opcode.Lit:
                return CreateLitOutputNode(instruction, componentIndex);
            default:
                throw new NotImplementedException($"{instruction.Opcode} not implemented");
        }
    }

    private HlslTreeNode CreateInstructionTree(D3D10Instruction instruction, RegisterComponentKey destinationKey)
    {
        HlslTreeNode node = CreateD3D10InstructionTree(instruction, destinationKey);
        node.ConsumesInteger ??= GetConsumedType(instruction.Opcode);
        StampSourceInstruction(node, destinationKey.ComponentIndex);
        return node;
    }

    /// <summary>
    /// Marks a computed value with the instruction position, telling apart the
    /// components of one instruction from two sharing a register. Reads are not marked.
    /// </summary>
    private void StampSourceInstruction(HlslTreeNode node, int component)
    {
        if (node.SourceInstruction == 0 && node is not RegisterInputNode and not ConstantNode)
        {
            node.SourceInstruction = _instructionPointer + 1;
            node.SourceComponent = component;
        }
    }

    // Whether an instruction reads its operands as integers; null for bit carriers.
    private static bool? GetConsumedType(D3D10Opcode opcode)
    {
        switch (opcode)
        {
            case D3D10Opcode.Mov:
            case D3D10Opcode.MovC:
            case D3D10Opcode.And:
            case D3D10Opcode.Or:
            case D3D10Opcode.Xor:
            case D3D10Opcode.Not:
                return null;
            case D3D10Opcode.IToF:
            case D3D10Opcode.UTof:
            case D3D10Opcode.IToD:
            case D3D10Opcode.UToD:
                return true;
            case D3D10Opcode.Ftoi:
            case D3D10Opcode.Ftou:
            case D3D10Opcode.F32ToF16:
                return false;
            case D3D10Opcode.F16ToF32:
                return true;
            // The address is an integer whatever the buffer holds.
            case D3D10Opcode.LdStructured:
            case D3D10Opcode.LdRaw:
            case D3D10Opcode.StoreStructured:
            case D3D10Opcode.StoreRaw:
                return true;
            default:
                return opcode.IsInteger();
        }
    }

    /// <summary>
    /// Records how a buffer store reads its values; a store is a statement, so the
    /// reader walk in GetConsumedType cannot see it otherwise.
    /// </summary>
    private void RecordStoredType(D3D10Instruction instruction, HlslTreeNode[] values)
    {
        ValueKind kind = instruction.Opcode == D3D10Opcode.StoreRaw
            ? ValueKind.Integer
            : _integerOperandAnalysis.GetStructuredElementKind(instruction);
        if (kind is not (ValueKind.Integer or ValueKind.Float))
        {
            return;
        }
        for (int i = 0; i < values.Length; i++)
        {
            // A direct literal was typed float by the opcode; into integer memory
            // it is the integer of the same bits.
            if (kind == ValueKind.Integer
                && values[i] is ConstantNode { IntegerValue: null } literal
                && !_polymorphicImmediates.Any(p => ReferenceEquals(p.Constant, literal)))
            {
                values[i] = new ConstantNode(BitConverter.SingleToInt32Bits(literal.Value));
            }
            _storedTypes[values[i]] = kind == ValueKind.Integer;
        }
    }

    /// <summary>
    /// Types mov'd immediates by their readers when they agree, since a reused
    /// register's type can be wrong for this particular value.
    /// </summary>
    private void ResolvePolymorphicImmediates()
    {
        foreach ((ConstantNode constant, uint bits) in _polymorphicImmediates)
        {
            bool? consumedAsInteger = GetConsumedType(constant, _storedTypes)
                ?? OutputTypeCarrying(constant);
            // HLSL has no NaN literal, so NaN bits are an integer.
            if (consumedAsInteger == null && constant.IntegerValue == null
                && float.IsNaN(BitConverter.UInt32BitsToSingle(bits)))
            {
                consumedAsInteger = true;
            }
            if (consumedAsInteger == null || consumedAsInteger == (constant.IntegerValue != null))
            {
                continue;
            }
            ConstantNode typed = consumedAsInteger == true
                ? new ConstantNode((int)bits)
                : new ConstantNode(BitConverter.UInt32BitsToSingle(bits));
            constant.Replace(typed);
            new StatementVisitor(_statements).Visit(statement =>
            {
                foreach (var key in statement.Outputs.Where(o => ReferenceEquals(o.Value, constant)).Select(o => o.Key).ToList())
                {
                    statement.Outputs[key] = typed;
                }
                foreach (var key in statement.Inputs.Where(o => ReferenceEquals(o.Value, constant)).Select(o => o.Key).ToList())
                {
                    statement.Inputs[key] = typed;
                }
            });
        }
    }

    // The signature's type where only moves, selects and joins carry the value into
    // signature outputs; else null. Float only without a join: a join's variable
    // can hold it as bits.
    private bool? OutputTypeCarrying(HlslTreeNode value)
    {
        bool joined = false;
        var carried = HlslTreeNode.NewNodeSet();
        var pending = new Stack<HlslTreeNode>();
        pending.Push(value);
        while (pending.Count != 0)
        {
            HlslTreeNode node = pending.Pop();
            if (!carried.Add(node))
            {
                continue;
            }
            foreach (HlslTreeNode reader in node.Outputs)
            {
                if (reader is MoveOperation or PhiNode
                    || (reader is MoveConditionalOperation && !ReferenceEquals(reader.Inputs[0], node)))
                {
                    joined |= reader is PhiNode;
                    pending.Push(reader);
                }
                else
                {
                    return null;
                }
            }
        }
        var types = new HashSet<bool>();
        new StatementVisitor(_statements).Visit(statement =>
        {
            foreach (var output in statement.Outputs)
            {
                if (output.Key.RegisterKey is D3D10RegisterKey { OperandType: OperandType.Output }
                    && carried.Contains(output.Value))
                {
                    types.Add(_integerOperandAnalysis.IsIntegerOutputSignature(output.Key));
                }
            }
        });
        return types.Count == 1 && (types.Single() || !joined) ? types.Single() : null;
    }

    // What the readers of a value agree it is, looking through carrier nodes;
    // null where they disagree or there are none.
    internal static bool? GetConsumedType(
        HlslTreeNode value, IReadOnlyDictionary<HlslTreeNode, bool> storedTypes = null)
    {
        bool? type = null;
        var visited = HlslTreeNode.NewNodeSet();
        var pending = new Stack<HlslTreeNode>();
        pending.Push(value);
        while (pending.Count != 0)
        {
            HlslTreeNode node = pending.Pop();
            if (storedTypes != null && storedTypes.TryGetValue(node, out bool stored))
            {
                if (type != null && type != stored)
                {
                    return null;
                }
                type = stored;
            }
            foreach (HlslTreeNode reader in node.Outputs)
            {
                if (!visited.Add(reader))
                {
                    continue;
                }
                // Bitwise operators carry bits too.
                bool carries = reader is MoveOperation or MoveConditionalOperation or PhiNode
                    or NegateOperation or AbsoluteOperation
                    or BitwiseAndOperation or BitwiseOrOperation or BitwiseXorOperation
                    or BitwiseNotOperation;
                if (reader is MoveConditionalOperation && ReferenceEquals(reader.Inputs[0], node))
                {
                    // The condition, not a value carried through.
                    continue;
                }
                if (carries)
                {
                    pending.Push(reader);
                    continue;
                }
                // `v[i]` reads floats, but its index is an integer.
                bool? consumed = reader is VectorComponentNode component && ReferenceEquals(component.Index, node)
                    ? true
                    : reader is ComparisonNode comparison
                    ? (comparison.IsBitsTest ? null : comparison.IsInteger)
                    : reader.ReadsIntegers;
                if (consumed == null)
                {
                    continue;
                }
                if (type != null && type != consumed)
                {
                    return null;
                }
                type = consumed;
            }
        }
        return type;
    }

    private HlslTreeNode CreateD3D10InstructionTree(D3D10Instruction instruction, RegisterComponentKey destinationKey)
    {
        int componentIndex = destinationKey.ComponentIndex;

        switch (instruction.Opcode)
        {
            case D3D10Opcode.MSAD:
                return CreateMsad4Node(instruction, componentIndex);
            case D3D10Opcode.DclInputPS:
            case D3D10Opcode.DclInputPSSgv:
            case D3D10Opcode.DclInputPSSiv:
            case D3D10Opcode.DclInputSiv:
            case D3D10Opcode.DclInputSgv:
            case D3D10Opcode.DclInput:
            case D3D10Opcode.DclOutput:
            case D3D10Opcode.DclOutputSgv:
            case D3D10Opcode.DclOutputSiv:
                {
                    var shaderInput = new RegisterInputNode(destinationKey);
                    return shaderInput;
                }
            case D3D10Opcode.Mov:
            case D3D10Opcode.Add:
            case D3D10Opcode.DerivRtx:
            case D3D10Opcode.DerivRty:
            case D3D10Opcode.DerivRtxCoarse:
            case D3D10Opcode.DerivRtxFine:
            case D3D10Opcode.DerivRtyCoarse:
            case D3D10Opcode.DerivRtyFine:
            case D3D10Opcode.Rcp:
            case D3D10Opcode.UBFE:
            case D3D10Opcode.IBFE:
            case D3D10Opcode.BFI:
            case D3D10Opcode.Exp:
            case D3D10Opcode.And:
            case D3D10Opcode.Xor:
            case D3D10Opcode.Not:
            case D3D10Opcode.Div:
            case D3D10Opcode.Eq:
            case D3D10Opcode.Or:
            case D3D10Opcode.Frc:
            case D3D10Opcode.GE:
            case D3D10Opcode.LT:
            case D3D10Opcode.Ne:
            case D3D10Opcode.Ftoi:
            case D3D10Opcode.Ftou:
            case D3D10Opcode.IAdd:
            case D3D10Opcode.IShl:
            case D3D10Opcode.IShr:
            case D3D10Opcode.UShr:
            case D3D10Opcode.IMad:
            case D3D10Opcode.IMax:
            case D3D10Opcode.IMin:
            case D3D10Opcode.Umad:
            case D3D10Opcode.UMax:
            case D3D10Opcode.UMin:
            case D3D10Opcode.INeg:
            case D3D10Opcode.Ine:
            case D3D10Opcode.RoundNe:
            case D3D10Opcode.RoundNi:
            case D3D10Opcode.RoundPi:
            case D3D10Opcode.RoundZ:
            case D3D10Opcode.Ieq:
            case D3D10Opcode.Ige:
            case D3D10Opcode.UGE:
            case D3D10Opcode.ULT:
            case D3D10Opcode.Ilt:
            case D3D10Opcode.IToF:
            case D3D10Opcode.UTof:
            case D3D10Opcode.LdStructured:
            case D3D10Opcode.LdRaw:
            case D3D10Opcode.Log:
            case D3D10Opcode.Mad:
            case D3D10Opcode.Max:
            case D3D10Opcode.Min:
            case D3D10Opcode.MovC:
            case D3D10Opcode.Mul:
            case D3D10Opcode.Rsq:
            case D3D10Opcode.Sqrt:
            case D3D10Opcode.CountBits:
            case D3D10Opcode.FirstBitLo:
            case D3D10Opcode.FirstBitHi:
            case D3D10Opcode.FirstBitSHi:
            case D3D10Opcode.BFRev:
            case D3D10Opcode.F32ToF16:
            case D3D10Opcode.F16ToF32:
            case D3D10Opcode.DAdd:
            case D3D10Opcode.DMul:
            case D3D10Opcode.DDiv:
            case D3D10Opcode.DMax:
            case D3D10Opcode.DMin:
            case D3D10Opcode.DEq:
            case D3D10Opcode.DGe:
            case D3D10Opcode.DLt:
            case D3D10Opcode.DNe:
            case D3D10Opcode.DMov:
            case D3D10Opcode.DMovC:
            case D3D10Opcode.DFMA:
            case D3D10Opcode.DRCP:
            case D3D10Opcode.DToF:
            case D3D10Opcode.DToI:
            case D3D10Opcode.DToU:
            case D3D10Opcode.FToD:
            case D3D10Opcode.IToD:
            case D3D10Opcode.UToD:
                {
                    HlslTreeNode[] inputs = GetInputs(instruction, componentIndex);
                    switch (instruction.Opcode)
                    {
                        case D3D10Opcode.Add:
                        case D3D10Opcode.IAdd:
                            return new AddOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.IShl:
                            return new ShiftLeftOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.IShr:
                        case D3D10Opcode.UShr:
                            return new ShiftRightOperation(inputs[0], inputs[1],
                                instruction.Opcode == D3D10Opcode.UShr);
                        case D3D10Opcode.DerivRtx:
                            return new PartialDerivativeXOperation(inputs[0]);
                        case D3D10Opcode.DerivRty:
                            return new PartialDerivativeYOperation(inputs[0]);
                        case D3D10Opcode.DerivRtxCoarse:
                            return new PartialDerivativeXOperation(inputs[0],
                                DerivativePrecision.Coarse);
                        case D3D10Opcode.DerivRtxFine:
                            return new PartialDerivativeXOperation(inputs[0],
                                DerivativePrecision.Fine);
                        case D3D10Opcode.DerivRtyCoarse:
                            return new PartialDerivativeYOperation(inputs[0],
                                DerivativePrecision.Coarse);
                        case D3D10Opcode.DerivRtyFine:
                            return new PartialDerivativeYOperation(inputs[0],
                                DerivativePrecision.Fine);
                        case D3D10Opcode.Rcp:
                            return new ReciprocalOperation(inputs[0]);
                        case D3D10Opcode.UBFE:
                        case D3D10Opcode.IBFE:
                            return new BitFieldExtractOperation(inputs[0], inputs[1], inputs[2],
                                instruction.Opcode == D3D10Opcode.UBFE);
                        case D3D10Opcode.BFI:
                            return new BitFieldInsertOperation(inputs[0], inputs[1], inputs[2], inputs[3]);
                        case D3D10Opcode.Exp:
                            return new ExponentialOperation(inputs[0]);
                        case D3D10Opcode.Frc:
                            return new FractionalOperation(inputs[0]);
                        case D3D10Opcode.GE:
                            return new GreaterEqualOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.Div:
                            return new DivisionOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.And:
                            return CreateLogicalOperation(instruction.Opcode, inputs, instruction);
                        case D3D10Opcode.Or:
                        case D3D10Opcode.Xor:
                            return CreateLogicalOperation(instruction.Opcode, inputs, instruction);
                        case D3D10Opcode.Not:
                            // A not of a condition inverts it (De Morgan); `~` on a
                            // bool would give -2, which tests true.
                            return InvertCondition(inputs[0]) ?? new BitwiseNotOperation(inputs[0]);
                        case D3D10Opcode.LT:
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.LT);
                        case D3D10Opcode.Eq:
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.EQ);
                        case D3D10Opcode.Ne:
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.NE);
                        case D3D10Opcode.Ilt:
                        case D3D10Opcode.ULT:
                            return new ComparisonNode(
                                inputs[0], inputs[1], IfComparison.LT, isInteger: true,
                                isUnsigned: instruction.Opcode == D3D10Opcode.ULT);
                        case D3D10Opcode.Ige:
                        case D3D10Opcode.UGE:
                            return new ComparisonNode(
                                inputs[0], inputs[1], IfComparison.GE, isInteger: true,
                                isUnsigned: instruction.Opcode == D3D10Opcode.UGE);
                        case D3D10Opcode.Ieq:
                            return new ComparisonNode(
                                inputs[0], inputs[1], IfComparison.EQ, isInteger: true);
                        case D3D10Opcode.Ine:
                            return new ComparisonNode(
                                inputs[0], inputs[1], IfComparison.NE, isInteger: true);
                        case D3D10Opcode.IMad:
                        case D3D10Opcode.Umad:
                            return new MultiplyAddOperation(inputs[0], inputs[1], inputs[2]);
                        case D3D10Opcode.IMin:
                        case D3D10Opcode.UMin:
                            return new MinimumOperation(inputs[0], inputs[1],
                                instruction.Opcode == D3D10Opcode.UMin);
                        case D3D10Opcode.IMax:
                        case D3D10Opcode.UMax:
                            return new MaximumOperation(inputs[0], inputs[1],
                                instruction.Opcode == D3D10Opcode.UMax);
                        case D3D10Opcode.INeg:
                            return new NegateOperation(inputs[0]);
                        case D3D10Opcode.RoundNe:
                            return new RoundOperation(inputs[0]);
                        case D3D10Opcode.RoundNi:
                            return new FloorOperation(inputs[0]);
                        case D3D10Opcode.RoundPi:
                            return new CeilingOperation(inputs[0]);
                        case D3D10Opcode.RoundZ:
                            return new TruncateOperation(inputs[0]);
                        case D3D10Opcode.LdStructured:
                            {
                                int elementByteOffset = instruction.GetOperandType(2) == OperandType.Immediate32
                                    ? instruction.GetParamInt(2, 0)
                                    : 0;
                                // A load at a consume slot is a component of Consume().
                                if (inputs[0] is ConsumeSlotNode slot
                                    && IsConsumeBuffer(instruction.GetParamRegisterKey(3)))
                                {
                                    return new ConsumeNode(
                                        (RegisterInputNode)inputs[2], elementByteOffset / 4, slot)
                                    {
                                        IsIntegerElement = _registerState.IsIntegerStructuredMember(
                                            instruction.GetParamRegisterKey(3),
                                            elementByteOffset
                                                + ((IHasComponentIndex)inputs[2]).ComponentIndex * 4),
                                    };
                                }
                                // The member at offset + selected component; a computed
                                // offset's constant part lands in its array member.
                                int memberStart = RegisterState.MemberByteOffset(inputs[1]);
                                int memberByteOffset = memberStart
                                    + ((IHasComponentIndex)inputs[2]).ComponentIndex * 4;
                                return new LoadStructuredNode(inputs[0], inputs[1], inputs[2])
                                {
                                    ElementByteOffset = elementByteOffset,
                                    MemberByteOffset = memberStart,
                                    // Groupshared has no reflection, so the analysis decides.
                                    IsIntegerElement = instruction.GetOperandType(3) == OperandType.ThreadGroupSharedMemory
                                        ? _integerOperandAnalysis.GetStructuredElementKind(instruction) == ValueKind.Integer
                                        : _registerState.IsIntegerStructuredMember(
                                            instruction.GetParamRegisterKey(3), memberByteOffset),
                                    IsUnsignedElement = _registerState.IsUnsignedStructuredMember(
                                        instruction.GetParamRegisterKey(3), memberByteOffset),
                                };
                            }
                        case D3D10Opcode.LdRaw:
                            // ld_raw dst, byteOffset, t#. Groupshared has no Load, so
                            // it is a subscript with the swizzle's dword folded in.
                            if (IsGroupSharedResource(instruction, 2))
                            {
                                var sharedKey = instruction.GetParamRegisterKey(2);
                                int dword = instruction.GetSourceSwizzleComponents(2)[componentIndex];
                                return new LoadStructuredNode(
                                    GroupSharedElementOfByteAddress(inputs[0], dword),
                                    new ConstantNode(0),
                                    GetActiveOutput(new RegisterComponentKey(sharedKey, 0)))
                                {
                                    // Raw bits: a float reader reinterprets, not converts.
                                    IsRaw = true,
                                    IsGroupShared = true,
                                };
                            }
                            return new LoadStructuredNode(inputs[0], new ConstantNode(0), inputs[1]) { IsRaw = true };
                        case D3D10Opcode.Log:
                            return new LogOperation(inputs[0]);
                        case D3D10Opcode.Mad:
                            return new MultiplyAddOperation(inputs[0], inputs[1], inputs[2]);
                        case D3D10Opcode.Max:
                            return new MaximumOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.Min:
                            return new MinimumOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.Mov:
                            return new MoveOperation(inputs[0]);
                        case D3D10Opcode.IToF:
                            return new ConvertOperation(inputs[0], "float") { SourceUnsigned = false };
                        case D3D10Opcode.UTof:
                            return new ConvertOperation(inputs[0], "float") { SourceUnsigned = true };
                        case D3D10Opcode.Ftoi:
                            return new ConvertOperation(inputs[0], "int");
                        case D3D10Opcode.Ftou:
                            return new ConvertOperation(inputs[0], "uint");
                        case D3D10Opcode.MovC:
                            return new MoveConditionalOperation(inputs[0], inputs[1], inputs[2]);
                        case D3D10Opcode.Mul:
                            return new MultiplyOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.DAdd:
                            return new AddOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.DMul:
                            return new MultiplyOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.DDiv:
                            return new DivisionOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.DMax:
                            return new MaximumOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.DMin:
                            return new MinimumOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.DEq:
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.EQ);
                        case D3D10Opcode.DGe:
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.GE);
                        case D3D10Opcode.DLt:
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.LT);
                        case D3D10Opcode.DNe:
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.NE);
                        case D3D10Opcode.DMov:
                            return new MoveOperation(inputs[0]);
                        case D3D10Opcode.DMovC:
                            return new MoveConditionalOperation(inputs[0], inputs[1], inputs[2]);
                        // Rounds once, so only a written fma() produces it.
                        case D3D10Opcode.DFMA:
                            return new FusedMultiplyAddOperation(inputs[0], inputs[1], inputs[2]);
                        case D3D10Opcode.DRCP:
                            return new ReciprocalOperation(inputs[0]);
                        case D3D10Opcode.DToF:
                            return new ConvertOperation(inputs[0], "float");
                        case D3D10Opcode.DToI:
                            return new ConvertOperation(inputs[0], "int");
                        case D3D10Opcode.DToU:
                            return new ConvertOperation(inputs[0], "uint");
                        case D3D10Opcode.FToD:
                            return new ConvertOperation(inputs[0], "double");
                        case D3D10Opcode.IToD:
                            return new ConvertOperation(inputs[0], "double") { SourceUnsigned = false };
                        case D3D10Opcode.UToD:
                            return new ConvertOperation(inputs[0], "double") { SourceUnsigned = true };
                        case D3D10Opcode.Rsq:
                            return new ReciprocalSquareRootOperation(inputs[0]);
                        case D3D10Opcode.Sqrt:
                            return new SquareRootOperation(inputs[0]);
                        case D3D10Opcode.CountBits:
                            return new BitCountOperation(inputs[0]);
                        case D3D10Opcode.FirstBitLo:
                            return new FirstBitLowOperation(inputs[0]);
                        // firstbit_hi counts from the top: 31 - firstbithigh(x). They
                        // differ only for x == 0, which fxc's guard (matched by a
                        // template) handles.
                        case D3D10Opcode.FirstBitHi:
                        case D3D10Opcode.FirstBitSHi:
                            return new SubtractOperation(
                                new ConstantNode(31),
                                new FirstBitHighOperation(
                                    inputs[0], instruction.Opcode == D3D10Opcode.FirstBitHi));
                        case D3D10Opcode.BFRev:
                            return new ReverseBitsOperation(inputs[0]);
                        case D3D10Opcode.F32ToF16:
                            return new FloatToHalfOperation(inputs[0]);
                        case D3D10Opcode.F16ToF32:
                            return new HalfToFloatOperation(inputs[0]);
                        default:
                            throw new NotImplementedException();
                    }
                }
            case D3D10Opcode.LD:
            case D3D10Opcode.LDMS:
            case D3D10Opcode.LdUAVTyped:
                return CreateResourceLoadNode(instruction, componentIndex);
            case D3D10Opcode.ResInfo:
                return CreateResourceInfoNode(instruction, componentIndex);
            case D3D10Opcode.BufInfo:
                return CreateBufferInfoNode(instruction, componentIndex);
            case D3D10Opcode.EvalSampleIndex:
            case D3D10Opcode.EvalSnapped:
            case D3D10Opcode.EvalCentroid:
                {
                    // Place: a sample index, a two-component offset, or none (centroid).
                    EvaluateAttributeAt place = instruction.Opcode switch
                    {
                        D3D10Opcode.EvalSnapped => EvaluateAttributeAt.Snapped,
                        D3D10Opcode.EvalCentroid => EvaluateAttributeAt.Centroid,
                        _ => EvaluateAttributeAt.Sample,
                    };
                    bool isSnapped = place == EvaluateAttributeAt.Snapped;
                    HlslTreeNode value = GetInputs(instruction, componentIndex)[0];
                    // Immediate places are integers (a snapped offset is in 1/16 pixels).
                    const int PlaceParamIndex = 2;
                    int places = place switch
                    {
                        EvaluateAttributeAt.Snapped => 2,
                        EvaluateAttributeAt.Centroid => 0,
                        _ => 1,
                    };
                    HlslTreeNode[] at = places == 0
                        ? []
                        : instruction.GetOperandType(PlaceParamIndex) == OperandType.Immediate32
                        ? [.. Enumerable.Range(0, places)
                            .Select(component => (HlslTreeNode)new ConstantNode(
                                (int)instruction.GetParamInt(PlaceParamIndex, component)))]
                        : [.. Enumerable.Range(0, places)
                            .Select(component => GetInputs(instruction, component)[1])];
                    return new EvaluateAttributeOperation(value, at, place);
                }
            case D3D10Opcode.SampleInfo:
                return CreateSampleInfoNode(instruction, componentIndex);
            case D3D10Opcode.SamplePos:
                return CreateSamplePositionNode(instruction, componentIndex);
            case D3D10Opcode.Gather4:
            case D3D10Opcode.Gather4C:
            case D3D10Opcode.Gather4Po:
            case D3D10Opcode.Gather4PoC:
            case D3D10Opcode.Lod:
            case D3D10Opcode.Sample:
            case D3D10Opcode.SampleC:
            case D3D10Opcode.SampleCLZ:
            case D3D10Opcode.SampleL:
            case D3D10Opcode.SampleD:
            case D3D10Opcode.SampleB:
                return CreateTextureLoadOutputNode(instruction, componentIndex);
            case D3D10Opcode.Dp2:
            case D3D10Opcode.Dp3:
            case D3D10Opcode.Dp4:
                return CreateDotProductNode(instruction);
            default:
                throw new NotImplementedException($"{instruction.Opcode} not implemented");
        }
    }

    // ld dest, srcAddress, srcResource
    private ResourceLoadNode CreateResourceLoadNode(D3D10Instruction instruction, int outputComponent)
    {
        const int AddressParamIndex = 1;
        const int ResourceParamIndex = 2;

        // The resource swizzle picks the channel: `ld r0.z, r0.z, t2.x` reads red.
        var resource = GetInputComponents(instruction, ResourceParamIndex, 4)[outputComponent] as RegisterInputNode;
        bool isUnorderedAccessView = instruction.Opcode == D3D10Opcode.LdUAVTyped;
        ResourceDefinition definition = _registerState.ResourceDefinitions
            .Where(d => d.ShaderInputType == (isUnorderedAccessView
                ? D3DShaderInputType.UavRWTyped
                : D3DShaderInputType.Texture))
            .FirstOrDefault(d => d.BindPoint == resource.RegisterComponentKey.RegisterKey.Number);

        // Load appends the mip level to the coordinates (Texture2D: int3), except
        // for buffers, multisampled textures and UAVs.
        bool multisampled = instruction.Opcode == D3D10Opcode.LDMS;
        int addressLength = definition?.Dimension == ResourceDimension.Buffer
            ? 1
            : (definition?.GetDimensionSize() ?? 2)
                + (multisampled || isUnorderedAccessView ? 0 : 1);
        HlslTreeNode[] address = instruction.GetOperandType(AddressParamIndex) == OperandType.Immediate32
            ? [.. Enumerable.Range(0, addressLength)
                .Select(i => (HlslTreeNode)new ConstantNode((int)instruction.GetParamInt(AddressParamIndex, i)))]
            : GetInputComponents(instruction, AddressParamIndex, addressLength);

        const int SampleIndexParamIndex = 3;
        HlslTreeNode sampleIndex = !multisampled
            ? null
            : instruction.GetOperandType(SampleIndexParamIndex) == OperandType.Immediate32
                ? new ConstantNode((int)instruction.GetParamInt(SampleIndexParamIndex, 0))
                : GetInputComponents(instruction, SampleIndexParamIndex, 1)[0];

        return new ResourceLoadNode(resource, address, resource.RegisterComponentKey.ComponentIndex, sampleIndex)
        {
            SampleOffsets = instruction.SampleOffsets,
            IsIntegerTexel = definition?.IsIntegerReturnType,
            IsUnsignedTexel = definition?.ResourceReturnType == D3DResourceReturnType.UInt,
        };
    }

    /// <summary>
    /// bufinfo r0.x, t0: the element count (bytes for a raw buffer). No mip level.
    /// </summary>
    private ResourceInfoNode CreateBufferInfoNode(D3D10Instruction instruction, int outputComponent)
    {
        const int ResourceParamIndex = 1;
        // Only the component this lane selects: a structured buffer seeds as many
        // components as its stride holds, fewer than the four of `t2.xyzw`.
        var resourceKey = new RegisterComponentKey(instruction.GetParamRegisterKey(ResourceParamIndex),
            instruction.GetSourceSwizzleComponents(ResourceParamIndex)[outputComponent]);
        var resource = (ActiveOutputs.TryGetValue(resourceKey, out HlslTreeNode active) ? active : null)
            as RegisterInputNode ?? new RegisterInputNode(resourceKey);
        return new ResourceInfoNode(resource, new ConstantNode(0), outputComponent,
            D3D10ResInfoReturnType.Uint)
        {
            IsBuffer = true,
            ReportsStride = _registerState.IsStructuredResource(resource.RegisterComponentKey.RegisterKey),
        };
    }

    private ResourceInfoNode CreateResourceInfoNode(D3D10Instruction instruction, int outputComponent)
    {
        const int MipLevelParamIndex = 1;
        const int ResourceParamIndex = 2;

        // The resource swizzle picks the measurement per component.
        var resource = GetInputComponents(instruction, ResourceParamIndex, 4)[outputComponent] as RegisterInputNode;
        HlslTreeNode mipLevel = instruction.GetOperandType(MipLevelParamIndex) == OperandType.Immediate32
            ? new ConstantNode((int)instruction.GetParamInt(MipLevelParamIndex, 0))
            : GetInputComponents(instruction, MipLevelParamIndex, 1)[0];
        return new ResourceInfoNode(resource, mipLevel, outputComponent, instruction.ResInfoReturnType);
    }

    // sampleinfo dest, resource. Mip level 0 so it joins the resinfo of the same
    // GetDimensions call.
    private HlslTreeNode CreateSampleInfoNode(D3D10Instruction instruction, int outputComponent)
    {
        const int ResourceParamIndex = 1;

        // The rasterizer (render target) has no declaration to look up.
        if (instruction.GetOperandType(ResourceParamIndex) == OperandType.Rasterizer)
        {
            return new RenderTargetSampleCountNode(outputComponent);
        }

        var resource = GetInputComponents(instruction, ResourceParamIndex, 1)[0] as RegisterInputNode;
        ResourceDefinition definition = _registerState.ResourceDefinitions
            .Where(d => d.ShaderInputType == D3DShaderInputType.Texture)
            .FirstOrDefault(d => d.BindPoint == resource.RegisterComponentKey.RegisterKey.Number);
        bool isArray = definition?.Dimension == ResourceDimension.Texture2DmsArray;
        return new ResourceInfoNode(
            resource, new ConstantNode(0), outputComponent, instruction.ResInfoReturnType)
        {
            IsSampleCount = true,
            SampleCountComponent = isArray ? 3 : 2,
        };
    }

    // samplepos dest, resource, sampleIndex: a float2.
    private SamplePositionNode CreateSamplePositionNode(D3D10Instruction instruction, int outputComponent)
    {
        const int ResourceParamIndex = 1;
        const int SampleIndexParamIndex = 2;

        // The rasterizer is not seeded as a register; build the node from the operand.
        var resource = instruction.GetOperandType(ResourceParamIndex) == OperandType.Rasterizer
            ? new RegisterInputNode(
                GetParamRegisterComponentKey(instruction, ResourceParamIndex, outputComponent))
            : GetInputComponents(instruction, ResourceParamIndex, 4)[outputComponent]
                as RegisterInputNode;
        HlslTreeNode sampleIndex = instruction.GetOperandType(SampleIndexParamIndex) == OperandType.Immediate32
            ? new ConstantNode((int)instruction.GetParamInt(SampleIndexParamIndex, 0))
            : GetInputComponents(instruction, SampleIndexParamIndex, 1)[0];
        return new SamplePositionNode(resource, sampleIndex, outputComponent);
    }

    private TextureLoadOutputNode CreateTextureLoadOutputNode(Instruction instruction, int outputComponent)
    {
        const int TextureCoordsParamIndex = 1;

        if (instruction is D3D9Instruction d3D9Instruction)
        {
            const int SamplerParamIndex = 2;
            // SM1 tex has no sampler operand (the destination's number is the stage).
            if (_shaderModel.MajorVersion == 1)
            {
                throw new NotImplementedException(
                    $"{d3D9Instruction.Opcode} in a shader model 1 pixel shader, "
                    + "where the sampler is the register written rather than an operand");
            }
            var sampler = GetInputComponents(instruction, SamplerParamIndex, 1)[0] as RegisterInputNode;

            bool isBias = false;
            bool isLod = false;
            bool isGrad = false;
            bool isProj = false;
            if (d3D9Instruction.Opcode == Opcode.Tex)
            {
                isProj = d3D9Instruction.TexldControls.HasFlag(TexldControls.Project);
                isBias = d3D9Instruction.TexldControls.HasFlag(TexldControls.Bias);
            }
            else if (d3D9Instruction.Opcode == Opcode.TexLDL)
            {
                isLod = true;
            }
            else if (d3D9Instruction.Opcode == Opcode.TexLDD)
            {
                isGrad = true;
            }
            var samplerConstant = _registerState.FindConstant(RegisterSet.Sampler, sampler.RegisterComponentKey.RegisterKey.Number);
            int numSamplerOutputComponents = (isBias || isLod || isProj) ? 4 : samplerConstant.GetSamplerDimension();
            HlslTreeNode[] texCoords = GetInputComponents(instruction, TextureCoordsParamIndex, numSamplerOutputComponents);

            if (isBias)
            {
                return TextureLoadOutputNode.CreateBias(sampler, texCoords, outputComponent);
            }
            if (isGrad)
            {
                HlslTreeNode[] ddx = GetInputComponents(instruction, 3, numSamplerOutputComponents);
                HlslTreeNode[] ddy = GetInputComponents(instruction, 4, numSamplerOutputComponents);
                return TextureLoadOutputNode.CreateGrad(sampler, texCoords, outputComponent, ddx, ddy);
            }
            if (isLod)
            {
                return TextureLoadOutputNode.CreateLod(sampler, texCoords, outputComponent);
            }
            if (isProj)
            {
                return TextureLoadOutputNode.CreateProj(sampler, texCoords, outputComponent);
            }
            return TextureLoadOutputNode.Create(sampler, texCoords, outputComponent);
        }
        else
        {
            // gather4_po has an offset operand before the texture.
            bool hasProgrammableOffset = ((D3D10Instruction)instruction).Opcode
                is D3D10Opcode.Gather4Po or D3D10Opcode.Gather4PoC;
            int TextureParamIndex = hasProgrammableOffset ? 3 : 2;
            int SamplerParamIndex = hasProgrammableOffset ? 4 : 3;

            // The resource swizzle picks each component's channel (`t2.yzxw`).
            var textureComponents = GetInputComponents(instruction, TextureParamIndex, 4);
            var texture = textureComponents[outputComponent] as RegisterInputNode;
            var textureDefinition = _registerState.ResourceDefinitions
                .Where(d => d.ShaderInputType == D3DShaderInputType.Texture)
                .FirstOrDefault(d => d.BindPoint == texture.RegisterComponentKey.RegisterKey.Number);
            var sampler = GetInputComponents(instruction, SamplerParamIndex, 1)[0] as RegisterInputNode;
            var samplerDefinition = _registerState.ResourceDefinitions
                .Where(d => d.ShaderInputType == D3DShaderInputType.Sampler)
                .FirstOrDefault(d => d.BindPoint == sampler.RegisterComponentKey.RegisterKey.Number);

            int dimension = textureDefinition.GetDimensionSize();
            HlslTreeNode[] texCoords = GetInputComponents(instruction, TextureCoordsParamIndex, dimension);

            const int ExtraParamIndex = 4;
            TextureLoadControls controls = ((D3D10Instruction)instruction).Opcode switch
            {
                D3D10Opcode.SampleL => TextureLoadControls.Lod,
                D3D10Opcode.SampleB => TextureLoadControls.Bias,
                D3D10Opcode.SampleD => TextureLoadControls.Grad,
                D3D10Opcode.SampleC => TextureLoadControls.Compare,
                D3D10Opcode.SampleCLZ => TextureLoadControls.Compare | TextureLoadControls.LevelZero,
                D3D10Opcode.Gather4 => TextureLoadControls.Gather,
                D3D10Opcode.Gather4C => TextureLoadControls.Gather | TextureLoadControls.Compare,
                D3D10Opcode.Gather4Po =>
                    TextureLoadControls.Gather | TextureLoadControls.ProgrammableOffset,
                D3D10Opcode.Gather4PoC => TextureLoadControls.Gather
                    | TextureLoadControls.Compare | TextureLoadControls.ProgrammableOffset,
                D3D10Opcode.Lod => TextureLoadControls.CalculateLod,
                _ => TextureLoadControls.None,
            };
            // lod writes clamped in x and unclamped in y; the swizzle picks which.
            if (controls.HasFlag(TextureLoadControls.CalculateLod)
                && instruction.GetSourceSwizzleComponents(TextureParamIndex)[0] == 1)
            {
                controls |= TextureLoadControls.Unclamped;
            }
            HlslTreeNode[] derivativeX = null;
            HlslTreeNode[] derivativeY = null;
            HlslTreeNode scalarArgument = null;
            HlslTreeNode[] offsets = hasProgrammableOffset
                ? GetInputComponents(instruction, TextureParamIndex - 1, dimension)
                : null;
            if (controls.HasFlag(TextureLoadControls.Grad))
            {
                derivativeX = GetInputComponents(instruction, ExtraParamIndex, dimension);
                derivativeY = GetInputComponents(instruction, ExtraParamIndex + 1, dimension);
            }
            else if (controls.HasFlag(TextureLoadControls.Lod)
                || controls.HasFlag(TextureLoadControls.Bias)
                || controls.HasFlag(TextureLoadControls.Compare))
            {
                // Only these have a fifth operand; with a po offset it comes last.
                scalarArgument = GetInputComponents(
                    instruction, hasProgrammableOffset ? ExtraParamIndex + 1 : ExtraParamIndex, 1)[0];
            }

            TextureLoadOutputNode node = TextureLoadOutputNode.CreateSample(
                sampler, texCoords, outputComponent, texture,
                controls, derivativeX, derivativeY, scalarArgument, offsets);
            node.SampleOffsets = ((D3D10Instruction)instruction).SampleOffsets;
            node.IsIntegerTexel = textureDefinition.IsIntegerReturnType == true;
            return node;
        }
    }

    /// <summary>
    /// Whether a raw access reaches groupshared memory, which HLSL can only write as
    /// an array subscript.
    /// </summary>
    private static bool IsGroupSharedResource(D3D10Instruction instruction, int operandIndex)
    {
        return instruction.GetParamRegisterKey(operandIndex)
            is D3D10RegisterKey { OperandType: OperandType.ThreadGroupSharedMemory };
    }

    /// <summary>
    /// The dword element index (address / 4 + dword) of a raw groupshared access,
    /// folded when the address is a literal.
    /// </summary>
    private static HlslTreeNode GroupSharedElementOfByteAddress(HlslTreeNode byteAddress, int dword)
    {
        if (byteAddress is ConstantNode constant)
        {
            int bytes = constant.IntegerValue ?? (int)constant.Value;
            if (bytes % 4 == 0)
            {
                return new ConstantNode(bytes / 4 + dword);
            }
        }
        HlslTreeNode element = new ShiftRightOperation(byteAddress, new ConstantNode(2), isUnsigned: true);
        return dword == 0 ? element : new AddOperation(element, new ConstantNode(dword));
    }

    /// <summary>
    /// Marks an inner node as reading floats. CreateInstructionTree types only the
    /// root, so the products of a dot product would otherwise take the type of a
    /// reused register.
    /// </summary>
    private static T ReadingFloats<T>(T node) where T : HlslTreeNode
    {
        node.ConsumesInteger ??= false;
        return node;
    }

    private HlslTreeNode CreateDotProduct2AddNode(Instruction instruction)
    {
        var vector1 = GetInputComponents(instruction, 1, 2);
        var vector2 = GetInputComponents(instruction, 2, 2);
        var add = GetInputComponents(instruction, 3, 1)[0];

        var dp2 = ReadingFloats(new AddOperation(
            ReadingFloats(new MultiplyOperation(vector1[0], vector2[0])),
            ReadingFloats(new MultiplyOperation(vector1[1], vector2[1]))));

        return ReadingFloats(new AddOperation(dp2, add));
    }

    private HlslTreeNode CreateDotProductNode(D3D9Instruction instruction)
    {
        var addends = new List<HlslTreeNode>();
        int numComponents = instruction.Opcode == Opcode.Dp3 ? 3 : 4;
        for (int component = 0; component < numComponents; component++)
        {
            IList<HlslTreeNode> componentInput = GetInputs(instruction, component);
            var multiply = ReadingFloats(new MultiplyOperation(componentInput[0], componentInput[1]));
            addends.Add(multiply);
        }

        return addends.Aggregate((addition, addend) =>
            ReadingFloats(new AddOperation(addition, addend)));
    }

    // mNxM: component `row` is dot(v, c[base + row]) over N columns. The matrix
    // operand is read without its swizzle.
    private HlslTreeNode CreateMatrixProductNode(D3D9Instruction instruction, int row)
    {
        int columns = instruction.Opcode is Opcode.M4x3 or Opcode.M4x4 ? 4 : 3;
        var baseKey = (D3D9RegisterKey)instruction.GetParamRegisterKey(2);
        var rowKey = new D3D9RegisterKey(baseKey.Type, baseKey.Number + row);
        SourceModifier matrixModifier = instruction.GetSourceModifier(2);

        HlslTreeNode[] vector = GetInputComponents(instruction, 1, columns);
        var addends = new List<HlslTreeNode>();
        for (int component = 0; component < columns; component++)
        {
            HlslTreeNode matrix = ApplyModifier(
                GetActiveOutput(new RegisterComponentKey(rowKey, component)), matrixModifier);
            addends.Add(ReadingFloats(new MultiplyOperation(vector[component], matrix)));
        }

        return addends.Aggregate((addition, addend) =>
            ReadingFloats(new AddOperation(addition, addend)));
    }

    private HlslTreeNode CreateCrossProductNode(D3D9Instruction instruction, int componentIndex)
    {
        if (componentIndex >= 3)
        {
            return new ConstantNode(0);
        }

        int next = (componentIndex + 1) % 3;
        int after = (componentIndex + 2) % 3;
        HlslTreeNode[] a = GetInputComponents(instruction, 1, 3);
        HlslTreeNode[] b = GetInputComponents(instruction, 2, 3);
        return ReadingFloats(new SubtractOperation(
            ReadingFloats(new MultiplyOperation(a[next], b[after])),
            ReadingFloats(new MultiplyOperation(a[after], b[next]))));
    }

    private HlslTreeNode CreateDotProductNode(D3D10Instruction instruction)
    {
        var numComponents = instruction.Opcode switch
        {
            D3D10Opcode.Dp2 => 2,
            D3D10Opcode.Dp3 => 3,
            D3D10Opcode.Dp4 => 4,
            _ => throw new InvalidOperationException(),
        };
        var operands = new HlslTreeNode[numComponents][];
        for (int component = 0; component < numComponents; component++)
        {
            operands[component] = GetInputs(instruction, component);
        }

        // An operand of literal and computed lanes is a vector fxc filled in with
        // movs (float4(x, 1, 2, k) against a matrix row): the sum of products would
        // lose the dot once `1 * b` folds. Trailing 1s (float4(xyz, 1)) fold where
        // the dot templates look for them, so those stay products.
        // So is one whose operand is all literal weights, `dp3 r.x, c.xzy, l(.25, .25, .5)`:
        // as products, equal weights block the dot templates and the lanes of two
        // such dots regroup as one vector expression costing a mul, add and mad.
        if (Enumerable.Range(0, 2).Any(side => IsFilledInVector([.. operands.Select(lanes => lanes[side])])
                || IsLiteralWeights([.. operands.Select(lanes => lanes[side])])))
        {
            return ReadingFloats(new DotProductOperation(
                new GroupNode([.. operands.Select(lanes => lanes[0])]),
                new GroupNode([.. operands.Select(lanes => lanes[1])])));
        }

        return operands
            .Select(componentInput => (HlslTreeNode)ReadingFloats(
                new MultiplyOperation(componentInput[0], componentInput[1])))
            .Aggregate((addition, addend) => ReadingFloats(new AddOperation(addition, addend)));
    }

    // Every lane a literal, not all alike (a repeated weight is a scaled sum)
    // and none 0 or 1, which fold.
    private static bool IsLiteralWeights(HlslTreeNode[] lanes)
    {
        return lanes.All(lane => lane is ConstantNode { Value: not 0 })
            && lanes.Select(lane => ((ConstantNode)lane).Value).Distinct().Count() > 1;
    }

    private static bool IsFilledInVector(HlslTreeNode[] lanes)
    {
        static ConstantNode Literal(HlslTreeNode lane) =>
            lane as ConstantNode ?? (lane as MoveOperation)?.Inputs[0] as ConstantNode;
        // A moved immediate is still its bits here.
        static bool IsOne(ConstantNode literal) =>
            literal.Value == 1 || literal.IntegerValue == 0x3F800000;
        int lastComputed = Array.FindLastIndex(lanes, lane => Literal(lane) == null);
        if (lastComputed < 0 || lanes.All(lane => Literal(lane) == null))
        {
            return false;
        }
        return lanes.Select((lane, index) => (Literal: Literal(lane), Index: index))
            .Any(lane => lane.Literal != null && (lane.Index < lastComputed || !IsOne(lane.Literal)));
    }

    // `lit src` reads x (n.l), y (n.h) and w (power) for every component.
    private HlslTreeNode CreateLitOutputNode(D3D9Instruction instruction, int outputComponent)
    {
        return new LitOutputNode(
            GetInputs(instruction, 0)[0],
            GetInputs(instruction, 1)[0],
            GetInputs(instruction, 3)[0],
            outputComponent);
    }

    private HlslTreeNode CreateNormalizeOutputNode(D3D9Instruction instruction, int outputComponent)
    {
        var inputs = new List<HlslTreeNode>();
        for (int component = 0; component < 3; component++)
        {
            IList<HlslTreeNode> componentInput = GetInputs(instruction, component);
            inputs.AddRange(componentInput);
        }

        return new NormalizeOutputNode(inputs, outputComponent);
    }

    private HlslTreeNode[] GetInputs(D3D9Instruction instruction, int componentIndex)
    {
        int numInputs = GetNumInputs(instruction.Opcode);
        var inputs = new HlslTreeNode[numInputs];
        int parameterIndex = instruction.Opcode.HasDestination() ? 1 : 0;
        for (int i = 0; i < numInputs; i++)
        {
            RegisterComponentKey inputKey = GetParamRegisterComponentKey(instruction, parameterIndex, componentIndex);
            SourceModifier modifier = instruction.GetSourceModifier(parameterIndex);
            HlslTreeNode input = instruction.Params.HasRelativeAddressing(parameterIndex)
                ? GetRelativeAddressInput(instruction, parameterIndex, inputKey)
                : GetActiveOutput(inputKey);
            inputs[i] = ApplyModifier(input, modifier);
            parameterIndex++;
        }
        return inputs;
    }

    // icb[r0.x + 0]: one index, no buffer number.
    private HlslTreeNode GetImmediateConstantBufferInput(
        D3D10Instruction instruction,
        int operandIndex,
        int componentIndex,
        D3D10OperandTokenCollection.OperandIndex[] operandIndices)
    {
        (OperandType indexType, int indexNumber, byte indexComponent) =
            instruction.OperandTokens.GetRelativeIndexOperand(operandIndex, 0);
        HlslTreeNode index = GetActiveOutput(new RegisterComponentKey(
            new D3D10RegisterKey(indexType, indexNumber), indexComponent));

        var registerKey = new D3D10RegisterKey(
            OperandType.ImmediateConstantBuffer, (int)operandIndices[0].Immediate);
        _registerState.DeclareImmediateConstantBufferRead(registerKey.Number);
        if (IsScaledIndex(index))
        {
            _registerState.MarkImmediateConstantBufferStrided();
        }
        byte[] swizzle = instruction.GetSourceSwizzleComponents(operandIndex);
        return new RelativeAddressNode(
            new RegisterComponentKey(registerKey, swizzle[componentIndex]), index);
    }

    // An index times a constant above one (a shift counts): a struct's stride.
    private static bool IsScaledIndex(HlslTreeNode index)
    {
        while (index is MoveOperation move)
        {
            index = move.Inputs[0];
        }
        return index switch
        {
            ShiftLeftOperation shift => shift.Inputs[1] is ConstantNode { IntegerValue: > 0 },
            MultiplyOperation multiply => multiply.Inputs.Any(input => input is ConstantNode constant
                && (constant.IntegerValue ?? (int)constant.Value) > 1),
            _ => false,
        };
    }

    // cb0[r0.x + 1]: the immediate is where element zero of the read would sit.
    private HlslTreeNode GetDynamicConstantBufferInput(
        D3D10Instruction instruction,
        int operandIndex,
        int componentIndex,
        D3D10OperandTokenCollection.OperandIndex[] operandIndices)
    {
        const int ElementIndex = 1;
        (OperandType indexType, int indexNumber, byte indexComponent) =
            instruction.OperandTokens.GetRelativeIndexOperand(operandIndex, ElementIndex);
        HlslTreeNode index = GetActiveOutput(new RegisterComponentKey(
            new D3D10RegisterKey(indexType, indexNumber), indexComponent));

        var registerKey = new D3D10RegisterKey(
            OperandType.ConstantBuffer,
            (int)operandIndices[0].Immediate,
            (int)operandIndices[ElementIndex].Immediate);
        byte[] swizzle = instruction.GetSourceSwizzleComponents(operandIndex);
        var componentKey = new RegisterComponentKey(registerKey, swizzle[componentIndex]);

        // Indexed by registers; ParsedIdioms removes a matrix-rows multiplier.
        return new RelativeAddressNode(componentKey, index);
    }

    // v[r0.x][0]: a dynamically indexed vertex of a GS/HS input.
    private HlslTreeNode GetDynamicVertexInput(
        D3D10Instruction instruction,
        int operandIndex,
        int componentIndex,
        D3D10OperandTokenCollection.OperandIndex[] operandIndices)
    {
        const int VertexIndex = 0;
        (OperandType indexType, int indexNumber, byte indexComponent) =
            instruction.OperandTokens.GetRelativeIndexOperand(operandIndex, VertexIndex);
        HlslTreeNode index = GetActiveOutput(new RegisterComponentKey(
            new D3D10RegisterKey(indexType, indexNumber), indexComponent));

        byte[] swizzle = instruction.GetSourceSwizzleComponents(operandIndex);
        // A VS dcl_indexrange read `v[r0.x + base]` has one index: the register.
        if (operandIndices.Length == 1)
        {
            return new RelativeAddressNode(
                new RegisterComponentKey(
                    new D3D10RegisterKey(OperandType.Input, (int)operandIndices[0].Immediate),
                    swizzle[componentIndex]),
                index);
        }

        // Add the immediate: `vicp[r0.x + 1]`.
        if (operandIndices[VertexIndex].Immediate != 0)
        {
            index = new AddOperation(index, new ConstantNode((int)operandIndices[VertexIndex].Immediate))
            {
                ConsumesInteger = true,
            };
        }

        // Vertex 0 stands for all; control point outputs are keyed apart from inputs.
        int register = (int)operandIndices[1].Immediate;
        var registerKey = instruction.GetOperandType(operandIndex) == OperandType.OutputControlPoint
            ? D3D10RegisterKey.CreateControlPointOutput(register, 0)
            : D3D10RegisterKey.CreateGSInput(register, 0);
        return new RelativeAddressNode(
            new RegisterComponentKey(registerKey, swizzle[componentIndex]), index);
    }

    // vpc[r0.x + 4]: a dcl_indexrange patch constant array read at a computed
    // index (`constants.weights[k]` in a domain shader).
    private HlslTreeNode GetDynamicPatchConstantInput(
        D3D10Instruction instruction,
        int operandIndex,
        int componentIndex,
        D3D10OperandTokenCollection.OperandIndex[] operandIndices)
    {
        (OperandType indexType, int indexNumber, byte indexComponent) =
            instruction.OperandTokens.GetRelativeIndexOperand(operandIndex, 0);
        HlslTreeNode index = GetActiveOutput(new RegisterComponentKey(
            new D3D10RegisterKey(indexType, indexNumber), indexComponent));

        byte[] swizzle = instruction.GetSourceSwizzleComponents(operandIndex);
        return new RelativeAddressNode(
            new RegisterComponentKey(
                new D3D10RegisterKey(OperandType.InputPatchConstant, (int)operandIndices[0].Immediate),
                swizzle[componentIndex]),
            index);
    }

    // The shared aL node, so that the components of c0[aL] group together.
    private LoopCounterNode _loopCounter;

    // Seed every component of a structured element before the first load.
    private void SeedResourceComponents(D3D10RegisterKey registerKey)
    {
        int components = _registerState.GetStructuredBufferComponents(registerKey);
        for (int component = 0; component < components; component++)
        {
            var destinationKey = new RegisterComponentKey(registerKey, component);
            SetActiveOutput(destinationKey, new RegisterInputNode(destinationKey));
        }
    }

    private HlslTreeNode GetRelativeAddressInput(
        D3D9Instruction instruction, int parameterIndex, RegisterComponentKey inputKey)
    {
        RegisterType relativeType = instruction.GetRelativeParamRegisterType(parameterIndex);
        _registerState.MarkIndexedConstant(inputKey.RegisterKey);
        if (relativeType == RegisterType.Loop)
        {
            _loopCounter ??= new LoopCounterNode();
            return new RelativeAddressNode(inputKey, _loopCounter);
        }
        if (relativeType != RegisterType.Addr)
        {
            throw new NotImplementedException(
                $"Relative addressing through {relativeType} in {instruction.Opcode}");
        }

        var addressKey = new RegisterComponentKey(
            relativeType,
            instruction.GetRelativeParamRegisterNumber(parameterIndex),
            instruction.GetRelativeParamComponent(parameterIndex));
        HlslTreeNode address = GetActiveOutput(addressKey);

        // Indexed by registers; ParsedIdioms removes a matrix-rows multiplier.
        return new RelativeAddressNode(inputKey, address);
    }

    // The element an x# operand names: literal, register, or register + literal.
    private HlslTreeNode GetIndexableTempElementIndex(D3D10Instruction instruction, int operandIndex)
    {
        const int ElementIndex = 1;
        D3D10OperandTokenCollection.OperandIndex element =
            instruction.OperandTokens.GetOperandIndices(operandIndex)[ElementIndex];
        if (!element.IsRelative)
        {
            return new ConstantNode((int)element.Immediate);
        }
        (OperandType indexType, int indexNumber, byte indexComponent) =
            instruction.OperandTokens.GetRelativeIndexOperand(operandIndex, ElementIndex);
        HlslTreeNode index = GetActiveOutput(new RegisterComponentKey(
            new D3D10RegisterKey(indexType, indexNumber), indexComponent));
        return element.Immediate == 0
            ? index
            : new AddOperation(index, new ConstantNode((int)element.Immediate));
    }

    private void InsertIndexableTempStore(D3D10Instruction instruction)
    {
        int destinationIndex = instruction.GetDestinationParamIndex().Value;
        int register = (int)instruction.OperandTokens.GetOperandIndices(destinationIndex)[0].Immediate;
        HlslTreeNode index = GetIndexableTempElementIndex(instruction, destinationIndex);
        RegisterComponentKey[] destinationKeys = GetDestinationKeys(instruction).ToArray();
        HlslTreeNode[] values = [.. destinationKeys.Select(key =>
        {
            // No MoveOperation wrapper, so a store-only value can be written inline.
            HlslTreeNode value = instruction.Opcode == D3D10Opcode.Mov
                ? GetInputs(instruction, key.ComponentIndex)[0]
                : CreateInstructionTree(instruction, key);
            return instruction.Saturate ? new SaturateOperation(value) : value;
        })];
        InsertStatement(new IndexableTempStoreStatement(
            register, index, [.. destinationKeys.Select(key => key.ComponentIndex)], values, ActiveOutputs)
        {
            IsIntegerElement = _integerOperandAnalysis.IsIntegerIndexableTemp(register),
        });
    }

    private HlslTreeNode[] GetInputs(D3D10Instruction instruction, int componentIndex)
    {
        int numInputs = GetNumInputs(instruction.Opcode);
        var inputs = new HlslTreeNode[numInputs];
        // With doubles the value ordinal differs from the component (dtof's .y
        // reads the double at .zw).
        int destinationComponent = componentIndex;
        int valueOrdinal = instruction.HasDoubleOperands
            ? instruction.GetValueOrdinal(componentIndex)
            : componentIndex;
        for (int i = 0; i < numInputs; i++)
        {
            int inputParameterIndex = i + 1;
            var operandType = instruction.GetOperandType(inputParameterIndex);
            if (instruction.HasDoubleOperands)
            {
                // Double writes read the destination's own pair; other double
                // instructions read one pair per value.
                componentIndex = instruction.IsDoubleOperand(inputParameterIndex)
                    ? instruction.WritesDoubles
                        ? destinationComponent
                        : valueOrdinal * 2
                    : valueOrdinal;
            }
            D3D10OperandTokenCollection.OperandIndex[] operandIndices =
                instruction.OperandTokens.GetOperandIndices(inputParameterIndex);
            if (operandType == OperandType.IndexableTemp)
            {
                byte[] swizzle = instruction.GetSourceSwizzleComponents(inputParameterIndex);
                var load = new IndexableTempLoadNode(
                    (int)operandIndices[0].Immediate,
                    GetIndexableTempElementIndex(instruction, inputParameterIndex),
                    swizzle[componentIndex])
                {
                    IsIntegerElement = _integerOperandAnalysis.IsIntegerIndexableTemp((int)operandIndices[0].Immediate),
                };
                inputs[i] = ApplyModifier(load, instruction.GetOperandModifier(inputParameterIndex));
                continue;
            }
            // A patch constant indexed by an unrolled phase's instance is a constant
            // index in each copy: `vpc[r0.x + 2]` with r0.x the join instance.
            if (operandType == OperandType.InputPatchConstant
                && operandIndices.Length == 1 && operandIndices[0].IsRelative
                && RelativeIndexConstant(instruction, inputParameterIndex) is int instanceOffset)
            {
                byte[] patchSwizzle = instruction.GetSourceSwizzleComponents(inputParameterIndex);
                var patchKey = new D3D10RegisterKey(operandType, (int)operandIndices[0].Immediate + instanceOffset);
                inputs[i] = ApplyModifier(
                    GetActiveOutput(new RegisterComponentKey(patchKey, patchSwizzle[componentIndex])),
                    instruction.GetOperandModifier(inputParameterIndex));
                continue;
            }
            if (_registerState.Linkage != null && _registerState.Linkage.TryGetClassData(
                instruction, inputParameterIndex, out LinkageModel.ClassInfo classInfo, out int row))
            {
                byte[] memberSwizzle = instruction.GetSourceSwizzleComponents(inputParameterIndex);
                var dataKey = new D3D10RegisterKey(OperandType.ConstantBuffer, classInfo.DataRegister, row);
                inputs[i] = ApplyModifier(
                    GetActiveOutput(new RegisterComponentKey(dataKey, memberSwizzle[componentIndex])),
                    instruction.GetOperandModifier(inputParameterIndex));
                continue;
            }
            if (operandIndices.Any(index => index.IsRelative))
            {
                inputs[i] = operandType switch
                {
                    OperandType.ConstantBuffer => GetDynamicConstantBufferInput(
                        instruction, inputParameterIndex, componentIndex, operandIndices),
                    OperandType.ImmediateConstantBuffer => GetImmediateConstantBufferInput(
                        instruction, inputParameterIndex, componentIndex, operandIndices),
                    OperandType.Input or OperandType.InputControlPoint or OperandType.OutputControlPoint
                        => GetDynamicVertexInput(
                            instruction, inputParameterIndex, componentIndex, operandIndices),
                    OperandType.InputPatchConstant => GetDynamicPatchConstantInput(
                        instruction, inputParameterIndex, componentIndex, operandIndices),
                    _ => throw new NotImplementedException(
                        $"Dynamically indexed {operandType} in {instruction.Opcode}"),
                };
                inputs[i] = ApplyModifier(
                    inputs[i], instruction.GetOperandModifier(inputParameterIndex));
                continue;
            }
            if (operandType is OperandType.InputForkInstanceID or OperandType.InputJoinInstanceID)
            {
                // With its modifier: `iadd r0.x, -vJoinInstanceID.x, l(1)` is 1 - id.
                inputs[i] = ApplyModifier(new ConstantNode(instruction.ForkInstance ?? 0),
                    instruction.GetOperandModifier(inputParameterIndex));
                continue;
            }
            if (operandType == OperandType.Immediate64)
            {
                byte[] doubleSwizzle = instruction.GetSourceSwizzleComponents(inputParameterIndex);
                var doubleConstant = new DoubleConstantNode(instruction.GetParamDouble(
                    inputParameterIndex, doubleSwizzle[componentIndex] / 2));
                _doubleValues.Add(doubleConstant);
                inputs[i] = ApplyModifier(
                    doubleConstant, instruction.GetOperandModifier(inputParameterIndex));
                continue;
            }
            if (operandType == OperandType.Immediate32)
            {
                // Typed by the consuming instruction; mov immediates are retyped
                // later by their readers. Denormal/NaN bits in a bitwise op and
                // addresses are integers, and so is the sign bit alone: -0.0 would
                // print as 0.
                uint immediateBits = (uint)instruction.GetParamInt(inputParameterIndex, componentIndex);
                bool notAFloat = (instruction.Opcode is D3D10Opcode.And or D3D10Opcode.Or or D3D10Opcode.Xor
                        && (IntegerOperandAnalysis.IsDenormalOrNaN(immediateBits) || immediateBits == 0x80000000))
                    || instruction.IsAddressOperand(inputParameterIndex);
                var constant = _integerOperandAnalysis.IsIntegerOperand(instruction) || notAFloat
                    ? new ConstantNode((int)immediateBits)
                    : new ConstantNode(instruction.GetParamSingle(inputParameterIndex, componentIndex));
                if (instruction.Opcode is D3D10Opcode.Mov or D3D10Opcode.MovC)
                {
                    _polymorphicImmediates.Add((constant, (uint)instruction.GetParamInt(inputParameterIndex, componentIndex)));
                }
                inputs[i] = constant;
            }
            else
            {
                var inputKey = GetParamRegisterComponentKey(instruction, inputParameterIndex, componentIndex);
                HlslTreeNode input = GetActiveOutput(inputKey);
                if (instruction.IsDoubleOperand(inputParameterIndex))
                {
                    input = ReadDoublePair(instruction, inputParameterIndex, componentIndex, input);
                }
                D3D10OperandModifier modifier = instruction.GetOperandModifier(inputParameterIndex);
                input = ApplyModifier(input, modifier);
                inputs[i] = input;
            }
        }
        return inputs;
    }

    /// <summary>
    /// The double a register pair holds: the lower value if already a double, else
    /// asdouble(low, high) of the two components (an implicit, free join).
    /// </summary>
    private HlslTreeNode ReadDoublePair(
        D3D10Instruction instruction, int inputParameterIndex, int componentIndex, HlslTreeNode low)
    {
        RegisterComponentKey lowKey = GetParamRegisterComponentKey(
            instruction, inputParameterIndex, componentIndex);
        if (HoldsDouble(lowKey, low))
        {
            return low;
        }
        // The swizzle repeats the pair (.xyxy), so the high word is at slot n + 1.
        RegisterComponentKey highKey = GetParamRegisterComponentKey(
            instruction, inputParameterIndex, componentIndex + 1);
        HlslTreeNode high = GetActiveOutput(highKey);
        // One join per pair of values, so its readers share it like any value.
        if (!_joinedDoubles.TryGetValue(low, out List<BitsToDoubleOperation> joins))
        {
            joins = [];
            _joinedDoubles[low] = joins;
        }
        if (joins.FirstOrDefault(j => ReferenceEquals(j.Inputs[1], high)) is BitsToDoubleOperation shared)
        {
            return shared;
        }
        var joined = new BitsToDoubleOperation(low, high);
        _doubleValues.Add(joined);
        joins.Add(joined);
        return joined;
    }

    private readonly Dictionary<AppendSlotNode, BufferAppendStatement> _appendsBySlot =
        new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<HlslTreeNode, List<BitsToDoubleOperation>> _joinedDoubles =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Where a store writes both halves of a double as dwords, makes the lower one
    /// the low word. A double member keys only one value and is left alone.
    /// </summary>
    private void SplitStoredDoubles(HlslTreeNode[] values)
    {
        for (int value = 0; value + 1 < values.Length; value++)
        {
            if (_doubleValues.Contains(values[value])
                && values[value + 1] is DoubleBitsNode { ComponentIndex: 1 } high
                && ReferenceEquals(high.Value, values[value]))
            {
                values[value] = new DoubleBitsNode(values[value], 0);
            }
        }
    }

    /// <summary>
    /// Whether the pair a component begins is already a double: recorded as one, or
    /// a constant buffer variable declared double (`cb0[0].zw`).
    /// </summary>
    private bool HoldsDouble(RegisterComponentKey registerComponent, HlslTreeNode value)
    {
        return _doubleValues.Contains(value)
            || _registerState.GetConstantComponentsPerElement(registerComponent) == 2;
    }

    private HlslTreeNode[] GetInputComponents(Instruction instruction, int inputParameterIndex, int numComponents)
    {
        var components = new HlslTreeNode[numComponents];
        for (int i = 0; i < numComponents; i++)
        {
            RegisterComponentKey inputKey = GetParamRegisterComponentKey(instruction, inputParameterIndex, i);
            HlslTreeNode input = GetActiveOutput(inputKey);
            if (instruction is D3D9Instruction d9Instruction)
            {
                var modifier = d9Instruction.GetSourceModifier(inputParameterIndex);
                input = ApplyModifier(input, modifier);
            }
            components[i] = input;
        }
        return components;
    }

    private static HlslTreeNode ApplyModifier(HlslTreeNode input, SourceModifier modifier)
    {
        return modifier switch
        {
            SourceModifier.Abs => new AbsoluteOperation(input),
            SourceModifier.Negate => new NegateOperation(input),
            SourceModifier.AbsAndNegate => new NegateOperation(new AbsoluteOperation(input)),
            SourceModifier.None => input,
            _ => throw new NotImplementedException(),
        };
    }

    private HlslTreeNode ApplyModifier(HlslTreeNode input, ResultModifier modifier)
    {
        HlslTreeNode result = input;
        if ((modifier & ResultModifier.Saturate) != 0)
        {
            result = new SaturateOperation(result);
        }
        if ((modifier & ResultModifier.PartialPrecision) != 0)
        {
            RegisterDeclaration declaration = input is RegisterInputNode registerInput
                ? _registerState.MethodInputRegisters.FirstOrDefault(
                    d => d.RegisterKey.Equals(registerInput.RegisterComponentKey.RegisterKey))
                : null;
            bool inputHasPartialPrecision = declaration != null
                && declaration.ResultModifier.HasFlag(ResultModifier.PartialPrecision);
            if (!inputHasPartialPrecision)
            {
                // ConvertOperation sizes the cast to what it is converting;
                // a fixed half4 over a two component result is X3014.
                result = new ConvertOperation(result, "half");
            }
        }
        return result;
    }

    private static HlslTreeNode ApplyModifier(HlslTreeNode input, D3D10OperandModifier modifier)
    {
        HlslTreeNode node = input;
        if (modifier.HasFlag(D3D10OperandModifier.Abs))
        {
            node = new AbsoluteOperation(node);
        }
        if (modifier.HasFlag(D3D10OperandModifier.Neg))
        {
            node = new NegateOperation(node);
        }
        return node;
    }

    private static int GetNumInputs(Opcode opcode)
    {
        switch (opcode)
        {
            case Opcode.Abs:
            case Opcode.CallNZ:
            case Opcode.DSX:
            case Opcode.DSY:
            case Opcode.Exp:
            case Opcode.ExpP:
            case Opcode.Frc:
            case Opcode.Lit:
            case Opcode.Log:
            case Opcode.LogP:
            case Opcode.Loop:
            case Opcode.Mov:
            case Opcode.MovA:
            case Opcode.Nrm:
            case Opcode.Rcp:
            case Opcode.Rsq:
            case Opcode.SinCos:
            case Opcode.TexKill:
            case Opcode.If:
                return 1;
            case Opcode.Add:
            case Opcode.Bem:
            case Opcode.Crs:
            case Opcode.Dp3:
            case Opcode.Dp4:
            case Opcode.Dst:
            case Opcode.M3x2:
            case Opcode.M3x3:
            case Opcode.M3x4:
            case Opcode.M4x3:
            case Opcode.M4x4:
            case Opcode.Max:
            case Opcode.Min:
            case Opcode.Mul:
            case Opcode.Pow:
            case Opcode.SetP:
            case Opcode.Sge:
            case Opcode.Slt:
            case Opcode.Sub:
            case Opcode.Tex:
            case Opcode.TexLDD:
            case Opcode.TexLDL:
            case Opcode.BreakC:
            case Opcode.IfC:
                return 2;
            case Opcode.Cmp:
            case Opcode.Cnd:
            case Opcode.DP2Add:
            case Opcode.Lrp:
            case Opcode.Mad:
            case Opcode.Sgn:
                return 3;
            default:
                throw new NotImplementedException(opcode.ToString());
        }
    }

    private static int GetNumInputs(D3D10Opcode opcode)
    {
        switch (opcode)
        {
            case D3D10Opcode.DerivRtx:
            case D3D10Opcode.DerivRty:
            case D3D10Opcode.DerivRtxCoarse:
            case D3D10Opcode.DerivRtxFine:
            case D3D10Opcode.DerivRtyCoarse:
            case D3D10Opcode.DerivRtyFine:
            case D3D10Opcode.Rcp:
            case D3D10Opcode.Exp:
            case D3D10Opcode.Frc:
            case D3D10Opcode.Ftoi:
            case D3D10Opcode.Ftou:
            case D3D10Opcode.INeg:
            case D3D10Opcode.Not:
            case D3D10Opcode.RoundNe:
            case D3D10Opcode.RoundNi:
            case D3D10Opcode.RoundPi:
            case D3D10Opcode.RoundZ:
            case D3D10Opcode.IToF:
            case D3D10Opcode.UTof:
            case D3D10Opcode.Log:
            case D3D10Opcode.Mov:
            case D3D10Opcode.Rsq:
            case D3D10Opcode.Sqrt:
            case D3D10Opcode.SinCos:
            case D3D10Opcode.CountBits:
            case D3D10Opcode.FirstBitLo:
            case D3D10Opcode.FirstBitHi:
            case D3D10Opcode.FirstBitSHi:
            case D3D10Opcode.BFRev:
            case D3D10Opcode.F32ToF16:
            case D3D10Opcode.F16ToF32:
            case D3D10Opcode.DMov:
            case D3D10Opcode.DRCP:
            case D3D10Opcode.DToF:
            case D3D10Opcode.DToI:
            case D3D10Opcode.DToU:
            case D3D10Opcode.FToD:
            case D3D10Opcode.IToD:
            case D3D10Opcode.UToD:
                return 1;
            case D3D10Opcode.Add:
            case D3D10Opcode.Dp2:
            case D3D10Opcode.Dp3:
            case D3D10Opcode.Dp4:
            case D3D10Opcode.And:
            case D3D10Opcode.Xor:
            case D3D10Opcode.Div:
            case D3D10Opcode.Eq:
            case D3D10Opcode.GE:
            case D3D10Opcode.LT:
            case D3D10Opcode.Ne:
            case D3D10Opcode.Or:
            case D3D10Opcode.IAdd:
            case D3D10Opcode.IShl:
            case D3D10Opcode.IShr:
            case D3D10Opcode.UShr:
            case D3D10Opcode.Ieq:
            case D3D10Opcode.Ige:
            case D3D10Opcode.UGE:
            case D3D10Opcode.ULT:
            case D3D10Opcode.Ilt:
            case D3D10Opcode.IMax:
            case D3D10Opcode.IMin:
            case D3D10Opcode.UMax:
            case D3D10Opcode.UMin:
            case D3D10Opcode.Ine:
            case D3D10Opcode.Max:
            case D3D10Opcode.Min:
            case D3D10Opcode.Mul:
            case D3D10Opcode.DAdd:
            case D3D10Opcode.DMul:
            case D3D10Opcode.DDiv:
            case D3D10Opcode.DMax:
            case D3D10Opcode.DMin:
            case D3D10Opcode.DEq:
            case D3D10Opcode.DGe:
            case D3D10Opcode.DLt:
            case D3D10Opcode.DNe:
                return 2;
            case D3D10Opcode.AtomicIAdd:
            case D3D10Opcode.AtomicAnd:
            case D3D10Opcode.AtomicOr:
            case D3D10Opcode.AtomicXor:
            case D3D10Opcode.AtomicIMax:
            case D3D10Opcode.AtomicIMin:
            case D3D10Opcode.AtomicUMax:
            case D3D10Opcode.AtomicUMin:
                return 2;
            case D3D10Opcode.ImmAtomicIAdd:
            case D3D10Opcode.ImmAtomicAnd:
            case D3D10Opcode.ImmAtomicOr:
            case D3D10Opcode.ImmAtomicXor:
            case D3D10Opcode.ImmAtomicIMax:
            case D3D10Opcode.ImmAtomicIMin:
            case D3D10Opcode.ImmAtomicUMax:
            case D3D10Opcode.ImmAtomicUMin:
            case D3D10Opcode.ImmAtomicExch:
                // Resource, address, value: the resource counts only so later
                // operands line up.
                return 3;
            case D3D10Opcode.ImmAtomicCmpExch:
            case D3D10Opcode.BFI:
                return 4;
            case D3D10Opcode.StoreUAVTyped:
                return 2;
            case D3D10Opcode.LdUAVTyped:
                return 2;
            case D3D10Opcode.EvalSampleIndex:
            case D3D10Opcode.EvalSnapped:
                return 2;
            case D3D10Opcode.EvalCentroid:
                return 1;
            case D3D10Opcode.UBFE:
            case D3D10Opcode.IBFE:
            case D3D10Opcode.IMad:
            case D3D10Opcode.Umad:
            case D3D10Opcode.Mad:
            case D3D10Opcode.MovC:
            case D3D10Opcode.LdStructured:
            case D3D10Opcode.StoreStructured:
            case D3D10Opcode.AtomicCmpStore:
            case D3D10Opcode.MSAD:
            case D3D10Opcode.DMovC:
            case D3D10Opcode.DFMA:
                return 3;
            case D3D10Opcode.LDMS:
                return 3;
            case D3D10Opcode.ResInfo:
            case D3D10Opcode.LdRaw:
            case D3D10Opcode.StoreRaw:
            case D3D10Opcode.SamplePos:
                return 2;
            case D3D10Opcode.SampleInfo:
                return 1;
            default:
                throw new NotImplementedException();
        }
    }

    private RegisterComponentKey GetParamRegisterComponentKey(Instruction instruction, int paramIndex, int component)
    {
        RegisterKey registerKey = instruction.GetParamRegisterKey(paramIndex);

        int componentIndex;
        if (registerKey is D3D9RegisterKey d3D9RegisterKey && d3D9RegisterKey.Type == RegisterType.MiscType && d3D9RegisterKey.Number == 1)
        {
            componentIndex = 0; // Force VFACE x component
        }
        else if (instruction is D3D10Instruction d3d10Instruction
            && d3d10Instruction.GetOperandComponentSelection(paramIndex) == D3D10OperandNumComponents.Operand1Component)
        {
            // A scalar operand (vPrim) is x for every component.
            componentIndex = 0;
        }
        else
        {
            byte[] swizzle = instruction.GetSourceSwizzleComponents(paramIndex);
            componentIndex = swizzle[component];
        }
        // A join phase's vpc6 is what a fork phase wrote to o6 (one function here).
        if (_shaderModel.Type == ShaderType.Hull
            && registerKey is D3D10RegisterKey { OperandType: OperandType.InputPatchConstant } patchConstant)
        {
            registerKey = new D3D10RegisterKey(OperandType.Output, patchConstant.Number);
        }
        return new RegisterComponentKey(registerKey, componentIndex);
    }
}
