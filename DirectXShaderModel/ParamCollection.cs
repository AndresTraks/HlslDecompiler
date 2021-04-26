using System;

namespace HlslDecompiler.DirectXShaderModel
{
    public class ParamCollection
    {
        public uint[] Tokens { get; }
        public virtual int Count => Tokens.Length;

        public ParamCollection(uint[] paramTokens)
        {
            Tokens = paramTokens;
        }

        public virtual uint this[int index] => Tokens[index];

        public virtual bool HasRelativeAddressing(int tokenIndex) => false;

        public virtual uint GetRelativeToken(int index)
        {
            throw new NotSupportedException();
        }
    }
}
