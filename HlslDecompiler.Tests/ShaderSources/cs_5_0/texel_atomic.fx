RWTexture2D<uint> histogram : register(u0);
RWStructuredBuffer<uint> bins : register(u1);

[numthreads(8, 8, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 t0 = sv_dispatchthreadid.xyyy & 3;
	InterlockedAdd(histogram[t0.xw], 1);
	int t1;
	InterlockedMax(histogram[t0.xw], sv_dispatchthreadid.x, t1);
	bins[sv_dispatchthreadid.x] = t1;
	histogram[t0.xy] = t1;
}
