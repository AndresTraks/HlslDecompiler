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

        public D3D10RegisterKey(float[] immediateSingle)
        {
            OperandType = OperandType.Immediate32;
            ImmediateSingle = immediateSingle;
        }

        public D3D10RegisterKey(int immediateInt)
        {
            OperandType = OperandType.Immediate32;
            ImmediateInt = immediateInt;
        }

        public OperandType OperandType { get; }
        public int Number { get; }
        public int? ConstantBufferOffset { get; }
        public float[] ImmediateSingle { get; }
        public int? ImmediateInt { get; }

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
            if (other.ImmediateSingle == null)
            {
                if (ImmediateSingle != null)
                {
                    return false;
                }
            }
            else
            {
                if (other.ImmediateSingle.Length != ImmediateSingle.Length)
                {
                    return false;
                }
                for (int i = 0; i < ImmediateSingle.Length; i++)
                {
                    if (other.ImmediateSingle[i] != ImmediateSingle[i])
                    {
                        return false;
                    }
                }
            }
            return
                other.Number == Number &&
                other.OperandType == OperandType &&
                other.ConstantBufferOffset == ConstantBufferOffset &&
                other.ImmediateInt == ImmediateInt;
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
            if (ImmediateSingle != null)
            {
                for (int i = 0; i < ImmediateSingle.Length; i++)
                {
                    hashCode ^= ImmediateSingle[i].GetHashCode();
                }
            }
            if (ImmediateInt != null)
            {
                hashCode ^= ImmediateInt.GetHashCode();
            }
            return hashCode;
        }

        public override string ToString()
        {
            if (ImmediateSingle != null)
            {
                if (ImmediateSingle.Length == 1)
                {
                    return ImmediateSingle[0].ToString();
                }
                return "[" + string.Join(", ", ImmediateSingle) + "]";
            }
            if (ImmediateInt.HasValue)
            {
                return ImmediateInt.Value.ToString();
            }
            string constantBufferOffset = ConstantBufferOffset != null ? $"[{ConstantBufferOffset}]" : "";
            return $"{OperandType}{Number}{constantBufferOffset}";
        }
    }
}
