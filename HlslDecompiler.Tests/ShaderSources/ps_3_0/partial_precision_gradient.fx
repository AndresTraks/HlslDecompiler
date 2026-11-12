float4 k;
sampler2D s0;
sampler2D s1;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half2 t0 = tex2D(s0, texcoord).xy;
	float4 t1 = tex2Dgrad(s1, t0, ddx(texcoord), ddy(texcoord));
	return t1 * k;
}
