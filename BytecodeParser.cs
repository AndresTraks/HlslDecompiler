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
                default:
                    throw new NotImplementedException();
            }
        }

        private HlslTreeNode[] GetInputs(Instruction instruction, int componentIndex)
        {
            int numInputs = GetNumInputs(instruction.Opcode);
            var inputs = new HlslTreeNode[numInputs];
            for (int i = 0; i < numInputs; i++)
            {
                var modifier = instruction.GetSourceModifier(i + 1);
                if (modifier != SourceModifier.None)
                {
                    // TODO
                }
                var inputKey = GetParamRegisterKey(instruction, i + 1, componentIndex);
                var input = _activeOutputs[inputKey];
                inputs[i] = input;
            }
            return inputs;
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
