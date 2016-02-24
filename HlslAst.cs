using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler
{
   public class RegisterKey
    {
        public int RegisterNumber { get; set; }
        public RegisterType RegisterType { get; set; }
        public int ComponentIndex { get; set; }

        public SourceModifier Modifier { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as RegisterKey;
            if (other == null) return false;
            return other.RegisterNumber == RegisterNumber &&
                other.RegisterType == RegisterType && 
                other.ComponentIndex == ComponentIndex;
        }

        public override int GetHashCode()
        {
            return RegisterNumber.GetHashCode() ^ RegisterType.GetHashCode() ^ ComponentIndex.GetHashCode();
        }

        public override string ToString()
        {
            return $"{RegisterType}{RegisterNumber} ({ComponentIndex})";
        }
    }

    public class HlslAst
    {
        ShaderModel _shader;
        public IEnumerable<KeyValuePair<RegisterKey, HlslTreeNode>> Roots { get; set; }

        public bool IsValid { get { return Roots != null; } }

        public HlslAst(ShaderModel shader)
        {
            _shader = shader;

            ParseBytecode();
            ReduceTree();
        }

        void ReduceTree()
        {
            if (Roots == null) return;

            Roots = Roots.Select(c => new KeyValuePair<RegisterKey, HlslTreeNode>(c.Key, c.Value.Reduce()));
        }

        RegisterKey GetParamRegisterKey(Instruction instruction, int paramIndex, int component)
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

        public void ParseBytecode()
        {
            int instructionPointer = 0;

            var currentOutputs = new Dictionary<RegisterKey, HlslTreeNode>();

            var constantTable = _shader.ParseConstantTable();
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
                    currentOutputs.Add(destinationKey, shaderInput);
                }
            }

            while (instructionPointer < _shader.Instructions.Count)
            {
                var instruction = _shader.Instructions[instructionPointer];
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

                        switch (instruction.Opcode)
                        {
                            case Opcode.Dcl:
                                {
                                    var shaderInput = new HlslShaderInput()
                                    {
                                        InputDecl = destinationKey,
                                        ComponentIndex = i
                                    };
                                    currentOutputs.Add(destinationKey, shaderInput);
                                }
                                break;
                            case Opcode.Def:
                                {
                                    var constant = new HlslConstant(instruction.GetParamSingle(i + 1));
                                    currentOutputs.Add(destinationKey, constant);
                                }
                                break;
                            case Opcode.Abs:
                            case Opcode.Mad:
                            case Opcode.Mov:
                                {
                                    int numInputs;
                                    switch (instruction.Opcode)
                                    {
                                        case Opcode.Abs:
                                        case Opcode.Mov:
                                            numInputs = 1;
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
                                        var inputKey = GetParamRegisterKey(instruction, j + 1, i);
                                        var input = currentOutputs[inputKey];
                                        operation.Children.Add(input);
                                    }

                                    currentOutputs.Add(destinationKey, operation);
                                }
                                break;
                            default:
                                throw new NotImplementedException();
                        }
                    }
                }

                instructionPointer++;
            }

            Roots = currentOutputs.Where(o => o.Key.RegisterType == RegisterType.ColorOut);
        }
    }
}
