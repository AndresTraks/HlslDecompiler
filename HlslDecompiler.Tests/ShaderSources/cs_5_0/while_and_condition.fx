cbuffer C : register(b0)
{
	uint n;
	uint d;
};

RWStructuredBuffer<uint> o : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0 = 977 * sv_dispatchthreadid.x + n;
	uint t1 = 0;
	while (t1 < 10 && t0 != 0) {
		t1 = t1 + t0 % d;
		t0 = t0 / d;
	}
	o[sv_dispatchthreadid.x] = t1;
}
