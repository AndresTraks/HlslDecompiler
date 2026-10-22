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
	uint t0 = k - sv_dispatchthreadid.x;
	uint t1 = s[sv_dispatchthreadid.x];
	uint2 t2 = uint2(t0 % 7, (t1 < t0 ? 1 : 0) + t1 / t0 + (t0 >> 2));
	o[sv_dispatchthreadid.x] = t2.y + min(k - sv_dispatchthreadid.x, t1) + t2.x;
}
