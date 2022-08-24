namespace HlslDecompiler.DirectXShaderModel
{
    public class D3D9RegisterKey : RegisterKey
    {
        public D3D9RegisterKey(RegisterType registerType, int registerNumber)
        {
            Type = registerType;
            Number = registerNumber;
        }

        public RegisterType Type { get; }


        public override bool TypeEquals(RegisterKey registerKey)
        {
            if (!(registerKey is D3D9RegisterKey other))
            {
                return false;
            }
            return other.Type == Type;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is D3D9RegisterKey other))
            {
                return false;
            }
            return
                other.Number == Number &&
                other.Type == Type;
        }

        public override int GetHashCode()
        {
            int hashCode =
                Number.GetHashCode() ^
                Type.GetHashCode();
            return hashCode;
        }

        public override string ToString()
        {
            return $"{Type}{Number}";
        }
    }
}
