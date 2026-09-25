namespace HlslDecompiler.DirectXShaderModel;

// D3D_TESSELLATOR_OUTPUT_PRIMITIVE: what the tessellator makes of a subdivided
// patch, and which way round its triangles wind, which HLSL writes as the
// [outputtopology(...)] attribute on a hull shader.
public enum D3D10TessellatorOutputPrimitive
{
    Undefined,
    Point,
    Line,
    TriangleClockwise,
    TriangleCounterClockwise,
}
