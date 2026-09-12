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
	float4 t0 = float4(0.5 * dot(transpose(lightViewProj)[0], i.texcoord) / dot(transpose(lightViewProj)[3], i.texcoord) + 0.5, -0.5 * dot(transpose(lightViewProj)[1], i.texcoord) / dot(transpose(lightViewProj)[3], i.texcoord) + 0.5, dot(transpose(lightViewProj)[2], i.texcoord) / dot(transpose(lightViewProj)[3], i.texcoord) - bias.x, 0);
	for (int t1 = -1; t1 <= 1; t1 = t1 + 1) {
		t0.w = t0.w + shadowMap.SampleCmpLevelZero(shadowSamp, float2((float)t1 * bias.z + t0.x, -1082130432 * bias.w + t0.y), t0.z).x;
	}
	float t2 = t0.w;
	for (int t3 = -1; t3 <= 1; t3 = t3 + 1) {
		t2 = shadowMap.SampleCmpLevelZero(shadowSamp, float2((float)t3 * bias.z + t0.x, t0.y), t0.z).x + t2;
	}
	float t4 = t2;
	for (int t5 = -1; t5 <= 1; t5 = t5 + 1) {
		t4 = t4 + shadowMap.SampleCmpLevelZero(shadowSamp, float2((float)t5 * bias.z + t0.x, 1065353216 * bias.w + t0.y), t0.z).x;
	}
	return 0.111111112 * t4 * albedo.Sample(samp, i.texcoord1);
}
