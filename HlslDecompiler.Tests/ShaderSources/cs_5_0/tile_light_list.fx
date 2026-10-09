cbuffer Params : register(b0)
{
	float4x4 invView;
	uint lightCount;
	float2 invScreen;
};

struct Light
{
	float3 position;
	float radius;
	float3 colour;
	float intensity;
};

StructuredBuffer<Light> lights : register(t0);
Texture2D gbuffer : register(t1);
Texture2D<float> depth;
RWTexture2D<float4> output : register(u0);

groupshared uint g0[32];
groupshared int g1;

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(8, 8, 1)]
void main(CS_IN i)
{
	if (i.sv_groupindex == 0) {
		g1 = 0;
	}
	GroupMemoryBarrierWithGroupSync();
	float3 t0 = float3(2 * ((float2)i.sv_dispatchthreadid.xy + 0.5) * invScreen - 1, depth.Load(int3(i.sv_dispatchthreadid.xy, 0)).x);
	for (uint t1 = i.sv_groupindex; t1 < lightCount; t1 = t1 + 64) {
		if (length(lights[t1].position - t0) < lights[t1].radius) {
			int t2;
			InterlockedAdd(g1, 1, t2);
			if ((uint)t2 < 32) {
				g0[t2] = t1;
			}
		}
	}
	GroupMemoryBarrierWithGroupSync();
	uint t3 = g1;
	float3 t4 = gbuffer.Load(int3(i.sv_dispatchthreadid.xy, 0)).xyz;
	float3 t5 = 0;
	uint t6 = min(t3, 32);
	for (uint t7 = 0; t7 < t6; t7 = t7 + 1) {
		uint t8 = g0[t7];
		float t9 = saturate(1 - length(lights[t8].position - t0) / lights[t8].radius);
		float t10 = t9 * t9;
		float4 t11 = float4(lights[t8].colour, lights[t8].intensity);
		t5 = t5 + t10 * t11.w * t11.xyz;
	}
	output[i.sv_dispatchthreadid.xy] = float4(t4 * t5, gbuffer.Load(int3(i.sv_dispatchthreadid.xy, 0)).w);
}
