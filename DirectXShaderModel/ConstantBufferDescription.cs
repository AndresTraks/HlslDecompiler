namespace HlslDecompiler.DirectXShaderModel
{
    public class ConstantBufferDescription
    {
        public int RegisterNumber { get; }
        public int Offset { get; }
        public int Size { get; }
        public string Name { get; }

        public ConstantBufferDescription(int registerNumber, int offset, int size, string name)
        {
            RegisterNumber = registerNumber;
            Offset = offset;
            Size = size;
            Name = name;
        }

        public override string ToString()
        {
            return $"{RegisterNumber}[{Offset}]: float{Size} {Name}";
        }
    }
}
