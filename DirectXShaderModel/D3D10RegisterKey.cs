namespace HlslDecompiler.DirectXShaderModel
{
    public class D3D10RegisterKey : RegisterKey
    {
        public D3D10RegisterKey(OperandType operandType, int registerNumber)
        {
            OperandType = operandType;
            Number = registerNumber;
        }

        public OperandType OperandType { get; }


        public override bool TypeEquals(RegisterKey registerKey)
        {
            if (!(registerKey is D3D10RegisterKey other))
            {
                return false;
            }
            return other.OperandType == OperandType;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is D3D10RegisterKey other))
            {
                return false;
            }
            return
                other.Number == Number &&
                other.OperandType == OperandType;
        }

        public override int GetHashCode()
        {
            int hashCode =
                Number.GetHashCode() ^
                OperandType.GetHashCode();
            return hashCode;
        }

        public override string ToString()
        {
            return $"{OperandType}{Number}";
        }
    }
}
