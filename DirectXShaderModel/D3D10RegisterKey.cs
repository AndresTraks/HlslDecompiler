namespace HlslDecompiler.DirectXShaderModel
{
    public class D3D10RegisterKey : RegisterKey
    {
        public D3D10RegisterKey(OperandType operandType, int registerNumber)
        {
            OperandType = operandType;
            Number = registerNumber;
        }

        public D3D10RegisterKey(OperandType operandType, int registerNumber, int constantBufferOffset)
            : this(operandType, registerNumber)
        {
            ConstantBufferOffset = constantBufferOffset;
        }

        public OperandType OperandType { get; }
        public int Number { get; }
        public int? ConstantBufferOffset { get; }

        public bool IsTempRegister => OperandType == OperandType.Temp;
        public bool IsOutput => OperandType == OperandType.Output;
        public bool IsConstant =>
            OperandType == OperandType.ConstantBuffer ||
            OperandType == OperandType.Immediate32 ||
            OperandType == OperandType.Immediate64 ||
            OperandType == OperandType.ImmediateConstantBuffer;

        public bool TypeEquals(RegisterKey registerKey)
        {
            if (registerKey is not D3D10RegisterKey other)
            {
                return false;
            }
            return other.OperandType == OperandType;
        }

        public override bool Equals(object obj)
        {
            if (obj is not D3D10RegisterKey other)
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
            if (ConstantBufferOffset != null)
            {
                hashCode ^= ConstantBufferOffset.GetHashCode();
            }
            return hashCode;
        }

        public override string ToString()
        {
            string constantBufferOffset = ConstantBufferOffset != null ? $"[{ConstantBufferOffset}]" : "";
            return $"{OperandType}{Number}{constantBufferOffset}";
        }
    }
}
