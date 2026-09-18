uint4 k;

StructuredBuffer<uint> keys : register(t0);
RWStructuredBuffer<uint4> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	output[sv_dispatchthreadid.x] = int4(countbits(keys[sv_dispatchthreadid.x] ^ k.x), firstbitlow(keys[sv_dispatchthreadid.x] ^ k.x), reversebits(keys[sv_dispatchthreadid.x] ^ k.x), countbits((keys[sv_dispatchthreadid.x] ^ k.x) & k.y));
}
