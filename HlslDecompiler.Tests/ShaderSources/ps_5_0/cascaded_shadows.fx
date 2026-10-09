cbuffer Shadows : register(b0)
{
	float4x4 cascadeTransforms[4];
	float4 cascadeSplits;
	float3 lightDirection;
	float shadowBias;
};

SamplerComparisonState shadowSampler;
SamplerState linearSampler;
Texture2DArray<float> shadowMaps;
Texture2D albedo;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float3 world : WORLD;
	float3 normal : NORMAL;
	float2 texcoord : TEXCOORD;
	float depth : DEPTH;
};

float4 main(PS_IN i) : SV_Target
{
	uint t0 = 3;
	for (uint t1 = 0; t1 < 4; t1 = t1 + 1) {
		if (i.depth < cascadeSplits[t1]) {
			t0 = t1;
			break;
		}
		t0 = 3;
	}
	float t2 = dot(float4(i.world, 1), transpose(cascadeTransforms[t0])[3]);
	float4 t3 = albedo.Sample(linearSampler, i.texcoord);
	float3 t4 = mul(float4(i.world, 1), (float4x3)cascadeTransforms[t0]) / t2;
	float t5 = shadowMaps.SampleCmpLevelZero(shadowSampler, float3(float2(0.5, -0.5) * t4.xy + 0.5, (float)t0), t4.z - shadowBias).x;
	float t6 = saturate(dot(normalize(i.normal), -lightDirection)) * t5 + 0.100000001;
	return t3 * t6;
}
