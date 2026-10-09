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
	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	float4 r4;
	if (i.sv_groupindex.x == 0) {
		g1 = 0;
	}
	GroupMemoryBarrierWithGroupSync();
	r0.xy = (float2)(uint2)i.sv_dispatchthreadid.xy;
	r0.xy = r0.xy + float2(0.5, 0.5);
	r0.xy = r0.xy * invScreen.xy;
	r0.xy = r0.xy * float2(2, 2) + float2(-1, -1);
	r1.xy = (float2)i.sv_dispatchthreadid.xy;
	r1.zw = int2(0, 0);
	r0.z = depth.Load(r1.xyw).x;
	r0.w = (float)i.sv_groupindex.x;
	while (true) {
		r2.x = ((uint)r0.w >= lightCount) ? -1 : 0;
		if (asint(r2.x) != 0) break;
		r2 = float4(lights[r0.w].position, lights[r0.w].radius);
		r2.xyz = -(r0.xyz) + r2.xyz;
		r2.x = dot(r2.xyz, r2.xyz);
		r2.x = sqrt(r2.x);
		r2.x = (r2.x < r2.w) ? -1 : 0;
		if (asint(r2.x) != 0) {
			InterlockedAdd(g1, 1, r2.x);
			r2.y = ((uint)r2.x < 32) ? -1 : 0;
			if (asint(r2.y) != 0) {
				g0[r2.x] = r0.w;
			}
		}
		r0.w = r0.w + 64;
	}
	GroupMemoryBarrierWithGroupSync();
	r1 = gbuffer.Load(r1.xyz);
	r0.w = g1;
	r0.w = min(r0.w, 32);
	r2 = int4(0, 0, 0, 0);
	while (true) {
		r3.x = ((uint)r2.w >= (uint)r0.w) ? -1 : 0;
		if (asint(r3.x) != 0) break;
		r3.x = g0[r2.w];
		r4 = float4(lights[r3.x].position, lights[r3.x].radius);
		r3 = float4(lights[r3.x].colour, lights[r3.x].intensity);
		r4.xyz = -(r0.xyz) + r4.xyz;
		r4.x = dot(r4.xyz, r4.xyz);
		r4.x = sqrt(r4.x);
		r4.x = r4.x / r4.w;
		r4.x = saturate(-(r4.x) + 1);
		r3.xyz = r3.www * r3.xyz;
		r3.w = r4.x * r4.x;
		r2.xyz = r3.www * r3.xyz + r2.xyz;
		r2.w = r2.w + 1;
	}
	r1.xyz = r1.xyz * r2.xyz;
	output[i.sv_dispatchthreadid.xy] = r1;
}
