float4x4 lightViewProj;
float4 bias;

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
	float3 t1 = float3(float2(0.5, -0.5) * (mul(i.texcoord, (float4x2)lightViewProj) / t0) + 0.5, dot(transpose(lightViewProj)[2], i.texcoord) / t0 - bias.x);
	float t2 = 0;
	for (int t3 = -1; t3 <= 1; t3 = t3 + 1) {
		t2 = t2 + shadowMap.SampleCmpLevelZero(shadowSamp, float2((float)t3 * bias.z + t1.x, t1.y - bias.w), t1.z).x;
	}
	float t4 = t2;
	for (int t5 = -1; t5 <= 1; t5 = t5 + 1) {
		t4 = shadowMap.SampleCmpLevelZero(shadowSamp, float2((float)t5 * bias.z + t1.x, t1.y), t1.z).x + t4;
	}
	float t6 = t4;
	for (int t7 = -1; t7 <= 1; t7 = t7 + 1) {
		t6 = t6 + shadowMap.SampleCmpLevelZero(shadowSamp, float2((float)t7 * bias.z + t1.x, t1.y + bias.w), t1.z).x;
	}
	return 0.111111112 * t6 * albedo.Sample(samp, i.texcoord1);
}
