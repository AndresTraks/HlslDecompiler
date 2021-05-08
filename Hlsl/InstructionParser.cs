using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    class BytecodeParser
    {
        private Dictionary<RegisterComponentKey, HlslTreeNode> _activeOutputs;
        private Dictionary<RegisterKey, HlslTreeNode> _noOutputInstructions;
        private Dictionary<RegisterKey, HlslTreeNode> _samplers;

        public HlslAst Parse(ShaderModel shader)
        {
            _activeOutputs = new Dictionary<RegisterComponentKey, HlslTreeNode>();
            _noOutputInstructions = new Dictionary<RegisterKey, HlslTreeNode>();
            _samplers = new Dictionary<RegisterKey, HlslTreeNode>();

            LoadConstantOutputs(shader);

            int instructionPointer = 0;
            while (instructionPointer < shader.Instructions.Count)
            {
                var instruction = shader.Instructions[instructionPointer];
                if (instruction.HasDestination)
                {
                    ParseAssignmentInstruction(instruction);
                }
                else
                {
                    switch (instruction.Opcode)
                    {
                        case Opcode.If:
                        case Opcode.IfC:
                        case Opcode.Else:
                        case Opcode.Loop:
                        case Opcode.Rep:
                        case Opcode.End:
                        case Opcode.Endif:
                        case Opcode.EndLoop:
                        case Opcode.EndRep:
                            ParseControlInstruction(instruction);
                            break;
                        case Opcode.Comment:
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
                instructionPointer++;
            }

            Dictionary<RegisterKey, HlslTreeNode> roots = GroupOutputs(shader);
            return new HlslAst(roots, _noOutputInstructions);
        }

        public Dictionary<RegisterKey, HlslTreeNode> GroupOutputs(ShaderModel shader)
        {
            var registerType = shader.Type == ShaderType.Pixel
                ? RegisterType.ColorOut
                : RegisterType.Output;
            var outputsByRegister = _activeOutputs
                .Where(o => o.Key.Type == registerType)
                .OrderBy(o => o.Key.ComponentIndex)
                .GroupBy(o => o.Key.RegisterKey);
            var groupsByRegister = outputsByRegister
                .ToDictionary(
                    o => o.Key,
                    o => (HlslTreeNode)new GroupNode(o.Select(o => o.Value).ToArray()));
            return groupsByRegister;
        }

        private void ParseControlInstruction(Instruction instruction)
        {
            // TODO
        }

        private void LoadConstantOutputs(ShaderModel shader)
        {
            IList<ConstantDeclaration> constantTable = shader.ParseConstantTable();

            foreach (var constant in constantTable)
            {
                if (constant.RegisterSet == RegisterSet.Sampler)
                {
                    var registerKey = new RegisterKey(RegisterType.Sampler, constant.RegisterIndex);
                    var destinationKey = new RegisterComponentKey(registerKey, 0);
                    int samplerTextureDimension;
                    switch (constant.ParameterType)
                    {
                        case ParameterType.Sampler1D:
                            samplerTextureDimension = 1;
                            break;
                        case ParameterType.Sampler2D:
                            samplerTextureDimension = 2;
                            break;
                        case ParameterType.Sampler3D:
                        case ParameterType.SamplerCube:
                            samplerTextureDimension = 3;
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                    var shaderInput = new RegisterInputNode(destinationKey, samplerTextureDimension);
                    _samplers.Add(registerKey, shaderInput);
                }
                else
                {
                    for (int r = 0; r < constant.RegisterCount; r++)
                    {
                        if (constant.ParameterType != ParameterType.Float)
                        {
                            throw new NotImplementedException();
                        }
                        var registerKey = new RegisterKey(RegisterType.Const, constant.RegisterIndex + r);
                        for (int i = 0; i < 4; i++)
                        {
                            var destinationKey = new RegisterComponentKey(registerKey, i);
                            var shaderInput = new RegisterInputNode(destinationKey);
                            _activeOutputs.Add(destinationKey, shaderInput);
                        }
                    }
                }
            }
        }

        private void ParseAssignmentInstruction(Instruction instruction)
        {
            var newOutputs = new Dictionary<RegisterComponentKey, HlslTreeNode>();

            RegisterComponentKey[] destinationKeys = GetDestinationKeys(instruction).ToArray();
            foreach (RegisterComponentKey destinationKey in destinationKeys)
            {
                HlslTreeNode instructionTree = CreateInstructionTree(instruction, destinationKey);
                newOutputs[destinationKey] = instructionTree;
            }

            foreach (var output in newOutputs)
            {
                if (instruction.Opcode == Opcode.TexKill)
                {
                    _noOutputInstructions[output.Key.RegisterKey] = output.Value;
                }
                else
                {
                    _activeOutputs[output.Key] = output.Value;
                }
            }
        }

        private static IEnumerable<RegisterComponentKey> GetDestinationKeys(Instruction instruction)
        {
            int index = instruction.GetDestinationParamIndex();
            RegisterKey registerKey = instruction.GetParamRegisterKey(index);

            if (registerKey.Type == RegisterType.Sampler)
            {
                yield break;
            }
            
            int mask = instruction.GetDestinationWriteMask();
            for (int component = 0; component < 4; component++)
            {
                if ((mask & (1 << component)) == 0) continue;

                yield return new RegisterComponentKey(registerKey, component);
            }
        }

        private HlslTreeNode CreateInstructionTree(Instruction instruction, RegisterComponentKey destinationKey)
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
                case Opcode.TexLDL:
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

        private TextureLoadOutputNode CreateTextureLoadOutputNode(Instruction instruction, int outputComponent)
        {
            const int TextureCoordsIndex = 1;
            const int SamplerIndex = 2;

            RegisterKey samplerRegister = instruction.GetParamRegisterKey(SamplerIndex);
            if (!_samplers.TryGetValue(samplerRegister, out HlslTreeNode samplerInput))
            {
                throw new InvalidOperationException();
            }
            var samplerRegisterInput = (RegisterInputNode)samplerInput;
            int numSamplerOutputComponents = samplerRegisterInput.SamplerTextureDimension;

            IList<HlslTreeNode> texCoords = new List<HlslTreeNode>();
            for (int component = 0; component < numSamplerOutputComponents; component++)
            {
                RegisterComponentKey textureCoordsKey = GetParamRegisterComponentKey(instruction, TextureCoordsIndex, component);
                HlslTreeNode textureCoord = _activeOutputs[textureCoordsKey];
                texCoords.Add(textureCoord);
            }

            return new TextureLoadOutputNode(samplerRegisterInput, texCoords, outputComponent);
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

        private HlslTreeNode CreateDotProductNode(Instruction instruction)
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

        private HlslTreeNode CreateNormalizeOutputNode(Instruction instruction, int outputComponent)
        {
            var inputs = new List<HlslTreeNode>();
            for (int component = 0; component < 3; component++)
            {
                IList<HlslTreeNode> componentInput = GetInputs(instruction, component);
                inputs.AddRange(componentInput);
            }

            return new NormalizeOutputNode(inputs, outputComponent);
        }

        private HlslTreeNode[] GetInputs(Instruction instruction, int componentIndex)
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

        private HlslTreeNode[] GetInputComponents(Instruction instruction, int inputParameterIndex, int numComponents)
        {
            var components = new HlslTreeNode[numComponents];
            for (int i = 0; i < numComponents; i++)
            {
                RegisterComponentKey inputKey = GetParamRegisterComponentKey(instruction, inputParameterIndex, i);
                HlslTreeNode input = _activeOutputs[inputKey];
                var modifier = instruction.GetSourceModifier(inputParameterIndex);
                input = ApplyModifier(input, modifier);
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

        private static int GetNumInputs(Opcode opcode)
        {
            switch (opcode)
            {
                case Opcode.Abs:
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

        private static RegisterComponentKey GetParamRegisterComponentKey(Instruction instruction, int paramIndex, int component)
        {
            RegisterKey registerKey = instruction.GetParamRegisterKey(paramIndex);
            byte[] swizzle = instruction.GetSourceSwizzleComponents(paramIndex);
            int componentIndex = swizzle[component];

            return new RegisterComponentKey(registerKey, componentIndex);
        }
    }
}
