using System;

namespace HlslDecompiler.DirectXShaderModel;

public class D3D10OperandTokenCollection
{
    private readonly D3D10Opcode _opcode;

    public uint[] Tokens { get; }
    public virtual int Count => Tokens.Length;

    public D3D10OperandTokenCollection(uint[] paramTokens, D3D10Opcode opcode)
    {
        Tokens = paramTokens;
        _opcode = opcode;
    }

    public Span<uint> GetSpan(int index)
    {
        int operandCount = 0;
        for (int i = 0; i < Tokens.Length;)
        {
            int spanStart = i;
            i = SkipOperand(i);

            if (operandCount == index)
            {
                return new Span<uint>(Tokens, spanStart, i - spanStart);
            }
            operandCount++;
        }
        return new Span<uint>(Tokens, 0, 1);
    }

    // Returns the index just past the operand starting at i. Operands are variable
    // length: an immediate carries its values inline, and a relatively addressed one
    // - cb0[r0.x + 2] - encodes its index as a nested operand, hence the recursion.
    private int SkipOperand(int i)
    {
        uint token = Tokens[i];
        i++;

        bool isExtended = (token & 0x80000000) != 0;
        if (isExtended)
        {
            i++;
        }

        OperandType operandType = (OperandType)((token >> 12) & 0xFF);
        if (_opcode == D3D10Opcode.DclResource || _opcode == D3D10Opcode.DclThreadGroup)
        {
            return i + 2;
        }

        if (operandType == OperandType.Immediate32)
        {
            var componentSelection = (D3D10OperandNumComponents)(token & 3);
            if (componentSelection == D3D10OperandNumComponents.Operand1Component)
            {
                i++;
            }
            else if (componentSelection == D3D10OperandNumComponents.Operand4Component)
            {
                i += 4;
            }
        }

        int indexDimension = (int)((token >> 20) & 3);
        for (int r = 0; r < indexDimension; r++)
        {
            var indexRepresentation = (D3D10OperandIndexRepresentation)((token >> (22 + r * 3)) & 7);
            switch (indexRepresentation)
            {
                case D3D10OperandIndexRepresentation.Immediate32:
                    i++;
                    break;
                case D3D10OperandIndexRepresentation.Immediate64:
                    i += 2;
                    break;
                case D3D10OperandIndexRepresentation.Relative:
                    i = SkipOperand(i);
                    break;
                case D3D10OperandIndexRepresentation.Immediate32PlusRelative:
                    i = SkipOperand(i + 1);
                    break;
                case D3D10OperandIndexRepresentation.Immediate64PlusRelative:
                    i = SkipOperand(i + 2);
                    break;
                default:
                    throw new NotImplementedException(indexRepresentation.ToString());
            }
        }

        return i;
    }

}
