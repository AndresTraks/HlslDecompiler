float4x4 projection;
float4 kernel[12];
float4 occlusion;

SamplerState samp;
Texture2D depthMap;
Texture2D normalMap;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float3 t1 = normalMap.Sample(samp, i.texcoord).xyz;
	float t0 = depthMap.Sample(samp, i.texcoord).x;
	float3 t2 = t0 * i.texcoord1;
	float3 t3 = 2 * t1 - 1;
	float t4 = 0;
	for (int t5 = 0; t5 < 12; t5 = t5 + 1) {
		float t6 = (float)sign(dot(kernel[t5].xyz, t3));
		float3 t7 = t6 * kernel[t5].xyz * occlusion.x + t2;
		float t8 = dot(transpose(projection)[3], float4(t7, 1));
		float t9 = depthMap.Sample(samp, float2(0.5, -0.5) * (mul(float4(t7, 1), (float4x2)projection) / t8) + 0.5).x * occlusion.y;
		t4 = t4 + (t9 >= t7.z + occlusion.z ? saturate(occlusion.x / abs(i.texcoord1.z * t0 - t9)) : 0);
	}
	return 0.0833333358 * -t4 + 1;
}
