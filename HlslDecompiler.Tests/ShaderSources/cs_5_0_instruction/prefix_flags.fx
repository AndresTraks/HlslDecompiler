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
	int4 r0;
	int4 r1;
	int4 r2;
	r0.x = i.sv_dispatchthreadid.x * stride;
	r0.yz = input.Load2(r0.x);
	r0.w = r0.z & 1;
	r0.w = (r0.w != 0) ? 1 : 0;
	g0[i.sv_groupindex.x] = r0.w;
	GroupMemoryBarrierWithGroupSync();
	r1 = (i.sv_groupindex.x >= int4(1, 2, 4, 8)) ? -1 : 0;
	r2 = i.sv_groupindex.x + int4(-1, -2, -4, -8);
	r2.x = g0[r2.x];
	r1.x = r1.x & r2.x;
	GroupMemoryBarrierWithGroupSync();
	r0.w = r0.w + r1.x;
	g0[i.sv_groupindex.x] = r0.w;
	GroupMemoryBarrierWithGroupSync();
	r1.x = g0[r2.y];
	r1.x = r1.x & r1.y;
	GroupMemoryBarrierWithGroupSync();
	r0.w = r0.w + r1.x;
	g0[i.sv_groupindex.x] = r0.w;
	GroupMemoryBarrierWithGroupSync();
	r1.x = g0[r2.z];
	r1.x = r1.x & r1.z;
	GroupMemoryBarrierWithGroupSync();
	r0.w = r0.w + r1.x;
	g0[i.sv_groupindex.x] = r0.w;
	GroupMemoryBarrierWithGroupSync();
	r1.x = g0[r2.w];
	r1.x = r1.x & r1.w;
	GroupMemoryBarrierWithGroupSync();
	r0.w = r0.w + r1.x;
	g0[i.sv_groupindex.x] = r0.w;
	GroupMemoryBarrierWithGroupSync();
	r1.xy = (i.sv_groupindex.xx >= int2(16, 32)) ? -1 : 0;
	r1.zw = i.sv_groupindex.xx + int2(-16, -32);
	r1.z = g0[r1.z];
	r1.x = r1.z & r1.x;
	GroupMemoryBarrierWithGroupSync();
	r0.w = r0.w + r1.x;
	g0[i.sv_groupindex.x] = r0.w;
	GroupMemoryBarrierWithGroupSync();
	r1.x = g0[r1.w];
	r1.x = r1.x & r1.y;
	GroupMemoryBarrierWithGroupSync();
	r0.w = r0.w + r1.x;
	g0[i.sv_groupindex.x] = r0.w;
	GroupMemoryBarrierWithGroupSync();
	r1.x = (i.sv_dispatchthreadid.x < elementCount) ? -1 : 0;
	if (r1.x != 0) {
		r1.x = asint(asfloat(r0.y) * scale);
		r0.y = (i.sv_groupindex.x >= 64) ? -1 : 0;
		r1.z = i.sv_groupindex.x + -64;
		r1.z = g0[r1.z];
		r0.y = r0.y & r1.z;
		r1.y = r0.y + r0.w;
		output.Store2(r0.x, r1.xy);
		r0.x = r0.z & 2;
		if (r0.x != 0) {
			r0.x = stride * elementCount;
			output.InterlockedAdd(r0.x, 1);
		}
	}
}
