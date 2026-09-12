namespace HlslDecompiler.DirectXShaderModel;

public class ParamRelativeCollection : D3D9ParamCollection
{
    public override int Count
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Tokens.Length; i++)
            {
                count++;
                // Skip relative addressing specifier
                if (HasSeparateRelativeToken && (Tokens[i] & (1 << 13)) != 0)
                {
                    i++;
                }
            }
            return count;
        }
    }

    /// <summary>
    /// Whether a relatively addressed operand is followed by a token naming the
    /// register that indexes it. Shader model 1 has no such token: the address
    /// register is always a0.x there, so the bit marks the addressing and nothing
    /// follows it. Assuming one anyway shifts every later parameter along by one.
    /// </summary>
    public bool HasSeparateRelativeToken { get; set; } = true;

    public ParamRelativeCollection(uint[] paramTokens)
        : base(paramTokens)
    {
    }

    public override uint this[int index]
    {
        get
        {
            int t = 0;
            for (int i = 0; i < index; i++)
            {
                // Skip relative addressing specifier
                if (HasSeparateRelativeToken && (Tokens[t] & (1 << 13)) != 0)
                {
                    t++;
                }
                t++;
            }
            return Tokens[t];
        }
    }

    public override bool HasRelativeAddressing(int index)
    {
        uint token = this[index];
        return (token & (1 << 13)) != 0;
    }

    public override uint GetRelativeToken(int index)
    {
        if (!HasSeparateRelativeToken)
        {
            // a0.x, spelled out: the register type in the top bits, number zero,
            // component zero.
            return (uint)RegisterType.Addr << 28;
        }

        int t = 0;
        for (int i = 0; i < index; i++)
        {
            if (HasRelativeAddressing(t))
            {
                t++;
            }
            t++;
        }
        return Tokens[t + 1];
    }
}
