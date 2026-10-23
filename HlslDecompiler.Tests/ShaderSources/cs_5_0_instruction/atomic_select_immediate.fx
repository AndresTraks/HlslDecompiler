cbuffer C : register(b0)
{
	int k;
};

RWByteAddressBuffer o : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int2 r0;
	r0.x = sv_dispatchthreadid.x << 2;
	r0.x = r0.x + 16;
	r0.x = o.Load(r0.x);
	r0.y = (r0.x < k) ? -1 : 0;
	r0.x = (r0.y != 0) ? r0.x : 9;
	o.InterlockedAdd(4, r0.x);
}
