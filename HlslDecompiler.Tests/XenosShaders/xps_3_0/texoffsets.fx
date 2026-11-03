// Texture fetches carrying offsets and an LOD bias, which no HLSL spells:
// the .bin is this source compiled by XnaXbox and the tfetches' offset and
// LODBias fields then set (offsets in texels, bias in LODs):
//   a, b: OffsetX/Y = (+-0.5, -+0.5)        c: LODBias = 0.5
//   d: LODBias = -0.5, OffsetX/Y = 1.5      e: OffsetX = 0.5 (1D)
// fxc compiles this source without them, so its compile is a reference for
// the coordinates, not for the fetches (see KnownReferenceDifferences).
sampler2D s0; sampler2D s1; sampler1D s2; float4 main(float2 uv : TEXCOORD0, float u : TEXCOORD1) : COLOR { float4 a = tex2D(s0, uv); float4 b = tex2D(s0, uv.yx); float4 c = tex2D(s1, uv); float4 d = tex2D(s1, uv.yx); float4 e = tex1D(s2, u); return (a + b) * c + d + e; }
