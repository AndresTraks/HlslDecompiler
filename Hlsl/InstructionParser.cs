using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
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

    // The immediates a mov or movc writes, with their bits: the instruction says
    // nothing about their type, so they are typed after parsing by what reads them.
    private readonly List<(ConstantNode Constant, uint Bits)> _polymorphicImmediates = [];
    private readonly Dictionary<HlslTreeNode, bool> _storedTypes =
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
        return parser.ParseToAst(shader);
    }

    private HlslAst ParseToAst(ShaderModel shader)
    {
        _shaderModel = shader;
        _integerOperandAnalysis = new IntegerOperandAnalysis(shader);
        _registerState = new RegisterState(shader);
        _statements = [];
        _currentStatements = new Stack<IStatement>();

        _instructionPointer = 0;
        if (shader.Instructions[0] is D3D10Instruction)
        {
            while (_instructionPointer < shader.Instructions.Count)
            {
                ParseInstruction(shader.Instructions[_instructionPointer] as D3D10Instruction);
                _instructionPointer++;
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

        ResolvePolymorphicImmediates();
        return new HlslAst(_statements, _registerState);
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
                ParseAssignmentInstruction(instruction);
            }
        }
        else
        {
            switch (instruction.Opcode)
            {
                case Opcode.Comment:
                    ParseConstantTableComment(instruction);
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

    private void ParseInstruction(D3D10Instruction instruction)
    {
        // StoreStructured names a destination operand but writes through a statement,
        // and SinCos, Udiv and IMul each write two destinations, so none of them fits
        // the single-destination assignment path.
        if (instruction.HasDestination
            && instruction.GetOperandType(instruction.GetDestinationParamIndex().Value) == OperandType.IndexableTemp)
        {
            // A write to a local array is a store into memory, not a register
            // assignment: see IndexableTempStoreStatement.
            InsertIndexableTempStore(instruction);
        }
        else if (instruction.HasDestination
            && instruction.Opcode != D3D10Opcode.StoreStructured
            && instruction.Opcode != D3D10Opcode.StoreUAVTyped
            && instruction.Opcode != D3D10Opcode.StoreRaw
            && instruction.Opcode != D3D10Opcode.SinCos
            && instruction.Opcode != D3D10Opcode.IMul
            && instruction.Opcode != D3D10Opcode.Udiv
            // An interlocked operation that keeps what it found writes a register
            // as well as the resource, and is still a statement: it has an effect,
            // and the order it runs in is the whole point of it.
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
                    ParseIntegerMultiplyInstruction(instruction);
                    break;
                case D3D10Opcode.Cut:
                    InsertRestartStrip();
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
                            // No seed value: a temp has no meaning until it is written.
                            // Seeding one made component 0 asymmetric with the rest and
                            // leaked into the output as a bare register name.
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
                case D3D10Opcode.DclGSOutputPrimitiveTopology:
                    {
                        _registerState.PrimitiveTopology = instruction.GetPrimitiveTopology();
                        break;
                    }
                case D3D10Opcode.DclResource:
                    {
                        var registerKey = instruction.GetParamRegisterKey(0);
                        _registerState.DeclareResource(registerKey, instruction.GetResourceDimension(), instruction.GetResourceReturnTypeToken(), instruction.ResourceSampleCount);
                        // Every component, not just the first: a sample names the
                        // resource with the swizzle that picks its result, and reading
                        // t2.xyzw wants all four of them to exist.
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
                        SeedResourceComponents(registerKey);
                        break;
                    }
                case D3D10Opcode.StoreUAVTyped:
                    {
                        // store_uav_typed u0, coordinate, value: a texel rather than
                        // an element and an offset within it, so the address is as
                        // wide as the resource has dimensions and the components
                        // written are the resource's own.
                        RegisterComponentKey[] destinationKeys = GetDestinationKeys(instruction).ToArray();
                        var output = new RegisterInputNode(destinationKeys[0]);
                        int dimensions = _registerState.GetResourceDimensionSize(
                            destinationKeys[0].RegisterKey);
                        HlslTreeNode[] coordinates = [.. Enumerable.Range(0, dimensions)
                            .Select(component => GetInputs(instruction, component)[0])];
                        HlslTreeNode[] values = destinationKeys
                            .Select(key => GetInputs(instruction, key.ComponentIndex)[1])
                            .ToArray();
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
                        // Four floats a row. The class of custom data is in the
                        // opcode token and the only one fxc emits here is an
                        // immediate constant buffer.
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
                        var destinationKey = new RegisterComponentKey(registerKey, 0);
                        var resourceInput = new RegisterInputNode(destinationKey);
                        SetActiveOutput(destinationKey, resourceInput);
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
                    InsertAppend();
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
                        // Address and offset are the same for every component of the
                        // element; the value stored is not.
                        HlslTreeNode address = GetInputs(instruction, destinationKeys[0].ComponentIndex)[0];
                        HlslTreeNode[] values = destinationKeys
                            .Select(key => GetInputs(instruction, key.ComponentIndex)[2])
                            .ToArray();
                        RecordStoredType(instruction, values);
                        InsertStatement(new StoreStructuredStatement(output, address, values, ActiveOutputs)
                        {
                            ElementByteOffset = instruction.GetOperandType(2) == OperandType.Immediate32
                                ? instruction.GetParamInt(2, 0)
                                : 0,
                            Components = [.. destinationKeys.Select(k => k.ComponentIndex)],
                        });
                        break;
                    }
                case D3D10Opcode.ImmAtomicConsume:
                    {
                        // The slot goes in a register, and every load from a consume
                        // buffer is a component of the call that took it - there is
                        // no other way to read one - so nothing ends up reading this
                        // and the assignment goes away as dead.
                        var consumeKey = new RegisterComponentKey(
                            instruction.GetParamRegisterKey(1), 0);
                        var slot = new ConsumeSlotNode(new RegisterInputNode(consumeKey));
                        var slotKey = (D3D10RegisterKey)instruction.GetParamRegisterKey(0);
                        _registerState.DeclareRegisterWrite(slotKey, instruction.GetWriteMask(0));
                        SetActiveOutput(
                            new RegisterComponentKey(slotKey, FirstWrittenComponent(instruction)),
                            slot);
                        break;
                    }
                case D3D10Opcode.ImmAtomicAlloc:
                    {
                        // imm_atomic_alloc takes the next slot and the store after it
                        // puts the element there: together they are one Append, and
                        // an append buffer has no other spelling - no subscript, and
                        // no counter to read the slot out of.
                        D3D10Instruction store = NextInstruction();
                        if (store?.Opcode != D3D10Opcode.StoreStructured
                            || store.GetParamRegisterNumber(0) != instruction.GetParamRegisterNumber(1)
                            || !ReadsRegister(store, 1, instruction.GetParamRegisterKey(0)))
                        {
                            throw new NotImplementedException(
                                "imm_atomic_alloc without the store that appends the element.");
                        }
                        var appendKey = new RegisterComponentKey(
                            instruction.GetParamRegisterKey(1), 0);
                        RegisterComponentKey[] appendedKeys = GetDestinationKeys(store).ToArray();
                        HlslTreeNode[] appended = appendedKeys
                            .Select(key => GetInputs(store, key.ComponentIndex)[2])
                            .ToArray();
                        RecordStoredType(store, appended);
                        InsertStatement(new BufferAppendStatement(
                            new RegisterInputNode(appendKey), appended, ActiveOutputs));
                        // Both instructions, the store having been taken with this one.
                        _instructionPointer++;
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
                        // atomic_iadd u0, address, value, and atomic_cmp_store with a
                        // compare between the two. The destination is the resource
                        // itself and carries no write mask, so there are no
                        // destination keys to walk - one statement, not one per
                        // component.
                        //
                        // The imm_ forms keep what the resource held and put it in a
                        // register, which goes in front of everything else - so the
                        // resource is the second operand there, and every source is
                        // one further along.
                        bool keepsOriginal = instruction.Opcode.IsImmediateAtomic();
                        int first = keepsOriginal ? 1 : 0;
                        var resourceKey = new RegisterComponentKey(
                            instruction.GetParamRegisterKey(first), 0);
                        var destination = new RegisterInputNode(resourceKey);
                        bool hasCompare = instruction.Opcode
                            is D3D10Opcode.AtomicCmpStore or D3D10Opcode.ImmAtomicCmpExch;
                        // A structured resource addresses an element and a byte offset
                        // within it, both components of the one operand; a byte address
                        // one has only the offset.
                        HlslTreeNode address = GetInputs(instruction, 0)[first];
                        HlslTreeNode elementByteOffset = _registerState.IsRawResource(resourceKey.RegisterKey)
                            ? null
                            : GetInputs(instruction, 1)[first];
                        TempVariableNode original = keepsOriginal
                            ? new TempVariableNode { IsInteger = true, VariableSize = 1 }
                            : null;
                        InsertStatement(new AtomicStatement(
                            destination,
                            address,
                            GetInputs(instruction, 0)[first + (hasCompare ? 2 : 1)],
                            instruction.Opcode.AtomicMethodName(),
                            ActiveOutputs)
                        {
                            ElementByteOffset = elementByteOffset,
                            Compare = hasCompare ? GetInputs(instruction, 0)[first + 1] : null,
                            Original = original,
                        });
                        if (original != null)
                        {
                            var originalKey = (D3D10RegisterKey)instruction.GetParamRegisterKey(0);
                            _registerState.DeclareRegisterWrite(
                                originalKey, instruction.GetWriteMask(0));
                            // One component: an atomic is over a single value, whatever
                            // the mask on the register it lands in says.
                            SetActiveOutput(
                                new RegisterComponentKey(originalKey, FirstWrittenComponent(instruction)),
                                original);
                        }
                        break;
                    }
                case D3D10Opcode.StoreRaw:
                    {
                        // store_raw u0.xy, byteOffset, value: a byte offset in place of
                        // an element and an offset, otherwise a structured store.
                        RegisterComponentKey[] destinationKeys = GetDestinationKeys(instruction).ToArray();
                        var output = new RegisterInputNode(destinationKeys[0]);
                        HlslTreeNode address = GetInputs(instruction, destinationKeys[0].ComponentIndex)[0];
                        HlslTreeNode[] values = destinationKeys
                            .Select(key => GetInputs(instruction, key.ComponentIndex)[1])
                            .ToArray();
                        RecordStoredType(instruction, values);
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
                    break;
                case D3D10Opcode.DclIndexRange:
                    _registerState.DeclareIndexRange(instruction);
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

    /// <summary>
    /// Unwinds to the innermost enclosing block of the given kind. Statements parsed
    /// inside a block sit on top of it and have to come off first.
    /// </summary>
    /// <param name="isOpen">
    /// Extra condition the block must satisfy - used to skip a block that has already
    /// been closed, so a nested one does not get closed twice.
    /// </param>
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

    /// <summary>
    /// The component an instruction's destination mask names. An atomic writes one
    /// value, so the mask has one bit - but it need not be x: fxc puts the result
    /// wherever the register is free.
    /// </summary>
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

    /// <summary>Whether a register names a buffer elements are consumed from.</summary>
    private bool IsConsumeBuffer(RegisterKey registerKey)
    {
        return _registerState.ResourceDefinitions.Any(d => d.BindPoint == registerKey.Number
            && d.ShaderInputType == D3DShaderInputType.UavConsumeStructured);
    }

    /// <summary>The instruction after the one being parsed, or null at the end.</summary>
    private D3D10Instruction NextInstruction()
    {
        return _instructionPointer + 1 < _shaderModel.Instructions.Count
            ? _shaderModel.Instructions[_instructionPointer + 1] as D3D10Instruction
            : null;
    }

    /// <summary>Whether an operand reads the given register.</summary>
    private static bool ReadsRegister(D3D10Instruction instruction, int operandIndex, RegisterKey registerKey)
    {
        return instruction.GetOperandType(operandIndex) != OperandType.Immediate32
            && instruction.GetParamRegisterKey(operandIndex).Equals(registerKey);
    }

    private void InsertStatement(IStatement statement)
    {
        // A block that has already been closed cannot take more statements, and nor
        // can a statement that is not a block at all.
        if (_currentStatements.Count != 0 && IsClosed(_currentStatements.Peek()))
        {
            _currentStatements.Pop();
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

    // discard_nz drops the pixel when its condition holds. Written as clip() when
    // the condition is the "value is negative" test clip() actually means, and as a
    // guarded discard otherwise.
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

    private void InsertAppend()
    {
        InsertStatement(new AppendStatement(ActiveOutputs));
    }

    private void InsertRestartStrip()
    {
        InsertStatement(new RestartStripStatement(ActiveOutputs));
    }

    /// <summary>
    /// Binds every register the loop body writes to a phi at the loop header, so that
    /// the body's expressions read the loop-carried value rather than the value from
    /// before the loop. <see cref="EndLoop"/> closes each phi with its backedge.
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
    /// Registers written between the loop instruction at the current pointer and its
    /// matching end. Read-only: it must not disturb parser state.
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
        // A defi gives the count outright. Otherwise iN is a uniform, and the count
        // is its x component - still a bounded loop, just not a constant one.
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
    /// <remarks>
    /// TODO: the _z form tests for zero instead; the test-boolean bit is not decoded yet.
    /// </remarks>
    private HlslTreeNode GetConditionNode(D3D10Instruction instruction)
    {
        byte component = instruction.GetSourceSwizzleComponents(0)[0];
        RegisterKey registerKey = instruction.GetParamRegisterKey(0);
        HlslTreeNode condition = GetActiveOutput(new RegisterComponentKey(registerKey, component));

        ComparisonNode comparison;
        if (condition is ComparisonNode conditionComparison)
        {
            comparison = conditionComparison;
        }
        // A float comparison feeding a branch reads as the condition itself rather
        // than as a value tested against zero.
        else if (condition is GreaterEqualOperation greaterEqual)
        {
            comparison = new ComparisonNode(greaterEqual.Inputs[0], greaterEqual.Inputs[1], IfComparison.GE);
        }
        else
        {
            comparison = new ComparisonNode(condition, new ConstantNode(0), IfComparison.NE);
        }
        // if_z, breakc_z and the rest take the branch when the register is zero, which
        // for a comparison mask is when the comparison does not hold.
        return instruction.TestNonZero ? comparison : comparison.Inverted();
    }

    /// <summary>
    /// <c>sincos dstSin, dstCos, src</c> writes two registers from one source. Either
    /// destination may be null when the shader only wants one of the two results.
    /// </summary>
    // udiv writes the quotient to its first destination and the remainder to its
    // second, either of which may be null when only one is wanted.
    // imul writes the high half of the product to its first destination and the low
    // half to its second. fxc leaves the high one null for an ordinary 32 bit
    // multiply, which is the only form HLSL has a way of saying.
    private void ParseIntegerMultiplyInstruction(D3D10Instruction instruction)
    {
        const int HighDestinationIndex = 0;
        const int LowDestinationIndex = 1;
        const int Factor1Index = 2;
        const int Factor2Index = 3;

        if (instruction.GetOperandType(HighDestinationIndex) != OperandType.Null)
        {
            throw new NotImplementedException("imul writing the high half of the product");
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

    private HlslTreeNode GetInputComponent(D3D10Instruction instruction, int operandIndex, int component)
    {
        if (instruction.GetOperandType(operandIndex) == OperandType.Immediate32)
        {
            return new ConstantNode((int)instruction.GetParamInt(operandIndex, component));
        }
        RegisterKey registerKey = instruction.GetParamRegisterKey(operandIndex);
        byte[] swizzle = instruction.GetSourceSwizzleComponents(operandIndex);
        return GetActiveOutput(new RegisterComponentKey(registerKey, swizzle[component]));
    }

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
    /// A <c>ret</c> inside a block returns early. The one at the end of the shader is
    /// implicit - <see cref="StatementFinalizer"/> turns the final assignment into the
    /// return - so emitting a statement for it as well would duplicate the value.
    /// </summary>
    private void InsertReturn()
    {
        // A closed block stays on the stack until the next statement displaces it, so
        // it does not count: a ret after `endloop` is at the top level.
        bool insideOpenBlock = _currentStatements.Any(s =>
            (s is IfStatement || s is LoopStatement || s is SwitchStatement) && !IsClosed(s));
        if (!insideOpenBlock)
        {
            return;
        }

        var returnStatement = new ReturnStatement(ActiveOutputs);

        // An assignment immediately before the ret produced the value being returned,
        // so it becomes the return rather than standing as its own statement.
        if (ActiveStatement is AssignmentStatement assignment)
        {
            _currentStatements.Pop();
            IList<IStatement> sequence = ActiveStatementSequence;
            if (sequence.Count != 0 && ReferenceEquals(sequence[sequence.Count - 1], assignment))
            {
                sequence[sequence.Count - 1] = returnStatement;
                _currentStatements.Push(returnStatement);
                return;
            }
            _currentStatements.Push(assignment);
        }

        InsertStatement(returnStatement);
    }

    /// <summary>
    /// The value the mask stands for. It is the bit pattern of what is wanted, so
    /// against a float comparison it is read as those bits rather than as whatever
    /// the operand typing made of them: step masks with 0x3f800000, which is 1.0f
    /// and not 1065353216. Against an integer comparison the number is the number.
    /// </summary>
    // What a condition selects. A constant is the bit pattern of the value wanted
    // and is read back as that; anything else is already the value.
    private static HlslTreeNode MaskedValue(HlslTreeNode condition, HlslTreeNode value)
    {
        return value is ConstantNode mask ? AsMaskedValue(condition, mask) : value;
    }

    private static ConstantNode AsMaskedValue(HlslTreeNode condition, ConstantNode mask)
    {
        bool isInteger = condition is ComparisonNode comparison && comparison.IsInteger;
        if (isInteger || mask.IntegerValue == null)
        {
            return mask;
        }
        return new ConstantNode(BitConverter.Int32BitsToSingle(mask.IntegerValue.Value));
    }

    // A comparison result. GE is still modelled as an operation rather than a
    // ComparisonNode, unlike every other comparison, so it has to be named here.
    // Two conditions combined are a condition too: any() over a bool4 is an or of
    // ors, and reading the outer one as bitwise costs fxc an `and ..., 1` to make
    // a bool of the mask again.
    private static bool IsCondition(HlslTreeNode node)
    {
        return node is ComparisonNode || node is GreaterEqualOperation
            || node is LogicalAndOperation || node is LogicalOrOperation;
    }

    /// <summary>
    /// `and` and `or` combine comparison masks, which a shader may mean in two
    /// different ways. Two conditions are a logical operator. A condition anded with
    /// anything else is the `cond ? value : 0` idiom that step() and friends compile
    /// to - a comparison writes all ones or all zeroes, so anding with it selects
    /// the value or nothing. Where the value is a constant it is the bit pattern of
    /// what was wanted and is read back as that; where it is computed it stands as
    /// it is, and has to, since HLSL will not and a float (X3082).
    /// </summary>
    private static HlslTreeNode CreateLogicalOperation(D3D10Opcode opcode, HlslTreeNode[] inputs)
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
                    inputs[0], MaskedValue(inputs[0], inputs[1]), new ConstantNode(0));
            }
            if (IsCondition(inputs[1]))
            {
                return new MoveConditionalOperation(
                    inputs[1], MaskedValue(inputs[1], inputs[0]), new ConstantNode(0));
            }
        }

        // Neither operand is a condition, so this is the bitwise use of the opcode
        // rather than the logical one. The registers involved are typed as integers,
        // which is what makes the operator legal in the output.
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

        // A register assigned in any case leaves the switch as a join over every case
        // that assigns it, plus the value that was live on entry. A register first
        // written inside a case still has to be carried out, or later reads of it
        // find nothing.
        var caseValues = new Dictionary<RegisterComponentKey, List<HlslTreeNode>>();
        foreach (SwitchCase switchCase in switchStatement.Cases)
        {
            if (switchCase.Body.Count == 0)
            {
                continue;
            }
            foreach (var caseOutput in switchCase.Body.Last().Outputs)
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

        foreach (var caseValue in caseValues)
        {
            List<HlslTreeNode> joined = caseValue.Value;
            if (switchStatement.Outputs.TryGetValue(caseValue.Key, out var parentNode)
                && !joined.Contains(parentNode))
            {
                joined = [.. joined, parentNode];
            }

            switchStatement.Outputs[caseValue.Key] = joined.Count == 1
                ? joined[0]
                : new PhiNode([.. joined]);
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
                // Still open, meaning it is the one this loop seeded at its header:
                // a branch join carries two values already and is not this loop's to
                // close. Two loops in a row over a register assigned in an if before
                // them reach here with one of those.
                if (parentNode is PhiNode headerPhi
                    && !headerPhi.IsLoopHeader
                    && headerPhi.Inputs.Count == 1)
                {
                    // Close the phi seeded at the header. The loop's output stays the
                    // phi, so code after the loop reads the loop-carried value.
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
        // An else belongs to the nearest if that is still open. A nested if that
        // has already been closed stays on the stack until the next statement
        // displaces it, and taking that one would overwrite its own else branch.
        IfStatement ifStatement = UnwindTo<IfStatement>(i => !i.IsParsed);
        ifStatement.IsTrueParsed = true;
        ifStatement.FalseBody = [];
    }

    private void EndIf()
    {
        IfStatement ifStatement = UnwindTo<IfStatement>(i => !i.IsParsed);
        ifStatement.IsTrueParsed = true;
        ifStatement.IsParsed = true;

        // fxc writes a continue as an if with nothing in its true branch. A branch
        // that assigns nothing carries no outputs of its own, and every register
        // keeps whatever reached the if.
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
        if (registerComponent.RegisterKey is D3D10RegisterKey d3D10RegisterKey && d3D10RegisterKey.OperandType == OperandType.Immediate32)
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
        return ActiveOutputs[registerComponent];
    }

    private void SetActiveOutput(RegisterComponentKey registerComponent, HlslTreeNode value)
    {
        InsertAssignment();
        ActiveOutputs[registerComponent] = value;
    }

    private void ParseAssignmentInstruction(D3D10Instruction instruction)
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
            if (instruction.Saturate)
            {
                instructionTree = new SaturateOperation(instructionTree);
            }
            newOutputs[destinationKey] = instructionTree;
        }

        foreach (var output in newOutputs)
        {
            SetActiveOutput(output.Key, output.Value);
        }
    }

    private static IEnumerable<RegisterComponentKey> GetDestinationKeys(Instruction instruction)
    {
        int index = instruction.GetDestinationParamIndex().Value;
        int mask = instruction.GetDestinationWriteMask();
        return GetParameterRegisterKeys(instruction, index, mask);
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

                        RegisterKey vertexKey = D3D10RegisterKey.CreateGSInput(registerKey.Number, vertex);
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
            case Opcode.Abs:
            case Opcode.Add:
            case Opcode.Cmp:
            case Opcode.DSX:
            case Opcode.DSY:
            case Opcode.Exp:
            case Opcode.Frc:
            case Opcode.Log:
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
            case Opcode.Nrm:
                return CreateNormalizeOutputNode(instruction, componentIndex);
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
        return node;
    }

    // What an instruction makes of the operands it reads. itof reads integers and
    // ftoi floats, whatever their results; a mov or movc passes bits along and the
    // bitwise operators treat them as bits, so neither says anything.
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
                return true;
            case D3D10Opcode.Ftoi:
            case D3D10Opcode.Ftou:
            // The float whose nearest half float it takes the bits of.
            case D3D10Opcode.F32ToF16:
                return false;
            case D3D10Opcode.F16ToF32:
                return true;
            // A buffer load or store reads its address as an integer, whatever the
            // buffer holds.
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
    /// Types the immediates moved into registers by what goes on to read them. The
    /// register rule types them by the register, and a register fxc reuses - a loop
    /// counter in one place, a sample offset in another - gets one type for both,
    /// so -1.0f moved into it printed as the -1082130432 of its bits. The readers of
    /// this particular value know better, and where they all agree, they win.
    /// </summary>
    /// <summary>
    /// What a buffer store reads the value it is given as, kept for the immediates
    /// nothing else types. A store is a statement rather than a node, so it is not
    /// in the graph the reader walk below follows, and a value whose only reader is
    /// one had nobody to ask: `output[i] = c ? x : -1` into an int buffer wrote the
    /// -1 as the float its bits are, which is NaN, which is not HLSL at all.
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
        foreach (HlslTreeNode value in values)
        {
            _storedTypes[value] = kind == ValueKind.Integer;
        }
    }

    private void ResolvePolymorphicImmediates()
    {
        foreach ((ConstantNode constant, uint bits) in _polymorphicImmediates)
        {
            bool? consumedAsInteger = GetConsumedType(constant, _storedTypes);
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

    // What the readers of a value agree it is, looking through the nodes that
    // merely carry it - moves, conditional moves, phis, and a sign or absolute
    // modifier - or null where they disagree or there are none.
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
            // A store reads what it is given and is not a node, so it says so here
            // rather than by being one of the outputs below.
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
                // A bitwise operator says nothing about what it was given either -
                // it carries the bits along - so the type comes from whatever reads
                // what it made. Stopping at one left the zero branch of a movc two
                // ands away from its readers untyped, and it printed as 0.000000 in
                // an expression of integers.
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
                bool? consumed = reader is ComparisonNode comparison ? comparison.IsInteger : reader.ConsumesInteger;
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
                        case D3D10Opcode.Exp:
                            return new ExponentialOperation(inputs[0]);
                        case D3D10Opcode.Frc:
                            return new FractionalOperation(inputs[0]);
                        case D3D10Opcode.GE:
                            return new GreaterEqualOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.Div:
                            return new DivisionOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.And:
                            return CreateLogicalOperation(instruction.Opcode, inputs);
                        case D3D10Opcode.Or:
                        case D3D10Opcode.Xor:
                            return CreateLogicalOperation(instruction.Opcode, inputs);
                        case D3D10Opcode.Not:
                            // A not of a comparison mask is the comparison the other way;
                            // of anything else, the bits flipped.
                            return inputs[0] is ComparisonNode comparison && comparison.Inverted() != null
                                ? comparison.Inverted()
                                : new BitwiseNotOperation(inputs[0]);
                        // Float comparisons, like their integer counterparts, only
                        // ever feed a branch or a movc, so they read as conditions.
                        case D3D10Opcode.LT:
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.LT);
                        case D3D10Opcode.Eq:
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.EQ);
                        case D3D10Opcode.Ne:
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.NE);
                        // The integer comparisons, marked as such: the mask one
                        // writes is an integer where a float comparison writes the
                        // bits of a float.
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
                            return new MinimumOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.IMax:
                        case D3D10Opcode.UMax:
                            return new MaximumOperation(inputs[0], inputs[1]);
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
                                // A load from a consume buffer is a component of the
                                // Consume that took the slot: the address is what the
                                // consume left, and the byte offset says which
                                // component of the element this is.
                                if (inputs[0] is ConsumeSlotNode slot
                                    && IsConsumeBuffer(instruction.GetParamRegisterKey(3)))
                                {
                                    return new ConsumeNode(
                                        (RegisterInputNode)inputs[2], elementByteOffset / 4, slot);
                                }
                                return new LoadStructuredNode(inputs[0], inputs[1], inputs[2])
                                {
                                    ElementByteOffset = elementByteOffset,
                                };
                            }
                        case D3D10Opcode.LdRaw:
                            // ld_raw dst, byteOffset, t#: the offset stands where an
                            // element index would, and there is no offset within one.
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
                        case D3D10Opcode.UTof:
                            return new ConvertOperation(inputs[0], "float");
                        case D3D10Opcode.Ftoi:
                            return new ConvertOperation(inputs[0], "int");
                        case D3D10Opcode.Ftou:
                            return new ConvertOperation(inputs[0], "uint");
                        case D3D10Opcode.MovC:
                            return new MoveConditionalOperation(inputs[0], inputs[1], inputs[2]);
                        case D3D10Opcode.Mul:
                            return new MultiplyOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.Rsq:
                            return new ReciprocalSquareRootOperation(inputs[0]);
                        case D3D10Opcode.Sqrt:
                            return new SquareRootOperation(inputs[0]);
                        case D3D10Opcode.CountBits:
                            return new BitCountOperation(inputs[0]);
                        case D3D10Opcode.FirstBitLo:
                            return new FirstBitLowOperation(inputs[0]);
                        // firstbit_hi counts from the top and firstbithigh counts
                        // from the bottom, so the instruction is the subtraction
                        // rather than the call: written as the nodes it is made of,
                        // the subtraction binds like any other and the pattern fxc
                        // guards it with is a pattern over ordinary arithmetic. The
                        // two differ only where there is no bit to find - the
                        // instruction answers 0xffffffff and the subtraction 32 -
                        // and that is the case the guard exists to handle, so the
                        // template that puts firstbithigh back takes it away again.
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
                {
                    // The attribute, and where to evaluate it: a sample index in one
                    // component, or an offset in two. The place is the same for
                    // every component of the result, the way a sample's coordinate
                    // is.
                    bool isSnapped = instruction.Opcode == D3D10Opcode.EvalSnapped;
                    HlslTreeNode value = GetInputs(instruction, componentIndex)[0];
                    // A snapped offset is written as an immediate, and it is a
                    // count of sixteenths of a pixel rather than the float those
                    // bits spell.
                    const int PlaceParamIndex = 2;
                    int places = isSnapped ? 2 : 1;
                    HlslTreeNode[] at = instruction.GetOperandType(PlaceParamIndex) == OperandType.Immediate32
                        ? [.. Enumerable.Range(0, places)
                            .Select(component => (HlslTreeNode)new ConstantNode(
                                (int)instruction.GetParamInt(PlaceParamIndex, component)))]
                        : [.. Enumerable.Range(0, places)
                            .Select(component => GetInputs(instruction, component)[1])];
                    return new EvaluateAttributeOperation(value, at, isSnapped);
                }
            case D3D10Opcode.SampleInfo:
                return CreateSampleInfoNode(instruction, componentIndex);
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

        var resource = GetInputComponents(instruction, ResourceParamIndex, 1)[0] as RegisterInputNode;
        bool isUnorderedAccessView = instruction.Opcode == D3D10Opcode.LdUAVTyped;
        ResourceDefinition definition = _registerState.ResourceDefinitions
            .Where(d => d.ShaderInputType == (isUnorderedAccessView
                ? D3DShaderInputType.UavRWTyped
                : D3DShaderInputType.Texture))
            .FirstOrDefault(d => d.BindPoint == resource.RegisterComponentKey.RegisterKey.Number);

        // Load reads a texel directly, so it takes the mip level alongside the
        // coordinates: a 2D texture is addressed by an int3. A buffer has no mips
        // and is addressed by its index alone, and a multisampled texture has none
        // either - it takes the sample index as an argument of its own instead.
        // A writable view has the one level, so its load takes no mip either.
        bool multisampled = instruction.Opcode == D3D10Opcode.LDMS;
        int addressLength = definition?.Dimension == ResourceDimension.Buffer
            ? 1
            : (definition?.GetDimensionSize() ?? 2)
                + (multisampled || isUnorderedAccessView ? 0 : 1);
        // The address is in texels, so an immediate operand holds integers rather
        // than the floats those same bits would spell.
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

        return new ResourceLoadNode(resource, address, outputComponent, sampleIndex)
        {
            SampleOffsets = instruction.SampleOffsets,
        };
    }

    /// <summary>
    /// bufinfo r0.x, t0: how many elements the buffer holds, or how many bytes a
    /// byte address one does. One number, with no mip level to ask it at - the
    /// resource operand's swizzle routes it rather than choosing between
    /// measurements the way a resinfo's does.
    /// </summary>
    private ResourceInfoNode CreateBufferInfoNode(D3D10Instruction instruction, int outputComponent)
    {
        const int ResourceParamIndex = 1;
        var resource = GetInputComponents(instruction, ResourceParamIndex, 4)[outputComponent]
            as RegisterInputNode;
        return new ResourceInfoNode(resource, new ConstantNode(0), outputComponent,
            D3D10ResInfoReturnType.Uint)
        {
            IsBuffer = true,
            IsRawBuffer = _registerState.IsRawResource(resource.RegisterComponentKey.RegisterKey),
        };
    }

    private ResourceInfoNode CreateResourceInfoNode(D3D10Instruction instruction, int outputComponent)
    {
        const int MipLevelParamIndex = 1;
        const int ResourceParamIndex = 2;

        // The resource operand's swizzle says which measurement each component of
        // the result is, the same way a sample's says which channel.
        var resource = GetInputComponents(instruction, ResourceParamIndex, 4)[outputComponent] as RegisterInputNode;
        // A mip level is an integer, whatever bits an immediate holds.
        HlslTreeNode mipLevel = instruction.GetOperandType(MipLevelParamIndex) == OperandType.Immediate32
            ? new ConstantNode((int)instruction.GetParamInt(MipLevelParamIndex, 0))
            : GetInputComponents(instruction, MipLevelParamIndex, 1)[0];
        return new ResourceInfoNode(resource, mipLevel, outputComponent, instruction.ResInfoReturnType);
    }

    // sampleinfo dest, resource: how many samples the resource has, with no mip
    // level to ask it at and the resource one operand earlier than resinfo has it.
    // The mip level is a constant zero here so that it joins the resinfo of the
    // same texture, which is the one GetDimensions call the two came from.
    private ResourceInfoNode CreateSampleInfoNode(D3D10Instruction instruction, int outputComponent)
    {
        const int ResourceParamIndex = 1;

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

    private TextureLoadOutputNode CreateTextureLoadOutputNode(Instruction instruction, int outputComponent)
    {
        const int TextureCoordsParamIndex = 1;

        if (instruction is D3D9Instruction d3D9Instruction)
        {
            const int SamplerParamIndex = 2;
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
            // gather4_po takes its offset from a register, in an operand of its own
            // between the coordinate and the texture, so everything after it is one
            // further along.
            bool hasProgrammableOffset = ((D3D10Instruction)instruction).Opcode
                is D3D10Opcode.Gather4Po or D3D10Opcode.Gather4PoC;
            int TextureParamIndex = hasProgrammableOffset ? 3 : 2;
            int SamplerParamIndex = hasProgrammableOffset ? 4 : 3;

            // The resource operand carries the swizzle that says which channel each
            // component of the result comes from - `t2.yzxw` puts the red channel in
            // z. Keeping only the first component threw that away and read the
            // channel the destination happened to be written to.
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

            // Everything past the sampler is what distinguishes the variant.
            const int ExtraParamIndex = 4;
            TextureLoadControls controls = ((D3D10Instruction)instruction).Opcode switch
            {
                D3D10Opcode.SampleL => TextureLoadControls.Lod,
                D3D10Opcode.SampleB => TextureLoadControls.Bias,
                D3D10Opcode.SampleD => TextureLoadControls.Grad,
                D3D10Opcode.SampleC => TextureLoadControls.Compare,
                D3D10Opcode.SampleCLZ => TextureLoadControls.Compare | TextureLoadControls.LevelZero,
                D3D10Opcode.Gather4 => TextureLoadControls.Gather,
                // A gather that compares: four texels tested against one value, the
                // way a comparison sample tests the one it reads.
                D3D10Opcode.Gather4C => TextureLoadControls.Gather | TextureLoadControls.Compare,
                D3D10Opcode.Gather4Po =>
                    TextureLoadControls.Gather | TextureLoadControls.ProgrammableOffset,
                D3D10Opcode.Gather4PoC => TextureLoadControls.Gather
                    | TextureLoadControls.Compare | TextureLoadControls.ProgrammableOffset,
                D3D10Opcode.Lod => TextureLoadControls.CalculateLod,
                _ => TextureLoadControls.None,
            };
            // lod puts the clamped level in x and the unclamped one in y, and the
            // resource swizzle picks between them - the only thing that tells
            // CalculateLevelOfDetail from CalculateLevelOfDetailUnclamped.
            if (controls.HasFlag(TextureLoadControls.CalculateLod)
                && instruction.GetSourceSwizzleComponents(TextureParamIndex)[0] == 1)
            {
                controls |= TextureLoadControls.Unclamped;
            }
            HlslTreeNode[] derivativeX = null;
            HlslTreeNode[] derivativeY = null;
            HlslTreeNode scalarArgument = null;
            // The offset is the operand before the texture, and as wide as the
            // texture has dimensions - two for a Texture2D.
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
                // Only these carry one more operand. gather4 takes the same operands
                // as sample, so reading a fifth would run off the end. A comparison
                // gather with a register offset has both, and the value compared
                // against is last of all.
                scalarArgument = GetInputComponents(
                    instruction, hasProgrammableOffset ? ExtraParamIndex + 1 : ExtraParamIndex, 1)[0];
            }

            TextureLoadOutputNode node = TextureLoadOutputNode.CreateSample(
                sampler, texCoords, outputComponent, texture,
                controls, derivativeX, derivativeY, scalarArgument, offsets);
            node.SampleOffsets = ((D3D10Instruction)instruction).SampleOffsets;
            return node;
        }
    }

    /// <summary>
    /// Says of a node that it reads floats. CreateInstructionTree types the tree's
    /// root by the opcode and nothing below it, which is enough while an instruction
    /// makes one node. A dot product makes a sum of products, and the products are
    /// what read the operands: left saying nothing, they left their operands typed
    /// by the register those were in, and fxc reuses registers. A texture load into
    /// the r0 that had held a thread id came out as an int2, which truncates it.
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

    private HlslTreeNode CreateDotProductNode(D3D10Instruction instruction)
    {
        var addends = new List<HlslTreeNode>();
        var numComponents = instruction.Opcode switch
        {
            D3D10Opcode.Dp2 => 2,
            D3D10Opcode.Dp3 => 3,
            D3D10Opcode.Dp4 => 4,
            _ => throw new InvalidOperationException(),
        };
        for (int component = 0; component < numComponents; component++)
        {
            IList<HlslTreeNode> componentInput = GetInputs(instruction, component);
            var multiply = ReadingFloats(new MultiplyOperation(componentInput[0], componentInput[1]));
            addends.Add(multiply);
        }

        return addends.Aggregate((addition, addend) =>
            ReadingFloats(new AddOperation(addition, addend)));
    }

    // `lit src` reads n.l from x, n.h from y and the specular power from w, whatever
    // component is being written, so the source is read three times over rather than
    // once per destination component.
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

    // cb0[r0.x + 1] reads an array element chosen at run time. The immediate is the
    // register that element zero of the read would sit at, and the array may start
    // further back in the buffer, so the difference is added to the index.
    // icb[r0.x + 0] reads the immediate constant buffer, which has one index
    // rather than a buffer and an element.
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
        byte[] swizzle = instruction.GetSourceSwizzleComponents(operandIndex);
        return new RelativeAddressNode(
            new RegisterComponentKey(registerKey, swizzle[componentIndex]), index);
    }

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
        return new RelativeAddressNode(
            new RegisterComponentKey(registerKey, swizzle[componentIndex]), index);
    }

    // v[r0.x][0] reads a vertex of a geometry shader input chosen at run time. The
    // second index names the register, so it is the vertex that is dynamic.
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
        // A vertex shader indexes the run of input registers a dcl_indexrange
        // declared and writes `v[r0.x + 0]`, one index, where a geometry shader
        // writes `v[r0.x][0]` and the second index names the register. The
        // immediate beside the index is the first register of the run.
        if (operandIndices.Length == 1)
        {
            return new RelativeAddressNode(
                new RegisterComponentKey(
                    new D3D10RegisterKey(OperandType.Input, (int)operandIndices[0].Immediate),
                    swizzle[componentIndex]),
                index);
        }

        // Any vertex will do to find the declaration; they share one.
        var registerKey = D3D10RegisterKey.CreateGSInput((int)operandIndices[1].Immediate, 0);
        return new RelativeAddressNode(
            new RegisterComponentKey(registerKey, swizzle[componentIndex]), index);
    }

    // `c0[a0.x]` picks an array element at run time. Reading it as plain c0 would
    // silently decompile a different shader, so the index is modelled instead.
    private LoopCounterNode _loopCounter;

    // A structured buffer element can be wider than one component, and any of them
    // can be read, so each needs a value before the first load.
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
            // aL counts the enclosing loop. One shared node stands for the register,
            // so that the components of c0[aL] group into a single expression.
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
        return new RelativeAddressNode(inputKey, GetActiveOutput(addressKey));
    }

    // The element an x# operand names: a literal, a register, or a register plus a
    // literal, as cb0[r0.x + 2] is. Whatever the shader indexes an array with is an
    // integer, so a literal is one too.
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
            // A mov's value is its source itself: wrapping it in a move would give
            // the source a consumer, and a value consumed by nothing but a store is
            // meant to be written into it inline.
            HlslTreeNode value = instruction.Opcode == D3D10Opcode.Mov
                ? GetInputs(instruction, key.ComponentIndex)[0]
                : CreateInstructionTree(instruction, key);
            return instruction.Saturate ? new SaturateOperation(value) : value;
        })];
        InsertStatement(new IndexableTempStoreStatement(
            register, index, [.. destinationKeys.Select(key => key.ComponentIndex)], values, ActiveOutputs));
    }

    private HlslTreeNode[] GetInputs(D3D10Instruction instruction, int componentIndex)
    {
        int numInputs = GetNumInputs(instruction.Opcode);
        var inputs = new HlslTreeNode[numInputs];
        for (int i = 0; i < numInputs; i++)
        {
            int inputParameterIndex = i + 1;
            var operandType = instruction.GetOperandType(inputParameterIndex);
            D3D10OperandTokenCollection.OperandIndex[] operandIndices =
                instruction.OperandTokens.GetOperandIndices(inputParameterIndex);
            if (operandType == OperandType.IndexableTemp)
            {
                byte[] swizzle = instruction.GetSourceSwizzleComponents(inputParameterIndex);
                var load = new IndexableTempLoadNode(
                    (int)operandIndices[0].Immediate,
                    GetIndexableTempElementIndex(instruction, inputParameterIndex),
                    swizzle[componentIndex]);
                inputs[i] = ApplyModifier(load, instruction.GetOperandModifier(inputParameterIndex));
                continue;
            }
            if (operandIndices.Any(index => index.IsRelative))
            {
                // The register number decoded from a relative operand is meaningless,
                // so the element has to be modelled rather than read.
                inputs[i] = operandType switch
                {
                    OperandType.ConstantBuffer => GetDynamicConstantBufferInput(
                        instruction, inputParameterIndex, componentIndex, operandIndices),
                    OperandType.ImmediateConstantBuffer => GetImmediateConstantBufferInput(
                        instruction, inputParameterIndex, componentIndex, operandIndices),
                    OperandType.Input => GetDynamicVertexInput(
                        instruction, inputParameterIndex, componentIndex, operandIndices),
                    _ => throw new NotImplementedException(
                        $"Dynamically indexed {operandType} in {instruction.Opcode}"),
                };
                // A relative operand carries a modifier like any other, and this
                // path used to return before applying it: `mov o1.xyz,
                // -v[r0.x + 0][1].xyzx` came out without the negation.
                inputs[i] = ApplyModifier(
                    inputs[i], instruction.GetOperandModifier(inputParameterIndex));
                continue;
            }
            if (operandType == OperandType.Immediate32)
            {
                // An immediate's 32 bits are typed by the instruction consuming them.
                // A mov consumes nothing - the register it writes is the best guess
                // for now, and what reads the value decides once the graph is whole.
                var constant = _integerOperandAnalysis.IsIntegerOperand(instruction)
                    ? new ConstantNode((int)instruction.GetParamInt(inputParameterIndex, componentIndex))
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
                D3D10OperandModifier modifier = instruction.GetOperandModifier(inputParameterIndex);
                input = ApplyModifier(input, modifier);
                inputs[i] = input;
            }
        }
        return inputs;
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
                // The resource, the address and the value: the destination register
                // is not a source, and the resource is read as one here only so that
                // the operands after it line up.
                return 3;
            case D3D10Opcode.ImmAtomicCmpExch:
                return 4;
            // The coordinate and the value; the resource is the destination operand.
            case D3D10Opcode.StoreUAVTyped:
                return 2;
            // The coordinate and the view.
            case D3D10Opcode.LdUAVTyped:
                return 2;
            // The attribute and where to evaluate it.
            case D3D10Opcode.EvalSampleIndex:
            case D3D10Opcode.EvalSnapped:
                return 2;
            case D3D10Opcode.IMad:
            case D3D10Opcode.Umad:
            case D3D10Opcode.Mad:
            case D3D10Opcode.MovC:
            case D3D10Opcode.LdStructured:
            case D3D10Opcode.StoreStructured:
            case D3D10Opcode.AtomicCmpStore:
                return 3;
            case D3D10Opcode.LDMS:
                return 3;
            case D3D10Opcode.ResInfo:
            case D3D10Opcode.LdRaw:
            case D3D10Opcode.StoreRaw:
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
            // A one component operand - vPrim, vThreadIDInGroupFlattened - has only
            // its x, whichever component of the result it is read for: `and r0.xyz,
            // vPrim, l(3, 1, 2, 0)` reads the one value three times.
            componentIndex = 0;
        }
        else
        {
            byte[] swizzle = instruction.GetSourceSwizzleComponents(paramIndex);
            componentIndex = swizzle[component];
        }
        return new RegisterComponentKey(registerKey, componentIndex);
    }
}
