namespace HlslDecompiler.DirectXShaderModel;

// D3DXPARAMETER_CLASS / D3D_SHADER_VARIABLE_CLASS
public enum ParameterClass
{
    Scalar,
    Vector,
    MatrixRows,
    MatrixColumns,
    Object,
    Struct,
    InterfaceClass,
    InterfacePointer
}
