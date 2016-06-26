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
                ParseInstruction(shader.Instructions[instructionPointer]);
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
                    var shaderInput = new HlslShaderInput()
                    {
                        InputDecl = destinationKey,
                        ComponentIndex = i
                    };
                    constantOutputs.Add(destinationKey, shaderInput);
                }
            }

            return constantOutputs;
        }

        private void ParseInstruction(Instruction instruction)
        {
            if (instruction.HasDestination)
            {
                int destIndex = instruction.GetDestinationParamIndex();
                var destRegisterType = instruction.GetParamRegisterType(destIndex);
                int destRegisterNumber = instruction.GetParamRegisterNumber(destIndex);
                int destMask = instruction.GetDestinationWriteMask();

                for (int i = 0; i < 4; i++)
                {
                    if ((destMask & (1 << i)) == 0) continue;

                    var destinationKey = new RegisterKey()
                    {
                        RegisterNumber = destRegisterNumber,
                        RegisterType = destRegisterType,
                        ComponentIndex = i
                    };

                    HlslTreeNode instructionTree = CreateInstructionTree(instruction, destinationKey);
                    _activeOutputs.Add(destinationKey, instructionTree);
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
                        var shaderInput = new HlslShaderInput()
                        {
                            InputDecl = destinationKey,
                            ComponentIndex = componentIndex
                        };
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
                        int numInputs;
                        switch (instruction.Opcode)
                        {
                            case Opcode.Abs:
                            case Opcode.Mov:
                                numInputs = 1;
                                break;
                            case Opcode.Add:
                            case Opcode.Mul:
                                numInputs = 2;
                                break;
                            case Opcode.Mad:
                                numInputs = 3;
                                break;
                            default:
                                throw new NotImplementedException();
                        }
                        var operation = new HlslOperation(instruction.Opcode);
                        for (int j = 0; j < numInputs; j++)
                        {
                            var modifier = instruction.GetSourceModifier(j + 1);
                            if (modifier != SourceModifier.None)
                            {
                                // TODO
                            }
                            var inputKey = GetParamRegisterKey(instruction, j + 1, componentIndex);
                            var input = _activeOutputs[inputKey];
                            operation.AddChild(input);
                        }

                        return operation;
                    }
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
