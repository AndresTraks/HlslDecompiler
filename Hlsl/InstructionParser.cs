using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    class InstructionParser
    {
        private RegisterState _registerState;
        private IList<IStatement> _statements;
        private Stack<IStatement> _currentStatements;

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
            _registerState = new RegisterState(shader);
            _statements = new List<IStatement>();
            _currentStatements = new Stack<IStatement>();

            LoadConstantOutputs(shader);

            int instructionPointer = 0;
            if (shader.Instructions[0] is D3D10Instruction)
            {
                while (instructionPointer < shader.Instructions.Count)
                {
                    ParseInstruction(shader.Instructions[instructionPointer] as D3D10Instruction);
                    instructionPointer++;
                }
            }
            else
            {
                while (instructionPointer < shader.Instructions.Count)
                {
                    ParseInstruction(shader.Instructions[instructionPointer] as D3D9Instruction);
                    instructionPointer++;
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
            if (instruction.HasDestination)
            {
                ParseAssignmentInstruction(instruction);
            }
            else
            {
                switch (instruction.Opcode)
                {
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
                                _registerState.DeclareRegister(registerKey);
                            }
                            break;
                        }
                    case D3D10Opcode.DclConstantBuffer:
                        {
                            int count = (int)instruction.GetParamInt(0);
                            for (int registerNumber = 0; registerNumber < count; registerNumber++)
                            {
                                var registerKey = new D3D10RegisterKey(OperandType.ConstantBuffer, registerNumber);
                                _registerState.DeclareRegister(registerKey);
                            }
                            break;
                        }
                    case D3D10Opcode.DclResource:
                    case D3D10Opcode.DclSampler:
                    case D3D10Opcode.Ret:
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
            foreach (ConstantDeclaration constant in constantTable.Declarations)
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
                D3D9RegisterKey registerKey = new D3D9RegisterKey(RegisterType.Loop, 0);
                _registerState.DeclareRegister(registerKey, 1);
            }
            else if (instruction.Opcode == Opcode.Rep)
            {
                InsertLoop(instruction);
            }
            else if (instruction.Opcode == Opcode.EndRep)
            {
                EndLoop();
            }
            else if (instruction.Opcode == Opcode.BreakC)
            {
                InsertBreak(instruction);
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
        }

        private void LoadConstantOutputs(ShaderModel shader)
        {
            foreach (ConstantBufferDescription constantBuffer in shader.ConstantBufferDescriptions)
            {
                // TODO
                int registerNumber = constantBuffer.RegisterNumber;
                var registerKey = new D3D10RegisterKey(OperandType.ConstantBuffer, registerNumber);
                for (int i = 0; i < constantBuffer.Size; i++)
                {
                    var destinationKey = new RegisterComponentKey(registerKey, i);
                    var shaderInput = new RegisterInputNode(destinationKey);
                    SetActiveOutput(destinationKey, shaderInput);
                }
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

        private void InsertStatement(IStatement statement)
        {
            if (_currentStatements.Count == 0)
            {
                ActiveStatementSequence.Add(statement);
                _currentStatements.Push(statement);
            }
            else if (_currentStatements.Peek() is IfStatement ifStatement)
            {
                if (ifStatement.IsParsed)
                {
                    _currentStatements.Pop();
                }
                ActiveStatementSequence.Add(statement);
                _currentStatements.Push(statement);
            }
            else if (_currentStatements.Peek() is LoopStatement loopStatement)
            {
                if (loopStatement.IsParsed)
                {
                    _currentStatements.Pop();
                }
                ActiveStatementSequence.Add(statement);
                _currentStatements.Push(statement);
            }
            else
            {
                _currentStatements.Pop();
                ActiveStatementSequence.Add(statement);
                _currentStatements.Push(statement);
            }
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
            if (instruction is D3D10Instruction)
            {
                values = GetParameterRegisterKeys(instruction, 0, 15)
                    .Select(GetActiveOutput)
                    .ToArray();
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

        private void InsertLoop(Instruction instruction)
        {
            int loopRegisterNumber = instruction.GetParamRegisterNumber(0);
            uint repeatCount = _registerState.FindConstantIntRegister(loopRegisterNumber)[0];
            var loop = new LoopStatement(repeatCount, ActiveOutputs);

            InsertStatement(loop);
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
            LoopStatement loopStatement;

            while ((loopStatement = _currentStatements.Peek() as LoopStatement) == null)
            {
                _currentStatements.Pop();
            }
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
                    loopStatement.Outputs[registerComponent] = new PhiNode(node, parentNode);
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
            while (ActiveStatement is not IfStatement)
            {
                _currentStatements.Pop();
            }

            var ifStatement = ActiveStatement as IfStatement;
            ifStatement.IsTrueParsed = true;
            ifStatement.FalseBody = [];
        }

        private void EndIf()
        {
            IfStatement ifStatement;
            while ((ifStatement = _currentStatements.Peek() as IfStatement) == null || ifStatement.IsParsed)
            {
                _currentStatements.Pop();
            }
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
                return new ConstantNode(d3D10RegisterKey.Number);
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
                newOutputs[destinationKey] = instructionTree;
            }

            foreach (var output in newOutputs)
            {
                SetActiveOutput(output.Key, output.Value);
            }
        }

        private static IEnumerable<RegisterComponentKey> GetDestinationKeys(Instruction instruction)
        {
            int index = instruction.GetDestinationParamIndex();
            int mask = instruction.GetDestinationWriteMask();
            return GetParameterRegisterKeys(instruction, index, mask);
        }

        private static IEnumerable<RegisterComponentKey> GetParameterRegisterKeys(Instruction instruction, int index, int mask)
        {
            RegisterKey registerKey = instruction.GetParamRegisterKey(index);

            if (registerKey is D3D9RegisterKey d3D9RegisterKey)
            {
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
                        var constant = new ConstantNode(instruction.GetParamSingle(componentIndex + 1));
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
                case D3D10Opcode.Frc:
                case D3D10Opcode.Log:
                case D3D10Opcode.Mad:
                case D3D10Opcode.Max:
                case D3D10Opcode.Min:
                case D3D10Opcode.Mul:
                case D3D10Opcode.Rsq:
                case D3D10Opcode.SinCos:
                case D3D10Opcode.Sqrt:
                    {
                        HlslTreeNode[] inputs = GetInputs(instruction, componentIndex);
                        switch (instruction.Opcode)
                        {
                            case D3D10Opcode.Add:
                                return new AddOperation(inputs[0], inputs[1]);
                            case D3D10Opcode.DerivRtx:
                                return new PartialDerivativeXOperation(inputs[0]);
                            case D3D10Opcode.DerivRty:
                                return new PartialDerivativeYOperation(inputs[0]);
                            case D3D10Opcode.Exp:
                                return new ExponentialOperation(inputs[0]);
                            case D3D10Opcode.Frc:
                                return new FractionalOperation(inputs[0]);
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
                            case D3D10Opcode.Mul:
                                return new MultiplyOperation(inputs[0], inputs[1]);
                            case D3D10Opcode.Rsq:
                                return new ReciprocalSquareRootOperation(inputs[0]);
                            case D3D10Opcode.Sqrt:
                                return new SquareRootOperation(inputs[0]);
                            case D3D10Opcode.SinCos:
                            default:
                                throw new NotImplementedException();
                        }
                    }
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

        private TextureLoadOutputNode CreateTextureLoadOutputNode(Instruction instruction, int outputComponent)
        {
            const int TextureCoordsParamIndex = 1;
            const int SamplerParamIndex = 2;

            bool isBias = false;
            bool isLod = false;
            bool isGrad = false;
            bool isProj = false;
            if (instruction is D3D9Instruction d3D9Instruction)
            {
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
            }

            var sampler = GetInputComponents(instruction, SamplerParamIndex, 1)[0] as RegisterInputNode;
            var samplerConstant = _registerState.FindConstant(RegisterSet.Sampler, sampler.RegisterComponentKey.RegisterKey.Number);
            int numSamplerOutputComponents = (isBias || isLod ||  isProj) ? 4 : samplerConstant.GetSamplerDimension();

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
            int numComponents;
            switch (instruction.Opcode)
            {
                case D3D10Opcode.Dp2:
                    numComponents = 2;
                    break;
                case D3D10Opcode.Dp3:
                    numComponents = 3;
                    break;
                case D3D10Opcode.Dp4:
                    numComponents = 4;
                    break;
                default:
                    throw new InvalidOperationException();
            }
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
                inputs[i] = ApplyModifier(GetActiveOutput(inputKey), modifier);
                parameterIndex++;
            }
            return inputs;
        }

        private HlslTreeNode[] GetInputs(D3D10Instruction instruction, int componentIndex)
        {
            int numInputs = GetNumInputs(instruction.Opcode);
            var inputs = new HlslTreeNode[numInputs];
            for (int i = 0; i < numInputs; i++)
            {
                int inputParameterIndex = i + 1;
                var operandType = instruction.GetOperandType(inputParameterIndex);
                if (operandType == OperandType.Immediate32)
                {
                    inputs[i] = new ConstantNode(instruction.GetParamSingle(inputParameterIndex, componentIndex));
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
            switch (modifier)
            {
                case SourceModifier.Abs:
                    return new AbsoluteOperation(input);
                case SourceModifier.Negate:
                    return new NegateOperation(input);
                case SourceModifier.AbsAndNegate:
                    return new NegateOperation(new AbsoluteOperation(input));
                case SourceModifier.None:
                    return input;
                default:
                    throw new NotImplementedException();
            }
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
                case D3D10Opcode.Max:
                case D3D10Opcode.Min:
                case D3D10Opcode.Mul:
                    return 2;
                case D3D10Opcode.Mad:
                    return 3;
                default:
                    throw new NotImplementedException();
            }
        }

        private static RegisterComponentKey GetParamRegisterComponentKey(Instruction instruction, int paramIndex, int component)
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
}
