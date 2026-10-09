uint count;

struct L
{
	float3 pos;
	float radius;
	float3 colour;
	float pad;
};

SamplerState pt;
StructuredBuffer<L> lights : register(t0);
Texture2D albedo : register(t1);
Texture2D normal;
Texture2D posTex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float3 t0 = normal.Sample(pt, texcoord).xyz;
	float3 t1 = albedo.Sample(pt, texcoord).xyz;
	float3 t2 = normalize(t0);
	float3 t3 = posTex.Sample(pt, texcoord).xyz;
	float3 t4 = 0;
	[loop]
	for (uint t5 = 0; t5 < count; t5 = t5 + 1) {
		float4 t6 = float4(lights[t5].pos, lights[t5].radius);
		float t7 = length(t6.xyz - t3);
		float t8 = saturate(dot(t2, (t6.xyz - t3) / t7));
		float t9 = saturate(1 - t7 / t6.w);
		t4 = t4 + t8 * lights[t5].colour * t9;
	}
	return float4(t1 * t4, 1);
}
