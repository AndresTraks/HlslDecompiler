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
	float t0 = tex2D(s0, i.texcoord).w;
	float3 t1 = 2 * tex2D(s0, i.texcoord).xyz - 1;
	float3 t2 = 2 * i.color.xyz - 1;
	float t3 = saturate(dot(t1, t2));
	float4 t4 = tex2D(s1, i.texcoord1);
	return float4(t4.xyz * (t3 + float3(0.200000003, 0.200000003, 0.25)), 1 - t4.w);
}
