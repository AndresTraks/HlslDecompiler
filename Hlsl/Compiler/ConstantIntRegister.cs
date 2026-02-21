namespace HlslDecompiler.Hlsl;

public class ConstantIntRegister
{
    public int RegisterIndex { get; private set; }
    public uint[] Value { get; private set; }

    public ConstantIntRegister(int registerIndex, uint value0, uint value1, uint value2, uint value3)
    {
        RegisterIndex = registerIndex;
        Value = new[] { value0, value1, value2, value3 };
    }

    public uint this[int index]
    {
        get
        {
            return Value[index];
        }
        set
        {
            Value[index] = value;
        }
    }
}
