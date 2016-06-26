using System.Collections.Generic;
using System.Globalization;

namespace HlslDecompiler
{
    public class HlslTreeNode
    {
        public IList<HlslTreeNode> Children { get; } = new List<HlslTreeNode>();
        public IList<HlslTreeNode> Parents { get; } = new List<HlslTreeNode>();

        public void AddChild(HlslTreeNode node)
        {
            Children.Add(node);
            node.Parents.Add(this);
        }

        public virtual HlslTreeNode Reduce()
        {
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i] = Children[i].Reduce();
            }
            return this;
        }

        public void Replace(HlslTreeNode with)
        {
            foreach (var child in Children)
            {
                child.Parents.Remove(this);
            }
            foreach (var parent in Parents)
            {
                for (int i = 0; i < parent.Children.Count; i++)
                {
                    if (parent.Children[i] == this)
                    {
                        parent.Children[i] = with;
                    }
                }
                with.Parents.Add(parent);
            }
        }
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
                        if (value1 == 0)
                        {
                            Replace(addend2);
                            return addend2;
                        }
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
                            sub.AddChild(addend1);
                            sub.AddChild(new HlslConstant(-value2));
                            Replace(sub);
                            return sub;
                        }
                    }
                    if (addend1 == addend2)
                    {
                        var mul = new HlslOperation(Opcode.Mul);
                        mul.AddChild(new HlslConstant(2));
                        mul.AddChild(addend1);
                        Replace(mul);
                        return mul;
                    }
                    break;
                }
                case Opcode.Mad:
                {
                    var mul2 = new HlslOperation(Opcode.Mul);
                    mul2.AddChild(Children[0]);
                    mul2.AddChild(Children[1]);
                    Children[0].Parents.Remove(this);
                    Children[1].Parents.Remove(this);

                    var add = new HlslOperation(Opcode.Add);
                    add.AddChild(mul2);
                    add.AddChild(Children[2]);
                    Replace(add);

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
                        if (value1 == 0)
                        {
                            Replace(multiplicand1);
                            return multiplicand1;
                        }
                        if (value1 == 1)
                        {
                            Replace(multiplicand2);
                            return multiplicand2;
                        }
                        if (constant2 != null)
                        {
                            return new HlslConstant(value1 * constant2.Value);
                        }
                    }
                    if (constant2 != null)
                    {
                        float value2 = constant2.Value;
                        if (value2 == 0)
                        {
                            Replace(multiplicand2);
                            return multiplicand2;
                        }
                        if (value2 == 1)
                        {
                            Replace(multiplicand1);
                            return multiplicand1;
                        }
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

        public override bool Equals(object obj)
        {
            return obj is HlslConstant && this == (HlslConstant)obj;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
        public static bool operator ==(HlslConstant x, HlslConstant y)
        {
            if (ReferenceEquals(x, null)) return ReferenceEquals(y, null);
            if (ReferenceEquals(y, null)) return false;
            return x.Value == y.Value;
        }
        public static bool operator !=(HlslConstant x, HlslConstant y)
        {
            return !(x == y);
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
