using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HlslDecompiler
{
    public class HlslTreeNode
    {
        public IList<HlslTreeNode> Children { get; set; }

        public HlslTreeNode()
        {
            Children = new List<HlslTreeNode>();
        }

        public virtual HlslTreeNode Reduce()
        {
            return this;
        }
    }

    public class HlslOperation : HlslTreeNode
    {
        public Opcode Operation { get; private set; }

        public HlslOperation(Opcode operation)
        {
            Operation = operation;
        }

        public override HlslTreeNode Reduce()
        {
            switch (Operation)
            {
                case Opcode.Mad:
                    {
                        var multiplicand1 = Children[0].Reduce();
                        var multiplicand2 = Children[1].Reduce();
                        var addend = Children[2].Reduce();

                        if (multiplicand1 is HlslConstant)
                        {
                            throw new NotImplementedException();
                        }
                        else if (multiplicand2 is HlslConstant)
                        {
                            float mul2Value = (multiplicand2 as HlslConstant).Value;
                            if (mul2Value == 0)
                            {
                                return addend;
                            }
                            else if (mul2Value == 1)
                            {
                                if (addend is HlslConstant)
                                {
                                    var addendConst = addend as HlslConstant;
                                    if (addendConst.Value == 0)
                                    {
                                        return multiplicand1;
                                    }
                                }
                            }
                            throw new NotImplementedException();
                        }
                        else if (addend is HlslConstant)
                        {
                            throw new NotImplementedException();
                        }
                    }
                    break;
                case Opcode.Mov:
                    {
                        return Children[0].Reduce();
                    }
            }
            return base.Reduce();
        }

        public override string ToString()
        {
            return Operation.ToString();
        }
    }

    public class HlslConstant : HlslTreeNode
    {
        public float Value { get; private set; }

        public HlslConstant(float value)
        {
            Value = value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public class HlslShaderInput : HlslTreeNode
    {
        public RegisterKey InputDecl { get; set; }
        public int ComponentIndex { get; set; }

        public override string ToString()
        {
            return string.Format("{0} ({1})", InputDecl, ComponentIndex);
        }
    }

    public class RegisterKey
    {
        public int RegisterNumber { get; set; }
        public RegisterType RegisterType { get; set; }
        public int ComponentIndex { get; set; }

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
            return RegisterNumber.GetHashCode() + RegisterType.GetHashCode() + ComponentIndex.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("{0}{1} ({2})", RegisterType, RegisterNumber, ComponentIndex);
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
                            case Opcode.Mad:
                                {
                                    var mad = new HlslOperation(Opcode.Mad);
                                    for (int j = 0; j < 3; j++)
                                    {
                                        var modifier = instruction.GetSourceModifier(j + 1);
                                        if (modifier != SourceModifier.None)
                                        {
                                            // TODO
                                        }
                                        var inputKey = GetParamRegisterKey(instruction, j + 1, i);
                                        var input = currentOutputs[inputKey];
                                        mad.Children.Add(input);
                                    }

                                    currentOutputs.Add(destinationKey, mad);
                                }
                                break;
                            case Opcode.Mov:
                                {
                                    var mov = new HlslOperation(Opcode.Mov);
                                    var inputKey = GetParamRegisterKey(instruction, 1, i);
                                    var input = currentOutputs[inputKey];
                                    mov.Children.Add(input);

                                    currentOutputs.Add(destinationKey, mov);
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
