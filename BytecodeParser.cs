using HlslDecompiler.Hlsl;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler
{
    class BytecodeParser
    {
        private Dictionary<RegisterKey, HlslTreeNode> _activeOutputs;

        public HlslAst Parse(ShaderModel shader)
        {
            _activeOutputs = GetConstantOutputs(shader);

            int instructionPointer = 0;
            bool ifBlock = false;
            while (instructionPointer < shader.Instructions.Count)
            {
                var instruction = shader.Instructions[instructionPointer];
                if (ifBlock)
                {
                    if (instruction.Opcode == Opcode.Else)
                    {
                        ifBlock = false;
                    }
                }
                else
                {
                    if (instruction.Opcode == Opcode.IfC)
                    {
                        ifBlock = true;
                    }
                    ParseInstruction(instruction);
                }
                instructionPointer++;
            }

            Dictionary<RegisterKey, HlslTreeNode> roots;
            if (shader.Type == ShaderType.Pixel)
            {
                roots = _activeOutputs
                    .Where(o => o.Key.RegisterType == RegisterType.ColorOut)
                    .ToDictionary(o => o.Key, o => o.Value);
            }
            else
            {
                roots = _activeOutputs
                    .Where(o => o.Key.RegisterType == RegisterType.Output && o.Key.RegisterNumber == 0)
                    .ToDictionary(o => o.Key, o => o.Value);
            }
            return new HlslAst(roots);
        }

        private static Dictionary<RegisterKey, HlslTreeNode> GetConstantOutputs(ShaderModel shader)
        {
            var constantTable = shader.ParseConstantTable();

            var constantOutputs = new Dictionary<RegisterKey, HlslTreeNode>();
            foreach (var constant in constantTable)
            {
                if (constant.RegisterSet == RegisterSet.Sampler)
                {
                    continue;
                }

                for (int r = 0; r < constant.RegisterCount; r++)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var destinationKey = new RegisterKey()
                        {
                            RegisterNumber = constant.RegisterIndex + r,
                            RegisterType = RegisterType.Const,
                            ComponentIndex = i
                        };
                        var shaderInput = new RegisterInputNode(destinationKey, i);
                        constantOutputs.Add(destinationKey, shaderInput);
                    }
                }
            }

            return constantOutputs;
        }

        private void ParseInstruction(Instruction instruction)
        {
            if (instruction.HasDestination)
            {
                var newOutputs = new Dictionary<RegisterKey, HlslTreeNode>();

                RegisterKey[] destinationKeys = GetDestinationKeys(instruction).ToArray();
                foreach (RegisterKey destinationKey in destinationKeys)
                {
                    HlslTreeNode instructionTree = CreateInstructionTree(instruction, destinationKey);
                    newOutputs[destinationKey] = instructionTree;
                }

                foreach (var output in newOutputs)
                {
                    _activeOutputs[output.Key] = output.Value;
                }
            }
        }

        private IEnumerable<RegisterKey> GetDestinationKeys(Instruction instruction)
        {
            int index = instruction.GetDestinationParamIndex();
            RegisterType registerType = instruction.GetParamRegisterType(index);
            int registerNumber = instruction.GetParamRegisterNumber(index);

            if (registerType == RegisterType.Sampler)
            {
                yield return new RegisterKey()
                {
                    RegisterNumber = registerNumber,
                    RegisterType = RegisterType.Sampler
                };
            }
            else
            {
                int mask = instruction.GetDestinationWriteMask();
                for (int component = 0; component < 4; component++)
                {
                    if ((mask & (1 << component)) == 0) continue;

                    yield return new RegisterKey()
                    {
                        RegisterNumber = registerNumber,
                        RegisterType = registerType,
                        ComponentIndex = component
                    };
                }
            }
        }

        private HlslTreeNode CreateInstructionTree(Instruction instruction, RegisterKey destinationKey)
        {
            int componentIndex = destinationKey.ComponentIndex;

            switch (instruction.Opcode)
            {
                case Opcode.Dcl:
                    {
                        var shaderInput = new RegisterInputNode(destinationKey, componentIndex);
                        SamplerTextureType textureType = instruction.GetDeclSamplerTextureType();
                        switch (textureType)
                        {
                            case SamplerTextureType.TwoD:
                                shaderInput.SamplerTextureDimension = 2;
                                break;
                            case SamplerTextureType.Cube:
                            case SamplerTextureType.Volume:
                                shaderInput.SamplerTextureDimension = 3;
                                break;
                        }
                        return shaderInput;
                    }
                case Opcode.Def:
                    {
                        var constant = new ConstantNode(instruction.GetParamSingle(componentIndex + 1));
                        return constant;
                    }
                case Opcode.Abs:
                case Opcode.Add:
                case Opcode.Frc:
                case Opcode.Lrp:
                case Opcode.Mad:
                case Opcode.Max:
                case Opcode.Min:
                case Opcode.Mov:
                case Opcode.Mul:
                case Opcode.Rcp:
                case Opcode.Rsq:
                case Opcode.SinCos:
                case Opcode.Sge:
                case Opcode.Slt:
                    {
                        HlslTreeNode[] inputs = GetInputs(instruction, componentIndex);
                        switch (instruction.Opcode)
                        {
                            case Opcode.Abs:
                                return new AbsoluteOperation(inputs[0]);
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
                    return CreateTextureLoadOutputNode(instruction, componentIndex);
                case Opcode.Dp3:
                    return CreateDotProductOutputNode(instruction, componentIndex);
                default:
                    throw new NotImplementedException($"{instruction.Opcode} not implemented");
            }
        }

        private HlslTreeNode[] GetInputs(Instruction instruction, int componentIndex)
        {
            int numInputs = GetNumInputs(instruction.Opcode);
            var inputs = new HlslTreeNode[numInputs];
            for (int i = 0; i < numInputs; i++)
            {
                int inputParameterIndex = i + 1;
                RegisterKey inputKey = GetParamRegisterKey(instruction, inputParameterIndex, componentIndex);
                if (_activeOutputs.TryGetValue(inputKey, out HlslTreeNode input) == false)
                {
                    if (inputKey.RegisterType == RegisterType.Const)
                    {
                        input = new ConstantNode(inputKey.RegisterNumber);
                        _activeOutputs[inputKey] = input;
                    }
                    else
                    {
                        throw new Exception($"Unknown input {inputKey}");
                    }
                }
                var modifier = instruction.GetSourceModifier(inputParameterIndex);
                input = ApplyModifier(input, modifier);
                inputs[i] = input;
            }
            return inputs;
        }

        private TextureLoadOutputNode CreateTextureLoadOutputNode(Instruction instruction, int outputComponent)
        {
            const int TextureCoordsIndex = 1;
            const int SamplerIndex = 2;

            RegisterKey sampler = GetParamRegisterKey(instruction, SamplerIndex, 0);
            if (!_activeOutputs.TryGetValue(sampler, out HlslTreeNode samplerInput))
            {
                throw new InvalidOperationException();
            }
            int numSamplerOutputComponents = ((RegisterInputNode)samplerInput).SamplerTextureDimension;

            IList<HlslTreeNode> texCoords = new List<HlslTreeNode>();
            for (int component = 0; component < numSamplerOutputComponents; component++)
            {
                RegisterKey textureCoordsKey = GetParamRegisterKey(instruction, TextureCoordsIndex, component);
                if (_activeOutputs.TryGetValue(textureCoordsKey, out HlslTreeNode textureCoord))
                {
                    texCoords.Add(textureCoord);
                }
            }

            return new TextureLoadOutputNode(samplerInput, texCoords, outputComponent);
        }

        private HlslTreeNode CreateDotProductOutputNode(Instruction instruction, int outputComponent)
        {
            var inputs = new List<HlslTreeNode>();
            for (int component = 0; component < 3; component++)
            {
                IList<HlslTreeNode> componentInput = GetInputs(instruction, component);
                inputs.AddRange(componentInput);
            }

            return new DotProductOutputNode(inputs, outputComponent);
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

        private static int GetNumInputs(Opcode opcode)
        {
            switch (opcode)
            {
                case Opcode.Abs:
                case Opcode.Frc:
                case Opcode.Mov:
                case Opcode.Rcp:
                case Opcode.Rsq:
                case Opcode.SinCos:
                    return 1;
                case Opcode.Add:
                case Opcode.Dp3:
                case Opcode.Max:
                case Opcode.Min:
                case Opcode.Mul:
                case Opcode.Sge:
                case Opcode.Slt:
                case Opcode.Tex:
                    return 2;
                case Opcode.Lrp:
                case Opcode.Mad:
                    return 3;
                default:
                    throw new NotImplementedException();
            }
        }

        private static RegisterKey GetParamRegisterKey(Instruction instruction, int paramIndex, int component)
        {
            int registerNumber = instruction.GetParamRegisterNumber(paramIndex);
            var registerType = instruction.GetParamRegisterType(paramIndex);
            byte[] swizzle = instruction.GetSourceSwizzleComponents(paramIndex);
            int componentIndex = swizzle[component];

            return new RegisterKey()
            {
                RegisterNumber = registerNumber,
                RegisterType = registerType,
                ComponentIndex = componentIndex
            };
        }
    }
}
