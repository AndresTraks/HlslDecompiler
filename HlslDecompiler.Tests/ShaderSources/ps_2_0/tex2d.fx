float4 tint;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	return tex2D(s0, texcoord) * tint;
}
