uint4 k;

StructuredBuffer<uint> keys : register(t0);
RWStructuredBuffer<uint4> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0 = keys[sv_dispatchthreadid.x] ^ k.x;
	output[sv_dispatchthreadid.x] = int4(countbits(t0), firstbitlow(t0), reversebits(t0), countbits(t0 & k.y));
}
