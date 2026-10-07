sampler2D s0;
sampler2D s1;
sampler2D s2;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
	float4 texcoord2 : TEXCOORD2;
	float4 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	float2 t0 = float2(0.0799999982, -0.0599999987) * (2 * tex2D(s0, i.texcoord).xy - 1) + i.texcoord1.xy;
	float2 t1 = i.texcoord2.xy / i.texcoord2.w;
	float3 t2 = tex2D(s1, t0).xyz;
	float4 t3 = tex2D(s2, t1);
	return float4(0.5 - t3.xyz >= 0 ? 1 - t2 : lerp(float3(0.899999976, 0.800000012, 0.699999988), t2 * i.color.xyz, t3.xyz), 2 * t3.w * i.color.w);
}
