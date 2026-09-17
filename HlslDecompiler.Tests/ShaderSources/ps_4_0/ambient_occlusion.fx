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
	float3 t0 = depthMap.Sample(samp, i.texcoord).x;
	float3 t1 = t0.z * i.texcoord1;
	float3 t2 = 2 * normalMap.Sample(samp, i.texcoord).xyz - 1;
	float t3 = 0;
	for (int t4 = 0; t4 < 12; t4 = t4 + 1) {
		float4 t5 = float4((float3)sign(dot(kernel[t4].xyz, t2)) * kernel[t4].xyz * occlusion.x + t1, 1);
		float t6 = depthMap.Sample(samp, float2(0.5, -0.5) * (mul(t5, (float4x2)projection) / dot(transpose(projection)[3], t5)) + 0.5).x * occlusion.y;
		t3 = t3 + (t6 >= t5.z + occlusion.z ? saturate(occlusion.x / abs(i.texcoord1.z * t0.z - t6)) : 0);
	}
	return 0.0833333358 * -t3 + 1;
}
