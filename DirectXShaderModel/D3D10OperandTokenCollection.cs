using System;

namespace HlslDecompiler.DirectXShaderModel
{
    public class D3D10OperandTokenCollection
    {
        private readonly bool _isResourceDeclaration;

        public uint[] Tokens { get; }
        public virtual int Count => Tokens.Length;

        public D3D10OperandTokenCollection(uint[] paramTokens, bool isResourceDeclaration)
        {
            Tokens = paramTokens;
            _isResourceDeclaration = isResourceDeclaration;
        }

        public Span<uint> GetSpan(int index)
        {
            int operandCount = 0;
            for (int i = 0; i < Tokens.Length;)
            {
                int spanStart = i;
                uint token = Tokens[i];
                i++;

                bool isExtended = (token & 0x80000000) != 0;
                if (isExtended)
                {
                    i++;
                }

                OperandType operandType = (OperandType)((token >> 12) & 0xFF);
                if (_isResourceDeclaration)
                {
                    i += 2;
                }
                else
                {
                    D3D10OperandNumComponents componentSelection = (D3D10OperandNumComponents)(token & 3);
                    if (componentSelection == D3D10OperandNumComponents.Operand1Component)
                    {
                        if (operandType == OperandType.Immediate32)
                        {
                            i++;
                        }
                    }
                    else if (componentSelection == D3D10OperandNumComponents.Operand4Component)
                    {
                        if (operandType == OperandType.Immediate32)
                        {
                            i += 4;
                        }
                    }

                    int indexDimension = (int)((token >> 20) & 3);
                    for (int r = 0; r < indexDimension; r++)
                    {
                        D3D10OperandIndexRepresentation indexRepresentation = (D3D10OperandIndexRepresentation)((token >> (22 + r * 3)) & 7);
                        if (indexRepresentation == D3D10OperandIndexRepresentation.Immediate32)
                        {
                            i++;
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                }

                if (operandCount == index)
                {
                    return new Span<uint>(Tokens, spanStart, i - spanStart);
                }

                operandCount++;
            }
            return new Span<uint>(Tokens, 0, 1);
        }
    }
}
