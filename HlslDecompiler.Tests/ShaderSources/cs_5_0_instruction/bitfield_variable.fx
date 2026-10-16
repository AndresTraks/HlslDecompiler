uint off;
uint width;

StructuredBuffer<uint> i : register(t0);
RWStructuredBuffer<uint> o : register(u0);

[numthreads(16, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int2 r0;
	r0.x = i[sv_dispatchthreadid.x];
	r0.y = (uint)r0.x >> off;
	r0.x = (r0.x & ~(((1 << width) - 1) << 4)) | ((r0.y << 4) & (((1 << width) - 1) << 4));
	o[sv_dispatchthreadid.x] = r0.x;
}
