cbuffer Params : register(b0)
{
	float4x4 viewProjection;
	float3 cameraPos;
	float maxDistance;
	float thickness;
	int stepCount;
};

SamplerState linearSampler;
Texture2D sceneColour;
Texture2D<float> depthBuffer;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
	float3 normal : NORMAL;
};

float4 main(PS_IN i) : SV_Target
{
	float3 t0 = normalize(i.texcoord1 - cameraPos);
	float4 t1 = float4(reflect(t0, normalize(i.normal)), (float)stepCount);
	float4 t2 = 0;
	for (int t3 = 1; stepCount >= t3; t3 = t3 + 1) {
		float3 t4 = t1.xyz * ((float3)t3 * maxDistance / t1.w) + i.texcoord1;
		float t5 = dot(transpose(viewProjection)[3], float4(t4, 1));
		float2 t6 = float2(0.5, -0.5) * (mul(float4(t4, 1), (float4x2)viewProjection) / t5) + 0.5;
		if (any(t6 > 1) || any(t6 < 0)) {
			break;
		}
		float t7 = dot(transpose(viewProjection)[2], float4(t4, 1)) / t5;
		float t8 = depthBuffer.SampleLevel(linearSampler, t6, 0).x;
		if (t7 - t8 < thickness && t8 < t7) {
			t2 = float4(sceneColour.SampleLevel(linearSampler, t6, 0).xyz, 1);
			break;
		}
		t2 = 0;
	}
	return float4(t2.xyz * t2.w, t2.w);
}
