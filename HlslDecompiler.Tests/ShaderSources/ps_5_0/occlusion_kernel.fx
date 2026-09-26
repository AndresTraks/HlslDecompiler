cbuffer Params : register(b0)
{
	float4x4 projection;
	float2 inverseSize;
	float radius;
	float bias;
	uint sampleCount;
};

static const float4 icb0[4] =
{
	float4(1, 0, 0, 0),
	float4(0, 1, 0, 0),
	float4(-1, 0, 0, 0),
	float4(0, -1, 0, 0),
};

static const float4 icb1[8] =
{
	float4(0.5, 0.5, 0.5, 0),
	float4(-0.5, 0.5, 0.5, 0),
	float4(0.5, -0.5, 0.5, 0),
	float4(-0.5, -0.5, 0.5, 0),
	float4(0.7, 0, 0.3, 0),
	float4(-0.7, 0, 0.3, 0),
	float4(0, 0.7, 0.3, 0),
	float4(0, -0.7, 0.3, 0),
};

SamplerState pointSampler;
Texture2D depthMap;
Texture2D normalMap;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float3 t0 = normalMap.SampleLevel(pointSampler, i.texcoord, 0).xyz;
	float4 t1 = float4(depthMap.SampleLevel(pointSampler, i.texcoord, 0).x, 2 * t0 - 1);
	int t2 = (uint)i.sv_position.x & 3;
	float t3 = 0;
	for (uint t4 = 0; t4 < sampleCount; t4 = t4 + 1) {
		float t5 = t1.x - depthMap.SampleLevel(pointSampler, (float2(icb1[t4 & 7].x * icb0[t2].x - icb0[t2].y * icb1[t4 & 7].y, dot(icb1[t4 & 7].yx, icb0[t2].xy))) * radius * inverseSize + i.texcoord, 0).x;
		t3 = (t5 < radius && bias < t5 ? saturate(dot(t1.yzw, icb1[t4 & 7].xyz)) : 0) + t3;
	}
	return 1 - t3 / (float4)sampleCount;
}
