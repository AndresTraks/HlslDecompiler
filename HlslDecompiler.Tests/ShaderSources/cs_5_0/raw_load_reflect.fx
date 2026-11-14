cbuffer C : register(b0)
{
	float3 n;
};

RWByteAddressBuffer buf : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0 = 12 * sv_dispatchthreadid.x;
	int3 t1 = buf.Load3(t0);
	buf.Store3(t0, asuint(2 * asfloat(t1)));
	buf.Store3(12 * sv_dispatchthreadid.x + 4096, asuint(reflect(asfloat(t1), n)));
}
