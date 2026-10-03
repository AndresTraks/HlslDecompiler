StructuredBuffer<uint2> source : register(t0);
RWStructuredBuffer<uint2> output : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0 = source[sv_dispatchthreadid.x].x;
	output[sv_dispatchthreadid.x] = uint2(asuint(asfloat(t0) * 2), source[sv_dispatchthreadid.x].y);
}
