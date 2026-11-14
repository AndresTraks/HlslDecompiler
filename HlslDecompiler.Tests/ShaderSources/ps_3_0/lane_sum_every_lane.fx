float4 c;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	return dot(c, tex2D(s0, texcoord));
}
