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
	float t1 = dot(transpose(lightViewProj)[3], i.texcoord);
	float3 t2 = mul(i.texcoord, (float4x3)lightViewProj) / t1;
	float t3 = t2.z - bias.x;
	float t4 = 0.5 * t2.x + 0.5;
	float t0 = -0.5 * t2.y + 0.5;
	float t5 = 0;
	float t6 = t0 - bias.w;
	for (int t7 = -1; t7 <= 1; t7 = t7 + 1) {
		float t8 = shadowMap.SampleCmpLevelZero(shadowSamp, float2((float)t7 * bias.z + t4, t6), t3).x;
		t5 = t5 + t8;
	}
	for (int t9 = -1; t9 <= 1; t9 = t9 + 1) {
		float t10 = shadowMap.SampleCmpLevelZero(shadowSamp, float2((float)t9 * bias.z + t4, t0), t3).x;
		t5 = t5 + t10;
	}
	float t11 = t0 + bias.w;
	for (int t12 = -1; t12 <= 1; t12 = t12 + 1) {
		float t13 = shadowMap.SampleCmpLevelZero(shadowSamp, float2((float)t12 * bias.z + t4, t11), t3).x;
		t5 = t5 + t13;
	}
	float4 t14 = albedo.Sample(samp, i.texcoord1);
	return t14 * (0.111111112 * t5);
}
