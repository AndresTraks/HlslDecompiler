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

    // The immediates a mov or movc writes, with their bits: the instruction says
    // nothing about their type, so they are typed after parsing by what reads them.
    private readonly List<(ConstantNode Constant, uint Bits)> _polymorphicImmediates = [];
    private readonly Dictionary<HlslTreeNode, bool> _storedTypes =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The values that are doubles. A double is not something a value can be asked -
    /// an addition of two of them is the same node as an addition of two floats - and
    /// not something the register can be asked either, because fxc reuses a register
    /// for a double in one place and a float in another and a set of register
    /// components has no notion of when. It is a fact about the value, recorded where
    /// the value is made, and what needs it is everything that has to tell a register
    /// pair holding a double from one holding two numbers: the plain mov fxc copies a
    /// double's raw halves with, the join a double operand reads a pair of dwords
    /// through, and the split a store of one takes it apart with.
    /// </summary>
    private readonly HashSet<HlslTreeNode> _doubleValues =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The register components holding the high word of a double that is keyed at
    /// the component below them. Kept apart from the values above because a word
    /// nothing reads must not exist: see ParseAssignmentInstruction.
    /// </summary>
    private readonly HashSet<RegisterComponentKey> _doubleHighWords = [];

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
    /// A hull shader is several programs under one name, and its bytecode runs them
    /// together: the declarations they share, then the phase that runs once per
    /// output control point, then the fork and join phases that compute the patch's
    /// own constants. HLSL writes that as two functions, so each is parsed on its
    /// own - shared declarations and one phase's instructions - and gets a register
    /// state of its own. They have to: both phases write o0, and in one of them that
    /// is a control point's position and in the other a tessellation factor.
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
                // Each body is a method of its own: statements of their own, parsed
                // against the same declarations the main program parsed (its inputs
                // and constants are what the bodies read), and joined back to the
                // classes by the writer.
                IList<IStatement> mainStatements = _statements;
                Stack<IStatement> mainStack = _currentStatements;
                // What the declarations leave in the map where main parsed: every
                // read is a RegisterInputNode naming the register itself - the
                // inputs, the constant buffers, the resources, the samplers. A body
                // starts from those and from none of main's values: what it reads
                // before it writes is a parameter, and a parameter is named for the
                // register, never for whatever main last left in one.
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
                linkageBodies = [];
                foreach (LinkageModel.FunctionBodyInfo body in _registerState.Linkage.Bodies)
                {
                    _statements = [];
                    _currentStatements = new Stack<IStatement>();
                    // Stands in for main's dcl instructions, which seeded the same
                    // reads and are not re-parsed here: the body's first instruction
                    // joins it the way main's first joined the first dcl, and its
                    // declaration entries are nothing by the time the statements are
                    // finalized.
                    InsertStatement(new AssignmentStatement(declared));
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

    private void ParseInstruction(D3D10Instruction instruction)
    {
        // Which run of an instanced phase this is comes out as the number it is,
        // because the phase was unrolled into a copy per run. The register it is read
        // through wants no declaration of its own: it stands for nothing HLSL can
        // name, and every read of it is already a constant.
        if (instruction.Opcode == D3D10Opcode.DclInput
            && instruction.GetOperandType(0) == OperandType.InputForkInstanceID)
        {
            return;
        }
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
                case D3D10Opcode.CutStream:
                    InsertRestartStrip(instruction.Stream);
                    break;
                // One stream is the ordinary geometry shader and the only one the
                // writer has a name for; the instruction names it because shader
                // model 5 allows up to four.
                case D3D10Opcode.EmitThenCut:
                case D3D10Opcode.EmitThenCutStream:
                    InsertAppend(instruction.Stream);
                    InsertRestartStrip(instruction.Stream);
                    break;
                // Shader model 5 allows four. Each is a parameter of its own with its
                // own output registers, and the stream travels with the instruction, so
                // a write of one stream's o1 is a different register from a write of
                // the other's.
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
                        // Every component, the way a texture is declared: a load
                        // names the view with the swizzle that picks its result.
                        for (int component = 0; component < 4; component++)
                        {
                            var destinationKey = new RegisterComponentKey(registerKey, component);
                            SetActiveOutput(destinationKey, new RegisterInputNode(destinationKey));
                        }
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
                        // Every component, the way a texture is declared. A gather
                        // names its channel on the sampler's swizzle - `s0.w` gathers
                        // the alpha channel - and reading only the x component asked
                        // for a component of the sampler that was never seeded.
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
                        // Raw shared memory declares a size in bytes and nothing
                        // else - no stride, no count - because fxc gives this shape
                        // to a groupshared scalar rather than an array. HLSL has no
                        // way to say that but an array, so it is as many four byte
                        // elements as the size holds.
                        uint bytes = instruction.GetParamIndexImmediate32(1, 0);
                        _registerState.DeclareThreadGroupSharedMemory(registerKey,
                            sizeof(uint), bytes / sizeof(uint));
                        // One access can name up to four dwords, and the operand
                        // carries a component per dword whatever the element is a
                        // single one. Seeding by the declared stride left anything
                        // but the first unseeded, and a two component load went
                        // looking for a component of the register that is not there.
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
                        // Address and offset are the same for every component of the
                        // element; the value stored is not.
                        HlslTreeNode address = GetInputs(instruction, destinationKeys[0].ComponentIndex)[0];
                        HlslTreeNode[] values = destinationKeys
                            .Select(key => GetInputs(instruction, key.ComponentIndex)[2])
                            .ToArray();
                        SplitStoredDoubles(values);
                        RecordStoredType(instruction, values);
                        // Storing at a slot an alloc took is the second half of an
                        // Append, and is written as one rather than as a subscripted
                        // store - which an append buffer does not have.
                        if (address is AppendSlotNode appendSlot
                            && appendSlot.Buffer.RegisterComponentKey.RegisterKey.Number
                                == instruction.GetParamRegisterNumber(0))
                        {
                            InsertStatement(new BufferAppendStatement(
                                appendSlot.Buffer, values, ActiveOutputs));
                            break;
                        }
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
                        // A buffer that only keeps a counter gives the slot back and
                        // nothing else: the element is read through a subscript, so
                        // the slot is a value the shader goes on to use rather than
                        // half of a Consume.
                        HlslTreeNode slot = IsConsumeBuffer(instruction.GetParamRegisterKey(1))
                            ? new ConsumeSlotNode(new RegisterInputNode(consumeKey))
                            : new BufferCounterNode(new RegisterInputNode(consumeKey), isIncrement: false);
                        // Which instruction took the slot. Two calls are independent in
                        // the graph - neither reads what the other left - so this is the
                        // only thing that says which consumed first, and with a consume
                        // buffer the order is the whole difference between them.
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
                        // imm_atomic_alloc takes the next slot and the store
                        // addressed by it puts the element there: together they are
                        // one Append. The store is not always the instruction after -
                        // what is appended is computed between the two whenever it
                        // reads nothing the alloc needed - so the slot is recorded
                        // here and the store makes the call, the way a consume's slot
                        // waits for the loads that read it.
                        var appendKey = new RegisterComponentKey(
                            instruction.GetParamRegisterKey(1), 0);
                        // Only an append buffer has no other spelling. A structured
                        // one that keeps a counter takes the slot the same way and
                        // stores through a subscript, which is an ordinary store, so
                        // the slot is a value rather than half of an Append.
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
                        // A typed view written by an interlocked operation holds a
                        // scalar, and is declared as one.
                        _registerState.DeclareAtomicTarget(resourceKey.RegisterKey);
                        var destination = new RegisterInputNode(resourceKey);
                        bool hasCompare = instruction.Opcode
                            is D3D10Opcode.AtomicCmpStore or D3D10Opcode.ImmAtomicCmpExch;
                        // A structured resource addresses an element and a byte offset
                        // within it, both components of the one operand; a byte address
                        // one has only the offset. A typed texture addresses a texel by
                        // a coordinate, as many components as it has dimensions, and
                        // has no byte offset to name a member with.
                        int addressWidth = _registerState.GetAtomicAddressWidth(
                            resourceKey.RegisterKey);
                        HlslTreeNode[] coordinates = addressWidth > 1
                            ? [.. Enumerable.Range(0, addressWidth)
                                .Select(component => GetInputs(instruction, component)[first])]
                            : null;
                        // The same node the coordinate starts with, so that a rewrite
                        // of one is a rewrite of the other.
                        HlslTreeNode address = coordinates?[0] ?? GetInputs(instruction, 0)[first];
                        HlslTreeNode elementByteOffset =
                            _registerState.IsRawResource(resourceKey.RegisterKey) || coordinates != null
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
                            Coordinates = coordinates,
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
                        SplitStoredDoubles(values);
                        RecordStoredType(instruction, values);
                        if (IsGroupSharedResource(instruction, 0))
                        {
                            // Each dword the store covers is an element of the array
                            // groupshared memory is declared as, so one store of a
                            // pair is two assignments rather than one of a pair.
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
                    // Only the one flag the source has to say again. The rest
                    // are what fxc worked out about the shader and writes back
                    // for itself.
                    _registerState.ForceEarlyDepthStencil = instruction.GetGlobalFlags()
                        .HasFlag(D3D10GlobalFlags.ForceEarlyDepthStencil);
                    break;
                // What a patch is and how the tessellator divides it, which HLSL
                // says with the [domain(...)] attribute and the patch's own size.
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
                // The bound on a factor. The shader clamps to it as well - the min is
                // in the instructions - but the bound is a declaration of its own,
                // and comes back only as the attribute.
                case D3D10Opcode.DclHSMaxTessFactor:
                    _registerState.MaxTessFactor = BitConverter.Int32BitsToSingle(instruction.GetParamInt(0));
                    break;
                // A phase boundary. One phase at a time reaches this parser, so the
                // markers are only the seams they were split along.
                case D3D10Opcode.HsDecls:
                case D3D10Opcode.HsControlPointPhase:
                case D3D10Opcode.HsForkPhase:
                case D3D10Opcode.HsJoinPhase:
                    break;
                case D3D10Opcode.DclIndexRange:
                    _registerState.DeclareIndexRange(instruction);
                    break;
                // How many times the phase runs, each run knowing which it is. The
                // phase is parsed once per instance, so by the time the body is
                // reached this has already been acted on.
                case D3D10Opcode.DclHSForkPhaseInstanceCount:
                case D3D10Opcode.DclHSJoinPhaseInstanceCount:
                    break;
                // The shape of the dynamic linkage is read off these three
                // declarations before anything is parsed - see LinkageModel. Here
                // they are instructions the walk still has to walk past.
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
    /// Where the uniforms a preshader reads are put, when the shader does not read
    /// them itself: past every float constant register any shader model has, so
    /// that none of them is one the shader reads for something else.
    /// </summary>
    private const int PreshaderInputBase = 0x2000;

    private readonly Dictionary<RegisterComponentKey, HlslTreeNode> _preshaderOutputs = [];

    /// <summary>
    /// The constants an fx_2_0 preshader computes, as what it computes them from.
    /// The shader's own table lists only the uniforms it reads as they are, and the
    /// preshader's lists those it reads, in a table of its own. One the shader reads
    /// as well is the same uniform and is named once; the rest are declared beside
    /// the shader's. Each register the preshader writes is then the expression it
    /// wrote there, and every read of it is that expression.
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
    /// A sampler the constant table does not name - there is no table, as in a
    /// shader assembled by hand or one rewritten from ps_1_x - is named for its
    /// register, and its type is the one its dcl gives it. The constant table
    /// comes first, so a sampler it names is already declared by here.
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

    private bool IsAppendBuffer(RegisterKey registerKey)
    {
        return _registerState.ResourceDefinitions.Any(d => d.BindPoint == registerKey.Number
            && d.ShaderInputType == D3DShaderInputType.UavAppendStructured);
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

    private void InsertAppend(int? stream = null)
    {
        InsertStatement(new AppendStatement(ActiveOutputs) { Stream = stream });
    }

    /// <summary>
    /// A call through an interface. The body it runs reads its arguments straight out
    /// of the caller's registers - so the call site hands over what those registers
    /// hold right here, whole registers at a time - and leaves its result in the one
    /// register the body writes, which every later read of it then finds as the call.
    /// </summary>
    private void InsertInterfaceCall(D3D10Instruction instruction)
    {
        // The function index sits in front as a dword of its own, and the interface
        // operand behind it carries the interface and the instance - the layout
        // AsmWriter prints as fp1[2][0].
        int function = (int)instruction.OperandTokens.Tokens[0];
        int interfaceNumber = (int)instruction.OperandTokens.Tokens[2];
        int instance = (int)instruction.OperandTokens.Tokens[3];
        LinkageModel.MethodInfo method =
            _registerState.Linkage.MethodForCall(interfaceNumber, function);

        var arguments = new List<HlslTreeNode>();
        foreach (RegisterKey parameter in method.Parameters)
        {
            for (int component = 0; component < 4; component++)
            {
                arguments.Add(GetActiveOutput(new RegisterComponentKey(parameter, component)));
            }
        }

        for (int component = 0; component < 4; component++)
        {
            var callNode = new InterfaceCallNode(
                interfaceNumber, instance, function, [.. arguments], component);
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
    private HlslTreeNode GetConditionNode(D3D10Instruction instruction)
    {
        byte component = instruction.GetSourceSwizzleComponents(0)[0];
        RegisterKey registerKey = instruction.GetParamRegisterKey(0);
        HlslTreeNode condition = GetActiveOutput(new RegisterComponentKey(registerKey, component));

        // Two conditions combined are a condition: `if (a < b && c < d)`, and not
        // `if ((a < b && c < d) != 0)`, which fxc compiles with a compare and an
        // and the original had no need of.
        if (instruction.TestNonZero && condition is LogicalAndOperation or LogicalOrOperation)
        {
            return condition;
        }

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
            comparison = new ComparisonNode(condition, new ConstantNode(0), IfComparison.NE)
            {
                IsBitsTest = true,
            };
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

    // One component of an integer operand, with its modifier: `imul null, r3.y,
    // r1.z, -r1.z` is -(k * k), and reading the register without the negation
    // made it k * k - the wrong sign on a Gaussian's exponent, and nothing to say
    // so, since the interpreter's trials happened not to run the loop.
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

        // An assignment immediately before the ret produced the value being returned,
        // so it becomes the return rather than standing as its own statement.
        if (ActiveStatement is AssignmentStatement assignment)
        {
            _currentStatements.Pop();
            IList<IStatement> sequence = ActiveStatementSequence;
            if (sequence.Count != 0 && ReferenceEquals(sequence[sequence.Count - 1], assignment))
            {
                // Its inputs as well as its outputs: a return whose two are the same
                // dictionary says it assigns nothing on the way out, which is true of
                // a return added after a statement and false of one that replaced
                // it. Told the first, the writer read every output as carried through
                // unchanged and wrote none of them - so a shader returning a struct
                // that set its fields and returned early from inside an if lost every
                // one of those writes, and returned whatever the struct held.
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
    /// The value the mask stands for. It is the bit pattern of what is wanted, so
    /// against a float comparison it is read as those bits rather than as whatever
    /// the operand typing made of them: step masks with 0x3f800000, which is 1.0f
    /// and not 1065353216. Against an integer comparison the number is the number.
    /// </summary>
    // What a condition selects. A constant is the bit pattern of the value wanted
    // and is read back as that; anything else is already the value.
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
            // Against an integer comparison the number is the number - unless the
            // and writes a float output. What an output is handed are its own
            // bytes, and the output signature says what they are: an isinf() test
            // anded with the bits of 1.0f selects the float 1.0, and printing the
            // number those bits make - 1065353216 - selects that instead.
            return WritesFloatOutput(instruction) ? AsFloatBits(mask) : mask;
        }
        // A float comparison anded with an integer immediate: the bits of the float
        // step() selects - 0x3f800000 for 1.0 - or an integer the shader selects
        // outright, `lum > threshold ? 1u : 0u`. The operand analysis calls both
        // integer, since a mask is bits, so the number itself has to say. Read as
        // a float, a small integer is a denormal, and no shader selects one of
        // those: the 1 printed as 0.000000 and the histogram bin it fed was always
        // zero. Read as an integer, a float's bits are an eight digit number no
        // shader selects either. So the bits of a normal float are that float and
        // anything smaller is the integer it is.
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

    // Whether the instruction writes an output the signature types as a float.
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
        if (registerComponent.RegisterKey is D3D10RegisterKey d3D10RegisterKey)
        {
            if (d3D10RegisterKey.ImmediateDouble != null)
            {
                // The double the read component's pair names, the same as the read
                // of any other operand.
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
            // The upper half of a pair a double sits in, read as a 32-bit value of
            // its own: what is there is the double's high word. Whatever the
            // register held at that component before is stale - the double
            // instruction wrote over it - and read as that a uint2 load's high word
            // was stored back unchanged beside a multiply of the low one, or there
            // was nothing there at all and the parse stopped.
            return new DoubleBitsNode(pairedDouble, 1);
        }
        return ActiveOutputs[registerComponent];
    }

    private void SetActiveOutput(RegisterComponentKey registerComponent, HlslTreeNode value)
    {
        InsertAssignment();
        ActiveOutputs[registerComponent] = value;
        // A write of either half ends whatever double was in the pair: the upper
        // component holds a word of its own now, or the lower one does and there is
        // no double left for the upper one to be the high word of.
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

        foreach (var output in newOutputs)
        {
            SetActiveOutput(output.Key, output.Value);
        }
        // The upper component of every pair a double went into, recorded after the
        // writes rather than keyed like one. The instruction wrote both halves of
        // the pair and the double is keyed at the lower one, so what the upper one
        // holds is the double's high word - but keying a value there would make it
        // an output of the statement and a second reader of the double, and what
        // reads a value is what decides whether the writer names it or writes it
        // inline: half the double fixtures grew a variable for a word no store ever
        // asked for. Recorded instead, the word is made where something reads it
        // and nowhere else - see GetActiveOutput.
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
        // A double lives across two components and the tree holds one value for
        // each, so a double destination is keyed at the lower component of every
        // pair it writes and the upper ones get no value of their own. The pairs
        // are aligned - .xy and .zw - so what is left of the mask is bits 0 and 2.
        if (instruction is D3D10Instruction d3d10)
        {
            mask = d3d10.WritesDoubles
                ? mask & 0b0101
                : GetMovedDoubleMask(d3d10, GetStructuredValueMask(d3d10, mask));
            // An output written through an index - `mov o[r0.x + 0].x` - where the
            // index is a value already known. fxc writes the tessellation factors of
            // a patch this way when they are all computed the same, one phase run per
            // factor, and the run's own number is what the index holds; the phase is
            // unrolled into a copy per run, so by here the number is a constant and
            // the register is the one that run writes. Left to the operand, the
            // register number decoded from a relative index is meaningless - it named
            // an output the signature has never heard of.
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
    /// The output register a relative-addressed write reaches, or null where the
    /// operand is not one or the index is not a value this parser already holds.
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
            || IndexConstant(value) is not ConstantNode constant)
        {
            return null;
        }
        int offset = constant.IntegerValue ?? (int)constant.Value;
        return new D3D10RegisterKey(OperandType.Output, (int)indices[0].Immediate + offset);
    }

    // The number an index holds, through the moves fxc puts in the way: it reads
    // vForkInstanceID into a register of its own and indexes by that, so the number
    // is a move or two away from the index.
    private static ConstantNode IndexConstant(HlslTreeNode value)
    {
        while (value is MoveOperation move)
        {
            value = move.Inputs[0];
        }
        return value as ConstantNode;
    }

    /// <summary>
    /// Whether the value an instruction writes at a destination is a double. The
    /// double instructions say so by their opcode; a structured load by the member it
    /// reads; and a mov by what it is moving, which is the only way to tell - fxc
    /// assembles a double vector out of plain movs of the raw halves, and nothing
    /// about `mov r0.zw, r0.xxxy` says it carries one number rather than two floats.
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
            int elementByteOffset = instruction.GetOperandType(2) == OperandType.Immediate32
                ? instruction.GetParamInt(2, 0)
                : 0;
            return _registerState.IsDoubleStructuredMember(
                instruction.GetParamRegisterKey(3),
                elementByteOffset + swizzle[destinationKey.ComponentIndex] * 4);
        }
        return instruction.Opcode is D3D10Opcode.Mov or D3D10Opcode.DMov
            && IsMovedDouble(instruction, destinationKey.ComponentIndex);
    }

    /// <summary>
    /// Whether a mov is carrying a double: the value sitting at the component it
    /// reads is one. Asked of the live value rather than of the register, so a
    /// register that held a double earlier and holds a float now answers for the
    /// float.
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
    /// The components of a mov that begin a value, where it is copying doubles. fxc
    /// moves a double as the two raw components it is - `mov r0.zw, r0.xxxy` puts the
    /// pair at r0.xy into the pair at r0.zw - and keyed a value to each of them the
    /// upper one asked for a half no value was ever keyed at.
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
            // Both halves of an aligned pair, and the lower one carrying a double:
            // anything else is the mov of floats it looks like.
            if ((mask & (0b11 << pair)) == (0b11 << pair) && IsMovedDouble(instruction, pair))
            {
                valueMask &= ~(1 << (pair + 1));
            }
        }
        return valueMask;
    }

    /// <summary>
    /// One component of an msad4, with the two words of its source recovered. HLSL
    /// takes the source as a uint2 and the instruction as four windows sliding a byte
    /// at a time along the eight bytes they make, so fxc builds the windows in front
    /// of the instruction and there is nothing else the operand can be written as.
    /// </summary>
    private HlslTreeNode CreateMsad4Node(D3D10Instruction instruction, int componentIndex)
    {
        const int WindowsOperand = 2;
        HlslTreeNode[] windows = GetInputComponents(instruction, WindowsOperand, 4);
        HlslTreeNode[] inputs = GetInputs(instruction, componentIndex);
        if (!TryGetMsadSource(instruction, WindowsOperand, windows,
            out HlslTreeNode low, out HlslTreeNode high))
        {
            throw new NotImplementedException(
                "msad over windows this does not recognise as a uint2");
        }
        var node = new Msad4Node(inputs[0], low, high, inputs[2], componentIndex);
        // The node reads the two words, so the bfi over each window is read by nothing
        // from here on. The register it was written to says nothing about that - the
        // msad overwrites it, and a value a register no longer holds is only as dead as
        // the graph says - so left attached it counts as a reader of the shifts under
        // it, and the writer went on naming `uint3 t0 = source >> uint3(8, 16, 24)` for
        // a value nothing reads. Only the windows themselves: what they read is either
        // the two words, which the node reads now, or the shifts, which fall out as
        // unread assignments on their own once nothing holds them here.
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
    /// The two words an msad's windows were built from. The first window is the low
    /// word as it stands; each of the others shifts it down by a byte more and fills
    /// the top that empties with the bottom of the high word - a bfi of that many bits
    /// at the complementary offset. Anything else is not this shape and is not an
    /// msad4 that can be written.
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
        // Nothing inserted over the top of any window: the high word was zero, so
        // there was nothing to insert and fxc folded the bfi away. Asked of all three
        // together, since a real high word needs one at every window and a zero one
        // needs none - a shader with some of each is not this shape.
        if (AllWindowsAreShiftsOf(windows, low))
        {
            high = new ConstantNode(0);
            return true;
        }
        // Where the high word is the known one, fxc adds it in rather than inserting
        // it: the top of a shifted low word is zeroes, so an or is an add and an add
        // of a literal is one instruction where the bfi was three operands of them.
        if (TryGetAddedMsadSource(windows, low, ref high))
        {
            return true;
        }
        // And the other way about: the low word known and the high one not, so fxc
        // shifts the low word itself and inserts the register over the top of the
        // number that comes to.
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

    // Through the moves fxc leaves between a value and where it is read.
    /// <summary>
    /// The windows as fxc folds them where it knows both words: four literals, each
    /// the eight byte source shifted down by one byte more than the last. The low word
    /// is the first of them and the high word's bottom three bytes are the top three
    /// of the last; the high word's fourth byte slides past the end of the windows and
    /// is no part of what msad4 computes, so zero stands in for it. The literal that
    /// comes back is therefore not always the one the shader was written with, and is
    /// always one that computes the same thing - fxc folds it to these same four
    /// windows again.
    /// </summary>
    private static bool TryGetFoldedMsadSource(D3D10Instruction instruction, int operand,
        ref HlslTreeNode low, ref HlslTreeNode high)
    {
        if (instruction.GetOperandType(operand) != OperandType.Immediate32)
        {
            return false;
        }
        // The bits, from the instruction rather than off the nodes. An immediate's
        // thirty-two bits are typed by what consumes them and these reach the graph as
        // floats, which is lossy for a number this size: 0x08010203 came back as
        // 0x08010200, and the sliding relation below then holds for nothing.
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

    /// <summary>
    /// Whether a window's value is the low word shifted down by that many bits - the
    /// shift fxc emits where the low word is a register, or the number that shift comes
    /// to where it is a literal and fxc did it itself.
    /// </summary>
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
    /// The windows where the low word is the literal: fxc shifts it down itself, so
    /// each window's value is the number that comes to and the register holding the
    /// high word is inserted over the top of it.
    ///
    /// The low word has to be read as the bits it is. An immediate a mov carried here
    /// is typed by whatever reads it, which happens after parsing, so until then it is
    /// a float - and 0x01020304 is 2.4e-38 as one, which is zero as a number. The
    /// constant the node reads from is replaced with those bits for the same reason.
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

    // A constant's thirty-two bits where they are known exactly: one already typed as
    // an integer, or one a mov carried in and the parser recorded on the way past. A
    // float constant is none of those and says so, rather than handing back a number
    // that is not what the bits are.
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
    /// The windows as fxc builds them where the high word is a literal and the low one
    /// is not: each is the low word shifted down and the high word's contribution added
    /// on, since the top of the shifted word is zeroes and an or of a literal into
    /// zeroes is an add of it. The last window carries three bytes of the high word,
    /// which is all of it that msad4 ever reads, and the other two have to agree with
    /// those - so the shape is checked rather than taken on the strength of one window.
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
    /// The components of a structured load or store that begin a value of the
    /// element, where the element holds doubles. A double is eight bytes and the
    /// components count in four, so two of them carry one number: a load of a
    /// StructuredBuffer&lt;double&gt; is `ld_structured r0.xy` for the one value, and
    /// keyed a value to each component the second one named a member of the element
    /// that is not there. Unchanged for every other instruction and every element
    /// without a double in it, which is all but a handful of shaders.
    /// </summary>
    private int GetStructuredValueMask(D3D10Instruction instruction, int mask)
    {
        const int ByteOffsetOperand = 2;
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
        int elementByteOffset = instruction.GetOperandType(ByteOffsetOperand) == OperandType.Immediate32
            ? instruction.GetParamInt(ByteOffsetOperand, 0)
            : 0;
        // Which part of the element a component is: a load says so on its resource
        // operand, whose swizzle picks the member each destination component takes,
        // and a store by the mask's own position - `store_structured u0.xyz, i,
        // l(8), ...` writes the element from byte eight on.
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
            case Opcode.M3x2:
            case Opcode.M3x3:
            case Opcode.M3x4:
            case Opcode.M4x3:
            case Opcode.M4x4:
                return CreateMatrixProductNode(instruction, componentIndex);
            case Opcode.Crs:
                return CreateCrossProductNode(instruction, componentIndex);
            case Opcode.Sgn:
                // The two registers after the value are scratch space the hardware
                // wanted and nothing reads, so they are not operands of the result.
                return ReadingFloats(new SignOperation(
                    GetInputComponents(instruction, 1, componentIndex + 1)[componentIndex]));
            case Opcode.Nrm:
                return CreateNormalizeOutputNode(instruction, componentIndex);
            case Opcode.Dst:
                // The fixed-function distance step: [1, a.y * b.y, a.z, b.w]. The
                // component being written decides which parts of the sources to
                // read, so the reads are per component here, and two of the four
                // results are the sources themselves.
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
        // Where the value came from, so that the components of one instruction can
        // be told from two that share a register. The position in the shader: no
        // two instructions have the same, and every component of this one does.
        StampSourceInstruction(node, destinationKey.ComponentIndex);
        return node;
    }

    /// <summary>
    /// Marks a value with the instruction being parsed, unless it is a value that
    /// was read rather than computed - an operand carries the instruction that made
    /// it, not the one reading it.
    /// </summary>
    private void StampSourceInstruction(HlslTreeNode node, int component)
    {
        if (node.SourceInstruction == 0 && node is not RegisterInputNode and not ConstantNode)
        {
            node.SourceInstruction = _instructionPointer + 1;
            node.SourceComponent = component;
        }
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
            // A conversion into a double reads an integer the same way one into a
            // float does; itod of a loop counter is the counter and not its bits.
            case D3D10Opcode.IToD:
            case D3D10Opcode.UToD:
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
        for (int i = 0; i < values.Length; i++)
        {
            // A literal the store is handed itself, rather than through a mov, was
            // typed by the opcode, and store_structured is not an integer one: the
            // bits 0x7F7FFFFF stored into a uint array came out as the float they
            // spell, 3.4e38, which the array then converted. Into integer memory it
            // is the integer its bits are - the same thirty-two bits either way.
            if (kind == ValueKind.Integer
                && values[i] is ConstantNode { IntegerValue: null } literal
                && !_polymorphicImmediates.Any(p => ReferenceEquals(p.Constant, literal)))
            {
                values[i] = new ConstantNode(BitConverter.SingleToInt32Bits(literal.Value));
            }
            _storedTypes[values[i]] = kind == ValueKind.Integer;
        }
    }

    private void ResolvePolymorphicImmediates()
    {
        foreach ((ConstantNode constant, uint bits) in _polymorphicImmediates)
        {
            bool? consumedAsInteger = GetConsumedType(constant, _storedTypes);
            // Bits that are a NaN as a float were not a float the source wrote - HLSL
            // has no NaN literal - so where the readers do not say, they are the
            // integer they are. A -1 flag carried through a movc into the condition
            // of another, which only asks whether its bits are zero, came out as
            // `NaN`: an undeclared identifier to fxc.
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
                bool? consumed = reader is ComparisonNode comparison
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
            // The four components are the four windows of one call, so the component
            // decides the value rather than which part of the source is read - and the
            // source has to be read back out of the windows fxc built.
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
            // The double precision arithmetic, which is the arithmetic it is named
            // after: the type is what the operands are, not what the operation does,
            // and the pairing of components is already off the keys by here.
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
                                // A load from a consume buffer is a component of the
                                // Consume that took the slot: the address is what the
                                // consume left, and the byte offset says which
                                // component of the element this is.
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
                                return new LoadStructuredNode(inputs[0], inputs[1], inputs[2])
                                {
                                    ElementByteOffset = elementByteOffset,
                                    // The component the resource operand selects is
                                    // which part of the element this load is, so the
                                    // member it reads is the one at that address and
                                    // not the one the element starts with. Groupshared
                                    // memory has no reflection data to ask, and holds
                                    // what the writer declares it as - the rule the
                                    // analysis answers by. A uint array an atomic
                                    // reaches holds a float's bits where the shader
                                    // stores one, and a float read back out of it was
                                    // converted from the integer those bits are.
                                    IsIntegerElement = instruction.GetOperandType(3) == OperandType.ThreadGroupSharedMemory
                                        ? _integerOperandAnalysis.GetStructuredElementKind(instruction) == ValueKind.Integer
                                        : _registerState.IsIntegerStructuredMember(
                                            instruction.GetParamRegisterKey(3),
                                            elementByteOffset
                                                + ((IHasComponentIndex)inputs[2]).ComponentIndex * 4),
                                    IsUnsignedElement = _registerState.IsUnsignedStructuredMember(
                                        instruction.GetParamRegisterKey(3),
                                        elementByteOffset
                                            + ((IHasComponentIndex)inputs[2]).ComponentIndex * 4),
                                };
                            }
                        case D3D10Opcode.LdRaw:
                            // ld_raw dst, byteOffset, t#: the offset stands where an
                            // element index would, and there is no offset within one.
                            // Out of groupshared memory it is an array subscript
                            // instead, because HLSL has no raw groupshared to Load.
                            // The resource swizzle there says which dword after the
                            // address this component reads, and each is an element
                            // of its own - so the resource is read whole, at the one
                            // component the declaration seeded, and the swizzle goes
                            // into the subscript.
                            if (IsGroupSharedResource(instruction, 2))
                            {
                                var sharedKey = instruction.GetParamRegisterKey(2);
                                int dword = instruction.GetSourceSwizzleComponents(2)[componentIndex];
                                return new LoadStructuredNode(
                                    GroupSharedElementOfByteAddress(inputs[0], dword),
                                    new ConstantNode(0),
                                    GetActiveOutput(new RegisterComponentKey(sharedKey, 0)))
                                {
                                    // Raw memory holds bits whichever it is, so a
                                    // reader wanting a float reinterprets rather
                                    // than converts: left structured, the
                                    // 1069547520 a 1.5f was stored as multiplied
                                    // as itself.
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
                        // fma rounds the once where a multiply and an add round
                        // twice, so it is the intrinsic and not the arithmetic: fxc
                        // compiles `a * b + c` on doubles to dmul and dadd, and only
                        // a written fma() to this.
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
            case D3D10Opcode.EvalCentroid:
                {
                    // The attribute, and where to evaluate it: a sample index in one
                    // component, an offset in two, or nothing at all for the
                    // centroid. The place is the same for every component of the
                    // result, the way a sample's coordinate is.
                    EvaluateAttributeAt place = instruction.Opcode switch
                    {
                        D3D10Opcode.EvalSnapped => EvaluateAttributeAt.Snapped,
                        D3D10Opcode.EvalCentroid => EvaluateAttributeAt.Centroid,
                        _ => EvaluateAttributeAt.Sample,
                    };
                    bool isSnapped = place == EvaluateAttributeAt.Snapped;
                    HlslTreeNode value = GetInputs(instruction, componentIndex)[0];
                    // A snapped offset is written as an immediate, and it is a
                    // count of sixteenths of a pixel rather than the float those
                    // bits spell.
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

        // The resource operand's swizzle says which channel of the texel each
        // component of the result is: `ld r0.z, r0.z, t2.x` puts the red channel
        // in z. Reading it at the first component alone named the result by the
        // destination, and a Texture2D<float> came out read as `.z`.
        var resource = GetInputComponents(instruction, ResourceParamIndex, 4)[outputComponent] as RegisterInputNode;
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

        return new ResourceLoadNode(resource, address, resource.RegisterComponentKey.ComponentIndex, sampleIndex)
        {
            SampleOffsets = instruction.SampleOffsets,
            IsIntegerTexel = definition?.IsIntegerReturnType,
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
            ReportsStride = _registerState.IsStructuredResource(resource.RegisterComponentKey.RegisterKey),
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
    private HlslTreeNode CreateSampleInfoNode(D3D10Instruction instruction, int outputComponent)
    {
        const int ResourceParamIndex = 1;

        // The rasterizer asks about the render target, which is no resource and has
        // no declaration to look up: reading it as a register found nothing seeded
        // under that name and threw.
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

    // samplepos dest, resource, sampleIndex: the position of one sample within the
    // pixel, which is a float2 whichever component is asked for.
    private SamplePositionNode CreateSamplePositionNode(D3D10Instruction instruction, int outputComponent)
    {
        const int ResourceParamIndex = 1;
        const int SampleIndexParamIndex = 2;

        // The rasterizer carries a swizzle picking x or y of the float2 the way a
        // resource operand does, but nothing seeds it as a register, so the node is
        // made from the operand itself.
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
            // A shader model 1 tex names no sampler, and before ps_1_4 no
            // coordinate either: both are the destination register's own number,
            // because a texture stage is what it writes to. Nothing here reads a
            // shader that way yet, and the operand a ps_2_0 texld would have
            // walks off the end of one that has two parameters or one.
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
    /// <summary>
    /// Whether a raw load or store reaches groupshared memory rather than a buffer.
    /// A ByteAddressBuffer is something HLSL can say and a groupshared one is not,
    /// so only the second has to be turned back into an array subscript.
    /// </summary>
    private static bool IsGroupSharedResource(D3D10Instruction instruction, int operandIndex)
    {
        return instruction.GetParamRegisterKey(operandIndex)
            is D3D10RegisterKey { OperandType: OperandType.ThreadGroupSharedMemory };
    }

    /// <summary>
    /// The element a raw groupshared access names, from the byte address it carries.
    /// fxc gives a groupshared variable that is not an array this shape, and the
    /// only way to declare one in HLSL is as an array of four byte elements, so the
    /// element is the address over four - folded where the address is the literal
    /// it almost always is, since the address of a variable's component is a
    /// constant offset. One access can reach several dwords, and each of them is an
    /// element of its own, so which one it is counts on top.
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

    // A matrix product is a dot product per row against consecutive registers:
    // m4x4 o0, v0, c0 is the dot of v0 with c0 for o0.x, with c1 for o0.y, and so
    // on, which is the same tree fxc builds out of the dp4s it writes instead. The
    // first number in the mnemonic is how wide the vector and each row are; the
    // second is how many rows there are, and the row is the component being
    // written. The matrix operand names the first register and is read without its
    // swizzle, the way the interpreter reads it.
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

    // Each component of a cross product is a difference of two products of the
    // other two components, and crs writes nothing into the fourth.
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

        return operands
            .Select(componentInput => (HlslTreeNode)ReadingFloats(
                new MultiplyOperation(componentInput[0], componentInput[1])))
            .Aggregate((addition, addend) => ReadingFloats(new AddOperation(addition, addend)));
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
        _registerState.DeclareImmediateConstantBufferRead(registerKey.Number);
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
        var componentKey = new RegisterComponentKey(registerKey, swizzle[componentIndex]);

        // Indexed by registers, as the bytecode does; where that is the element times
        // the rows of a matrix, ParsedIdioms takes the multiplication off.
        return new RelativeAddressNode(componentKey, index);
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
        HlslTreeNode address = GetActiveOutput(addressKey);

        // Indexed by registers, as the DXBC read is; ParsedIdioms takes off the
        // element's stride where the mova was handed the element times the rows.
        return new RelativeAddressNode(inputKey, address);
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
        // Which value of the instruction is being built, where that is not the
        // component it is written at: a double takes two components, so dtof's
        // second result is written at .y and reads the double at .zw.
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
                // Where the instruction writes doubles, the operand pair read is
                // the destination's own: the double written at .zw is read from
                // the second pair of every double operand, whether or not it is
                // the instruction's first value. A comparison answers a value per
                // pair and reads its operands one pair per value.
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
                    // A control point of a patch is read the way a vertex of a
                    // primitive is - `vicp[2][0]` is which control point then which
                    // register of it - so a hull shader that loops over its control
                    // points rather than being unrolled reaches here, and used to
                    // stop the parse outright.
                    OperandType.Input or OperandType.InputControlPoint
                        => GetDynamicVertexInput(
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
            // Which run of an instanced phase this is, which the unrolling settled:
            // every copy of the body reads it as the number of its own run.
            if (operandType == OperandType.InputForkInstanceID)
            {
                inputs[i] = new ConstantNode(instruction.ForkInstance ?? 0);
                continue;
            }
            if (operandType == OperandType.Immediate64)
            {
                // The operand holds two doubles and the component being read names
                // a pair of its slots, which is one of them. A double instruction
                // has already said which of its values this is by here.
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
    /// The double a double operand's register pair holds: the value at the lower
    /// component where that one is a double already, and the bits of both
    /// components joined where it is not.
    ///
    /// A pair holds a double already where a double instruction wrote it or a
    /// member declared one was loaded into it, which is what the parser's record of
    /// doubles says. Where neither did, the two components are two values of their
    /// own - a uint2 member loaded into the pair, say - and what the instruction
    /// reads is a double built out of their bits. That is `asdouble`, and it costs
    /// no instruction: reading a register pair as a double is free, so the only
    /// thing that says a join happened is that the pair was not a double before it.
    /// Read as the lower component alone the high word went missing, and a dmul of
    /// the pair came back as a single multiply of the low word.
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
        // The pair's upper slot, which fxc writes beside the lower one: a double
        // operand repeats its pair across the four swizzle slots - .xyxy for the
        // double at x - so the high word of the double at slot n is at slot n + 1.
        RegisterComponentKey highKey = GetParamRegisterComponentKey(
            instruction, inputParameterIndex, componentIndex + 1);
        var joined = new BitsToDoubleOperation(low, GetActiveOutput(highKey));
        _doubleValues.Add(joined);
        return joined;
    }

    /// <summary>
    /// A double a store is writing out as the two dwords it is made of. A store
    /// names the components of the element it writes, so one that names both halves
    /// of the pair a double sits in is taking it apart: the high word is keyed at
    /// the upper component already, and this is what makes the lower one the low
    /// word rather than the double itself.
    ///
    /// Where the element's member is a double nothing names the upper half - the
    /// load and the store of a `StructuredBuffer&lt;double&gt;` key one value to the
    /// pair, which is what the element holds - so that store is left alone.
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
    /// Whether the pair a component begins is a double already, rather than two
    /// 32-bit values a double instruction is about to read as one. Two things say
    /// so, and both have to be asked: the parser's record, which holds every double
    /// an instruction wrote or a member declared one was loaded into, and the
    /// declaration, for the one operand that is read without being written. A
    /// constant buffer variable declared double is read straight off the register
    /// pair it occupies - `dmul r0.xy, r0.xy, cb0[0].zw` - and its two components
    /// name the one variable, so joined they came out as `asdouble(scale, scale)`.
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
                // The resource, the address and the value: the destination register
                // is not a source, and the resource is read as one here only so that
                // the operands after it line up.
                return 3;
            case D3D10Opcode.ImmAtomicCmpExch:
            // The width, the offset, the bits going in and the value they go into.
            case D3D10Opcode.BFI:
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
            // The centroid takes no saying where.
            case D3D10Opcode.EvalCentroid:
                return 1;
            // The width, the offset and the value.
            case D3D10Opcode.UBFE:
            case D3D10Opcode.IBFE:
            case D3D10Opcode.IMad:
            case D3D10Opcode.Umad:
            case D3D10Opcode.Mad:
            case D3D10Opcode.MovC:
            case D3D10Opcode.LdStructured:
            case D3D10Opcode.StoreStructured:
            case D3D10Opcode.AtomicCmpStore:
            // The reference, the windows of the source, and what to add.
            case D3D10Opcode.MSAD:
            case D3D10Opcode.DMovC:
            case D3D10Opcode.DFMA:
                return 3;
            case D3D10Opcode.LDMS:
                return 3;
            case D3D10Opcode.ResInfo:
            case D3D10Opcode.LdRaw:
            case D3D10Opcode.StoreRaw:
            // The resource and which of its samples.
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
        // A hull shader reads a patch constant it computed itself: a join phase's
        // vpc6 is what a fork phase wrote to o6, and the two are one function here.
        // Read as a register of its own it was a value from nowhere, and the writer
        // put the write of it after the reads, since nothing connected them.
        if (_shaderModel.Type == ShaderType.Hull
            && registerKey is D3D10RegisterKey { OperandType: OperandType.InputPatchConstant } patchConstant)
        {
            registerKey = new D3D10RegisterKey(OperandType.Output, patchConstant.Number);
        }
        return new RegisterComponentKey(registerKey, componentIndex);
    }
}
