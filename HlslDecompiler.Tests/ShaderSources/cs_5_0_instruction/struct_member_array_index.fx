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
	int4 r0;
	float4 r1;
	r0.x = (uint)sv_dispatchthreadid.x % (uint)rig.count;
	r0.x = r0.x * 5;
	r1.x = dot(float4(1, 2, 3, 1), transpose(rig.bones[r0.x / 5].m)[0]);
	r1.y = dot(float4(1, 2, 3, 1), transpose(rig.bones[r0.x / 5].m)[1]);
	r1.z = dot(float4(1, 2, 3, 1), transpose(rig.bones[r0.x / 5].m)[2]);
	r1.w = dot(float4(1, 2, 3, 1), transpose(rig.bones[r0.x / 5].m)[3]);
	r0 = asint(r1 * rig.bones[r0.x / 5].scale);
	o[sv_dispatchthreadid.x] = asfloat(r0);
}
