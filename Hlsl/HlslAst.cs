using HlslDecompiler.Hlsl.FlowControl;

namespace HlslDecompiler.Hlsl
{
    public class HlslAst
    {
        public IStatement Statement { get; private set; }
        public RegisterState RegisterState { get; private set; }

        public HlslAst(IStatement statement, RegisterState registerState)
        {
            Statement = statement;
            RegisterState = registerState;
        }
    }
}
