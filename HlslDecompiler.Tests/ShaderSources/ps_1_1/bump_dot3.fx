sampler2D s0;
sampler2D s1;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float2 texcoord1 : TEXCOORD1;
	float4 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	float3 t0 = 2 * tex2D(s0, i.texcoord).xyz - 1;
	float3 t1 = 2 * i.color.xyz - 1;
	float t2 = saturate(dot(t0, t1));
	float4 t3 = tex2D(s1, i.texcoord1);
	return float4(t3.xyz * (t2 + float3(0.200000003, 0.200000003, 0.25)), 1 - t3.w);
}
