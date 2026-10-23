struct Bone
{
	float4x4 m;
	float4 scale;
};

struct Rig
{
	Bone bones[8];
	uint count;
	float3 pad;
};

cbuffer R : register(b0)
{
	Rig rig;
};

RWStructuredBuffer<float4> o : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0 = sv_dispatchthreadid.x % rig.count;
	float t1 = dot(float4(1, 2, 3, 1), transpose(rig.bones[t0].m)[0]);
	o[sv_dispatchthreadid.x] = float4(t1, float3(dot(float4(1, 2, 3, 1), transpose(rig.bones[t0].m)[1]), dot(float4(1, 2, 3, 1), transpose(rig.bones[t0].m)[2]), dot(float4(1, 2, 3, 1), transpose(rig.bones[t0].m)[3]))) * rig.bones[t0].scale;
}
