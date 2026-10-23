cbuffer C : register(b0)
{
	uint n;
	int k;
};

StructuredBuffer<uint> s : register(t0);
RWStructuredBuffer<uint> o : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int3 r0;
	r0.x = s[sv_dispatchthreadid.x];
	r0.y = -(sv_dispatchthreadid.x) + k;
	r0.x = max((uint)r0.x, r0.y);
	r0.z = (uint)r0.y >> 3;
	r0.x = r0.x + r0.z;
	r0.z = ((uint)r0.y < n) ? -1 : 0;
	r0.z = r0.z & 1;
	r0.x = r0.z + r0.x;
	r0.z = (uint)r0.y / 5;
	r0.x = r0.z + r0.x;
	r0.z = (uint)r0.y != 0 ? 31 - firstbithigh((uint)r0.y) : -1;
	r0.z = -(r0.z) + 31;
	r0.z = (r0.y != 0) ? r0.z : -1;
	r0.y = r0.y != 0 && r0.y != -1 ? 31 - firstbithigh(r0.y) : -1;
	r0.x = r0.z + r0.x;
	r0.z = (r0.y == -1) ? -1 : 0;
	r0.y = -(r0.y) + 31;
	r0.y = (r0.z != 0) ? -1 : r0.y;
	r0.x = r0.y + r0.x;
	o[sv_dispatchthreadid.x] = r0.x;
}
