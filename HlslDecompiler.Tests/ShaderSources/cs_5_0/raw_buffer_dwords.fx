ByteAddressBuffer src : register(t0);
RWByteAddressBuffer dst : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	dst.Store(sv_dispatchthreadid.x * 4, src.Load4(sv_dispatchthreadid.x * 16).w);
	dst.Store3(12 * sv_dispatchthreadid.x, src.Load3(sv_dispatchthreadid.x * 16));
}
