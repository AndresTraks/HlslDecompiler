uint4 k;

StructuredBuffer<uint> keys : register(t0);
RWStructuredBuffer<uint4> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int2 r0;
	int4 r1;
	r0.x = keys[sv_dispatchthreadid.x];
	r0.x = r0.x ^ k.x;
	r0.y = r0.x & k.y;
	r1.xw = countbits(r0.xy);
	r1.y = firstbitlow(r0.x);
	r1.z = reversebits(r0.x);
	output[sv_dispatchthreadid.x] = r1;
}
