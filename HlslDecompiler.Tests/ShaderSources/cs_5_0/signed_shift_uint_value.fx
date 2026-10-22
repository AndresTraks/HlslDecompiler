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
	int2 t0 = int2((int)n >> (sv_dispatchthreadid.x & 3), (int)s[sv_dispatchthreadid.x] >> 2);
	o[sv_dispatchthreadid.x] = t0.y + ((int)(sv_dispatchthreadid.x - 4) >> 1) + (s[sv_dispatchthreadid.x] >> 3) + t0.x;
}
