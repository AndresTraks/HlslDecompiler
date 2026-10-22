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
	uint t0 = i.sv_dispatchthreadid.x * stride;
	int t2 = input.Load(t0);
	int t1 = input.Load2(t0).y;
	int t3 = t1 & 1 ? 1 : 0;
	g0[i.sv_groupindex] = t3;
	GroupMemoryBarrierWithGroupSync();
	int t4 = i.sv_groupindex >= 1 ? g0[i.sv_groupindex - 1] : 0;
	bool3 t5 = i.sv_groupindex >= uint3(2, 4, 8);
	uint3 t6 = i.sv_groupindex - uint3(2, 4, 8);
	GroupMemoryBarrierWithGroupSync();
	t3 = t3 + t4;
	g0[i.sv_groupindex] = t3;
	GroupMemoryBarrierWithGroupSync();
	int t7 = t5.x ? g0[t6.x] : 0;
	GroupMemoryBarrierWithGroupSync();
	t3 = t3 + t7;
	g0[i.sv_groupindex] = t3;
	GroupMemoryBarrierWithGroupSync();
	int t8 = t5.y ? g0[t6.y] : 0;
	GroupMemoryBarrierWithGroupSync();
	t3 = t3 + t8;
	g0[i.sv_groupindex] = t3;
	GroupMemoryBarrierWithGroupSync();
	int t9 = t5.z ? g0[t6.z] : 0;
	GroupMemoryBarrierWithGroupSync();
	t3 = t3 + t9;
	g0[i.sv_groupindex] = t3;
	GroupMemoryBarrierWithGroupSync();
	int t10 = i.sv_groupindex >= 16 ? g0[i.sv_groupindex - 16] : 0;
	bool t11 = i.sv_groupindex >= 32;
	uint t12 = i.sv_groupindex - 32;
	GroupMemoryBarrierWithGroupSync();
	t3 = t3 + t10;
	g0[i.sv_groupindex] = t3;
	GroupMemoryBarrierWithGroupSync();
	int t13 = t11 ? g0[t12] : 0;
	GroupMemoryBarrierWithGroupSync();
	t3 = t3 + t13;
	g0[i.sv_groupindex] = t3;
	GroupMemoryBarrierWithGroupSync();
	if (i.sv_dispatchthreadid.x < elementCount) {
		int t14 = g0[i.sv_groupindex - 64];
		output.Store2(t0, uint2(asuint(asfloat(t2) * scale), t3 + (i.sv_groupindex >= 64 ? t14 : 0)));
		if ((t1 & 2) != 0) {
			output.InterlockedAdd(elementCount * stride, 1);
		}
	}
}
