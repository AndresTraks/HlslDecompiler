cbuffer C : register(b0)
{
	float4x4 cas[4];
	float4 splitFar;
	float bias;
	float normalOffset;
	float blendBand;
	float2 poisson[4];
	float texel;
};

SamplerComparisonState cmp;
Texture2DArray<float> sm;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float3 wpos : WPOS;
	float viewz : VIEWZ;
	float3 normal : NORMAL;
};

float4 main(PS_IN i) : SV_Target
{
	float3 t1 = i.normal * normalOffset + i.wpos;
	int t0 = i.viewz < splitFar.x ? 0 : i.viewz < splitFar.y ? 1 : i.viewz < splitFar.z ? 2 : 3;
	float3 t3 = mul(float4(t1, 1), (float4x3)cas[t0]);
	float t4 = t3.z - bias;
	float t5 = (float)t0;
	float t6 = sm.SampleCmpLevelZero(cmp, float3(poisson[0] * texel + t3.xy, t5), t4).x;
	float t7 = sm.SampleCmpLevelZero(cmp, float3(poisson[1] * texel + t3.xy, t5), t4).x;
	float t8 = sm.SampleCmpLevelZero(cmp, float3(poisson[2] * texel + t3.xy, t5), t4).x;
	float t9 = sm.SampleCmpLevelZero(cmp, float3(poisson[3] * texel + t3.xy, t5), t4).x;
	float t2 = t6 + t7 + t8 + t9;
	float t10 = (splitFar[t0] - i.viewz) / blendBand;
	float t11 = 0.25 * t2;
	if (t0 < 3 && t10 < 1) {
		float3 t12 = mul(float4(t1, 1), (float4x3)cas[t0 + 1]);
		float t13 = t12.z - bias;
		float t14 = (float)(t0 + 1);
		float t15 = sm.SampleCmpLevelZero(cmp, float3(poisson[0] * texel + t12.xy, t14), t13).x;
		float t16 = sm.SampleCmpLevelZero(cmp, float3(poisson[1] * texel + t12.xy, t14), t13).x;
		float t17 = sm.SampleCmpLevelZero(cmp, float3(poisson[2] * texel + t12.xy, t14), t13).x;
		float t18 = sm.SampleCmpLevelZero(cmp, float3(poisson[3] * texel + t12.xy, t14), t13).x;
		t11 = lerp(0.25 * (t16 + t15 + t17 + t18), 0.25 * t2, t10);
	}
	return float4(t11, t11, t11, 1);
}
