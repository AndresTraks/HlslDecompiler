int4 k;

StructuredBuffer<int> values : register(t0);
RWStructuredBuffer<int> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0 = values[sv_dispatchthreadid.x];
	bool t1 = k.x < t0;
	output[sv_dispatchthreadid.x] = t1 ? values[sv_dispatchthreadid.x] : -1;
}
