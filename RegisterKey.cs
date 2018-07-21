namespace HlslDecompiler
{
    public class RegisterKey
    {
        public int RegisterNumber { get; set; }
        public RegisterType RegisterType { get; set; }
        public int ComponentIndex { get; set; }

        private string Component
        {
            get
            {
                switch (ComponentIndex)
                {
                    case 0:
                        return "x";
                    case 1:
                        return "y";
                    case 2:
                        return "z";
                    case 3:
                        return "w";
                    default:
                        return $"({ComponentIndex})";
                }
            }
        }

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
            return $"{RegisterType}{RegisterNumber}.{Component}";
        }
    }
}
