uint count;

StructuredBuffer<uint> input : register(t0);
RWStructuredBuffer<float> output : register(u0);

groupshared int g0[64];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(64, 1, 1)]
void main(CS_IN i)
{
	int3 r0;
	int r1;
	r0.x = input[i.sv_dispatchthreadid.x];
	r0.y = (uint)r0.x >> 16;
	r0.x = r0.y ^ r0.x;
	g0[i.sv_groupindex.x] = r0.x;
	GroupMemoryBarrierWithGroupSync();
	r0.xy = int2(0, 0);
	while (true) {
		r0.z = (r0.y >= 8) ? -1 : 0;
		if (r0.z != 0) break;
		r0.z = r0.y + i.sv_groupindex.x;
		r0.z = r0.z & 63;
		r1 = g0[r0.z];
		r0.z = r1.x & 255;
		r0.x = r0.z + r0.x;
		r0.y = r0.y + 1;
	}
	r0.x = asint((float)(uint)r0.x);
	r0.y = asint((float)(uint)count);
	r0.x = asint(asfloat(r0.x) / asfloat(r0.y));
	output[i.sv_dispatchthreadid.x] = asfloat(r0.x);
}
