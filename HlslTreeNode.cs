using System.Collections.Generic;
using System.Globalization;
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

        public bool Inline { get { return Children.All(c => c.Inline); } }
    }

    public class HlslOperation : HlslTreeNode
    {
        public Opcode Operation { get; }

        public HlslOperation(Opcode operation)
        {
            Operation = operation;
        }

        public override HlslTreeNode Reduce()
        {
            switch (Operation)
            {
                case Opcode.Add:
                {
                    var addend1 = Children[0].Reduce();
                    var addend2 = Children[1].Reduce();
                    var constant1 = addend1 as HlslConstant;
                    var constant2 = addend2 as HlslConstant;
                    if (constant1 != null)
                    {
                        float value1 = constant1.Value;
                        if (value1 == 0) return addend2;
                        if (constant2 != null)
                        {
                            return new HlslConstant(value1 + constant2.Value);
                        }
                    }
                    if (constant2 != null)
                    {
                        float value2 = constant2.Value;
                        if (value2 == 0) return addend1;
                        if (value2 < 0)
                        {
                            var sub = new HlslOperation(Opcode.Sub);
                                sub.Children.Add(addend1);
                                sub.Children.Add(new HlslConstant(-value2));
                            return sub;
                        }
                    }
                    break;
                }
                case Opcode.Mad:
                {
                    var add = new HlslOperation(Opcode.Add);
                    var mul2 = new HlslOperation(Opcode.Mul);
                    mul2.Children.Add(Children[0]);
                    mul2.Children.Add(Children[1]);
                    add.Children.Add(mul2);
                    add.Children.Add(Children[2]);
                    return add.Reduce();
                }
                case Opcode.Mov:
                {
                    return Children[0].Reduce();
                }
                case Opcode.Mul:
                {
                    var multiplicand1 = Children[0].Reduce();
                    var multiplicand2 = Children[1].Reduce();
                    var constant1 = multiplicand1 as HlslConstant;
                    var constant2 = multiplicand2 as HlslConstant;
                    if (constant1 != null)
                    {
                        float value1 = constant1.Value;
                        if (value1 == 0) return multiplicand1;
                        if (value1 == 1) return multiplicand2;
                        if (constant2 != null)
                        {
                            return new HlslConstant(value1 * constant2.Value);
                        }
                    }
                    if (constant2 != null)
                    {
                        float value2 = constant2.Value;
                        if (value2 == 0) return multiplicand2;
                        if (value2 == 1) return multiplicand1;
                    }
                    break;
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
        public float Value { get; }

        public HlslConstant(float value)
        {
            Value = value;
        }

        public override string ToString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public class HlslShaderInput : HlslTreeNode
    {
        public RegisterKey InputDecl { get; set; }
        public int ComponentIndex { get; set; }

        public override string ToString()
        {
            return $"{InputDecl} ({ComponentIndex})";
        }
    }
}
