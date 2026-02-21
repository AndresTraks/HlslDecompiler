namespace HlslDecompiler.DirectXShaderModel;

public class RegisterSignature
{
    public D3D10RegisterKey RegisterKey { get; }
    public string Name { get; }
    public int Index { get; }
    public byte Mask { get; }

    public RegisterSignature(D3D10RegisterKey registerKey, string name, int index, byte mask)
    {
        RegisterKey = registerKey;
        Name = name;
        Index = index;
        Mask = mask;
    }
}
