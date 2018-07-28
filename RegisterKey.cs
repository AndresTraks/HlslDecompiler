namespace HlslDecompiler
{
    public class RegisterKey
    {
        public RegisterKey(RegisterType registerType, int registerNumber)
        {
            Type = registerType;
            Number = registerNumber;
        }

        public int Number { get; }
        public RegisterType Type { get; }


        public override bool Equals(object obj)
        {
            if (!(obj is RegisterKey other))
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
