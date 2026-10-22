uint off;
uint width;

StructuredBuffer<uint> i : register(t0);
RWStructuredBuffer<uint> o : register(u0);

[numthreads(16, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0 = i[sv_dispatchthreadid.x] >> off;
	o[sv_dispatchthreadid.x] = (i[sv_dispatchthreadid.x] & ~(((1 << width) - 1) << 4)) | ((t0 & ((1 << width) - 1)) << 4);
}
