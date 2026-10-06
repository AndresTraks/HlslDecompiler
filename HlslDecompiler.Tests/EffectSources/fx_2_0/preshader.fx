// Compiled as effects are, with preshaders: arithmetic on uniforms alone is
// moved out of the shaders and into programs the effect runs before them, and
// the shaders read what those leave in their constant registers.
float4x4 world;
float4x4 viewProjection;
float4 tint;
float4 colors[4];
float scale;
float angle;
float2 range;
float3 lightDirection;
int index;

sampler diffuseSampler;

// The product of two matrices, and a vector normalized, both before the shader.
float4 vs_matrices(float4 position : POSITION, float3 normal : NORMAL, out float4 color : COLOR) : POSITION
{
    color = saturate(dot(normal, normalize(lightDirection))) * tint;
    return mul(position, mul(world, viewProjection));
}

// tint is read by the shader as it is and by the preshader scaled, so both
// tables list it.
float4 ps_shared(float2 texcoord : TEXCOORD0) : COLOR
{
    return tex2D(diffuseSampler, texcoord) * tint + tint * (scale * 2 + 1);
}

float4 ps_functions(float2 texcoord : TEXCOORD0) : COLOR
{
    float4 a = float4(sin(angle), cos(angle), 1 / scale, rsqrt(abs(scale)));
    float4 b = float4(frac(scale), exp2(scale), log2(abs(scale) + 1), max(angle, scale));
    return a * texcoord.x + b * texcoord.y + angle / range.x;
}

// Inverse trigonometry, which the effect compiler expands into polynomials even
// in a preshader - too many instructions for ps_2_0 once they are back in the
// shader, as they are in its decompilation.
float4 ps_inverse(float2 texcoord : TEXCOORD0) : COLOR
{
    return float4(asin(saturate(scale)), acos(saturate(angle)), atan(scale), atan2(scale, angle)) * texcoord.x;
}

float4 ps_select(float2 texcoord : TEXCOORD0) : COLOR
{
    float4 chosen = scale > 0.5 ? tint : colors[0];
    return chosen * min(range.x, range.y) * texcoord.x + colors[index] * texcoord.y
        + lerp(tint, colors[1], scale);
}

// A branch on uniforms alone, which the preshader can decide.
float4 ps_branch(float2 texcoord : TEXCOORD0) : COLOR
{
    float4 color = tex2D(diffuseSampler, texcoord);
    if (scale > angle)
    {
        color *= tint;
    }
    return color;
}

technique Preshaded
{
    pass Matrices
    {
        VertexShader = compile vs_2_0 vs_matrices();
        PixelShader = compile ps_2_0 ps_shared();
    }
    pass Functions
    {
        PixelShader = compile ps_2_0 ps_functions();
    }
    pass Inverse
    {
        PixelShader = compile ps_3_0 ps_inverse();
    }
    pass Select
    {
        PixelShader = compile ps_2_0 ps_select();
    }
    pass Branch
    {
        PixelShader = compile ps_3_0 ps_branch();
    }
}
