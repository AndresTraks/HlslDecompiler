StructuredBuffer<float> b : register(t0);
RWStructuredBuffer<float2> o : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float t0 = b[sv_dispatchthreadid.x];
	o[sv_dispatchthreadid.x] = float2(asfloat(t0 != 0 ? (1056964608 & ~8388607) | (asint(t0) & 8388607) : 0), (float)((t0 != 0 ? (asint(t0) & 2139095040) - 1056964608 : 0) >> 23));
}
