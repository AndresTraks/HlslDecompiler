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
	float3 t0 = float3(0.5 * dot(transpose(lightViewProj)[0], i.texcoord) / dot(transpose(lightViewProj)[3], i.texcoord) + 0.5, -0.5 * dot(transpose(lightViewProj)[1], i.texcoord) / dot(transpose(lightViewProj)[3], i.texcoord) + 0.5, dot(transpose(lightViewProj)[2], i.texcoord) / dot(transpose(lightViewProj)[3], i.texcoord) - bias.x);
	float t1 = 0;
	for (int t2 = -1; t2 <= 1; t2 = t2 + 1) {
		t1 = t1 + shadowMap.SampleCmpLevelZero(shadowSamp, float2((float)t2 * bias.z + t0.x, t0.y - bias.w), t0.z).x;
	}
	float t3 = t1;
	for (int t4 = -1; t4 <= 1; t4 = t4 + 1) {
		t3 = shadowMap.SampleCmpLevelZero(shadowSamp, float2((float)t4 * bias.z + t0.x, t0.y), t0.z).x + t3;
	}
	float t5 = t3;
	for (int t6 = -1; t6 <= 1; t6 = t6 + 1) {
		t5 = t5 + shadowMap.SampleCmpLevelZero(shadowSamp, float2((float)t6 * bias.z + t0.x, t0.y + bias.w), t0.z).x;
	}
	return 0.111111112 * t5 * albedo.Sample(samp, i.texcoord1);
}
