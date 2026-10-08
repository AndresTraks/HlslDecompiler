cbuffer Occlusion : register(b0)
{
	float4x4 projection;
	float4 kernel[8];
	float2 noiseScale;
	float radius;
	float bias;
};

SamplerState pointSampler;
Texture2D depthMap;
Texture2D normalMap;
Texture2D noiseMap;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float3 t2 = noiseMap.Sample(pointSampler, i.texcoord * noiseScale).xyz;
	float3 t3 = 2 * t2 - 1;
	float3 t5 = normalMap.Sample(pointSampler, i.texcoord).xyz;
	float3 t0 = 2 * t5 - 1;
	float t4 = dot(t3, t0);
	float3 t1 = normalize(-t0 * t4 + t3);
	float3 t6 = cross(t0, t1);
	float3 t7 = float3(2 * i.texcoord - 1, depthMap.Sample(pointSampler, i.texcoord).x);
	float t8 = 0;
	for (int t9 = 0; t9 < 8; t9 = t9 + 1) {
		float3 t10 = (t1 * kernel[t9].x + t6 * kernel[t9].y + t0 * kernel[t9].z) * radius + t7;
		float t11 = dot(transpose(projection)[3], float4(t10, 1));
		float t12 = depthMap.Sample(pointSampler, 0.5 * (mul(float4(t10, 1), (float4x2)projection) / t11) + 0.5).x;
		t8 = t8 + step(t10.z + bias, t12);
	}
	return -0.125 * t8 + 1;
}
