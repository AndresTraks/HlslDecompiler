namespace HlslDecompiler.DirectXShaderModel
{
    public class InstructionVerifier
    {
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Verify(Instruction instruction)
        {
            //Assert(currentInstruction.Modifier == 0);
            Assert(!instruction.Predicated);

            switch (instruction.Opcode)
            {
                case Opcode.Dcl:
                    // https://msdn.microsoft.com/en-us/library/windows/hardware/ff549176(v=vs.85).aspx
                    Assert(instruction.Params.Length == 2);
                    uint param0 = instruction.Params[0];
                    switch (instruction.GetParamRegisterType(1))
                    {
                        case RegisterType.Sampler:
                            Assert((param0 & 0x07FFFFFF) == 0);
                            break;
                        case RegisterType.Input:
                        case RegisterType.Output:
                        case RegisterType.Texture:
                            Assert((param0 & 0x0000FFF0) == 0);
                            Assert((param0 & 0x7FF00000) == 0);
                            break;
                    }
                    Assert((param0 & 0x80000000) != 0);
                    break;
                case Opcode.Def:
                    {
                        Assert(instruction.Params.Length == 5);
                        var registerType = instruction.GetParamRegisterType(0);
                        Assert(
                            registerType == RegisterType.Const ||
                            registerType == RegisterType.Const2 ||
                            registerType == RegisterType.Const3 ||
                            registerType == RegisterType.Const4);
                    }
                    break;
                case Opcode.DefI:
                    {
                        Assert(instruction.Params.Length == 5);
                        var registerType = instruction.GetParamRegisterType(0);
                        Assert(registerType == RegisterType.ConstInt);
                    }
                    break;
                case Opcode.IfC:
                    IfComparison comp = (IfComparison)instruction.Modifier;
                    Assert(
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

        private static void Assert(bool condition)
        {
            System.Diagnostics.Debug.Assert(condition);
        }
    }
}
