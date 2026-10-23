cbuffer C : register(b0)
{
	int k;
};

RWByteAddressBuffer o : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0 = o.Load(sv_dispatchthreadid.x * 4 + 16);
	bool t1 = t0 < k;
	o.InterlockedAdd(4, t1 ? t0 : 9);
}
