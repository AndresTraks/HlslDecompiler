sampler2D s0;
sampler2D s1;
sampler2D s2;
sampler2D s3;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = tex2D(s0, texcoord);
	float4 t1 = tex2D(s3, t0.yz);
	return tex2D(s1, t0.wx) * tex2D(s2, t0.xy) + t1;
}
