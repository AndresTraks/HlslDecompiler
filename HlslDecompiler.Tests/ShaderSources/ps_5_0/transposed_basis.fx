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
		float3 t10 = float3(dot(float3(t1.x, t6.x, t0.x), kernel[t9].xyz), dot(float3(t1.y, t6.y, t0.y), kernel[t9].xyz), dot(float3(t1.z, t6.z, t0.z), kernel[t9].xyz));
		float3 t11 = t10 * radius + t7;
		float t12 = dot(transpose(projection)[3], float4(t11, 1));
		float t13 = depthMap.Sample(pointSampler, 0.5 * (mul(float4(t11, 1), (float4x2)projection) / t12) + 0.5).x;
		t8 = t8 + step(t11.z + bias, t13);
	}
	return -0.125 * t8 + 1;
}
