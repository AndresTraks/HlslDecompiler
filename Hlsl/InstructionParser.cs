using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

class InstructionParser
{
    private ShaderModel _shaderModel;
    private RegisterState _registerState;
    private IList<IStatement> _statements;
    private Stack<IStatement> _currentStatements;
    private int _instructionPointer;
    private IntegerOperandAnalysis _integerOperandAnalysis;

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
        // and SinCos and Udiv each write two destinations, so none of them fits the
        // single-destination assignment path.
        if (instruction.HasDestination
            && instruction.Opcode != D3D10Opcode.StoreStructured
            && instruction.Opcode != D3D10Opcode.SinCos
            && instruction.Opcode != D3D10Opcode.Udiv)
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
                        _registerState.DeclareResource(registerKey, instruction.GetResourceDimension(), instruction.GetResourceReturnTypeToken());
                        var destinationKey = new RegisterComponentKey(registerKey, 0);
                        var resourceInput = new RegisterInputNode(destinationKey);
                        SetActiveOutput(destinationKey, resourceInput);
                        break;
                    }
                case D3D10Opcode.DclResourceStructured:
                    {
                        var registerKey = instruction.GetParamRegisterKey(0);
                        _registerState.DeclareStructuredBuffer(registerKey, instruction.GetResourceStructuredBufferStride());
                        SeedResourceComponents(registerKey);
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
                        InsertStatement(new StoreStructuredStatement(output, address, values, ActiveOutputs));
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
        return instruction is D3D10Instruction d3d10 && d3d10.Opcode == D3D10Opcode.StoreStructured;
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

        if (condition is ComparisonNode)
        {
            return condition;
        }
        // A float comparison feeding a branch reads as the condition itself rather
        // than as a value tested against zero.
        if (condition is GreaterEqualOperation greaterEqual)
        {
            return new ComparisonNode(greaterEqual.Inputs[0], greaterEqual.Inputs[1], IfComparison.GE);
        }
        return new ComparisonNode(condition, new ConstantNode(0), IfComparison.NE);
    }

    /// <summary>
    /// <c>sincos dstSin, dstCos, src</c> writes two registers from one source. Either
    /// destination may be null when the shader only wants one of the two results.
    /// </summary>
    // udiv writes the quotient to its first destination and the remainder to its
    // second, either of which may be null when only one is wanted.
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
                    ? new DivisionOperation(dividend, divisor)
                    : new ModuloOperation(dividend, divisor);
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

    // A comparison result. GE is still modelled as an operation rather than a
    // ComparisonNode, unlike every other comparison, so it has to be named here.
    private static bool IsCondition(HlslTreeNode node)
    {
        return node is ComparisonNode || node is GreaterEqualOperation;
    }

    /// <summary>
    /// `and` and `or` combine comparison masks, which a shader may mean in two
    /// different ways. Two conditions are a logical operator. A condition masked with
    /// a constant is the `cond ? constant : 0` idiom that step() and friends compile
    /// to, where the constant is the bit pattern of the wanted value.
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
            if (IsCondition(inputs[0]) && inputs[1] is ConstantNode)
            {
                return new MoveConditionalOperation(inputs[0], inputs[1], new ConstantNode(0));
            }
            if (IsCondition(inputs[1]) && inputs[0] is ConstantNode)
            {
                return new MoveConditionalOperation(inputs[1], inputs[0], new ConstantNode(0));
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
                if (parentNode is PhiNode headerPhi && !headerPhi.IsLoopHeader)
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

        foreach (var trueOutput in ifStatement.TrueBody.Last().Outputs)
        {
            RegisterComponentKey registerComponent = trueOutput.Key;
            HlslTreeNode trueNode = trueOutput.Value;
            if (ifStatement.FalseBody != null && ifStatement.FalseBody.Last().Outputs.TryGetValue(registerComponent, out var falseNode))
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
            foreach (var falseOutput in ifStatement.FalseBody.Last().Outputs)
            {
                RegisterComponentKey registerComponent = falseOutput.Key;
                HlslTreeNode falseNode = falseOutput.Value;
                if (ifStatement.TrueBody.Last().Outputs.ContainsKey(registerComponent))
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
            default:
                throw new NotImplementedException($"{instruction.Opcode} not implemented");
        }
    }

    private HlslTreeNode CreateInstructionTree(D3D10Instruction instruction, RegisterComponentKey destinationKey)
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
            case D3D10Opcode.Div:
            case D3D10Opcode.Eq:
            case D3D10Opcode.Or:
            case D3D10Opcode.Frc:
            case D3D10Opcode.GE:
            case D3D10Opcode.LT:
            case D3D10Opcode.Ne:
            case D3D10Opcode.Ftoi:
            case D3D10Opcode.IAdd:
            case D3D10Opcode.IShl:
            case D3D10Opcode.IMad:
            case D3D10Opcode.IMax:
            case D3D10Opcode.IMin:
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
            case D3D10Opcode.Log:
            case D3D10Opcode.Mad:
            case D3D10Opcode.Max:
            case D3D10Opcode.Min:
            case D3D10Opcode.MovC:
            case D3D10Opcode.Mul:
            case D3D10Opcode.Rsq:
            case D3D10Opcode.Sqrt:
                {
                    HlslTreeNode[] inputs = GetInputs(instruction, componentIndex);
                    switch (instruction.Opcode)
                    {
                        case D3D10Opcode.Add:
                        case D3D10Opcode.IAdd:
                            return new AddOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.IShl:
                            return new ShiftLeftOperation(inputs[0], inputs[1]);
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
                        // Float comparisons, like their integer counterparts, only
                        // ever feed a branch or a movc, so they read as conditions.
                        case D3D10Opcode.LT:
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.LT);
                        case D3D10Opcode.Eq:
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.EQ);
                        case D3D10Opcode.Ne:
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.NE);
                        case D3D10Opcode.Ilt:
                            // Only ever consumed by a branch, so model it as the condition itself.
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.LT);
                        case D3D10Opcode.Ige:
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.GE);
                        case D3D10Opcode.UGE:
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.GE);
                        case D3D10Opcode.ULT:
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.LT);
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.GE);
                        case D3D10Opcode.Ieq:
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.EQ);
                        case D3D10Opcode.Ine:
                            return new ComparisonNode(inputs[0], inputs[1], IfComparison.NE);
                        case D3D10Opcode.IMad:
                            return new MultiplyAddOperation(inputs[0], inputs[1], inputs[2]);
                        case D3D10Opcode.IMin:
                            return new MinimumOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.IMax:
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
                            return new LoadStructuredNode(inputs[0], inputs[1], inputs[2]);
                        case D3D10Opcode.Log:
                            return new LogOperation(inputs[0]);
                        case D3D10Opcode.Mad:
                            return new MultiplyAddOperation(inputs[0], inputs[1], inputs[2]);
                        case D3D10Opcode.Max:
                            return new MaximumOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.Min:
                            return new MinimumOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.Mov:
                        case D3D10Opcode.IToF:
                        case D3D10Opcode.UTof:
                        // TODO: emit an explicit cast rather than relying on implicit conversion.
                        case D3D10Opcode.Ftoi:
                            return new MoveOperation(inputs[0]);
                        case D3D10Opcode.MovC:
                            return new MoveConditionalOperation(inputs[0], inputs[1], inputs[2]);
                        case D3D10Opcode.Mul:
                            return new MultiplyOperation(inputs[0], inputs[1]);
                        case D3D10Opcode.Rsq:
                            return new ReciprocalSquareRootOperation(inputs[0]);
                        case D3D10Opcode.Sqrt:
                            return new SquareRootOperation(inputs[0]);
                        default:
                            throw new NotImplementedException();
                    }
                }
            case D3D10Opcode.LD:
                return CreateResourceLoadNode(instruction, componentIndex);
            case D3D10Opcode.Gather4:
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
        ResourceDefinition definition = _registerState.ResourceDefinitions
            .Where(d => d.ShaderInputType == D3DShaderInputType.Texture)
            .FirstOrDefault(d => d.BindPoint == resource.RegisterComponentKey.RegisterKey.Number);

        // Load reads a texel directly, so it takes the mip level alongside the
        // coordinates: a 2D texture is addressed by an int3.
        int addressLength = (definition?.GetDimensionSize() ?? 2) + 1;
        // The address is in texels, so an immediate operand holds integers rather
        // than the floats those same bits would spell.
        HlslTreeNode[] address = instruction.GetOperandType(AddressParamIndex) == OperandType.Immediate32
            ? [.. Enumerable.Range(0, addressLength)
                .Select(i => (HlslTreeNode)new ConstantNode((int)instruction.GetParamInt(AddressParamIndex, i)))]
            : GetInputComponents(instruction, AddressParamIndex, addressLength);

        return new ResourceLoadNode(resource, address, outputComponent);
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
            const int TextureParamIndex = 2;
            const int SamplerParamIndex = 3;

            var texture = GetInputComponents(instruction, TextureParamIndex, 1)[0] as RegisterInputNode;
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
                _ => TextureLoadControls.None,
            };
            HlslTreeNode[] derivativeX = null;
            HlslTreeNode[] derivativeY = null;
            HlslTreeNode scalarArgument = null;
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
                // as sample, so reading a fifth would run off the end.
                scalarArgument = GetInputComponents(instruction, ExtraParamIndex, 1)[0];
            }

            TextureLoadOutputNode node = TextureLoadOutputNode.CreateSample(
                sampler, texCoords, outputComponent, texture,
                controls, derivativeX, derivativeY, scalarArgument);
            node.SampleOffsets = ((D3D10Instruction)instruction).SampleOffsets;
            return node;
        }
    }

    private HlslTreeNode CreateDotProduct2AddNode(Instruction instruction)
    {
        var vector1 = GetInputComponents(instruction, 1, 2);
        var vector2 = GetInputComponents(instruction, 2, 2);
        var add = GetInputComponents(instruction, 3, 1)[0];

        var dp2 = new AddOperation(
            new MultiplyOperation(vector1[0], vector2[0]),
            new MultiplyOperation(vector1[1], vector2[1]));

        return new AddOperation(dp2, add);
    }

    private HlslTreeNode CreateDotProductNode(D3D9Instruction instruction)
    {
        var addends = new List<HlslTreeNode>();
        int numComponents = instruction.Opcode == Opcode.Dp3 ? 3 : 4;
        for (int component = 0; component < numComponents; component++)
        {
            IList<HlslTreeNode> componentInput = GetInputs(instruction, component);
            var multiply = new MultiplyOperation(componentInput[0], componentInput[1]);
            addends.Add(multiply);
        }

        return addends.Aggregate((addition, addend) => new AddOperation(addition, addend));
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
            var multiply = new MultiplyOperation(componentInput[0], componentInput[1]);
            addends.Add(multiply);
        }

        return addends.Aggregate((addition, addend) => new AddOperation(addition, addend));
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

        // Any vertex will do to find the declaration; they share one.
        var registerKey = D3D10RegisterKey.CreateGSInput((int)operandIndices[1].Immediate, 0);
        byte[] swizzle = instruction.GetSourceSwizzleComponents(operandIndex);
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
            if (operandIndices.Any(index => index.IsRelative))
            {
                // The register number decoded from a relative operand is meaningless,
                // so the element has to be modelled rather than read.
                inputs[i] = operandType switch
                {
                    OperandType.ConstantBuffer => GetDynamicConstantBufferInput(
                        instruction, inputParameterIndex, componentIndex, operandIndices),
                    OperandType.Input => GetDynamicVertexInput(
                        instruction, inputParameterIndex, componentIndex, operandIndices),
                    _ => throw new NotImplementedException(
                        $"Dynamically indexed {operandType} in {instruction.Opcode}"),
                };
                continue;
            }
            if (operandType == OperandType.Immediate32)
            {
                // An immediate's 32 bits are typed by the instruction consuming them.
                inputs[i] = _integerOperandAnalysis.IsIntegerOperand(instruction)
                    ? new ConstantNode((int)instruction.GetParamInt(inputParameterIndex, componentIndex))
                    : new ConstantNode(instruction.GetParamSingle(inputParameterIndex, componentIndex));
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
            bool inputHasPartialPrecision = input is RegisterInputNode registerInput
                && _registerState.MethodInputRegisters.TryGetValue(registerInput.RegisterComponentKey.RegisterKey, out var declaration)
                && declaration.ResultModifier.HasFlag(ResultModifier.PartialPrecision);
            if (!inputHasPartialPrecision)
            {
                // TODO: determine vector size
                result = new CastOperation(result, "half4");
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
            case D3D10Opcode.INeg:
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
            case D3D10Opcode.Ieq:
            case D3D10Opcode.Ige:
            case D3D10Opcode.UGE:
            case D3D10Opcode.ULT:
            case D3D10Opcode.Ilt:
            case D3D10Opcode.IMax:
            case D3D10Opcode.IMin:
            case D3D10Opcode.Ine:
            case D3D10Opcode.Max:
            case D3D10Opcode.Min:
            case D3D10Opcode.Mul:
                return 2;
            case D3D10Opcode.IMad:
            case D3D10Opcode.Mad:
            case D3D10Opcode.MovC:
            case D3D10Opcode.LdStructured:
            case D3D10Opcode.StoreStructured:
                return 3;
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
        else
        {
            byte[] swizzle = instruction.GetSourceSwizzleComponents(paramIndex);
            componentIndex = swizzle[component];
        }
        return new RegisterComponentKey(registerKey, componentIndex);
    }
}
