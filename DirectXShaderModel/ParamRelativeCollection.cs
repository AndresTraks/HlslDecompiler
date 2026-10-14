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
    /// Whether a relatively addressed operand is followed by an index register
    /// token. Shader model 1 has none: the index is always a0.x.
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
            // a0.x
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
