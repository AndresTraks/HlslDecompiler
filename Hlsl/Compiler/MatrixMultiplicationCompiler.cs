using HlslDecompiler.DirectXShaderModel;
namespace HlslDecompiler.Hlsl;

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
        if (context.ElementIndexNode != null)
        {
            // Rows read through the address register: the index counts registers
            // across the array, and the element is that over the registers one takes.
            string element = nodeCompiler.CompileRegisterIndexAsElement(
                context.ElementIndexNode, context.MatrixDeclaration.RegistersPerElement);
            if (context.ElementIndex is int and not 0)
            {
                element += $" + {context.ElementIndex}";
            }
            matrixName = $"{matrixName}[{element}]";
        }
        else if (context.ElementIndex is int element)
        {
            matrixName = $"{matrixName}[{element}]";
        }
        // A matrix member of a struct element is the element and then the member.
        matrixName += context.MemberPath;
        ShaderTypeInfo matrixType = context.MatrixTypeInfo ?? context.MatrixDeclaration.TypeInfo;
        // A submatrix is cast to its own size. In mul(matrix, vector) the dot
        // products are the rows and their width the columns; in mul(vector, matrix)
        // it is the other way about, and a float4x3 read whole by four wide dots
        // three times is the float4x3 it was declared as.
        int rows = context.IsMatrixByVector ? context.MatrixRowCount : context.MatrixColumnCount;
        int columns = context.IsMatrixByVector ? context.MatrixColumnCount : context.MatrixRowCount;
        if (rows != matrixType.Rows || columns != matrixType.Columns)
        {
            matrixName = $"(float{rows}x{columns}){matrixName}";
        }
        string vector = nodeCompiler.Compile(context.Vector);
        return context.IsMatrixByVector
            ? $"mul({matrixName}, {vector})"
            : $"mul({vector}, {matrixName})";
    }
}
