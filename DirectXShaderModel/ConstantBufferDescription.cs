namespace HlslDecompiler.DirectXShaderModel
{
    public class ConstantBufferDescription
    {
        public int RegisterNumber { get; }
        public int Size { get; }
        public string Name { get; }

        public ConstantBufferDescription(int registerNumber, int size, string name)
        {
            RegisterNumber = registerNumber;
            Size = size;
            Name = name;
        }

        public override string ToString()
        {
            return $"{RegisterNumber}: float{Size} {Name}";
        }
    }
}
