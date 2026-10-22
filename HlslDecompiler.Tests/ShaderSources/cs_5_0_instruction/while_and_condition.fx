cbuffer C : register(b0)
{
	uint n;
	uint d;
};

RWStructuredBuffer<uint> o : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	int r1;
	int r2;
	r0.x = sv_dispatchthreadid.x * 977 + n;
	r1 = r0.x;
	r0.y = 0;
	while (true) {
		r0.z = (r1.x != 0) ? -1 : 0;
		r0.w = ((uint)r0.y < 10) ? -1 : 0;
		r0.z = r0.w & r0.z;
		if (r0.z == 0) break;
		uint dividend0 = (uint)r1.x;
		r1 = dividend0 / (uint)d;
		r2 = dividend0 % (uint)d;
		r0.y = r0.y + r2.x;
	}
	o[sv_dispatchthreadid.x] = r0.y;
}
