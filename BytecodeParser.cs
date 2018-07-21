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
            _activeOutputs = new Dictionary<RegisterKey, HlslTreeNode>();

            int instructionPointer = 0;
            while (instructionPointer < shader.Instructions.Count)
            {
                var instruction = shader.Instructions[instructionPointer];
                ParseInstruction(instruction);
                instructionPointer++;
            }

            var roots = _activeOutputs
                .Where(o => o.Key.RegisterType == RegisterType.ColorOut)
                .ToDictionary(o => o.Key, o => o.Value);
            return new HlslAst(roots);
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
                case Opcode.Mad:
                case Opcode.Mov:
                case Opcode.Mul:
                    {
                        HlslTreeNode[] inputs = GetInputs(instruction, componentIndex);
                        switch (instruction.Opcode)
                        {
                            case Opcode.Abs:
                                return new AbsoluteOperation(inputs[0]);
                            case Opcode.Mov:
                                return new MoveOperation(inputs[0]);
                            case Opcode.Add:
                                return new AddOperation(inputs[0], inputs[1]);
                            case Opcode.Mul:
                                return new MultiplyOperation(inputs[0], inputs[1]);
                            case Opcode.Mad:
                                return new MultiplyAddOperation(inputs[0], inputs[1], inputs[2]);
                            default:
                                throw new NotImplementedException();
                        }
                    }
                case Opcode.Tex:
                    return CreateTextureLoadOutputNode(instruction, componentIndex);
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
                if (_activeOutputs.TryGetValue(inputKey, out HlslTreeNode input))
                {
                    var modifier = instruction.GetSourceModifier(inputParameterIndex);
                    input = ApplyModifier(input, modifier);
                    inputs[i] = input;
                }
                else
                {
                    throw new Exception($"Unknown input {inputKey}");
                }
            }
            return inputs;
        }

        private TextureLoadOutputNode CreateTextureLoadOutputNode(Instruction instruction, int componentIndex)
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

            return new TextureLoadOutputNode(samplerInput, texCoords, componentIndex);
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
                case Opcode.Mov:
                    return 1;
                case Opcode.Add:
                case Opcode.Mul:
                case Opcode.Tex:
                    return 2;
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

            return new RegisterKey()
            {
                RegisterNumber = registerNumber,
                RegisterType = registerType,
                ComponentIndex = swizzle[component]
            };
        }
    }
}
