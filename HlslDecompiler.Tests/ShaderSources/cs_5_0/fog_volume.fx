cbuffer Params : register(b0)
{
	float3 cameraPos;
	float stepSize;
	float3 fogColour;
	float density;
	uint3 gridSize;
};

SamplerState linearSampler;
Texture3D<float> noise;
RWTexture3D<float4> fogVolume : register(u0);

[numthreads(4, 4, 4)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	if (any(sv_dispatchthreadid >= gridSize)) return;
	float3 t0 = normalize(2 * (((float3)sv_dispatchthreadid + 0.5) / (float3)gridSize) - 1);
	float4 t1 = float4(0, 0, 0, 1);
	for (uint t2 = 0; t2 < 16; t2 = t2 + 1) {
		float t3 = noise.SampleLevel(linearSampler, 0.100000001 * (t0 * (float3)t2 * stepSize + cameraPos), 0).x;
		float t4 = saturate(t3) * density;
		float4 t5 = float4(t1.w * t4 * fogColour.x * stepSize + t1.x, t1.w * exp(-t4 * stepSize), t1.w * t4 * fogColour.yz * stepSize + t1.yz);
		if (t5.y < 0.00999999978) {
			t1 = t5.xzwy;
			break;
		}
		t1 = t5.xzwy;
	}
	fogVolume[sv_dispatchthreadid] = t1;
}
