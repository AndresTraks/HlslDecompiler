cbuffer c : register(b0)
{
	float scale;
	uint n;
};

RWStructuredBuffer<uint> o : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0 = n & 15;
	float t1 = (float)sv_dispatchthreadid.x * scale;
	int t2 = 0;
	for (uint t3 = 0; t3 < t0; t3 = t3 + 1) {
		t1 = frac(2 * t1);
		t2 = ((t1 >= 0.5 ? -1 : 0) & 1) | ((t2 << 1) & -2);
	}
	o[sv_dispatchthreadid.x] = t2;
}
