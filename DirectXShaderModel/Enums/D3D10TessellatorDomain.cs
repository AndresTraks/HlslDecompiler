namespace HlslDecompiler.DirectXShaderModel;

// D3D_TESSELLATOR_DOMAIN: what the tessellator subdivides, which HLSL writes as
// the [domain(...)] attribute on a hull or domain shader.
public enum D3D10TessellatorDomain
{
    Undefined,
    Isoline,
    Triangle,
    Quad,
}
