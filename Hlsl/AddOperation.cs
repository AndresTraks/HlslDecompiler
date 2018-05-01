namespace HlslDecompiler.Hlsl
{
    public class AddOperation : Operation
    {
        public AddOperation(HlslTreeNode addend1, HlslTreeNode addend2)
            : base(OperationType.Add)
        {
            AddChild(addend1);
            AddChild(addend2);
        }

        public HlslTreeNode Addend1 => Children[0];
        public HlslTreeNode Addend2 => Children[1];

        public override HlslTreeNode Reduce()
        {
            var addend1 = Addend1.Reduce();
            var addend2 = Addend2.Reduce();
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
                    var sub = new SubtractOperation(addend1, new HlslConstant(-value2));
                    Replace(sub);
                    return sub;
                }
            }

            if (addend1 == addend2)
            {
                var mul = new MultiplyOperation(new HlslConstant(2), addend1);
                Replace(mul);
                return mul;
            }

            return base.Reduce();
        }
    }
}
