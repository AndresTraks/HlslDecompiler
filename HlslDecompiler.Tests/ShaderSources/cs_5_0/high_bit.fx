int4 k;

StructuredBuffer<int> values : register(t0);
RWStructuredBuffer<int> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0 = values[sv_dispatchthreadid.x];
	int2 t1 = int2(firstbithigh(t0), 31 - (31 - firstbithigh((uint)(t0 + k.x))));
	output[sv_dispatchthreadid.x] = (t0 + k.x ? t1.y : -1) + t1.x;
}
