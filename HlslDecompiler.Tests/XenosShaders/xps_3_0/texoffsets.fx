// Texture fetches carrying offsets and an LOD bias, which no HLSL spells:
// the .bin is this source compiled by XnaXbox and the tfetches' offset and
// LODBias fields then set (offsets in texels, bias in LODs):
//   a, b: OffsetX/Y = (+-0.5, -+0.5)        c: LODBias = 0.5
//   d: LODBias = -0.5, OffsetX/Y = 1.5      e: OffsetX = 0.5 (1D)
// fxc compiles this source without them, so its compile is a reference for
// the coordinates, not for the fetches (see KnownReferenceDifferences).
sampler2D s0;
sampler2D s1;
sampler1D s2;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float4 a = tex2D(s0, i.texcoord);
	float4 b = tex2D(s0, i.texcoord.yx);
	float4 c = tex2D(s1, i.texcoord);
	float4 d = tex2D(s1, i.texcoord.yx);
	float4 e = tex1D(s2, i.texcoord1);
	return (a + b) * c + d + e;
}
