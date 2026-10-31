float k;
sampler2D s;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	return float4(0, 0, 0, k * tex2D(s, texcoord).w);
}
