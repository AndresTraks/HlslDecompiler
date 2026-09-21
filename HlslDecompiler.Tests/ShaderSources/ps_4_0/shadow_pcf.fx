cbuffer cb : register(b0)
{
	float4x4 lightViewProj;
	float4 bias;
};

SamplerComparisonState shadowSamp;
SamplerState samp;
Texture2D shadowMap;
Texture2D albedo;

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	float2 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float t0 = dot(transpose(lightViewProj)[3], i.texcoord);
	float3 t1 = mul(i.texcoord, (float4x3)lightViewProj) / t0;
	float3 t2 = float3(float2(0.5, -0.5) * t1.xy + 0.5, t1.z - bias.x);
	float t3 = 0;
	for (int t4 = -1; t4 <= 1; t4 = t4 + 1) {
		float2 t5 = float2((float)t4 * bias.z + t2.x, t2.y - bias.w);
		t3 = t3 + shadowMap.SampleCmpLevelZero(shadowSamp, t5, t2.z).x;
	}
	float t6 = t3;
	for (int t7 = -1; t7 <= 1; t7 = t7 + 1) {
		t6 = shadowMap.SampleCmpLevelZero(shadowSamp, float2((float)t7 * bias.z + t2.x, t2.y), t2.z).x + t6;
	}
	float t8 = t6;
	for (int t9 = -1; t9 <= 1; t9 = t9 + 1) {
		float2 t10 = float2((float)t9 * bias.z + t2.x, t2.y + bias.w);
		t8 = t8 + shadowMap.SampleCmpLevelZero(shadowSamp, t10, t2.z).x;
	}
	return 0.111111112 * t8 * albedo.Sample(samp, i.texcoord1);
}
