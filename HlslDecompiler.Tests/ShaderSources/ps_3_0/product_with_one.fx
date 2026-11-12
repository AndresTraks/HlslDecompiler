float4 k;
float4 k2;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half t0 = tex2D(s0, texcoord).x;
	half t1 = 1 / t0;
	half t2 = rsqrt(t0);
	return float4(t1, t2, t1 * k2.x, 1) * k;
}
