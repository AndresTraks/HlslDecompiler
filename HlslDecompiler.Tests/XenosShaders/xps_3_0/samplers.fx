sampler2D s0;
sampler2D s1;
sampler2D s2;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = tex2D(s0, texcoord);
	float4 t1 = tex2D(s2, t0.xy);
	return t0 * tex2D(s1, texcoord.yx) + t1;
}
