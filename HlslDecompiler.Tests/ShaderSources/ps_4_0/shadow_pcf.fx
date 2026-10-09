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
	float4 sv_position : SV_Position;
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
		float t5 = shadowMap.SampleCmpLevelZero(shadowSamp, float2((float)t4 * bias.z + t2.x, t2.y - bias.w), t2.z).x;
		t3 = t3 + t5;
	}
	for (int t6 = -1; t6 <= 1; t6 = t6 + 1) {
		float t7 = shadowMap.SampleCmpLevelZero(shadowSamp, float2((float)t6 * bias.z + t2.x, t2.y), t2.z).x;
		t3 = t3 + t7;
	}
	for (int t8 = -1; t8 <= 1; t8 = t8 + 1) {
		float t9 = shadowMap.SampleCmpLevelZero(shadowSamp, float2((float)t8 * bias.z + t2.x, t2.y + bias.w), t2.z).x;
		t3 = t3 + t9;
	}
	float4 t10 = albedo.Sample(samp, i.texcoord1);
	return 0.111111112 * t3 * t10;
}
