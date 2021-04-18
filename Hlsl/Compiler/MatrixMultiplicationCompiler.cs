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
            if (context.Vector.Length != context.MatrixRowCount)
            {
                matrixName = $"(float{context.MatrixDeclaration.Columns}x{context.Vector.Length}){matrixName}";
            }
            string vector = nodeCompiler.Compile(context.Vector.Inputs);
            return context.IsMatrixByVector
                ? $"mul({matrixName}, {vector})"
                : $"mul({vector}, {matrixName})";
        }
    }
}
