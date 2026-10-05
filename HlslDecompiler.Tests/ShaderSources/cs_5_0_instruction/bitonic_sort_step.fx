cbuffer Sort : register(b0)
{
	uint span;
	uint block;
};

RWStructuredBuffer<uint> keys : register(u0);

[numthreads(256, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	int r1;
	r0.x = sv_dispatchthreadid.x ^ span;
	r0.y = (sv_dispatchthreadid.x < (uint)r0.x) ? -1 : 0;
	if (r0.y != 0) {
		r0.y = keys[sv_dispatchthreadid.x];
		r0.z = keys[r0.x];
		r0.w = sv_dispatchthreadid.x & block;
		r0.w = (r0.w == 0) ? -1 : 0;
		r1 = ((uint)r0.z < (uint)r0.y) ? -1 : 0;
		r0.w = (r0.w == r1.x) ? -1 : 0;
		if (r0.w != 0) {
			keys[sv_dispatchthreadid.x] = r0.z;
			keys[r0.x] = r0.y;
		}
	}
}
