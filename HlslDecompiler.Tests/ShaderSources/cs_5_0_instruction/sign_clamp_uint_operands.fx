cbuffer C : register(b0)
{
	uint n;
	int k;
};

StructuredBuffer<uint> s : register(t0);
RWStructuredBuffer<int> o : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	r0.x = (int)sv_dispatchthreadid.x + -4;
	r0.y = (0 < r0.x) ? -1 : 0;
	r0.z = (r0.x < 0) ? -1 : 0;
	r0.y = -(r0.y) + r0.z;
	r0.z = max(-(r0.x), r0.x);
	r0.x = max(r0.x, -2);
	r0.x = min(r0.x, k);
	r0.y = r0.y + r0.z;
	r0.z = s[sv_dispatchthreadid.x];
	r0.z = max(-(r0.z), r0.z);
	r0.y = r0.z + r0.y;
	r0.z = (int)n + -3;
	r0.w = (0 < r0.z) ? -1 : 0;
	r0.z = (r0.z < 0) ? -1 : 0;
	r0.z = -(r0.w) + r0.z;
	r0.y = r0.z + r0.y;
	r0.x = r0.x + r0.y;
	o[sv_dispatchthreadid.x] = r0.x;
}
