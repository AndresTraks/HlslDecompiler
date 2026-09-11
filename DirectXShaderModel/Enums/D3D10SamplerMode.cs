namespace HlslDecompiler.DirectXShaderModel;

// How dcl_sampler declares a sampler. Comparison is the one behind
// SamplerComparisonState, which sample_c and sample_c_lz read through.
public enum D3D10SamplerMode
{
    Default,
    Comparison,
    Mono
}
