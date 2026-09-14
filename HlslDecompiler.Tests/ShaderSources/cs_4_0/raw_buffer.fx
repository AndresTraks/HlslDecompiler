uint count;

ByteAddressBuffer input : register(t0);
RWByteAddressBuffer output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int2 t0;
	if (sv_dispatchthreadid.x < count) {
		t0 = input.Load2(sv_dispatchthreadid.x * 8);
		output.Store(sv_dispatchthreadid.x * 4, t0.y + t0.x);
	}
}
