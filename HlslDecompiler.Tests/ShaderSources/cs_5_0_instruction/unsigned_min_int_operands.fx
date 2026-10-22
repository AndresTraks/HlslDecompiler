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
	int4 r0;
	r0.x = s[sv_dispatchthreadid.x];
	r0.y = -(sv_dispatchthreadid.x) + k;
	r0.z = ((uint)r0.x < (uint)r0.y) ? -1 : 0;
	r0.z = r0.z & 1;
	r0.w = (uint)r0.x / (uint)r0.y;
	r0.x = min((uint)r0.y, r0.x);
	r0.z = r0.w + r0.z;
	r0.w = (uint)r0.y >> 2;
	r0.y = (uint)r0.y % 7;
	r0.z = r0.w + r0.z;
	r0.x = r0.x + r0.z;
	r0.x = r0.y + r0.x;
	o[sv_dispatchthreadid.x] = r0.x;
}
