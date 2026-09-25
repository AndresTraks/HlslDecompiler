ByteAddressBuffer src : register(t0);
RWByteAddressBuffer dst : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int2 r0;
	int4 r1;
	r0 = sv_dispatchthreadid.xx << int2(2, 4);
	r1 = src.Load4(r0.y);
	dst.Store(r0.x, r1.w);
	r0.x = sv_dispatchthreadid.x * 12;
	dst.Store3(r0.x, r1.xyz);
}
