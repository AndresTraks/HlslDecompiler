cbuffer Args : register(b0)
{
	uint elementCount;
	uint stride;
	float scale;
};

ByteAddressBuffer input : register(t0);
RWByteAddressBuffer output : register(u0);

groupshared int g0[128];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(128, 1, 1)]
void main(CS_IN i)
{
	int t0 = i.sv_dispatchthreadid.x * stride;
	int t2 = input.Load(t0);
	int t1 = input.Load2(t0).y;
	int t3 = t1 & 1 ? 1 : 0;
	g0[i.sv_groupindex] = t3;
	GroupMemoryBarrierWithGroupSync();
	int4 t4 = int4(i.sv_groupindex >= 1 ? g0[i.sv_groupindex - 1] : 0, i.sv_groupindex >= int3(2, 4, 8));
	int3 t5 = i.sv_groupindex - int3(2, 4, 8);
	GroupMemoryBarrierWithGroupSync();
	t3 = t3 + t4.x;
	g0[i.sv_groupindex] = t3;
	GroupMemoryBarrierWithGroupSync();
	t4.x = t4.y ? g0[t5.x] : 0;
	GroupMemoryBarrierWithGroupSync();
	t3 = t3 + t4.x;
	g0[i.sv_groupindex] = t3;
	GroupMemoryBarrierWithGroupSync();
	t4.x = t4.z ? g0[t5.y] : 0;
	GroupMemoryBarrierWithGroupSync();
	t3 = t3 + t4.x;
	g0[i.sv_groupindex] = t3;
	GroupMemoryBarrierWithGroupSync();
	t4.x = t4.w ? g0[t5.z] : 0;
	GroupMemoryBarrierWithGroupSync();
	t3 = t3 + t4.x;
	g0[i.sv_groupindex] = t3;
	GroupMemoryBarrierWithGroupSync();
	t4.xyw = int3(i.sv_groupindex >= 16 ? g0[i.sv_groupindex - 16] : 0, i.sv_groupindex >= 32, i.sv_groupindex - 32);
	GroupMemoryBarrierWithGroupSync();
	t3 = t3 + t4.x;
	g0[i.sv_groupindex] = t3;
	GroupMemoryBarrierWithGroupSync();
	t4.x = t4.y ? g0[t4.w] : 0;
	GroupMemoryBarrierWithGroupSync();
	t3 = t3 + t4.x;
	g0[i.sv_groupindex] = t3;
	GroupMemoryBarrierWithGroupSync();
	int t6;
	if (i.sv_dispatchthreadid.x < elementCount) {
		t6 = g0[i.sv_groupindex - 64];
		output.Store2(t0, uint2(asuint(asfloat(t2) * scale), (i.sv_groupindex >= 64 ? t6 : 0) + t3));
		if ((t1 & 2) != 0) {
			output.InterlockedAdd(stride * elementCount, 1);
		}
	}
}
