using HlslDecompiler.DirectXShaderModel;
namespace HlslDecompiler.Hlsl;

public sealed class MatrixMultiplicationCompiler
{
    private NodeCompiler nodeCompiler;

    public MatrixMultiplicationCompiler(NodeCompiler nodeCompiler)
    {
        this.nodeCompiler = nodeCompiler;
    }

    /// <summary>
    /// A matrix held in a structured buffer, named from buffer and element:
    /// `instances[id].world`.
    /// </summary>
    private string CompileStructured(MatrixMultiplicationContext context)
    {
        string element = nodeCompiler.CompileAsInteger([context.StructuredElement]);
        string matrixName =
            $"{context.StructuredBufferName}[{element}]{context.StructuredMemberPath}";
        ShaderTypeInfo matrixType = context.MatrixTypeInfo;
        bool matrixByVector = matrixType.ParameterClass == ParameterClass.MatrixRows
            ? !context.IsMatrixByVector
            : context.IsMatrixByVector;
        int rows = matrixByVector ? context.MatrixRowCount : context.MatrixColumnCount;
        int columns = matrixByVector ? context.MatrixColumnCount : context.MatrixRowCount;
        if (rows != matrixType.Rows || columns != matrixType.Columns)
        {
            matrixName = $"(float{rows}x{columns}){matrixName}";
        }
        string vector = nodeCompiler.Compile(context.Vector);
        return matrixByVector
            ? $"mul({matrixName}, {vector})"
            : $"mul({vector}, {matrixName})";
    }

    /// <summary>
    /// `mul(world, viewProjection)` from `mul(world[0], viewProjection)`, where the
    /// context's vector is a row of leftMatrix. Null if the matrix is on the left,
    /// partial, or indexed.
    /// </summary>
    public string CompileMatrixProduct(MatrixMultiplicationContext context, string leftMatrix)
    {
        if (context.StructuredElement != null || context.ElementIndexNode != null
            || context.ElementIndex != null || context.MemberPath != null)
        {
            return null;
        }
        ShaderTypeInfo matrixType = context.MatrixTypeInfo ?? context.MatrixDeclaration.TypeInfo;
        bool matrixByVector = matrixType.ParameterClass == ParameterClass.MatrixRows
            ? !context.IsMatrixByVector
            : context.IsMatrixByVector;
        int rows = matrixByVector ? context.MatrixRowCount : context.MatrixColumnCount;
        int columns = matrixByVector ? context.MatrixColumnCount : context.MatrixRowCount;
        if (matrixByVector || rows != matrixType.Rows || columns != matrixType.Columns)
        {
            return null;
        }
        return $"mul({leftMatrix}, {context.MatrixDeclaration.Name})";
    }

    public string Compile(MatrixMultiplicationContext context)
    {
        if (context.StructuredElement != null)
        {
            return CompileStructured(context);
        }
        string matrixName = context.MatrixDeclaration.Name;
        if (context.ElementIndexNode != null)
        {
            // The address register counts registers; divide by registers per element.
            string element = context.ElementIndexCountsElements
                ? nodeCompiler.Compile(context.ElementIndexNode)
                : nodeCompiler.CompileRegisterIndexAsElement(
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
        matrixName += context.MemberPath;
        ShaderTypeInfo matrixType = context.MatrixTypeInfo ?? context.MatrixDeclaration.TypeInfo;
        // A row major matrix packs the other way, so the same dot products are the
        // other multiplication: `mul(v, M)` instead of `mul(M, v)`.
        bool matrixByVector = matrixType.ParameterClass == ParameterClass.MatrixRows
            ? !context.IsMatrixByVector
            : context.IsMatrixByVector;
        // A submatrix is cast to its own size. In mul(M, v) the dot products are the
        // rows; in mul(v, M) they are the columns.
        int rows = matrixByVector ? context.MatrixRowCount : context.MatrixColumnCount;
        int columns = matrixByVector ? context.MatrixColumnCount : context.MatrixRowCount;
        if (rows != matrixType.Rows || columns != matrixType.Columns)
        {
            matrixName = $"(float{rows}x{columns}){matrixName}";
        }
        string vector = nodeCompiler.Compile(context.Vector);
        return matrixByVector
            ? $"mul({matrixName}, {vector})"
            : $"mul({vector}, {matrixName})";
    }
}
