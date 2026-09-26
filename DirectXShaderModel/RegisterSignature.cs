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

    /// <summary>
    /// Whether this element describes a patch constant - one of the values computed
    /// once for the whole patch. The chunk that holds them is the same in both
    /// shaders that touch them, so it says nothing about direction: the hull shader
    /// writes them and the domain shader reads them.
    /// </summary>
    public bool IsPatchConstant { get; init; }

    /// <summary>
    /// Which output stream the element belongs to. Shader model 5 lets a geometry
    /// shader write up to four, each with a signature of its own, and the stream is
    /// the only thing telling two elements at the same register apart.
    /// </summary>
    public int Stream { get; init; }

    /// <summary>
    /// The same element keyed as an output register, which is what a hull shader's
    /// phases write it as. Read from the chunk it is keyed as a domain shader's
    /// input, and left that way a fork phase's `dcl_output_siv o0.x` found whatever
    /// the control point output signature had at register zero instead.
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
