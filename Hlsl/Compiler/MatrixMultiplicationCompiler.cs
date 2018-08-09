namespace HlslDecompiler.Hlsl
{
    public sealed class MatrixMultiplicationCompiler
    {
        private NodeCompiler nodeCompiler;

        public MatrixMultiplicationCompiler(NodeCompiler nodeCompiler)
        {
            this.nodeCompiler = nodeCompiler;
        }

        public string Compile(MatrixMultiplicationContext context)
        {
            string matrixName = context.MatrixDeclaration.Name;
            string vector = nodeCompiler.Compile(context.Vector);
            return context.IsMatrixByVector
                ? $"mul({matrixName}, {vector})"
                : $"mul({vector}, {matrixName})";
        }
    }
}
