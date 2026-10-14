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

    public bool IsPatchConstant { get; init; }

    /// <summary>A geometry shader output stream; tells apart elements at one register.</summary>
    public int Stream { get; init; }

    /// <summary>
    /// A patch constant keyed as an output, as a hull shader's phases write it;
    /// PCSG keys it as a domain shader input.
    /// </summary>
    public RegisterSignature AsOutput()
    {
        return new RegisterSignature(
            new D3D10RegisterKey(OperandType.Output, RegisterKey.Number),
            Name, Index, Mask, ValueType, ComponentType, ReadWriteMask)
        {
            IsPatchConstant = true,
            Stream = Stream,
        };
    }

    public override string ToString()
    {
        return $"{RegisterKey} {Name}{Index}";
    }
}
