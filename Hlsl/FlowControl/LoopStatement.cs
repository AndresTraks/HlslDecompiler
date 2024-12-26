namespace HlslDecompiler.Hlsl.FlowControl
{
    public class LoopStatement : IStatement
    {
        public LoopStatement(uint repeatCount, Closure closure)
        {
            RepeatCount = repeatCount;
            Closure = closure;

            Body = new StatementSequence(closure);
        }

        public uint RepeatCount { get; }
        public Closure Closure { get; }
        public StatementSequence Body { get; }

        public Closure EndClosure { get; set; }
    }
}
