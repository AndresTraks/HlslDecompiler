namespace HlslDecompiler.Hlsl
{
    public class AddOperation : Operation
    {
        public AddOperation(HlslTreeNode addend1, HlslTreeNode addend2)
        {
            AddChild(addend1);
            AddChild(addend2);
        }

        public HlslTreeNode Addend1 => Children[0];
        public HlslTreeNode Addend2 => Children[1];

        public override string Mnemonic => "add";

        public override HlslTreeNode Reduce()
        {
            var addend1 = Addend1.Reduce();
            var addend2 = Addend2.Reduce();
            var constant1 = addend1 as ConstantNode;
            var constant2 = addend2 as ConstantNode;
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
                    return new ConstantNode(value1 + constant2.Value);
                }
            }

            if (constant2 != null)
            {
                float value2 = constant2.Value;
                if (value2 == 0) return addend1;
                if (value2 < 0)
                {
                    var sub = new SubtractOperation(addend1, new ConstantNode(-value2));
                    Replace(sub);
                    return sub;
                }
            }

            if (addend1 == addend2)
            {
                var mul = new MultiplyOperation(new ConstantNode(2), addend1);
                Replace(mul);
                return mul;
            }

            if (addend1 is NegateOperation negate1 && addend2 is NegateOperation == false)
            {
                var sub = new SubtractOperation(addend2, negate1.Value);
                Replace(sub);
                return sub;
            }

            if (addend1 is NegateOperation == false && addend2 is NegateOperation negate2)
            {
                var sub = new SubtractOperation(addend1, negate2.Value);
                Replace(sub);
                return sub;
            }

            Children[0] = addend1;
            Children[1] = addend2;
            return this;
        }
    }
}
