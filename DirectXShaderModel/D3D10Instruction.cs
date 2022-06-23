using System;

namespace HlslDecompiler.DirectXShaderModel
{
    public class D3D10Instruction : Instruction
    {
        public D3D10Opcode Opcode { get; }
        public D3D10ParamCollection ParamTokens { get; }

        public D3D10Instruction(D3D10Opcode opcode, uint[] paramTokens)
        {
            Opcode = opcode;
            ParamTokens = new D3D10ParamCollection(paramTokens);
        }

        public override bool HasDestination
        {
            get
            {
                switch (Opcode)
                {
                    case D3D10Opcode.Add:
                    case D3D10Opcode.DclInputPS:
                    case D3D10Opcode.DclOutput:
                    case D3D10Opcode.DclTemps:
                    case D3D10Opcode.Dp2:
                    case D3D10Opcode.GE:
                    case D3D10Opcode.Mad:
                    case D3D10Opcode.Mov:
                    case D3D10Opcode.MovC:
                    case D3D10Opcode.Mul:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public override bool IsTextureOperation
        {
            get
            {
                switch (Opcode)
                {
                    default:
                        return false;
                }
            }
        }

        public override int GetDestinationParamIndex()
        {
            throw new NotImplementedException();
        }

        public override int GetDestinationWriteMask()
        {
            throw new NotImplementedException();
        }

        public override int GetDestinationMaskedLength()
        {
            return 4;
        }

        public override int GetSourceSwizzle(int srcIndex)
        {
            throw new NotImplementedException();
        }

        public override string GetSourceSwizzleName(int srcIndex)
        {
            throw new NotImplementedException();
        }

        public override string GetDeclSemantic()
        {
            switch (GetParamOperandType(0))
            {
                case OperandType.Output:
                    return "sem";
                //return GetDeclUsage().ToString().ToUpper();
                default:
                    throw new NotImplementedException();
            }
        }

        public override float GetParamSingle(int index)
        {
            throw new NotImplementedException();
        }

        public override float GetParamInt(int index)
        {
            throw new NotImplementedException();
        }

        private Span<uint> GetParamSpan(int index)
        {
            int paramCount = 0;
            for (int i = 0; i < ParamTokens.Count; i++)
            {
                uint param = ParamTokens.Tokens[i];
                int numComponents = (int)(param & 3);
                numComponents.ToString();
            }
            return new Span<uint>(ParamTokens.Tokens, 0, 1);
        }

        public override RegisterKey GetParamRegisterKey(int index)
        {
            return new RegisterKey(
                GetParamOperandType(index));
        }

        public OperandType GetParamOperandType(int index)
        {
            uint p = ParamTokens.Tokens[index];
            return (OperandType)((p >> 12) & 0xFF);
        }

        public override string GetParamRegisterName(int index)
        {
            throw new NotImplementedException();
        }

        public override int GetParamRegisterNumber(int index)
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return Opcode.ToString();
        }
    }
}
