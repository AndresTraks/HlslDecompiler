namespace HlslDecompiler.DirectXShaderModel
{
    public class RegisterKey
    {
        private readonly bool isD3D10Type;

        public RegisterKey(RegisterType registerType, int registerNumber)
        {
            Type = registerType;
            Number = registerNumber;
            isD3D10Type = false;
        }

        public RegisterKey(OperandType operandType)
        {
            OperandType = operandType;
            isD3D10Type = true;
        }

        public int Number { get; }
        public RegisterType Type { get; }
        public OperandType OperandType { get; }


        public override bool Equals(object obj)
        {
            if (!(obj is RegisterKey other))
            {
                return false;
            }
            return
                other.Number == Number &&
                other.Type == Type &&
                other.OperandType == OperandType;
        }

        public override int GetHashCode()
        {
            int hashCode = 
                Number.GetHashCode() ^
                Type.GetHashCode() ^
                OperandType.GetHashCode();
            return hashCode;
        }

        public override string ToString()
        {
            if (isD3D10Type)
            {
                return $"{OperandType}{Number}";
            }
            return $"{Type}{Number}";
        }
    }
}
