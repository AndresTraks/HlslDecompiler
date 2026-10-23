cbuffer C : register(b0)
{
	double scale;
	uint n;
};

StructuredBuffer<double> a : register(t0);
RWStructuredBuffer<double> o : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	double t0 = 0;
	for (uint t1 = 0; t1 < n; t1 = t1 + 1) {
		t0 = a[t1] + t0 * scale;
	}
	o[sv_dispatchthreadid.x] = t0;
}
