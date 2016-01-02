using System;
using System.IO;
using System.Text;

namespace HlslDecompiler
{
    public class ShaderReader : BinaryReader
    {
        ShaderModel _shader;
        Instruction _instruction;

        protected static int MakeFourCC(string id)
        {
            if (BitConverter.IsLittleEndian)
            {
                return (id[0]) + (id[1] << 8) + (id[2] << 16) + (id[3] << 24);
            }
            return (id[3]) + (id[2] << 8) + (id[1] << 16) + (id[0] << 24);
        }

        void ReadInstruction()
        {
            uint instructionToken = ReadUInt32();
            Opcode opcode = (Opcode)(instructionToken & 0xffff);

            int size;
            if (opcode == Opcode.Comment)
            {
                size = (int)((instructionToken >> 16) & 0x7FFF);
            }
            else
            {
                size = (int)((instructionToken >> 24) & 0x0f);
            }

            _instruction = new Instruction(opcode, size);
            _shader.Instructions.Add(_instruction);

            for (int i = 0; i < size; i++)
            {
                _instruction.Params[i] = ReadUInt32();
            }

            if (opcode == Opcode.Comment)
            {
                return;
            }

            _instruction.Modifier = (int)((instructionToken >> 16) & 0xff);
            _instruction.Predicated = (instructionToken & 0x10000000) != 0;
            System.Diagnostics.Debug.Assert((instructionToken & 0xE0000000) == 0);
        }

        void VerifyInstruction()
        {
            //System.Diagnostics.Debug.Assert(currentInstruction.Modifier == 0);
            System.Diagnostics.Debug.Assert(!_instruction.Predicated);

            switch (_instruction.Opcode)
            {
                case Opcode.Dcl:
                    // https://msdn.microsoft.com/en-us/library/windows/hardware/ff549176(v=vs.85).aspx
                    System.Diagnostics.Debug.Assert(_instruction.Params.Length == 2);
                    uint param0 = _instruction.Params[0];
                    switch (_instruction.GetParamRegisterType(1))
                    {
                        case RegisterType.Sampler:
                            System.Diagnostics.Debug.Assert((param0 & 0x07FFFFFF) == 0);
                            break;
                        case RegisterType.Input:
                        case RegisterType.Output:
                        case RegisterType.Texture:
                            System.Diagnostics.Debug.Assert((param0 & 0x0000FFF0) == 0);
                            System.Diagnostics.Debug.Assert((param0 & 0x7FF00000) == 0);
                            break;
                    }
                    System.Diagnostics.Debug.Assert((param0 & 0x80000000) != 0);
                    break;
                case Opcode.Def:
                    {
                        System.Diagnostics.Debug.Assert(_instruction.Params.Length == 5);
                        var registerType = _instruction.GetParamRegisterType(0);
                        System.Diagnostics.Debug.Assert(
                            registerType == RegisterType.Const ||
                            registerType == RegisterType.Const2 ||
                            registerType == RegisterType.Const3 ||
                            registerType == RegisterType.Const4);
                    }
                    break;
                case Opcode.DefI:
                    {
                        System.Diagnostics.Debug.Assert(_instruction.Params.Length == 5);
                        var registerType = _instruction.GetParamRegisterType(0);
                        System.Diagnostics.Debug.Assert(registerType == RegisterType.ConstInt);
                    }
                    break;
                case Opcode.IfC:
                    IfComparison comp = (IfComparison)_instruction.Modifier;
                    System.Diagnostics.Debug.Assert(
                        comp == IfComparison.GT ||
                        comp == IfComparison.EQ ||
                        comp == IfComparison.GE ||
                        comp == IfComparison.LT ||
                        comp == IfComparison.NE ||
                        comp == IfComparison.LE);
                    break;
                default:
                    //throw new NotImplementedException();
                    break;
            }
        }

        public ShaderReader(Stream input, bool leaveOpen = false)
            : base(input, new UTF8Encoding(false, true), leaveOpen)
        {
        }

        virtual public ShaderModel ReadShader()
        {
            // Version token
            byte minorVersion = ReadByte();
            byte majorVersion = ReadByte();
            ShaderType shaderType = (ShaderType)ReadUInt16();

            _shader = new ShaderModel(majorVersion, minorVersion, shaderType);

            do
            {
                ReadInstruction();
                VerifyInstruction();
            } while (_instruction.Opcode != Opcode.End);


            _instruction = null;
            return _shader;
        }
    }
}
