namespace HlslDecompiler.DirectXShaderModel;

public class RegisterSignature
{
    public D3D10RegisterKey RegisterKey { get; }
    public string Name { get; }
    public int Index { get; }
    public byte Mask { get; }
    public int ValueType { get; }
    public int ComponentType { get; }
    public byte ReadWriteMask { get; }

    public RegisterSignature(D3D10RegisterKey registerKey, string name, int index, byte mask, int valueType, int componentType, byte readWriteMask)
    {
        RegisterKey = registerKey;
        Name = name;
        Index = index;
        Mask = mask;
        ValueType = valueType;
        ComponentType = componentType;
        ReadWriteMask = readWriteMask;
    }

    public override string ToString()
    {
        return $"{RegisterKey} {Name}{Index}";
    }
}
