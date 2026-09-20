int4 k;

StructuredBuffer<int> values : register(t0);
RWStructuredBuffer<int> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int2 r0;
	r0.x = values[sv_dispatchthreadid.x];
	r0.y = (k.x < r0.x) ? -1 : 0;
	r0.x = (r0.y != 0) ? r0.x : -1;
	output[sv_dispatchthreadid.x] = r0.x;
}
