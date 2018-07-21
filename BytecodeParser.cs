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

        private static Dictionary<RegisterKey, HlslTreeNode> GetConstantOutputs(ShaderModel shader)
        {
            var constantTable = shader.ParseConstantTable();

            var constantOutputs = new Dictionary<RegisterKey, HlslTreeNode>();
            foreach (var constant in constantTable)
            {
                for (int i = 0; i < 4; i++)
                {
                    var destinationKey = new RegisterKey()
                    {
                        RegisterNumber = constant.RegisterIndex,
                        RegisterType = RegisterType.Const,
                        ComponentIndex = i
                    };
                    var shaderInput = new RegisterInputNode(destinationKey, i);
                    constantOutputs.Add(destinationKey, shaderInput);
                }
            }

            return constantOutputs;
        }

        private void ParseInstruction(Instruction instruction)
        {
            if (instruction.HasDestination)
            {
                foreach (RegisterKey destinationKey in GetDestinationKeys(instruction))
                {
                    HlslTreeNode instructionTree = CreateInstructionTree(instruction, destinationKey);
                    _activeOutputs[destinationKey] = instructionTree;
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
                        return shaderInput;
                    }
                case Opcode.Def:
                    {
                        var constant = new HlslConstant(instruction.GetParamSingle(componentIndex + 1));
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
                    return new TextureLoadOutputNode(GetTextureLoadInputs(instruction), componentIndex);
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

        private IList<HlslTreeNode> GetTextureLoadInputs(Instruction instruction)
        {
            const int TextureCoordsIndex = 1;
            const int SamplerIndex = 2;

            var inputs = new List<HlslTreeNode>();
            
            for (int component = 0; component < 4; component++)
            {
                RegisterKey textureCoordsKey = GetParamRegisterKey(instruction, TextureCoordsIndex, component);
                if (_activeOutputs.TryGetValue(textureCoordsKey, out HlslTreeNode textureCoord))
                {
                    inputs.Add(textureCoord);
                }
            }

            RegisterKey samplerKey = GetParamRegisterKey(instruction, SamplerIndex, 0);
            if (_activeOutputs.TryGetValue(samplerKey, out HlslTreeNode input))
            {
                inputs.Add(input);
            }

            return inputs;
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
