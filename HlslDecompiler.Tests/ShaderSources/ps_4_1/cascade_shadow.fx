float4x4 cascadeTransform[4];
float4 cascadeSplit;
float4 shadowParameters;

static const float4 icb[4] =
{
	float4(1, 0, 0, 0),
	float4(0, 1, 0, 0),
	float4(0, 0, 1, 0),
	float4(0, 0, 0, 1),
};

SamplerComparisonState shadowSampler;
Texture2DArray cascades;

struct PS_IN
{
	float3 texcoord : TEXCOORD;
	float texcoord1 : TEXCOORD1;
	float3 normal : NORMAL;
};

float4 main(PS_IN i) : SV_Target
{
	int t0 = 0;
	for (int t1 = 0; t1 < 4; t1 = t1 + 1) {
		t0 = dot(cascadeSplit, icb[t1]) < i.texcoord1 ? t1 : t0;
	}
	int t2 = min(t0, 3);
	float t3 = dot(float4(i.texcoord, 1), transpose(cascadeTransform[t2])[3]);
	float2 t4 = mul(float4(i.texcoord, 1), (float4x2)cascadeTransform[t2]) / t3;
	float2 t5 = float2(0.5, -0.5) * t4 + 0.5;
	float t6 = dot(float4(i.texcoord, 1), transpose(cascadeTransform[t2])[2]) / t3 - (shadowParameters.x * (1 - saturate(i.normal.y)) + shadowParameters.y);
	float t7 = saturate(i.normal.y) * (cascades.SampleCmpLevelZero(shadowSampler, float3(0.5 * t4.x + 0.5 + shadowParameters.z, 0.5 + -0.5 * t4.y, (float)t2), t6).x + cascades.SampleCmpLevelZero(shadowSampler, float3(t5, (float)t2), t6).x + cascades.SampleCmpLevelZero(shadowSampler, float3(t5.x - shadowParameters.z, t5.y, (float)t2), t6).x);
	return 0.333333343 * t7;
}
