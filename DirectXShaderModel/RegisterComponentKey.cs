using System;

namespace HlslDecompiler.DirectXShaderModel;

public class RegisterComponentKey : IHasComponentIndex
{
    public RegisterComponentKey(RegisterType registerType, int registerNumber, int componentIndex)
    {
        RegisterKey = new D3D9RegisterKey(registerType, registerNumber);
        ComponentIndex = componentIndex;
    }

    public RegisterComponentKey(RegisterKey registerKey, int componentIndex)
    {
        RegisterKey = registerKey ?? throw new ArgumentNullException(nameof(registerKey));
        ComponentIndex = componentIndex;
    }

    public RegisterKey RegisterKey { get; }
    public int ComponentIndex { get; }

    public int Number => RegisterKey.Number;

    private string Component
    {
        get
        {
            return ComponentIndex switch
            {
                0 => "x",
                1 => "y",
                2 => "z",
                3 => "w",
                _ => $"({ComponentIndex})",
            };
        }
    }

    public override bool Equals(object obj)
    {
        if (obj is not RegisterComponentKey other)
        {
            return false;
        }
        return
            other.RegisterKey.Equals(RegisterKey) &&
            other.ComponentIndex == ComponentIndex;
    }

    public override int GetHashCode()
    {
        int hashCode =
            RegisterKey.GetHashCode() ^
            ComponentIndex.GetHashCode();
        return hashCode;
    }

    public override string ToString()
    {
        return $"{RegisterKey}.{Component}";
    }
}
