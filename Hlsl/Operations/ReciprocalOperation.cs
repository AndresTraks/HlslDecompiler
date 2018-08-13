namespace HlslDecompiler.Hlsl
{
    public class ReciprocalOperation : UnaryOperation
    {
        public ReciprocalOperation(HlslTreeNode value)
        {
            AddInput(value);
        }

        public override string Mnemonic => "rcp";

        public override HlslTreeNode Reduce()
        {
            switch (Value)
            {
                case ReciprocalSquareRootOperation reciprocalSquareRoot:
                    {
                        var newValue = new SquareRootOperation(reciprocalSquareRoot.Value);
                        Replace(newValue);
                        return newValue;
                    }
            }
            return base.Reduce();
        }
    }
}
