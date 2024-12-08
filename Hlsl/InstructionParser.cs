using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HlslDecompiler.Hlsl
{
    class InstructionParser
    {
        private Dictionary<RegisterComponentKey, HlslTreeNode> _activeOutputs;
        private RegisterState _registerState;
        private List<StatementSequence> _statementSequences;
        private StatementSequence _currentStatementSequence;

        public static HlslAst Parse(ShaderModel shader)
        {
            var parser = new InstructionParser();
            return parser.ParseToAst(shader);
        }

        private HlslAst ParseToAst(ShaderModel shader)
        {
            _activeOutputs = new Dictionary<RegisterComponentKey, HlslTreeNode>();
            _registerState = new RegisterState(shader);
            _statementSequences = new List<StatementSequence>();

            _currentStatementSequence = new StatementSequence();
            _statementSequences.Add(_currentStatementSequence);

            LoadConstantOutputs(shader);

            int instructionPointer = 0;
            while (instructionPointer < shader.Instructions.Count)
            {
                var instruction = shader.Instructions[instructionPointer];
                if (instruction is D3D10Instruction d3D10Instruction)
                {
                    ParseInstruction(d3D10Instruction);
                }
                else if (instruction is D3D9Instruction d3D9Instruction)
                {
                    ParseInstruction(d3D9Instruction);
                }
                else
                {
                    throw new NotImplementedException();
                }
                instructionPointer++;
            }

            _currentStatementSequence.Outputs = new Dictionary<RegisterComponentKey, HlslTreeNode>(_activeOutputs);

            return new HlslAst(_statementSequences, _registerState);
        }

        private void ParseInstruction(D3D9Instruction instruction)
        {
            if (instruction.HasDestination)
            {
                ParseAssignmentInstruction(instruction);
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
                        ParseControlInstruction(instruction);
                        break;
                    case Opcode.End:
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
                            RegisterKey registerKey = instruction.GetParamRegisterKey(0);
                            _currentStatementSequence.Outputs = new Dictionary<RegisterComponentKey, HlslTreeNode>();
                            RegisterComponentKey registerComponentKey = new RegisterComponentKey(registerKey, 0);
                            ClipOperation clip = new ClipOperation(new RegisterInputNode(registerComponentKey));
                            _currentStatementSequence.Outputs.Add(registerComponentKey, clip);

                            _currentStatementSequence = new StatementSequence();
                            _statementSequences.Add(_currentStatementSequence);
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
            int ctabToken = FourCC.Make("CTAB");
            if (instruction.Params[0] != ctabToken)
            {
                return;
            }

            byte[] constantTable = new byte[instruction.Params.Count * 4];
            for (int i = 1; i < instruction.Params.Count; i++)
            {
                constantTable[i * 4 - 4] = (byte)(instruction.Params[i] & 0xFF);
                constantTable[i * 4 - 3] = (byte)((instruction.Params[i] >> 8) & 0xFF);
                constantTable[i * 4 - 2] = (byte)((instruction.Params[i] >> 16) & 0xFF);
                constantTable[i * 4 - 1] = (byte)((instruction.Params[i] >> 24) & 0xFF);
            }

            var ctabStream = new MemoryStream(constantTable);
            using (var ctabReader = new BinaryReader(ctabStream))
            {
                int ctabSize = ctabReader.ReadInt32();
                System.Diagnostics.Debug.Assert(ctabSize == 0x1C);
                long creatorPosition = ctabReader.ReadInt32();

                int minorVersion = ctabReader.ReadByte();
                int majorVersion = ctabReader.ReadByte();
                var shaderType = (ShaderType)ctabReader.ReadUInt16();

                int numConstants = ctabReader.ReadInt32();
                long constantInfoPosition = ctabReader.ReadInt32();
                ShaderFlags shaderFlags = (ShaderFlags)ctabReader.ReadInt32();

                long shaderModelPosition = ctabReader.ReadInt32();

                ctabStream.Position = creatorPosition;
                string compilerInfo = ReadStringNullTerminated(ctabStream);

                ctabStream.Position = shaderModelPosition;
                string shaderModel = ReadStringNullTerminated(ctabStream);

                for (int i = 0; i < numConstants; i++)
                {
                    ctabStream.Position = constantInfoPosition + i * 20;
                    ConstantDeclaration constant = ReadConstantDeclaration(ctabReader);
                    LoadConstantDeclaration(constant);
                }
            }
        }

        private static ConstantDeclaration ReadConstantDeclaration(BinaryReader ctabReader)
        {
            var ctabStream = ctabReader.BaseStream;

            // D3DXSHADER_CONSTANTINFO
            int nameOffset = ctabReader.ReadInt32();
            RegisterSet registerSet = (RegisterSet)ctabReader.ReadInt16();
            short registerIndex = ctabReader.ReadInt16();
            short registerCount = ctabReader.ReadInt16();
            ctabStream.Position += sizeof(short); // Reserved
            int typeInfoOffset = ctabReader.ReadInt32();
            int defaultValueOffset = ctabReader.ReadInt32();
            System.Diagnostics.Debug.Assert(defaultValueOffset == 0);

            ctabStream.Position = nameOffset;
            string name = ReadStringNullTerminated(ctabStream);

            // D3DXSHADER_TYPEINFO
            ctabStream.Position = typeInfoOffset;
            ParameterClass cl = (ParameterClass)ctabReader.ReadInt16();
            ParameterType type = (ParameterType)ctabReader.ReadInt16();
            short rows = ctabReader.ReadInt16();
            short columns = ctabReader.ReadInt16();
            short numElements = ctabReader.ReadInt16();
            short numStructMembers = ctabReader.ReadInt16();
            int structMemberInfoOffset = ctabReader.ReadInt32();
            //System.Diagnostics.Debug.Assert(numElements == 1);
            System.Diagnostics.Debug.Assert(structMemberInfoOffset == 0);

            return new ConstantDeclaration(name, registerSet, registerIndex, registerCount, cl, type, rows, columns);
        }

        private void LoadConstantDeclaration(ConstantDeclaration constant)
        {
            _registerState.DeclareConstant(constant);

            if (constant.RegisterSet != RegisterSet.Sampler)
            {
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
                        _activeOutputs.Add(destinationKey, shaderInput);
                    }
                }
            }
        }

        private void ParseControlInstruction(D3D9Instruction instruction)
        {
            if (instruction.Opcode == Opcode.Loop)
            {
                D3D9RegisterKey registerKey = new D3D9RegisterKey(RegisterType.Loop, 0);
                _registerState.DeclareRegister(registerKey);
            }

            EndStatementSequence(_activeOutputs);
            // TODO: save temporary assignments
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
                    _activeOutputs.Add(destinationKey, shaderInput);
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
                newOutputs[destinationKey] = instructionTree;
            }

            if (instruction.Opcode == Opcode.TexKill)
            {
                EndStatementSequence(newOutputs);
            }
            else
            {
                foreach (var output in newOutputs)
                {
                    _activeOutputs[output.Key] = output.Value;
                }
            }
        }

        private void EndStatementSequence(Dictionary<RegisterComponentKey, HlslTreeNode> newOutputs)
        {
            _currentStatementSequence.Outputs = new Dictionary<RegisterComponentKey, HlslTreeNode>(newOutputs);

            _currentStatementSequence = new StatementSequence();
            _statementSequences.Add(_currentStatementSequence);
        }

        private void ParseAssignmentInstruction(D3D10Instruction instruction)
        {
            _registerState.DeclareDestinationRegister(instruction);

            var newOutputs = new Dictionary<RegisterComponentKey, HlslTreeNode>();

            RegisterComponentKey[] destinationKeys = GetDestinationKeys(instruction).ToArray();
            foreach (RegisterComponentKey destinationKey in destinationKeys)
            {
                HlslTreeNode instructionTree = CreateInstructionTree(instruction, destinationKey);
                newOutputs[destinationKey] = instructionTree;
            }

            foreach (var output in newOutputs)
            {
                _activeOutputs[output.Key] = output.Value;
            }
        }

        private static IEnumerable<RegisterComponentKey> GetDestinationKeys(Instruction instruction)
        {
            int index = instruction.GetDestinationParamIndex();
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
            
            int mask = instruction.GetDestinationWriteMask();
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
                case Opcode.Frc:
                case Opcode.Lrp:
                case Opcode.Mad:
                case Opcode.Max:
                case Opcode.Min:
                case Opcode.Mov:
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
                            case Opcode.Frc:
                                return new FractionalOperation(inputs[0]);
                            case Opcode.Lrp:
                                return new LinearInterpolateOperation(inputs[0], inputs[1], inputs[2]);
                            case Opcode.Max:
                                return new MaximumOperation(inputs[0], inputs[1]);
                            case Opcode.Min:
                                return new MinimumOperation(inputs[0], inputs[1]);
                            case Opcode.Mov:
                                return new MoveOperation(inputs[0]);
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
                            case Opcode.TexKill:
                                return new ClipOperation(inputs[0]);
                            default:
                                throw new NotImplementedException();
                        }
                    }
                case Opcode.Tex:
                    return CreateTextureLoadOutputNode(instruction, componentIndex, false);
                case Opcode.TexLDL:
                    return CreateTextureLoadOutputNode(instruction, componentIndex, true);
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
                case D3D10Opcode.Frc:
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
                            case D3D10Opcode.Mad:
                                return new MultiplyAddOperation(inputs[0], inputs[1], inputs[2]);
                            case D3D10Opcode.Mov:
                                return new MoveOperation(inputs[0]);
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
                case D3D10Opcode.Sample:
                case D3D10Opcode.SampleC:
                case D3D10Opcode.SampleCLZ:
                case D3D10Opcode.SampleL:
                case D3D10Opcode.SampleD:
                case D3D10Opcode.SampleB:
                    return CreateTextureLoadOutputNode(instruction, componentIndex, false);
                case D3D10Opcode.Dp2:
                case D3D10Opcode.Dp3:
                case D3D10Opcode.Dp4:
                    return CreateDotProductNode(instruction);
                default:
                    throw new NotImplementedException($"{instruction.Opcode} not implemented");
            }
        }

        private TextureLoadOutputNode CreateTextureLoadOutputNode(Instruction instruction, int outputComponent, bool isLod)
        {
            const int TextureCoordsIndex = 1;
            const int SamplerIndex = 2;

            RegisterKey samplerRegister = instruction.GetParamRegisterKey(SamplerIndex);
            if (!_registerState.Samplers.TryGetValue(samplerRegister, out HlslTreeNode samplerInput))
            {
                throw new InvalidOperationException();
            }
            var samplerRegisterInput = (RegisterInputNode)samplerInput;
            int numSamplerOutputComponents = isLod ? 4 : samplerRegisterInput.SamplerTextureDimension;

            IList<HlslTreeNode> texCoords = new List<HlslTreeNode>();
            for (int component = 0; component < numSamplerOutputComponents; component++)
            {
                RegisterComponentKey textureCoordsKey = GetParamRegisterComponentKey(instruction, TextureCoordsIndex, component);
                HlslTreeNode textureCoord = _activeOutputs[textureCoordsKey];
                texCoords.Add(textureCoord);
            }

            return new TextureLoadOutputNode(samplerRegisterInput, texCoords, outputComponent, isLod);
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
            for (int i = 0; i < numInputs; i++)
            {
                int inputParameterIndex = i;
                if (instruction.Opcode != Opcode.TexKill)
                {
                    inputParameterIndex++;
                }
                RegisterComponentKey inputKey = GetParamRegisterComponentKey(instruction, inputParameterIndex, componentIndex);
                HlslTreeNode input = _activeOutputs[inputKey];
                SourceModifier modifier = instruction.GetSourceModifier(inputParameterIndex);
                input = ApplyModifier(input, modifier);
                inputs[i] = input;
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
                    HlslTreeNode input = _activeOutputs[inputKey];
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
                HlslTreeNode input = _activeOutputs[inputKey];
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
                case Opcode.DSX:
                case Opcode.DSY:
                case Opcode.Frc:
                case Opcode.Mov:
                case Opcode.Nrm:
                case Opcode.Rcp:
                case Opcode.Rsq:
                case Opcode.SinCos:
                case Opcode.TexKill:
                    return 1;
                case Opcode.Add:
                case Opcode.Dp3:
                case Opcode.Dp4:
                case Opcode.Max:
                case Opcode.Min:
                case Opcode.Mul:
                case Opcode.Pow:
                case Opcode.Sge:
                case Opcode.Slt:
                case Opcode.Tex:
                    return 2;
                case Opcode.Cmp:
                case Opcode.DP2Add:
                case Opcode.Lrp:
                case Opcode.Mad:
                    return 3;
                default:
                    throw new NotImplementedException();
            }
        }

        private static int GetNumInputs(D3D10Opcode opcode)
        {
            switch (opcode)
            {
                case D3D10Opcode.DerivRtx:
                case D3D10Opcode.DerivRty:
                case D3D10Opcode.Frc:
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

        private static string ReadStringNullTerminated(Stream stream)
        {
            var builder = new StringBuilder();
            char b;
            while ((b = (char)stream.ReadByte()) != 0)
            {
                builder.Append(b);
            }
            return builder.ToString();
        }
    }
}
