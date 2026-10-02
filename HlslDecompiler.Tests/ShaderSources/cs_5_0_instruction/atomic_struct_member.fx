struct BufElement
{
	int v;
	uint u;
};

StructuredBuffer<int> idx : register(t0);
RWStructuredBuffer<BufElement> buf : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int3 r0;
	int r1;
	r0.y = 0;
	r0.z = idx[sv_dispatchthreadid.x];
	r0.x = r0.z & 7;
	InterlockedCompareExchange(buf[r0.x].v, 1, 2, r1);
	buf[r0.z].u = r1.x;
}
