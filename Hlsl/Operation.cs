namespace HlslDecompiler.Hlsl
{
    public class Operation : HlslTreeNode
    {
        public OperationType Type { get; }

        public Operation(OperationType type)
        {
            Type = type;
        }

        public override string ToString()
        {
            return Type.ToString();
        }
    }
}
