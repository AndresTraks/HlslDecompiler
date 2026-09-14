uint count;

ByteAddressBuffer input : register(t0);
RWByteAddressBuffer output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int2 r0;
	int2 r1;
	r0.x = (sv_dispatchthreadid.x < count) ? -1 : 0;
	if (r0.x != 0) {
		r0.x = sv_dispatchthreadid.x << 3;
		r0.y = sv_dispatchthreadid.x << 2;
		r1 = input.Load2(r0.x);
		r0.x = r1.y + r1.x;
		output.Store(r0.y, r0.x);
	}
}
