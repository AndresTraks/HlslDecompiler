namespace HlslDecompiler
{
    public class RegisterKey
    {
        public int RegisterNumber { get; set; }
        public RegisterType RegisterType { get; set; }
        public int ComponentIndex { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as RegisterKey;
            if (other == null) return false;
            return other.RegisterNumber == RegisterNumber &&
                other.RegisterType == RegisterType &&
                other.ComponentIndex == ComponentIndex;
        }

        public override int GetHashCode()
        {
            return RegisterNumber.GetHashCode() ^ RegisterType.GetHashCode() ^ ComponentIndex.GetHashCode();
        }

        public override string ToString()
        {
            return $"{RegisterType}{RegisterNumber} ({ComponentIndex})";
        }
    }
}
