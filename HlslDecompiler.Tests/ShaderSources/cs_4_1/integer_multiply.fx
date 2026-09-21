cbuffer cb : register(b0)
{
	uint n;
	uint stride;
};

RWStructuredBuffer<uint> data : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0 = sv_dispatchthreadid.x * stride;
	int2 t1 = int2(t0, t0 + stride);
	int t2;
	if (t1.y < n) {
		t2 = data[t1.y];
		data[t1.y] = data[t1.x] + t2;
	}
}
