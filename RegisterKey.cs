namespace HlslDecompiler
{
    public class RegisterKey : IHasComponentIndex
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
            if (!(obj is RegisterKey other))
            {
                return false;
            }
            if (other.RegisterNumber == RegisterNumber &&
                other.RegisterType == RegisterType)
            {
                if (IsSampler)
                {
                    return true;
                }
                else
                {
                    return other.ComponentIndex == ComponentIndex;
                }
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hashCode = RegisterNumber.GetHashCode() ^ RegisterType.GetHashCode();
            if (!IsSampler)
            {
                hashCode ^= ComponentIndex.GetHashCode();
            }
            return hashCode;
        }

        public override string ToString()
        {
            return IsSampler
                ? $"{RegisterType}{RegisterNumber}"
                : $"{RegisterType}{RegisterNumber}.{Component}";
        }

        private bool IsSampler => RegisterType == RegisterType.Sampler;
    }
}
