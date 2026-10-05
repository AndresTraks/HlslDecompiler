cbuffer Sort : register(b0)
{
	uint span;
	uint block;
};

RWStructuredBuffer<uint> keys : register(u0);

[numthreads(256, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0 = sv_dispatchthreadid.x ^ span;
	uint2 t1;
	if (sv_dispatchthreadid.x < t0) {
		t1 = uint2(keys[sv_dispatchthreadid.x], keys[t0]);
		if (((sv_dispatchthreadid.x & block) == 0) == (t1.y < t1.x)) {
			keys[sv_dispatchthreadid.x] = t1.y;
			keys[t0] = t1.x;
		}
	}
}
