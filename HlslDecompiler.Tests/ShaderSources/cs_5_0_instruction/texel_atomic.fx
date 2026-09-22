RWTexture2D<uint> histogram : register(u0);
RWStructuredBuffer<uint> bins : register(u1);

[numthreads(8, 8, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	float r1;
	r0 = sv_dispatchthreadid.xyyy & int4(3, 3, 3, 3);
	InterlockedAdd(histogram[r0.xw], 1);
	InterlockedMax(histogram[r0.xw], (uint)sv_dispatchthreadid.x, r1);
	bins[sv_dispatchthreadid.x] = r1.x;
	histogram[r0.xy] = r1.x;
}
